using System.Text.Json.Serialization;

namespace LocalTrafficInspector.Models;

/// <summary>
/// 크롬 익스텐션에서 전송된 하나의 DOM 인터랙션 이벤트
/// </summary>
public class DomEvent
{
    public int Id { get; set; }

    /// <summary>이벤트 발생 시각 (UTC ms → 로컬 DateTime 변환)</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>이벤트 타입: click, input, change, submit, navigate, scroll</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>이벤트가 발생한 페이지 URL</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>CSS 셀렉터</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>XPath</summary>
    public string XPath { get; set; } = string.Empty;

    /// <summary>태그 이름 (BUTTON, INPUT, A 등)</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>요소의 텍스트 내용</summary>
    public string InnerText { get; set; } = string.Empty;

    /// <summary>input/change 이벤트의 값</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>요소 속성 (class, id, href 등)</summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>클릭 좌표 (뷰포트 기준)</summary>
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    /// <summary>Canvas 내부 상대 좌표 (canvas 좌상단 기준)</summary>
    public int CanvasRelX { get; set; }
    public int CanvasRelY { get; set; }

    /// <summary>Canvas 비율 좌표 (0~1, 크기 무관)</summary>
    public double CanvasRatioX { get; set; }
    public double CanvasRatioY { get; set; }

    /// <summary>Canvas 크기</summary>
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }

    /// <summary>매칭된 API 트래픽 세션 ID (null이면 매칭 안 됨)</summary>
    public int? MatchedSessionId { get; set; }

    // ── 표시용 ──

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss.fff");

    public string Summary => EventType switch
    {
        "click" => $"Click: {TagName} \"{Truncate(InnerText, 30)}\"",
        "input" or "change" => string.IsNullOrEmpty(Value)
            ? $"Input: {TagName} [{SelectorShort}]"
            : $"Input: {TagName} [{SelectorShort}] = \"{Truncate(Value, 30)}\"",
        "submit" => $"Submit: {Selector}",
        "navigate" => $"Navigate: {Url}",
        "scroll" => $"Scroll: ({PositionX}, {PositionY})",
        _ => $"{EventType}: {TagName}"
    };

    private string SelectorShort => Selector.Length > 25 ? Selector[..25] + "..." : Selector;

    public string MatchStatus => MatchedSessionId.HasValue ? $"→ #{MatchedSessionId}" : "";

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "...";
}
