using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalTrafficInspector.Models;

namespace LocalTrafficInspector.Services;

/// <summary>
/// 캡처된 세션 목록을 JSON 또는 TXT 파일로 내보내는 서비스.
/// </summary>
public class ExportService
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    /// <summary>
    /// 세션 목록을 JSON 파일로 내보냅니다.
    /// </summary>
    public async Task ExportAsJsonAsync(IEnumerable<TrafficSession> sessions, string filePath)
    {
        var exportData = sessions.Select(s => new
        {
            s.Id,
            Timestamp = s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            s.Method,
            s.Host,
            s.Url,
            s.StatusCode,
            s.ContentType,
            s.DurationMs,
            Protocol = s.IsHttps ? "HTTPS" : "HTTP",
            s.RequestHeaders,
            s.RequestBody,
            s.ResponseHeaders,
            s.ResponseBody
        });

        var json = JsonSerializer.Serialize(exportData, ExportJsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// 세션 목록을 사람이 읽기 쉬운 TXT 파일로 내보냅니다.
    /// </summary>
    public async Task ExportAsTxtAsync(IEnumerable<TrafficSession> sessions, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine("  Bridomo - Export");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine();

        foreach (var s in sessions)
        {
            sb.AppendLine($"── #{s.Id} ──────────────────────────────────────");
            sb.AppendLine($"  Time:         {s.Timestamp:HH:mm:ss.fff}");
            sb.AppendLine($"  Method:       {s.Method}");
            sb.AppendLine($"  URL:          {s.Url}");
            sb.AppendLine($"  Status:       {s.StatusCodeDisplay}");
            sb.AppendLine($"  Content-Type: {s.ContentType}");
            sb.AppendLine($"  Duration:     {s.DurationMs}ms");
            sb.AppendLine($"  Protocol:     {s.Protocol}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(s.RequestHeaders))
            {
                sb.AppendLine("  [Request Headers]");
                foreach (var line in s.RequestHeaders.Split('\n'))
                    sb.AppendLine($"    {line.TrimEnd()}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(s.RequestBody))
            {
                sb.AppendLine("  [Request Body]");
                sb.AppendLine($"    {Truncate(s.RequestBody, 2000)}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(s.ResponseHeaders))
            {
                sb.AppendLine("  [Response Headers]");
                foreach (var line in s.ResponseHeaders.Split('\n'))
                    sb.AppendLine($"    {line.TrimEnd()}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(s.ResponseBody))
            {
                sb.AppendLine("  [Response Body]");
                sb.AppendLine($"    {Truncate(s.ResponseBody, 2000)}");
                sb.AppendLine();
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + $"\n    ... (truncated, total {text.Length} chars)";
    }
}
