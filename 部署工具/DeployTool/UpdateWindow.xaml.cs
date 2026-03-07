using System.IO;
using System.Windows;
using System.Windows.Media;
using DeployTool.Services;

namespace DeployTool;

public partial class UpdateWindow : Window
{
    private readonly UpdateService _updateService;
    private UpdateInfo? _updateInfo;
    private string? _downloadedFilePath;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _checkCts;
    private bool _autoCheckMode;

    public UpdateWindow(bool autoCheck = false)
    {
        InitializeComponent();
        _updateService = UpdateService.Instance;
        _updateService.DownloadProgressChanged += UpdateService_DownloadProgressChanged;
        _updateService.UpdateError += UpdateService_UpdateError;
        _autoCheckMode = autoCheck;
        
        CurrentVersionText.Text = $"当前版本: v{_updateService.CurrentVersion}";
        
        Loaded += UpdateWindow_Loaded;
    }

    private void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CheckForUpdate();
    }

    public void CheckForUpdate()
    {
        ShowPanel("Checking");
        _checkCts = new CancellationTokenSource();
        
        Task.Run(async () =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_checkCts.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                
                var updateInfo = await _updateService.CheckForUpdateAsync();
                
                if (cts.Token.IsCancellationRequested)
                    return;
                
                await Dispatcher.InvokeAsync(() =>
                {
                    _updateInfo = updateInfo;
                    
                    if (_updateInfo != null)
                    {
                        var hasUpdate = !string.IsNullOrEmpty(_updateInfo.DownloadUrl) && 
                                        _updateService.IsNewerVersion(_updateInfo.Version, _updateService.CurrentVersion);
                        
                        if (hasUpdate)
                        {
                            NewVersionText.Text = $"发现新版本: v{_updateInfo.Version}";
                            ReleaseDateText.Text = _updateInfo.ReleaseDate.HasValue 
                                ? $"发布日期: {_updateInfo.ReleaseDate.Value:yyyy-MM-dd}" 
                                : "";
                            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes) 
                                ? "暂无更新说明" 
                                : _updateInfo.ReleaseNotes;
                            
                            VersionBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                            VersionBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"));
                            NewVersionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#276749"));
                            
                            ShowPanel("UpdateAvailable");
                            DownloadButton.Visibility = Visibility.Visible;
                            OpenReleaseButton.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            if (_autoCheckMode)
                            {
                                Close();
                            }
                            else
                            {
                                NewVersionText.Text = $"当前已是最新版本: v{_updateService.CurrentVersion}";
                                ReleaseDateText.Text = _updateInfo.ReleaseDate.HasValue 
                                    ? $"发布日期: {_updateInfo.ReleaseDate.Value:yyyy-MM-dd}" 
                                    : "";
                                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes) 
                                    ? "暂无更新说明" 
                                    : _updateInfo.ReleaseNotes;
                                
                                VersionBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
                                VersionBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3182CE"));
                                NewVersionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CB0"));
                                
                                ShowPanel("UpdateAvailable");
                                OpenReleaseButton.Visibility = Visibility.Visible;
                            }
                        }
                    }
                    else
                    {
                        if (_autoCheckMode)
                        {
                            Close();
                        }
                        else
                        {
                            NoUpdateVersionText.Text = $"当前版本: v{_updateService.CurrentVersion}";
                            ShowPanel("NoUpdate");
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_autoCheckMode)
                    {
                        Close();
                    }
                    else
                    {
                        ErrorText.Text = "检查更新超时，请稍后重试。";
                        ShowPanel("Error");
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_autoCheckMode)
                    {
                        Close();
                    }
                    else
                    {
                        ErrorText.Text = $"检查更新失败:\n{ex.Message}";
                        ShowPanel("Error");
                    }
                });
            }
        });
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateInfo == null || string.IsNullOrEmpty(_updateInfo.DownloadUrl))
            return;

        DownloadButton.IsEnabled = false;
        ShowPanel("Downloading");
        CancelButton.Visibility = Visibility.Visible;
        CloseButton.Visibility = Visibility.Collapsed;

        _downloadCts = new CancellationTokenSource();
        
        var tempPath = Path.Combine(Path.GetTempPath(), $"DeployTool_v{_updateInfo.Version}.zip");
        _downloadedFilePath = tempPath;
        
        var success = await _updateService.DownloadUpdateAsync(
            _updateInfo.DownloadUrl, 
            tempPath, 
            _downloadCts.Token);

        if (success)
        {
            DownloadStatusText.Text = "下载完成！";
            DownloadProgressBar.Value = 100;
            DownloadProgressText.Text = "100%";
            DownloadSpeedText.Text = "";
            
            InstallButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Collapsed;
            
            InstallUpdate();
        }
        else if (_downloadCts.IsCancellationRequested)
        {
            ShowPanel("UpdateAvailable");
            DownloadButton.IsEnabled = true;
            DownloadButton.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Visible;
        }
        
        _downloadCts.Dispose();
        _downloadCts = null;
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallUpdate();
    }

    private void InstallUpdate()
    {
        if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
        {
            MessageBox.Show("更新文件不存在，请重新下载。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(
            "即将安装更新，程序将会关闭并自动重启。\n\n" +
            $"旧版本将被备份为: {_updateService.OldVersionBackupName}\n\n" +
            "是否继续？",
            "安装更新",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _updateService.InstallUpdate(_downloadedFilePath);
        }
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        _updateService.OpenReleasePage();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _checkCts?.Cancel();
        Close();
    }

    private void UpdateService_DownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgressBar.Value = e.ProgressPercentage;
            DownloadProgressText.Text = $"{e.ProgressPercentage:F1}%";
            
            var mbReceived = e.BytesReceived / (1024.0 * 1024.0);
            var mbTotal = e.TotalBytes / (1024.0 * 1024.0);
            DownloadSpeedText.Text = $"{mbReceived:F2} MB / {mbTotal:F2} MB";
        });
    }

    private void UpdateService_UpdateError(object? sender, string e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_autoCheckMode)
            {
                Close();
            }
            else
            {
                ErrorText.Text = e;
                ShowPanel("Error");
            }
        });
    }

    private void ShowPanel(string panelName)
    {
        CheckingPanel.Visibility = panelName == "Checking" ? Visibility.Visible : Visibility.Collapsed;
        NoUpdatePanel.Visibility = panelName == "NoUpdate" ? Visibility.Visible : Visibility.Collapsed;
        UpdateAvailablePanel.Visibility = panelName == "UpdateAvailable" ? Visibility.Visible : Visibility.Collapsed;
        DownloadingPanel.Visibility = panelName == "Downloading" ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = panelName == "Error" ? Visibility.Visible : Visibility.Collapsed;
        
        DownloadButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Collapsed;
        OpenReleaseButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateService.DownloadProgressChanged -= UpdateService_DownloadProgressChanged;
        _updateService.UpdateError -= UpdateService_UpdateError;
        _checkCts?.Cancel();
        _checkCts?.Dispose();
        _downloadCts?.Dispose();
        base.OnClosed(e);
    }

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }

    public void SetGiteeRepo(string repo)
    {
        _updateService.SetGiteeRepo(repo);
    }
}
