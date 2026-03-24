using System.Windows;

namespace LocalTrafficInspector.Services;

/// <summary>
/// 런타임에 언어 ResourceDictionary를 교체하여 UI 언어를 전환합니다.
/// DynamicResource로 바인딩된 텍스트가 즉시 바뀝니다.
/// </summary>
public class LocalizationService
{
    private const string LangDictPrefix = "Resources/Lang.";
    private const string LangDictSuffix = ".xaml";

    private string _currentLanguage = "en";

    public string CurrentLanguage => _currentLanguage;

    public string[] AvailableLanguages { get; } = ["en", "ko"];
    public string[] LanguageDisplayNames { get; } = ["English", "한국어"];

    /// <summary>
    /// 지정된 언어로 UI를 전환합니다.
    /// Application.Resources에서 기존 언어 사전을 제거하고 새 사전을 추가합니다.
    /// </summary>
    public void SwitchLanguage(string langCode)
    {
        if (_currentLanguage == langCode) return;

        var app = Application.Current;
        if (app == null) return;

        // 기존 언어 사전 제거
        ResourceDictionary? toRemove = null;
        foreach (var dict in app.Resources.MergedDictionaries)
        {
            if (dict.Source != null && dict.Source.OriginalString.Contains(LangDictPrefix))
            {
                toRemove = dict;
                break;
            }
        }
        if (toRemove != null)
            app.Resources.MergedDictionaries.Remove(toRemove);

        // 새 언어 사전 추가
        var uri = new Uri($"{LangDictPrefix}{langCode}{LangDictSuffix}", UriKind.Relative);
        var newDict = new ResourceDictionary { Source = uri };
        app.Resources.MergedDictionaries.Add(newDict);

        _currentLanguage = langCode;
    }

    /// <summary>
    /// 코드에서 문자열 리소스를 가져옵니다. (ViewModel 등에서 사용)
    /// </summary>
    public static string GetString(string key)
    {
        var value = Application.Current?.TryFindResource(key);
        return value as string ?? key;
    }
}
