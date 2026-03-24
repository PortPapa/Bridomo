// Services/SettingsService.cs
// 프록시 설정(host, port 등)을 JSON 파일로 저장/로드하는 서비스.
// 앱 재시작 시에도 사용자 설정이 유지됩니다.

using System.IO;
using System.Text.Json;
using LocalTrafficInspector.Models;

namespace LocalTrafficInspector.Services;

/// <summary>
/// ProxySettings를 로컬 JSON 파일에 영속화하는 서비스
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "LocalTrafficInspector");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly string LangPath =
        Path.Combine(SettingsDir, "language.txt");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 설정을 파일에서 로드합니다. 파일이 없으면 기본값을 반환합니다.
    /// </summary>
    public ProxySettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new ProxySettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ProxySettings>(json) ?? new ProxySettings();
        }
        catch
        {
            // 설정 파일이 손상된 경우 기본값 반환
            return new ProxySettings();
        }
    }

    /// <summary>
    /// 설정을 파일에 저장합니다.
    /// </summary>
    public void Save(ProxySettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 저장 실패는 무시 (다음 실행 시 기본값 사용)
        }
    }

    /// <summary>저장된 언어 코드를 로드합니다. 기본값: ko</summary>
    public string LoadLanguage()
    {
        try
        {
            if (File.Exists(LangPath))
                return File.ReadAllText(LangPath).Trim();
        }
        catch { }
        return "ko";
    }

    /// <summary>언어 코드를 파일에 저장합니다.</summary>
    public void SaveLanguage(string langCode)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(LangPath, langCode);
        }
        catch { }
    }
}
