// Services/JsonFormatterService.cs
// JSON 문자열을 pretty print 형태로 변환하는 서비스.
// 응답 바디가 JSON일 때 보기 좋게 포맷팅합니다.

using System.Text.Json;

namespace LocalTrafficInspector.Services;

/// <summary>
/// JSON 포맷팅 유틸리티 서비스
/// </summary>
public class JsonFormatterService
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// JSON 문자열을 들여쓰기된 형태로 변환합니다.
    /// 유효한 JSON이 아니면 원본 그대로 반환합니다.
    /// </summary>
    public string FormatJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return rawJson;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(doc.RootElement, PrettyOptions);
        }
        catch (JsonException)
        {
            // JSON이 아니면 원본 반환
            return rawJson;
        }
    }

    /// <summary>
    /// 주어진 Content-Type이 JSON인지 판별합니다.
    /// </summary>
    public static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;

        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase);
    }
}
