using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LocalTrafficInspector.ViewModels;
using Microsoft.Win32;

namespace LocalTrafficInspector;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.ExportAction = HandleExportAsync;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        StateChanged += OnStateChanged;
    }

    /// <summary>최대화 시 윈도우 테두리 잘림 보정</summary>
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            BorderThickness = new Thickness(8);
            MaximizeBtn.Content = "\u25A0";
        }
        else
        {
            BorderThickness = new Thickness(0);
            MaximizeBtn.Content = "\u25A1";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsVerticalSplit))
            ApplyLayout(_viewModel.IsVerticalSplit);
    }

    /// <summary>상하(false) ↔ 좌우(true) 레이아웃 전환</summary>
    private void ApplyLayout(bool vertical)
    {
        if (vertical)
        {
            // 좌우 분할: Row → 단일, Column → 분할
            ContentRow0.Height = new GridLength(1, GridUnitType.Star);
            SplitterRow.Height = new GridLength(0);
            ContentRow2.Height = new GridLength(0);

            ContentCol0.Width = new GridLength(1, GridUnitType.Star);
            SplitterCol.Width = new GridLength(4);
            ContentCol2.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(RequestGrid, 0);
            Grid.SetColumn(RequestGrid, 0);
            Grid.SetRowSpan(RequestGrid, 3);
            Grid.SetColumnSpan(RequestGrid, 1);

            Grid.SetRow(ContentSplitter, 0);
            Grid.SetColumn(ContentSplitter, 1);
            Grid.SetRowSpan(ContentSplitter, 3);
            Grid.SetColumnSpan(ContentSplitter, 1);
            ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            ContentSplitter.Cursor = Cursors.SizeWE;

            Grid.SetRow(DetailTabs, 0);
            Grid.SetColumn(DetailTabs, 2);
            Grid.SetRowSpan(DetailTabs, 3);
            Grid.SetColumnSpan(DetailTabs, 1);
        }
        else
        {
            // 상하 분할 (기본)
            ContentRow0.Height = new GridLength(1, GridUnitType.Star);
            SplitterRow.Height = new GridLength(4);
            ContentRow2.Height = new GridLength(1, GridUnitType.Star);

            ContentCol0.Width = new GridLength(1, GridUnitType.Star);
            SplitterCol.Width = new GridLength(0);
            ContentCol2.Width = new GridLength(0);

            Grid.SetRow(RequestGrid, 0);
            Grid.SetColumn(RequestGrid, 0);
            Grid.SetRowSpan(RequestGrid, 1);
            Grid.SetColumnSpan(RequestGrid, 3);

            Grid.SetRow(ContentSplitter, 1);
            Grid.SetColumn(ContentSplitter, 0);
            Grid.SetRowSpan(ContentSplitter, 1);
            Grid.SetColumnSpan(ContentSplitter, 3);
            ContentSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            ContentSplitter.VerticalAlignment = VerticalAlignment.Stretch;
            ContentSplitter.Cursor = Cursors.SizeNS;

            Grid.SetRow(DetailTabs, 2);
            Grid.SetColumn(DetailTabs, 0);
            Grid.SetRowSpan(DetailTabs, 1);
            Grid.SetColumnSpan(DetailTabs, 3);
        }
    }

    private async Task HandleExportAsync(string format, string filter, string? _)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = format == "json" ? ".json" : ".txt",
            FileName = format == "macro"
                ? $"macro_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"traffic_export_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            if (format == "macro")
                await _viewModel.DoMacroExportAsync(dialog.FileName);
            else
                await _viewModel.DoExportAsync(dialog.FileName, format);
        }
    }

    // ═══ 컨텍스트 메뉴 ═══
    private void FilterByHost_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSession != null)
            _viewModel.FilterHost = _viewModel.SelectedSession.Host;
    }

    private void DeleteDomEvent_Click(object sender, RoutedEventArgs e)
    {
        if (DomGrid.SelectedItem is LocalTrafficInspector.Models.DomEvent domEvent)
        {
            _viewModel.DomEvents.Remove(domEvent);
        }
    }

    // ═══ 타이틀바 버튼 ═══
    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        MaximizeBtn.Content = WindowState == WindowState.Maximized
            ? "\u25A0"   // ■ Restore
            : "\u25A1";  // □ Maximize
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
