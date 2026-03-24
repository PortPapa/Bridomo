// Models/ProxySettings.cs
// 프록시 서버 설정 모델. 주소, 포트, HTTPS 활성화 여부를 보관합니다.
// SettingsService에서 로드/저장에 사용됩니다.

namespace LocalTrafficInspector.Models;

/// <summary>
/// 프록시 서버 설정 값
/// </summary>
public class ProxySettings
{
    /// <summary>프록시 수신 주소 (기본: 127.0.0.1)</summary>
    public string ListenAddress { get; set; } = "127.0.0.1";

    /// <summary>프록시 수신 포트 (기본: 8888)</summary>
    public int Port { get; set; } = 8888;

    /// <summary>HTTPS 트래픽 복호화 활성화 여부</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>캡처할 최대 바디 크기 (바이트). 초과 시 잘림 (기본 1MB)</summary>
    public int MaxBodySize { get; set; } = 1_048_576;
}
