using System.IO;
using System.Windows;
using DeployTool.Services;

namespace DeployTool;

public partial class UpdateWindow : Window
{
    private readonly UpdateService _updateService;
    private UpdateInfo? _updateInfo;
    private string? _downloadedFilePath;
    private CancellationTokenSource? _downloadCts;

    public UpdateWindow()
    {
        InitializeComponent();
        _updateService = UpdateService.Instance;
        _updateService.DownloadProgressChanged += UpdateService_DownloadProgressChanged;
        _updateService.UpdateError += UpdateService_UpdateError;
        
        CurrentVersionText.Text = $"当前版本: v{_updateService.CurrentVersion}";
        
        CheckForUpdate();
    }

    private async void CheckForUpdate()
    {
        ShowPanel("Checking");
        
        try
        {
            _updateInfo = await _updateService.CheckForUpdateAsync();
            
            if (_updateInfo != null)
            {
                NewVersionText.Text = $"发现新版本: v{_updateInfo.Version}";
                ReleaseDateText.Text = _updateInfo.ReleaseDate.HasValue 
                    ? $"发布日期: {_updateInfo.ReleaseDate.Value:yyyy-MM-dd}" 
                    : "";
                ReleaseNotesText.Text = _updateInfo.ReleaseNotes;
                
                ShowPanel("UpdateAvailable");
                DownloadButton.Visibility = Visibility.Visible;
                OpenReleaseButton.Visibility = Visibility.Visible;
            }
            else
            {
                ShowPanel("NoUpdate");
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"检查更新失败:\n{ex.Message}";
            ShowPanel("Error");
        }
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
        if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
        {
            MessageBox.Show("更新文件不存在，请重新下载。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show(
            "即将安装更新，程序将会关闭并自动重启。\n\n是否继续？",
            "安装更新",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            _updateService.InstallUpdate(_downloadedFilePath, currentDir);
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
            ErrorText.Text = e;
            ShowPanel("Error");
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
        _downloadCts?.Dispose();
        base.OnClosed(e);
    }
}
