using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace DeployTool;

public partial class SettingsWindow : Window
{
    public ObservableCollection<FlagItem> FlagItems { get; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
        LoadFlags();
        
        NtpServerComboBox.SelectionChanged += NtpServerComboBox_SelectionChanged;
    }

    private void LoadSettings()
    {
        EnableCpuTempCheckBox.IsChecked = AppSettings.EnableCpuTempMonitor;
        RefreshIntervalTextBox.Text = AppSettings.CpuTempRefreshInterval.ToString();
        NtpServerTextBox.Text = AppSettings.NtpServer;
        AutoSyncTimeCheckBox.IsChecked = AppSettings.AutoSyncTime;
    }

    private void LoadFlags()
    {
        FlagItems.Clear();
        foreach (var kvp in AppSettings.TaskFlags)
        {
            if (kvp.Value)
            {
                FlagItems.Add(new FlagItem
                {
                    Key = kvp.Key,
                    Description = AppSettings.GetFlagDescription(kvp.Key)
                });
            }
        }
        
        NoFlagsText.Visibility = FlagItems.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void NtpServerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NtpServerComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            NtpServerTextBox.Text = item.Tag.ToString();
        }
    }

    private void TeachingPreset_Click(object sender, RoutedEventArgs e)
    {
        EnableCpuTempCheckBox.IsChecked = true;
        RefreshIntervalTextBox.Text = "1";
        AutoSyncTimeCheckBox.IsChecked = true;
        NtpServerTextBox.Text = "210.72.145.44";
    }

    private void OfficePreset_Click(object sender, RoutedEventArgs e)
    {
        EnableCpuTempCheckBox.IsChecked = false;
        RefreshIntervalTextBox.Text = "10";
        AutoSyncTimeCheckBox.IsChecked = true;
        NtpServerTextBox.Text = "ntp.aliyun.com";
    }

    private void DefaultPreset_Click(object sender, RoutedEventArgs e)
    {
        EnableCpuTempCheckBox.IsChecked = true;
        RefreshIntervalTextBox.Text = "1";
        AutoSyncTimeCheckBox.IsChecked = true;
        NtpServerTextBox.Text = "210.72.145.44";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RefreshIntervalTextBox.Text, out var interval) || interval < 1)
        {
            MessageBox.Show("刷新间隔必须是大于 0 的整数！", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppSettings.EnableCpuTempMonitor = EnableCpuTempCheckBox.IsChecked ?? true;
        AppSettings.CpuTempRefreshInterval = interval;
        AppSettings.NtpServer = NtpServerTextBox.Text;
        AppSettings.AutoSyncTime = AutoSyncTimeCheckBox.IsChecked ?? true;
        AppSettings.Save();

        MessageBox.Show("设置已保存！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ClearFlag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string key)
        {
            AppSettings.ClearFlag(key);
            LoadFlags();
        }
    }

    private void ClearAllFlags_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定要清除所有执行标记吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            AppSettings.ClearAllFlags();
            LoadFlags();
        }
    }

    private void SelfDestruct_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要清理部署工具吗？\n\n" +
            "此操作将：\n" +
            "1. 删除桌面快捷方式\n" +
            "2. 强制关闭本程序\n" +
            "3. 删除程序所在目录及所有文件\n\n" +
            "此操作不可撤销！",
            "确认清理",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        var finalConfirm = MessageBox.Show(
            "最后确认：\n\n" +
            "您真的要删除部署工具吗？\n" +
            "删除后将无法恢复！",
            "最后确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);

        if (finalConfirm != MessageBoxResult.Yes)
            return;

        try
        {
            var exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var programDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var programDirName = new DirectoryInfo(programDir).Name;
            
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var shortcutPath = Path.Combine(desktopPath, "西林民高部署工具.lnk");
            
            var tempBatPath = Path.Combine(Path.GetTempPath(), "cleanup_deploy_tool.bat");
            
            var batContent = $@"@echo off
chcp 65001 >nul
echo 正在清理部署工具...

REM 删除桌面快捷方式
del /f /q ""{shortcutPath}"" 2>nul

REM 等待程序退出
timeout /t 2 /nobreak >nul

REM 删除程序目录
:retry
rd /s /q ""{programDir}"" 2>nul
if exist ""{programDir}"" (
    timeout /t 1 /nobreak >nul
    goto retry
)

echo 清理完成！
timeout /t 2 /nobreak >nul
del ""%~f0""
exit
";
            
            File.WriteAllText(tempBatPath, batContent, System.Text.Encoding.UTF8);
            
            var psi = new ProcessStartInfo
            {
                FileName = tempBatPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            
            Process.Start(psi);
            
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清理失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }
}

public class FlagItem
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
