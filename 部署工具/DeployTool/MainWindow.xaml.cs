﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DeployTool.Models;
using DeployTool.ViewModels;

namespace DeployTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _cpuTempTimer;
    private readonly DispatcherTimer _logScrollTimer;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.ConfirmRequested += ViewModel_ConfirmRequested;
        _viewModel.InputRequested += ViewModel_InputRequested;
        _viewModel.LogAdded += ViewModel_LogAdded;
        _viewModel.CheckpointResumeRequested += ViewModel_CheckpointResumeRequested;

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        Width = screenWidth / 2;
        Height = screenHeight / 2;
        MinWidth = 1000;
        MinHeight = 650;

        _cpuTempTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AppSettings.CpuTempRefreshInterval)
        };
        _cpuTempTimer.Tick += CpuTempTimer_Tick;
        _cpuTempTimer.Start();

        _logScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _logScrollTimer.Tick += LogScrollTimer_Tick;
    }

    public string DataPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.SetMainWindow(this);
        
        _viewModel.LoadSystemInfo();
        
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _viewModel.CheckForCheckpointOnStartup();
        }), DispatcherPriority.ApplicationIdle);
    }

    private void CpuTempTimer_Tick(object? sender, EventArgs e)
    {
        if (AppSettings.EnableCpuTempMonitor)
        {
            _viewModel.RefreshCpuTemperature();
        }
    }

    private void ViewModel_LogAdded(LogEntry obj)
    {
        _logScrollTimer.Start();
    }

    private void LogScrollTimer_Tick(object? sender, EventArgs e)
    {
        _logScrollTimer.Stop();
        LogScrollViewer.ScrollToEnd();
    }

    private void TopMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is TopMenuItem topMenu)
        {
            _viewModel.SelectTopMenu(topMenu);
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            if (button.Tag is MenuItem item)
            {
                if (item.HasChildren)
                {
                    return;
                }
                
                if (item.ConfirmMessage == "WINDOW_EASY_SETTINGS")
                {
                    OpenWindowsEasySettings();
                }
                else
                {
                    _viewModel.ExecuteMenuItem(item);
                }
            }
            else if (button.Name == "MenuButton")
            {
                MenuPopup.IsOpen = !MenuPopup.IsOpen;
            }
        }
    }

    private void OpenWindowsEasySettings()
    {
        var window = new WindowsEasySettingsWindow { Owner = this };
        window.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
        
        _cpuTempTimer.Interval = TimeSpan.FromSeconds(AppSettings.CpuTempRefreshInterval);
        if (AppSettings.EnableCpuTempMonitor)
        {
            _cpuTempTimer.Start();
        }
        else
        {
            _cpuTempTimer.Stop();
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var aboutWindow = new AboutWindow { Owner = this };
        aboutWindow.ShowDialog();
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        var updateWindow = new UpdateWindow { Owner = this };
        updateWindow.ShowDialog();
    }

    private void SystemDetail_Click(object sender, RoutedEventArgs e)
    {
        var detailWindow = new SystemDetailWindow { Owner = this };
        detailWindow.ShowDialog();
    }

    private void ViewModel_ConfirmRequested(object? sender, ConfirmEventArgs e)
    {
        var dialog = new ConfirmDialog(e.Message) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            e.Confirmed = dialog.IsConfirmed;
        }
        else
        {
            e.Confirmed = false;
        }
    }

    private void ViewModel_InputRequested(object? sender, InputEventArgs e)
    {
        var inputDialog = new InputDialog(e.Title, e.Prompt);
        if (inputDialog.ShowDialog() == true)
        {
            e.InputText = inputDialog.InputText;
            e.Confirmed = true;
        }
    }

    private void ViewModel_CheckpointResumeRequested(object? sender, CheckpointResumeEventArgs e)
    {
        var checkpoint = e.Checkpoint;
        var remainingTasks = checkpoint.TaskIds.Count - checkpoint.NextTaskIndex;
        var message = $"检测到未完成的部署流程！\n\n" +
                      $"流程名称: {checkpoint.ProcessName}\n" +
                      $"开始时间: {checkpoint.StartTime:yyyy-MM-dd HH:mm:ss}\n" +
                      $"当前进度: {checkpoint.NextTaskIndex}/{checkpoint.TaskIds.Count}\n" +
                      $"剩余任务: {remainingTasks} 个\n\n" +
                      $"是否继续执行未完成的流程？\n\n" +
                      $"选择\"是\"将继续执行流程\n" +
                      $"选择\"否\"将放弃断点并开始新的操作";

        var result = MessageBox.Show(
            message,
            "断点续传 - 发现未完成的流程",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Yes)
        {
            e.Confirmed = true;
            _viewModel.ResumeFromCheckpoint(checkpoint);
        }
        else
        {
            var clearResult = MessageBox.Show(
                "是否清除断点记录？\n\n选择\"是\"将删除断点记录，可以重新开始新的部署。\n选择\"否\"保留断点记录，下次启动时仍会提示。",
                "清除断点",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (clearResult == MessageBoxResult.Yes)
            {
                var systemService = Services.SystemService.Instance;
                systemService.ClearCheckpoint();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cpuTempTimer.Stop();
        _logScrollTimer.Stop();
        base.OnClosed(e);
    }

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }
}
