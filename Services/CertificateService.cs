using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Network;

namespace LocalTrafficInspector.Services;

/// <summary>
/// HTTPS 트래픽 복호화를 위한 루트 인증서 생성/설치/상태 확인을 담당합니다.
/// </summary>
public class CertificateService
{
    private const string CertIssuerKeyword = "Titanium";
    private const string CertIssuerKeyword2 = "LocalTrafficInspector";

    /// <summary>
    /// 시스템 신뢰 저장소(CurrentUser\Root)에 루트 인증서가 있는지 확인합니다.
    /// </summary>
    public bool IsRootCertificateInstalled() => IsRootCertInstalledStatic();

    /// <summary>Static 버전 - ProxyService에서도 사용</summary>
    public static bool IsRootCertInstalledStatic()
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                if (cert.Issuer.Contains(CertIssuerKeyword, StringComparison.OrdinalIgnoreCase)
                    || cert.Issuer.Contains(CertIssuerKeyword2, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 루트 인증서를 생성하고 CurrentUser\Root 저장소에 설치합니다.
    /// Windows 보안 경고 팝업이 표시됩니다 (사용자가 "예" 클릭 필요).
    /// </summary>
    /// <returns>설치 성공 여부</returns>
    public (bool success, string message) InstallRootCertificate()
    {
        try
        {
            // Titanium.Web.Proxy로 인증서 생성
            using var proxyServer = new ProxyServer(userTrustRootCertificate: true);
            proxyServer.CertificateManager.CertificateEngine = CertificateEngine.BouncyCastleFast;
            proxyServer.CertificateManager.EnsureRootCertificate();
            proxyServer.CertificateManager.TrustRootCertificate(true);

            var rootCert = proxyServer.CertificateManager.RootCertificate;
            if (rootCert == null)
            {
                return (false, "인증서 생성에 실패했습니다.");
            }

            // CurrentUser\Root 저장소에 직접 설치
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(rootCert);
            store.Close();

            // 설치 확인
            if (IsRootCertificateInstalled())
            {
                return (true, "인증서가 성공적으로 설치되었습니다.\n\n" +
                              "이제 HTTPS 트래픽을 캡처할 수 있습니다.\n" +
                              "브라우저를 재시작한 후 사용하세요.");
            }
            else
            {
                return (false, "인증서 설치가 취소되었거나 실패했습니다.\n" +
                               "Windows 보안 경고에서 '예'를 선택해야 합니다.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"인증서 설치 중 오류 발생:\n{ex.Message}");
        }
    }

    /// <summary>
    /// 설치된 Titanium 루트 인증서를 제거합니다.
    /// </summary>
    public (bool success, string message) UninstallRootCertificate()
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var toRemove = new List<X509Certificate2>();
            foreach (var cert in store.Certificates)
            {
                if (cert.Issuer.Contains(CertIssuerKeyword, StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(cert);
            }

            if (toRemove.Count == 0)
                return (true, "제거할 인증서가 없습니다.");

            foreach (var cert in toRemove)
                store.Remove(cert);

            store.Close();
            return (true, $"{toRemove.Count}개의 인증서를 제거했습니다.");
        }
        catch (Exception ex)
        {
            return (false, $"인증서 제거 중 오류 발생:\n{ex.Message}");
        }
    }

    /// <summary>상태바용 짧은 상태 텍스트</summary>
    public string GetShortStatus()
    {
        return IsRootCertificateInstalled() ? "Cert: Installed" : "Cert: Not Installed";
    }
}
