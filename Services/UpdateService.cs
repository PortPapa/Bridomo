using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace LocalTrafficInspector.Services;

public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/PortPapa/Bridomo/releases/latest";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            UserAgent = { new ProductInfoHeaderValue("Bridomo", CurrentVersion) }
        }
    };

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task CheckAndApplyUpdatesAsync()
    {
        try
        {
            var response = await Http.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[UpdateService] GitHub API 응답 실패: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString();
            if (string.IsNullOrEmpty(tagName)) return;

            var latestVersion = tagName.TrimStart('v');
            if (!Version.TryParse(latestVersion, out var latest) ||
                !Version.TryParse(CurrentVersion, out var current))
                return;

            if (latest <= current)
            {
                Debug.WriteLine("[UpdateService] 최신 버전입니다.");
                return;
            }

            Debug.WriteLine($"[UpdateService] 업데이트 발견: {latestVersion}");

            // Setup exe 에셋 찾기
            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("Bridomo-Setup-", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl)) return;

            var result = MessageBox.Show(
                $"새 버전 {latestVersion}이(가) 있습니다.\n지금 업데이트하시겠습니까?",
                "Bridomo 업데이트",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            var tempPath = Path.Combine(Path.GetTempPath(), $"Bridomo-Setup-{latestVersion}.exe");

            using (var stream = await Http.GetStreamAsync(downloadUrl))
            await using (var fs = File.Create(tempPath))
            {
                await stream.CopyToAsync(fs);
            }

            Debug.WriteLine($"[UpdateService] 다운로드 완료: {tempPath}");

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] 업데이트 실패: {ex.Message}");
        }
    }
}
