using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LocalTrafficInspector.Models;
using LocalTrafficInspector.Services;

namespace LocalTrafficInspector.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    private readonly ProxyService _proxyService;
    private readonly JsonFormatterService _jsonFormatter;
    private readonly SettingsService _settingsService;
    private readonly CertificateService _certificateService;
    private readonly ExportService _exportService;
    private readonly LocalizationService _localizationService;
    private readonly WebSocketService _webSocketService;

    // ════════════════════════════════════════
    // 세션 컬렉션
    // ════════════════════════════════════════

    public ObservableCollection<TrafficSession> Sessions { get; } = new();
    public ICollectionView SessionsView { get; }

    /// <summary>크롬 익스텐션에서 수신한 DOM 이벤트 목록</summary>
    public ObservableCollection<DomEvent> DomEvents { get; } = new();

    private void DeleteDomEvent(int? id)
    {
        if (id == null) return;
        var ev = DomEvents.FirstOrDefault(d => d.Id == id.Value);
        if (ev != null)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() => DomEvents.Remove(ev));
            OnPropertyChanged(nameof(DomEventCountText));
        }
    }

    // ════════════════════════════════════════
    // WebSocket 상태
    // ════════════════════════════════════════

    private string _wsStatus = "WS: Off";
    public string WsStatus
    {
        get => _wsStatus;
        private set => SetProperty(ref _wsStatus, value);
    }

    public string DomEventCountText => $"DOM: {DomEvents.Count}";

    // ════════════════════════════════════════
    // 프록시 상태
    // ════════════════════════════════════════

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(StartStopButtonText));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CanEditSettings));
            }
        }
    }

    public string StartStopButtonText => IsRunning
        ? LocalizationService.GetString("Str_Stop")
        : LocalizationService.GetString("Str_Start");
    public bool CanEditSettings => !IsRunning;

    public string StatusText => IsRunning
        ? $"{LocalizationService.GetString("Str_ProxyRunning")} {ListenAddress}:{Port}"
        : LocalizationService.GetString("Str_ProxyStopped");

    // ════════════════════════════════════════
    // 설정
    // ════════════════════════════════════════

    private string _listenAddress = "127.0.0.1";
    public string ListenAddress
    {
        get => _listenAddress;
        set => SetProperty(ref _listenAddress, value);
    }

    private int _port = 8888;
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private bool _enableSsl = true;
    public bool EnableSsl
    {
        get => _enableSsl;
        set => SetProperty(ref _enableSsl, value);
    }

    // ════════════════════════════════════════
    // UI 레이아웃
    // ════════════════════════════════════════

    private bool _isSettingsPanelOpen;
    /// <summary>설정 패널 열림/닫힘</summary>
    public bool IsSettingsPanelOpen
    {
        get => _isSettingsPanelOpen;
        set => SetProperty(ref _isSettingsPanelOpen, value);
    }

    private bool _isVerticalSplit;
    /// <summary>true=좌우 분할, false=상하 분할</summary>
    public bool IsVerticalSplit
    {
        get => _isVerticalSplit;
        set => SetProperty(ref _isVerticalSplit, value);
    }

    // ════════════════════════════════════════
    // 필터
    // ════════════════════════════════════════

    private string _filterHost = string.Empty;
    public string FilterHost
    {
        get => _filterHost;
        set { if (SetProperty(ref _filterHost, value)) SessionsView.Refresh(); }
    }

    private string _filterMethod = "All";
    public string FilterMethod
    {
        get => _filterMethod;
        set { if (SetProperty(ref _filterMethod, value)) SessionsView.Refresh(); }
    }

    private string _filterStatusCode = string.Empty;
    public string FilterStatusCode
    {
        get => _filterStatusCode;
        set { if (SetProperty(ref _filterStatusCode, value)) SessionsView.Refresh(); }
    }

    public string[] MethodOptions { get; } =
        ["All", "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "CONNECT"];

    private bool _hideJunkApis = true;
    /// <summary>광고/트래킹 API 자동 숨김 토글</summary>
    public bool HideJunkApis
    {
        get => _hideJunkApis;
        set { if (SetProperty(ref _hideJunkApis, value)) { _showOnlyJunk = false; OnPropertyChanged(nameof(ShowOnlyJunk)); SessionsView.Refresh(); } }
    }

    private bool _showOnlyJunk;
    /// <summary>정크 API만 표시 (정크 관리용)</summary>
    public bool ShowOnlyJunk
    {
        get => _showOnlyJunk;
        set { if (SetProperty(ref _showOnlyJunk, value)) { if (value) { _hideJunkApis = false; OnPropertyChanged(nameof(HideJunkApis)); } SessionsView.Refresh(); } }
    }

    private static readonly string[] DefaultJunkHosts =
    [
        "google.com", "googleapis.com", "doubleclick.net", "google-analytics.com",
        "googletagmanager.com", "googlesyndication.com", "gstatic.com",
        "google.co.kr", "gvt2.com", "gvt1.com",
        "facebook.com", "facebook.net", "fbcdn.net",
        "clarity.ms",
        "acecounter.com",
        "analytics.google.com", "clients4.google.com",
        "cro.myshp.us", "mcro.myshp.us",
        "toast.com", "adlc-exchange.toast.com",
        "cmail.kakao.com", "wcs.naver.com",
        "hotjar.com", "sentry.io", "newrelic.com",
        "segment.io", "mixpanel.com", "amplitude.com",
        "beacons.gcp.gvt2.com", "cloudflare.com"
    ];

    // 사용자 정크: URL 경로까지 매칭 (예: "otlp-gateway.../otlp/v1/logs")
    private readonly HashSet<string> _customJunkUrls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string JunkFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "junk_urls.txt");

    public ICommand AddToJunkCommand { get; }
    public ICommand AddUrlToJunkCommand { get; }
    public ICommand DeleteDomEventCommand { get; }
    public ICommand RemoveFromJunkCommand { get; }

    private void LoadCustomJunkHosts()
    {
        try
        {
            // 기존 junk_hosts.txt도 호환 로드
            var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "junk_hosts.txt");
            if (File.Exists(oldPath))
            {
                foreach (var line in File.ReadAllLines(oldPath))
                {
                    var h = line.Trim();
                    if (!string.IsNullOrEmpty(h) && !h.StartsWith('#'))
                        _customJunkUrls.Add(h);
                }
            }

            if (File.Exists(JunkFilePath))
            {
                foreach (var line in File.ReadAllLines(JunkFilePath))
                {
                    var h = line.Trim();
                    if (!string.IsNullOrEmpty(h) && !h.StartsWith('#'))
                        _customJunkUrls.Add(h);
                }
            }
        }
        catch { /* ignore */ }
    }

    private void SaveCustomJunkUrls()
    {
        try
        {
            File.WriteAllLines(JunkFilePath, _customJunkUrls.Order());
        }
        catch { /* ignore */ }
    }

    /// <summary>호스트 전체를 정크에 추가</summary>
    private void AddHostToJunk(string? host)
    {
        if (string.IsNullOrEmpty(host)) return;
        _customJunkUrls.Add(host);
        SaveCustomJunkUrls();
        if (HideJunkApis) SessionsView.Refresh();
    }

    /// <summary>선택된 세션의 URL 경로를 정크에 추가</summary>
    private void AddUrlToJunk()
    {
        var session = SelectedSession;
        if (session == null) return;

        // host + path (쿼리 제거)
        try
        {
            var uri = new Uri(session.Url);
            var pattern = uri.Host + uri.AbsolutePath;
            _customJunkUrls.Add(pattern);
            SaveCustomJunkUrls();
            if (HideJunkApis) SessionsView.Refresh();
        }
        catch
        {
            // URL 파싱 실패 시 전체 URL 사용
            _customJunkUrls.Add(session.Url);
            SaveCustomJunkUrls();
            if (HideJunkApis) SessionsView.Refresh();
        }
    }

    /// <summary>정크에서 제거 (URL 패턴)</summary>
    private void RemoveFromJunk(string? host)
    {
        if (string.IsNullOrEmpty(host)) return;

        // host 이름으로 매칭되는 것 전부 제거
        var toRemove = _customJunkUrls.Where(u =>
            u.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith(host + "/", StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith(host + ":", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (toRemove.Count > 0)
        {
            foreach (var r in toRemove) _customJunkUrls.Remove(r);
        }
        else
        {
            _customJunkUrls.Remove(host);
        }

        SaveCustomJunkUrls();
        if (HideJunkApis) SessionsView.Refresh();
    }

    /// <summary>정크 여부 확인 — 기본 호스트 + 사용자 URL 패턴</summary>
    private bool IsJunkHost(string host, string? url = null)
    {
        if (string.IsNullOrEmpty(host)) return false;

        // 기본 정크 호스트 (analytics, tracking 등)
        if (DefaultJunkHosts.Any(j => host.Contains(j, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 사용자 정크: 정확한 URL 경로 매칭
        if (url != null)
        {
            try
            {
                var uri = new Uri(url);
                var hostPath = uri.Host + uri.AbsolutePath;

                foreach (var pattern in _customJunkUrls)
                {
                    // 패턴이 "/"를 포함하면 URL 경로 매칭
                    if (pattern.Contains('/'))
                    {
                        if (hostPath.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else
                    {
                        // 호스트만 있으면 호스트 전체 매칭 (레거시 호환)
                        if (host.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch { }
        }

        // URL 없이 호스트만으로도 체크
        return _customJunkUrls.Contains(host);
    }

    // ════════════════════════════════════════
    // 선택 세션 상세 보기
    // ════════════════════════════════════════

    private TrafficSession? _selectedSession;
    public TrafficSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
                UpdateDetailView();
        }
    }

    private string _overviewText = string.Empty;
    public string OverviewText
    {
        get => _overviewText;
        private set => SetProperty(ref _overviewText, value);
    }

    private string _requestHeadersText = string.Empty;
    public string RequestHeadersText
    {
        get => _requestHeadersText;
        private set => SetProperty(ref _requestHeadersText, value);
    }

    private string _requestBodyText = string.Empty;
    public string RequestBodyText
    {
        get => _requestBodyText;
        private set => SetProperty(ref _requestBodyText, value);
    }

    private string _responseHeadersText = string.Empty;
    public string ResponseHeadersText
    {
        get => _responseHeadersText;
        private set => SetProperty(ref _responseHeadersText, value);
    }

    private string _responseBodyText = string.Empty;
    public string ResponseBodyText
    {
        get => _responseBodyText;
        private set => SetProperty(ref _responseBodyText, value);
    }

    private bool _jsonPrettyPrint = true;
    public bool JsonPrettyPrint
    {
        get => _jsonPrettyPrint;
        set
        {
            if (SetProperty(ref _jsonPrettyPrint, value))
                UpdateDetailView();
        }
    }

    // ════════════════════════════════════════
    // 인증서 상태
    // ════════════════════════════════════════

    private string _certificateStatus = string.Empty;
    public string CertificateStatus
    {
        get => _certificateStatus;
        private set => SetProperty(ref _certificateStatus, value);
    }

    // ════════════════════════════════════════
    // 카운트
    // ════════════════════════════════════════

    public string SessionCountText => $"{LocalizationService.GetString("Str_Total")} {Sessions.Count}";

    // ════════════════════════════════════════
    // 언어 설정
    // ════════════════════════════════════════

    public string[] LanguageDisplayNames => _localizationService.LanguageDisplayNames;

    private string _selectedLanguageDisplay = "한국어";
    public string SelectedLanguageDisplay
    {
        get => _selectedLanguageDisplay;
        set
        {
            if (SetProperty(ref _selectedLanguageDisplay, value))
            {
                int idx = Array.IndexOf(_localizationService.LanguageDisplayNames, value);
                if (idx >= 0)
                {
                    _localizationService.SwitchLanguage(_localizationService.AvailableLanguages[idx]);
                    _settingsService.SaveLanguage(_localizationService.AvailableLanguages[idx]);
                    RefreshLocalizedProperties();
                }
            }
        }
    }

    // ════════════════════════════════════════
    // 커맨드
    // ════════════════════════════════════════

    public ICommand StartStopCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportTxtCommand { get; }
    public ICommand ShowCertInfoCommand { get; }
    public ICommand LaunchChromeCommand { get; }
    public ICommand ExportMacroCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand ToggleLayoutCommand { get; }

    /// <summary>View에서 호출하는 Export 액션. 파일 경로를 받아 처리합니다.</summary>
    public Func<string, string, string?, Task>? ExportAction { get; set; }

    // ════════════════════════════════════════
    // 생성자
    // ════════════════════════════════════════

    public MainViewModel(
        ProxyService proxyService,
        JsonFormatterService jsonFormatter,
        SettingsService settingsService,
        CertificateService certificateService,
        ExportService exportService,
        LocalizationService localizationService,
        WebSocketService webSocketService)
    {
        _proxyService = proxyService;
        _jsonFormatter = jsonFormatter;
        _settingsService = settingsService;
        _certificateService = certificateService;
        _exportService = exportService;
        _localizationService = localizationService;
        _webSocketService = webSocketService;

        // 저장된 설정 로드
        var settings = _settingsService.Load();
        _listenAddress = settings.ListenAddress;
        _port = settings.Port;
        _enableSsl = settings.EnableSsl;

        // 저장된 언어 설정 로드 및 적용
        var savedLang = _settingsService.LoadLanguage();
        _localizationService.SwitchLanguage(savedLang);
        int langIdx = Array.IndexOf(_localizationService.AvailableLanguages, savedLang);
        if (langIdx >= 0) _selectedLanguageDisplay = _localizationService.LanguageDisplayNames[langIdx];

        // CollectionView (필터링)
        SessionsView = CollectionViewSource.GetDefaultView(Sessions);
        SessionsView.Filter = FilterSession;

        // 커맨드
        StartStopCommand = new RelayCommand(async () => await ToggleProxyAsync());
        ClearCommand = new RelayCommand(ClearSessions);
        ExportJsonCommand = new RelayCommand(async () => await RequestExportAsync("json"));
        ExportTxtCommand = new RelayCommand(async () => await RequestExportAsync("txt"));
        ShowCertInfoCommand = new RelayCommand(ShowCertificateInfo);
        LaunchChromeCommand = new RelayCommand(LaunchChromeWithProxy);
        ExportMacroCommand = new RelayCommand(async () => await ExportMacroAsync());
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsPanelOpen = !IsSettingsPanelOpen);
        ToggleLayoutCommand = new RelayCommand(() => IsVerticalSplit = !IsVerticalSplit);
        AddToJunkCommand = new RelayCommand<string>(AddHostToJunk);
        AddUrlToJunkCommand = new RelayCommand(AddUrlToJunk);
        RemoveFromJunkCommand = new RelayCommand<string>(RemoveFromJunk);
        DeleteDomEventCommand = new RelayCommand<int?>(DeleteDomEvent);

        LoadCustomJunkHosts();

        // 프록시 이벤트
        _proxyService.SessionCaptured += OnSessionCaptured;
        _proxyService.ErrorOccurred += OnErrorOccurred;

        // WebSocket 이벤트
        _webSocketService.DomEventReceived += OnDomEventReceived;
        _webSocketService.ClientCountChanged += OnWsClientCountChanged;
        _webSocketService.ErrorOccurred += OnErrorOccurred;

        // 인증서 상태 초기화
        UpdateCertStatus();
    }

    /// <summary>
    /// 언어가 바뀐 후 동적 문자열 속성들을 갱신합니다.
    /// DynamicResource로 바인딩된 XAML 텍스트는 자동 갱신되지만,
    /// ViewModel의 C# 코드에서 조합하는 문자열은 수동 통보가 필요합니다.
    /// </summary>
    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(StartStopButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SessionCountText));
        UpdateCertStatus();
    }

    // ════════════════════════════════════════
    // 프록시 시작/중지
    // ════════════════════════════════════════

    private async Task ToggleProxyAsync()
    {
        if (IsRunning)
        {
            _proxyService.Stop();
            _webSocketService.Stop();
            WsStatus = "WS: Off";
            IsRunning = false;
        }
        else
        {
            var settings = new ProxySettings
            {
                ListenAddress = ListenAddress,
                Port = Port,
                EnableSsl = EnableSsl
            };

            _settingsService.Save(settings);
            await _proxyService.StartAsync(settings);
            IsRunning = _proxyService.IsRunning;

            // WebSocket 서버도 같이 시작 (포트: 프록시포트+1)
            if (IsRunning)
            {
                await _webSocketService.StartAsync(ListenAddress, Port + 1);
                WsStatus = _webSocketService.IsRunning
                    ? $"WS: :{Port + 1} (0)"
                    : "WS: Failed";
            }

            UpdateCertStatus();
        }
    }

    // ════════════════════════════════════════
    // Clear
    // ════════════════════════════════════════

    private void ClearSessions()
    {
        Sessions.Clear();
        DomEvents.Clear();
        SelectedSession = null;
        _proxyService.ResetCounter();
        _webSocketService.ResetCounter();
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(DomEventCountText));
    }

    // ════════════════════════════════════════
    // Export
    // ════════════════════════════════════════

    private async Task RequestExportAsync(string format)
    {
        if (Sessions.Count == 0)
        {
            MessageBox.Show(
                LocalizationService.GetString("Str_NoSessionsToExport"),
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ExportAction != null)
        {
            await ExportAction(format, format == "json" ? "JSON Files|*.json" : "Text Files|*.txt", null);
        }
    }

    public async Task DoExportAsync(string filePath, string format)
    {
        try
        {
            var sessionsToExport = SessionsView.Cast<TrafficSession>().ToList();

            if (format == "json")
                await _exportService.ExportAsJsonAsync(sessionsToExport, filePath);
            else
                await _exportService.ExportAsTxtAsync(sessionsToExport, filePath);

            MessageBox.Show(
                $"{sessionsToExport.Count} {LocalizationService.GetString("Str_ExportComplete")}\n{filePath}",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{LocalizationService.GetString("Str_ExportFailed")} {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ════════════════════════════════════════
    // 인증서 안내
    // ════════════════════════════════════════

    private void ShowCertificateInfo()
    {
        bool installed = _certificateService.IsRootCertificateInstalled();
        var title = LocalizationService.GetString("Str_CertTitle");

        if (installed)
        {
            // 이미 설치됨 → 제거 여부 확인
            var result = MessageBox.Show(
                LocalizationService.GetString("Str_CertInstalledMsg") + "\n\n" +
                LocalizationService.GetString("Str_CertUninstallAsk"),
                title, MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                var (success, msg) = _certificateService.UninstallRootCertificate();
                MessageBox.Show(msg, title, MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        else
        {
            // 미설치 → 설치 여부 확인
            var result = MessageBox.Show(
                LocalizationService.GetString("Str_CertNotInstalledMsg") + "\n\n" +
                LocalizationService.GetString("Str_CertInstallAsk"),
                title, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var (success, msg) = _certificateService.InstallRootCertificate();
                MessageBox.Show(msg, title, MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }

        UpdateCertStatus();
    }

    private void UpdateCertStatus()
    {
        bool installed = _certificateService.IsRootCertificateInstalled();
        CertificateStatus = LocalizationService.GetString(installed ? "Str_CertInstalled" : "Str_CertNotInstalled");
    }

    // ════════════════════════════════════════
    // 세션 캡처 이벤트
    // ════════════════════════════════════════

    private void OnSessionCaptured(TrafficSession session)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            Sessions.Add(session);
            OnPropertyChanged(nameof(SessionCountText));

            // API가 들어올 때 직전 DOM 이벤트와 매칭 시도
            if (DomEvents.Count > 0)
                TryMatchSessionToDomEvents(session);
        });
    }

    private void OnErrorOccurred(string message)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            MessageBox.Show(message, "Proxy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsRunning = false;
        });
    }

    // ════════════════════════════════════════
    // 필터 로직
    // ════════════════════════════════════════

    private bool FilterSession(object obj)
    {
        if (obj is not TrafficSession session) return false;

        // 정크만 보기 모드
        if (ShowOnlyJunk && !IsJunkHost(session.Host, session.Url))
            return false;

        // Junk API 필터
        if (HideJunkApis && IsJunkHost(session.Host, session.Url))
            return false;

        // Host/URL 검색
        if (!string.IsNullOrEmpty(FilterHost))
        {
            bool hostMatch = session.Host.Contains(FilterHost, StringComparison.OrdinalIgnoreCase);
            bool urlMatch = session.Url.Contains(FilterHost, StringComparison.OrdinalIgnoreCase);
            if (!hostMatch && !urlMatch) return false;
        }

        // Method 필터
        if (FilterMethod != "All"
            && !session.Method.Equals(FilterMethod, StringComparison.OrdinalIgnoreCase))
            return false;

        // StatusCode 필터: 200, 404, 2xx, 4xx, 5xx 등
        if (!string.IsNullOrEmpty(FilterStatusCode))
        {
            var code = FilterStatusCode.Trim();
            if (int.TryParse(code, out int exact))
            {
                if (session.StatusCode != exact) return false;
            }
            else if (code.Length == 3
                     && code.EndsWith("xx", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(code[..1], out int range))
            {
                if (session.StatusCode < range * 100 || session.StatusCode > range * 100 + 99)
                    return false;
            }
        }

        return true;
    }

    // ════════════════════════════════════════
    // 상세 보기
    // ════════════════════════════════════════

    private void UpdateDetailView()
    {
        if (SelectedSession == null)
        {
            OverviewText = string.Empty;
            RequestHeadersText = string.Empty;
            RequestBodyText = string.Empty;
            ResponseHeadersText = string.Empty;
            ResponseBodyText = string.Empty;
            return;
        }

        var s = SelectedSession;
        OverviewText = s.OverviewText;
        RequestHeadersText = s.RequestHeaders;
        ResponseHeadersText = s.ResponseHeaders;

        // JSON이면 pretty print 적용
        RequestBodyText = TryFormatJson(s.RequestBody);
        ResponseBodyText = TryFormatJson(s.ResponseBody, s.ContentType);
    }

    private string TryFormatJson(string body, string? contentType = null)
    {
        if (!JsonPrettyPrint || string.IsNullOrEmpty(body))
            return body;

        // Content-Type으로 판별
        if (contentType != null && JsonFormatterService.IsJsonContentType(contentType))
            return _jsonFormatter.FormatJson(body);

        // Content-Type 없어도 JSON처럼 생긴 문자열이면 시도
        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return _jsonFormatter.FormatJson(body);

        return body;
    }

    // ════════════════════════════════════════
    // 크롬 프록시 모드 실행
    // ════════════════════════════════════════

    private void LaunchChromeWithProxy()
    {
        var chromePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Google", "Chrome", "Application", "chrome.exe")
        };

        var chromePath = chromePaths.FirstOrDefault(File.Exists);

        if (chromePath == null)
        {
            MessageBox.Show(
                LocalizationService.GetString("Str_ChromeNotFound"),
                "Chrome", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var proxyAddr = $"http://{ListenAddress}:{Port}";
            var args = $"--proxy-server=\"{proxyAddr}\" --ignore-certificate-errors";

            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = args,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{LocalizationService.GetString("Str_ChromeLaunchFailed")} {ex.Message}",
                "Chrome", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ════════════════════════════════════════
    // DOM 이벤트 수신 + 매칭
    // ════════════════════════════════════════

    private void OnDomEventReceived(DomEvent domEvent)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            DomEvents.Add(domEvent);
            OnPropertyChanged(nameof(DomEventCountText));
        });
    }

    /// <summary>
    /// API 세션이 새로 들어올 때마다, 직전 5초 이내 미매칭 DOM 이벤트와 연결합니다.
    /// "클릭 → API 호출" 순서이므로, API가 들어온 시점에 역방향으로 DOM을 찾습니다.
    /// </summary>
    private void TryMatchSessionToDomEvents(TrafficSession session)
    {
        if (session.Method == "CONNECT") return;

        // 이 API 요청 시점 기준 5초 전~현재 사이의 미매칭 DOM 이벤트 찾기
        var windowStart = session.Timestamp.AddSeconds(-5);

        foreach (var dom in DomEvents)
        {
            if (dom.MatchedSessionId.HasValue) continue; // 이미 매칭됨
            if (dom.Timestamp < windowStart) continue;
            if (dom.Timestamp > session.Timestamp) continue;
            if (dom.EventType == "scroll") continue; // 스크롤은 매칭 제외

            // 같은 호스트면 우선 매칭
            var domHost = GetHostFromUrl(dom.Url);
            if (!string.IsNullOrEmpty(domHost) && session.Host.Contains(domHost, StringComparison.OrdinalIgnoreCase))
            {
                dom.MatchedSessionId = session.Id;
                return;
            }
        }

        // 호스트 매칭 실패 시, 가장 가까운 미매칭 DOM 이벤트에 연결
        var closest = DomEvents
            .Where(d => !d.MatchedSessionId.HasValue
                        && d.Timestamp >= windowStart
                        && d.Timestamp <= session.Timestamp
                        && d.EventType != "scroll")
            .OrderByDescending(d => d.Timestamp)
            .FirstOrDefault();

        if (closest != null)
            closest.MatchedSessionId = session.Id;
    }

    private static string GetHostFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.Host;
        }
        catch { }
        return string.Empty;
    }

    private void OnWsClientCountChanged(int count)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            WsStatus = _webSocketService.IsRunning
                ? $"WS: :{Port + 1} ({count})"
                : "WS: Off";
        });
    }

    // ════════════════════════════════════════
    // 매크로 Export
    // ════════════════════════════════════════

    private async Task ExportMacroAsync()
    {
        if (DomEvents.Count == 0)
        {
            MessageBox.Show(
                LocalizationService.GetString("Str_NoDomEvents"),
                "Macro Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ExportAction != null)
        {
            await ExportAction("macro", "JSON Files|*.json", null);
        }
    }

    /// <summary>매칭된 DOM+API 데이터를 매크로 JSON으로 내보냅니다.</summary>
    public async Task DoMacroExportAsync(string filePath)
    {
        try
        {
            var steps = DomEvents.Select((dom, idx) =>
            {
                var apiCalls = new List<object>();

                // 이 DOM 이벤트에 직접 매칭된 세션
                if (dom.MatchedSessionId.HasValue)
                {
                    var matched = Sessions.FirstOrDefault(s => s.Id == dom.MatchedSessionId);
                    if (matched != null)
                        apiCalls.Add(BuildApiCallObj(matched));
                }

                // 매칭 안 된 세션도 타임스탬프 범위로 추가 탐색
                var rangeStart = dom.Timestamp;
                var rangeEnd = dom.Timestamp.AddSeconds(3);
                var rangeSessions = Sessions
                    .Where(s => s.Timestamp >= rangeStart && s.Timestamp <= rangeEnd
                                && s.Id != dom.MatchedSessionId)
                    .OrderBy(s => s.Timestamp);

                foreach (var s in rangeSessions)
                    apiCalls.Add(BuildApiCallObj(s));

                return new
                {
                    step = idx + 1,
                    action = new
                    {
                        type = dom.EventType,
                        selector = dom.Selector,
                        xpath = dom.XPath,
                        tagName = dom.TagName,
                        innerText = dom.InnerText,
                        value = dom.Value,
                        url = dom.Url,
                        timestamp = dom.Timestamp.ToString("HH:mm:ss.fff"),
                        position = new { x = dom.PositionX, y = dom.PositionY },
                        attributes = dom.Attributes
                    },
                    apiCalls
                };
            }).ToList();

            var exportObj = new
            {
                recordedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                totalDomEvents = DomEvents.Count,
                totalApiCalls = Sessions.Count,
                steps
            };

            var json = JsonSerializer.Serialize(exportObj, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);

            MessageBox.Show(
                $"{steps.Count} steps exported.\n{filePath}",
                "Macro Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static object BuildApiCallObj(TrafficSession s) => new
    {
        method = s.Method,
        url = s.Url,
        statusCode = s.StatusCode,
        contentType = s.ContentType,
        requestHeaders = s.RequestHeaders,
        requestBody = s.RequestBody,
        responseHeaders = s.ResponseHeaders,
        responseBody = s.ResponseBody,
        durationMs = s.DurationMs
    };

    // ════════════════════════════════════════
    // 정리
    // ════════════════════════════════════════

    public void Dispose()
    {
        _proxyService.SessionCaptured -= OnSessionCaptured;
        _proxyService.ErrorOccurred -= OnErrorOccurred;
        _webSocketService.DomEventReceived -= OnDomEventReceived;
        _webSocketService.ClientCountChanged -= OnWsClientCountChanged;
        _proxyService.Dispose();
        _webSocketService.Dispose();
        GC.SuppressFinalize(this);
    }
}
