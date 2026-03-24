// Models/TrafficSession.cs
// 하나의 HTTP 요청/응답 트랜잭션을 표현하는 데이터 모델.
// DataGrid 바인딩 및 상세 보기에서 사용됩니다.

namespace LocalTrafficInspector.Models;

/// <summary>
/// 프록시를 통과한 하나의 HTTP 요청/응답 쌍
/// </summary>
public class TrafficSession
{
    /// <summary>고유 식별자 (자동 증가)</summary>
    public int Id { get; set; }

    /// <summary>요청 수신 시각</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>HTTP 메서드 (GET, POST, PUT, DELETE 등)</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>요청 호스트 (예: www.example.com)</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>전체 URL 경로</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>HTTP 응답 상태 코드 (200, 404 등). 응답 전에는 0</summary>
    public int StatusCode { get; set; }

    /// <summary>응답 Content-Type (예: application/json)</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>요청 헤더 (줄바꿈 구분 텍스트)</summary>
    public string RequestHeaders { get; set; } = string.Empty;

    /// <summary>요청 본문</summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>응답 헤더 (줄바꿈 구분 텍스트)</summary>
    public string ResponseHeaders { get; set; } = string.Empty;

    /// <summary>응답 본문</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>요청~응답 소요 시간 (밀리초)</summary>
    public long DurationMs { get; set; }

    /// <summary>HTTPS 여부</summary>
    public bool IsHttps { get; set; }

    /// <summary>요청 Content-Length</summary>
    public long ContentLength { get; set; }

    /// <summary>표시용: 시간 포맷</summary>
    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss.fff");

    /// <summary>표시용: 상태코드 (0이면 빈 문자열)</summary>
    public string StatusCodeDisplay => StatusCode == 0 ? "-" : StatusCode.ToString();

    /// <summary>표시용: 프로토콜</summary>
    public string Protocol => IsHttps ? "HTTPS" : "HTTP";

    /// <summary>Overview 요약 텍스트</summary>
    public string OverviewText =>
        $"[{Protocol}] {Method} {Url}\n" +
        $"Status: {StatusCodeDisplay}\n" +
        $"Host: {Host}\n" +
        $"Content-Type: {ContentType}\n" +
        $"Duration: {DurationMs}ms\n" +
        $"Time: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
}
