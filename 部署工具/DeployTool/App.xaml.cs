﻿using System.Windows;
using DeployTool.Services;

namespace DeployTool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppSettings.Load();
        AppSettings.Save();
        
        if (!SystemService.IsAdministrator())
        {
            MessageBox.Show(
                "此程序需要管理员权限运行！\n\n" +
                "请右键点击程序，选择\"以管理员身份运行\"。",
                "权限不足",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppSettings.Save();
        SystemService.CloseHardwareMonitor();
        base.OnExit(e);
    }
}
