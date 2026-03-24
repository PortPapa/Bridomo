using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LocalTrafficInspector.Models;

namespace LocalTrafficInspector.Services;

/// <summary>
/// WebSocket 서버. 크롬 익스텐션으로부터 DOM 이벤트를 실시간 수신합니다.
/// HttpListener + WebSocket을 사용하여 NuGet 없이 동작합니다.
/// </summary>
public class WebSocketService : IDisposable
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly List<WebSocket> _clients = new();
    private readonly object _lock = new();
    private int _eventCounter;
    private bool _isRunning;

    public bool IsRunning => _isRunning;
    public int ClientCount { get { lock (_lock) return _clients.Count; } }

    /// <summary>DOM 이벤트를 수신하면 발생</summary>
    public event Action<DomEvent>? DomEventReceived;

    /// <summary>클라이언트 연결/해제 시 발생</summary>
    public event Action<int>? ClientCountChanged;

    /// <summary>에러 발생 시</summary>
    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// WebSocket 서버를 시작합니다.
    /// </summary>
    public Task StartAsync(string listenAddress = "127.0.0.1", int port = 5555)
    {
        if (_isRunning) return Task.CompletedTask;

        try
        {
            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://{listenAddress}:{port}/");
            _httpListener.Start();
            _isRunning = true;

            // 백그라운드에서 연결 수락 루프 실행
            _ = AcceptClientsLoopAsync(_cts.Token);

            Debug.WriteLine($"[WebSocketService] Started on ws://{listenAddress}:{port}");
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            // Access Denied: URL reservation 필요
            ErrorOccurred?.Invoke(
                $"WebSocket 서버 시작 실패 (권한 부족).\n" +
                $"관리자 CMD에서 실행:\nnetsh http add urlacl url=http://{listenAddress}:{port}/ user=Everyone");
            _isRunning = false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"WebSocket 서버 시작 실패: {ex.Message}");
            _isRunning = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>서버를 중지합니다.</summary>
    public void Stop()
    {
        _cts?.Cancel();

        lock (_lock)
        {
            foreach (var ws in _clients)
            {
                try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None).Wait(1000); }
                catch { }
                try { ws.Dispose(); }
                catch { }
            }
            _clients.Clear();
        }

        try { _httpListener?.Stop(); }
        catch { }
        try { _httpListener?.Close(); }
        catch { }

        _httpListener = null;
        _isRunning = false;
        ClientCountChanged?.Invoke(0);
        Debug.WriteLine("[WebSocketService] Stopped");
    }

    public void ResetCounter() => Interlocked.Exchange(ref _eventCounter, 0);

    // ──────────────────── 연결 수락 루프 ────────────────────

    private async Task AcceptClientsLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var ws = wsContext.WebSocket;

                    lock (_lock) _clients.Add(ws);
                    ClientCountChanged?.Invoke(ClientCount);
                    Debug.WriteLine($"[WebSocketService] Client connected. Total: {ClientCount}");

                    _ = HandleClientAsync(ws, ct);
                }
                else
                {
                    // WebSocket이 아닌 요청은 CORS preflight 등 처리
                    context.Response.StatusCode = 200;
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                    context.Response.Headers.Add("Access-Control-Allow-Headers", "*");
                    context.Response.Close();
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Debug.WriteLine($"[WebSocketService] Accept error: {ex.Message}");
            }
        }
    }

    // ──────────────────── 클라이언트 메시지 처리 ────────────────────

    private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(json);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebSocketService] Client error: {ex.Message}");
        }
        finally
        {
            lock (_lock) _clients.Remove(ws);
            ClientCountChanged?.Invoke(ClientCount);
            Debug.WriteLine($"[WebSocketService] Client disconnected. Total: {ClientCount}");

            try { ws.Dispose(); }
            catch { }
        }
    }

    /// <summary>JSON 메시지를 파싱하여 DomEvent로 변환</summary>
    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var domEvent = new DomEvent
            {
                Id = Interlocked.Increment(ref _eventCounter),
                EventType = root.GetPropertySafe("type"),
                Url = root.GetPropertySafe("url"),
                Selector = root.GetPropertySafe("selector"),
                XPath = root.GetPropertySafe("xpath"),
                TagName = root.GetPropertySafe("tagName"),
                InnerText = root.GetPropertySafe("innerText"),
                Value = root.GetPropertySafe("value"),
            };

            // timestamp: JS의 Date.now() (UTC ms) → 로컬 DateTime
            if (root.TryGetProperty("timestamp", out var tsProp) && tsProp.TryGetInt64(out var tsMs))
            {
                domEvent.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).LocalDateTime;
            }
            else
            {
                domEvent.Timestamp = DateTime.Now;
            }

            // position (뷰포트 좌표)
            if (root.TryGetProperty("position", out var pos))
            {
                if (pos.TryGetProperty("x", out var px)) domEvent.PositionX = px.GetInt32();
                if (pos.TryGetProperty("y", out var py)) domEvent.PositionY = py.GetInt32();
            }

            // canvasPosition (canvas 내부 상대/비율 좌표)
            if (root.TryGetProperty("canvasPosition", out var cp) && cp.ValueKind == JsonValueKind.Object)
            {
                if (cp.TryGetProperty("relX", out var rx)) domEvent.CanvasRelX = rx.GetInt32();
                if (cp.TryGetProperty("relY", out var ry)) domEvent.CanvasRelY = ry.GetInt32();
                if (cp.TryGetProperty("ratioX", out var rax)) domEvent.CanvasRatioX = rax.GetDouble();
                if (cp.TryGetProperty("ratioY", out var ray)) domEvent.CanvasRatioY = ray.GetDouble();
                if (cp.TryGetProperty("canvasWidth", out var cw)) domEvent.CanvasWidth = cw.GetInt32();
                if (cp.TryGetProperty("canvasHeight", out var ch)) domEvent.CanvasHeight = ch.GetInt32();
            }

            // attributes
            if (root.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in attrs.EnumerateObject())
                {
                    domEvent.Attributes[prop.Name] = prop.Value.GetString() ?? "";
                }
            }

            DomEventReceived?.Invoke(domEvent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebSocketService] Parse error: {ex.Message} | JSON: {json}");
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

/// <summary>JsonElement 확장 메서드</summary>
internal static class JsonElementExtensions
{
    public static string GetPropertySafe(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString() ?? string.Empty;
        return string.Empty;
    }
}
