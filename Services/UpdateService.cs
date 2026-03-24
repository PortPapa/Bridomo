using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace LocalTrafficInspector.Services;

public class UpdateService
{
    private const string GitHubRepoUrl = "https://github.com/PortPapa/Bridomo";

    private readonly UpdateManager _updateManager;

    public UpdateService()
    {
        var source = new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: false);
        _updateManager = new UpdateManager(source);
    }

    public bool IsInstalled => _updateManager.IsInstalled;

    public string? CurrentVersion => _updateManager.CurrentVersion?.ToString();

    /// <summary>
    /// 업데이트 확인 → 다운로드 → 앱 종료 시 자동 적용
    /// </summary>
    public async Task CheckAndApplyUpdatesAsync()
    {
        if (!IsInstalled)
        {
            Debug.WriteLine("[UpdateService] Velopack 설치가 아님 (개발모드) - 업데이트 스킵");
            return;
        }

        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                Debug.WriteLine("[UpdateService] 최신 버전입니다.");
                return;
            }

            Debug.WriteLine($"[UpdateService] 업데이트 발견: {updateInfo.TargetFullRelease.Version}");

            await _updateManager.DownloadUpdatesAsync(updateInfo);
            Debug.WriteLine("[UpdateService] 다운로드 완료 - 앱 종료 시 적용됩니다.");

            // 앱 종료 시 자동으로 업데이트 적용
            _updateManager.WaitExitThenApplyUpdates(updateInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] 업데이트 실패: {ex.Message}");
        }
    }
}
