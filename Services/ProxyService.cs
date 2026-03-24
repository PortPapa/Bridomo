using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using LocalTrafficInspector.Models;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace LocalTrafficInspector.Services;

public class ProxyService : IDisposable
{
    private ProxyServer? _proxyServer;
    private ExplicitProxyEndPoint? _endPoint;
    private int _sessionCounter;
    private bool _isRunning;
    private int _maxBodySize = 1_048_576;

    public bool IsRunning => _isRunning;

    public event Action<TrafficSession>? SessionCaptured;
    public event Action<string>? ErrorOccurred;

    public Task StartAsync(ProxySettings settings)
    {
        if (_isRunning) return Task.CompletedTask;

        try
        {
            _maxBodySize = settings.MaxBodySize;

            _proxyServer = new ProxyServer(userTrustRootCertificate: false);

            // 시스템 프록시 설정을 건드리지 않음 (수동 프록시 설정 유지)
            _proxyServer.ProxyBasicAuthenticateFunc = null;

            // BouncyCastle 엔진 사용 (Windows 기본 엔진보다 호환성 좋음)
            _proxyServer.CertificateManager.CertificateEngine =
                Titanium.Web.Proxy.Network.CertificateEngine.BouncyCastleFast;

            // 인증서를 고정 경로에 저장 (매번 재생성 방지)
            var certPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LocalTrafficInspector");
            Directory.CreateDirectory(certPath);
            _proxyServer.CertificateManager.PfxFilePath = Path.Combine(certPath, "rootCert.pfx");
            _proxyServer.CertificateManager.PfxPassword = "LTI2026";
            _proxyServer.CertificateManager.RootCertificateName = "LocalTrafficInspector Root CA";

            if (settings.EnableSsl)
            {
                _proxyServer.CertificateManager.EnsureRootCertificate();
                // 인증서가 이미 신뢰 저장소에 있으면 다시 설치하지 않음
                if (!CertificateService.IsRootCertInstalledStatic())
                {
                    _proxyServer.CertificateManager.TrustRootCertificate(true);
                }
            }

            // 이벤트 핸들러
            _proxyServer.BeforeRequest += OnBeforeRequest;
            _proxyServer.BeforeResponse += OnBeforeResponse;
            _proxyServer.ServerCertificateValidationCallback += OnServerCertificateValidation;
            _proxyServer.ExceptionFunc = OnProxyException;

            // 엔드포인트
            _endPoint = new ExplicitProxyEndPoint(
                IPAddress.Parse(settings.ListenAddress),
                settings.Port,
                decryptSsl: settings.EnableSsl
            );

            _proxyServer.AddEndPoint(_endPoint);
            _proxyServer.Start();

            // 중요: 시스템 프록시로 등록하지 않음
            // SetAsSystemHttpProxy/SetAsSystemHttpsProxy를 호출하지 않으면
            // Stop() 시에도 시스템 프록시 설정을 건드리지 않음
            _isRunning = true;

            Debug.WriteLine($"[ProxyService] Started on {settings.ListenAddress}:{settings.Port}, SSL={settings.EnableSsl}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"프록시 시작 실패: {ex.Message}");
            Stop();
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (_proxyServer == null) return;

        // Stop/Dispose 전에 시스템 프록시 설정 백업
        var proxyBefore = GetSystemProxySettings();

        try
        {
            _proxyServer.BeforeRequest -= OnBeforeRequest;
            _proxyServer.BeforeResponse -= OnBeforeResponse;
            _proxyServer.ServerCertificateValidationCallback -= OnServerCertificateValidation;

            if (_isRunning)
                _proxyServer.Stop();

            _proxyServer.Dispose();
        }
        catch { }
        finally
        {
            _proxyServer = null;
            _endPoint = null;
            _isRunning = false;
            Debug.WriteLine("[ProxyService] Stopped");
        }

        // Titanium이 시스템 프록시를 꺼버렸다면 복원
        RestoreSystemProxySettings(proxyBefore);
    }

    /// <summary>현재 시스템 프록시 설정을 읽어옵니다.</summary>
    private static (int enabled, string? server) GetSystemProxySettings()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false);
            if (key == null) return (0, null);

            var enabled = (int)(key.GetValue("ProxyEnable") ?? 0);
            var server = key.GetValue("ProxyServer") as string;
            return (enabled, server);
        }
        catch { return (0, null); }
    }

    /// <summary>시스템 프록시 설정을 복원합니다.</summary>
    private static void RestoreSystemProxySettings((int enabled, string? server) settings)
    {
        try
        {
            var current = GetSystemProxySettings();
            // Titanium이 프록시를 꺼버렸다면 (이전에는 켜져있었는데 지금 꺼져있음)
            if (settings.enabled == 1 && current.enabled == 0)
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                if (key == null) return;

                key.SetValue("ProxyEnable", settings.enabled);
                if (settings.server != null)
                    key.SetValue("ProxyServer", settings.server);

                Debug.WriteLine("[ProxyService] Restored system proxy settings");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProxyService] Failed to restore proxy: {ex.Message}");
        }
    }

    public void ResetCounter() => Interlocked.Exchange(ref _sessionCounter, 0);

    // ──────────────────── 이벤트 핸들러 ────────────────────

    private async Task OnBeforeRequest(object sender, SessionEventArgs e)
    {
        try
        {
            var request = e.HttpClient.Request;

            // CONNECT 터널 자체는 기록하지 않음 (HTTPS 핸드셰이크 단계)
            // 실제 요청은 터널 내부에서 별도로 BeforeRequest가 다시 호출됨
            if (request.Method?.Equals("CONNECT", StringComparison.OrdinalIgnoreCase) == true)
                return;

            var session = new TrafficSession
            {
                Id = Interlocked.Increment(ref _sessionCounter),
                Timestamp = DateTime.Now,
                Method = request.Method?.ToUpper() ?? "UNKNOWN",
                Host = request.Host ?? string.Empty,
                Url = request.Url ?? string.Empty,
                IsHttps = e.IsHttps,
                RequestHeaders = FormatHeaders(request.Headers),
                ContentLength = request.ContentLength
            };

            // 요청 바디 읽기
            if (request.HasBody)
            {
                try
                {
                    if (request.ContentLength > _maxBodySize)
                    {
                        session.RequestBody = $"[요청 바디 너무 큼: {FormatSize(request.ContentLength)}]";
                    }
                    else
                    {
                        var body = await e.GetRequestBodyAsString();
                        session.RequestBody = body ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    session.RequestBody = $"[바디 읽기 실패: {ex.Message}]";
                }
            }

            e.UserData = (session, Stopwatch.StartNew());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProxyService] OnBeforeRequest error: {ex.Message}");
        }
    }

    private async Task OnBeforeResponse(object sender, SessionEventArgs e)
    {
        try
        {
            if (e.UserData is not (TrafficSession session, Stopwatch sw))
                return;

            sw.Stop();
            var response = e.HttpClient.Response;

            session.StatusCode = response.StatusCode;
            session.ContentType = response.ContentType ?? string.Empty;
            session.ResponseHeaders = FormatHeaders(response.Headers);
            session.DurationMs = sw.ElapsedMilliseconds;

            // 응답 바디 읽기
            if (response.HasBody)
            {
                try
                {
                    if (response.ContentLength > _maxBodySize)
                    {
                        session.ResponseBody = $"[응답 바디 너무 큼: {FormatSize(response.ContentLength)}]";
                    }
                    else
                    {
                        var bodyBytes = await e.GetResponseBody();
                        if (bodyBytes.Length > _maxBodySize)
                        {
                            session.ResponseBody = $"[응답 바디 너무 큼: {FormatSize(bodyBytes.Length)}]";
                        }
                        else
                        {
                            session.ResponseBody = DecodeBody(bodyBytes, response.ContentType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    session.ResponseBody = $"[바디 읽기 실패: {ex.Message}]";
                }
            }

            SessionCaptured?.Invoke(session);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProxyService] OnBeforeResponse error: {ex.Message}");
        }
    }

    private Task OnServerCertificateValidation(object sender, CertificateValidationEventArgs e)
    {
        // 개발용 프록시이므로 서버 인증서 검증 스킵
        e.IsValid = true;
        return Task.CompletedTask;
    }

    private void OnProxyException(Exception ex)
    {
        if (ex is System.IO.IOException || ex is ObjectDisposedException)
            return;
        Debug.WriteLine($"[ProxyService] Exception: {ex.GetType().Name}: {ex.Message}");
    }

    // ──────────────────── 유틸리티 ────────────────────

    private static string DecodeBody(byte[] bodyBytes, string? contentType)
    {
        if (bodyBytes.Length == 0) return string.Empty;

        try
        {
            var encoding = ParseEncoding(contentType);
            return encoding.GetString(bodyBytes);
        }
        catch
        {
            return $"[바이너리 데이터 - {FormatSize(bodyBytes.Length)}]";
        }
    }

    private static Encoding ParseEncoding(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return Encoding.UTF8;

        foreach (var part in contentType.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("charset=", StringComparison.OrdinalIgnoreCase))
            {
                var charsetName = trimmed["charset=".Length..].Trim().Trim('"');
                try { return Encoding.GetEncoding(charsetName); }
                catch { return Encoding.UTF8; }
            }
        }
        return Encoding.UTF8;
    }

    private static string FormatHeaders(HeaderCollection headers)
    {
        var sb = new StringBuilder();
        foreach (var header in headers)
            sb.AppendLine($"{header.Name}: {header.Value}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
