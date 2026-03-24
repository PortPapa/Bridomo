using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LocalTrafficInspector.Services;
using LocalTrafficInspector.ViewModels;
namespace LocalTrafficInspector;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    [STAThread]
    private static void Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // 서비스
        services.AddSingleton<ProxyService>();
        services.AddSingleton<JsonFormatterService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<CertificateService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<WebSocketService>();
        services.AddSingleton<UpdateService>();

        // ViewModel
        services.AddSingleton<MainViewModel>();

        // View
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 백그라운드에서 업데이트 체크
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateService = _serviceProvider?.GetService<UpdateService>();
            if (updateService != null)
            {
                await updateService.CheckAndApplyUpdatesAsync();
            }
        }
        catch
        {
            // 업데이트 실패해도 앱은 정상 동작
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            var vm = _serviceProvider.GetService<MainViewModel>();
            vm?.Dispose();
            _serviceProvider.Dispose();
        }
        base.OnExit(e);
    }
}
