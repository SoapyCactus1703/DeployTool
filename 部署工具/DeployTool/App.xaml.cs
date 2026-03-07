﻿﻿﻿﻿﻿using System.Windows;
using DeployTool.Services;

namespace DeployTool;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppSettings.Load();
        AppSettings.Save();
        
        if (!SystemService.IsAdministrator())
        {
            var errorDialog = new ErrorDialog("此程序需要管理员权限运行！\n\n请右键点击程序，选择\"以管理员身份运行\"。")
            {
                Owner = _mainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            errorDialog.ShowDialog();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        
        Dispatcher.BeginInvoke(new Action(CheckForUpdatesOnStartup), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void CheckForUpdatesOnStartup()
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000);
                
                var updateService = UpdateService.Instance;
                var updateInfo = await updateService.CheckForUpdateAsync();
                
                if (updateInfo != null)
                {
                    var hasUpdate = !string.IsNullOrEmpty(updateInfo.DownloadUrl) && 
                                    updateService.IsNewerVersion(updateInfo.Version, updateService.CurrentVersion);
                    
                    if (hasUpdate)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_mainWindow == null || !_mainWindow.IsLoaded)
                                return;
                            
                            var dialog = new UpdateNotificationDialog(updateInfo)
                            {
                                Owner = _mainWindow,
                                WindowStartupLocation = WindowStartupLocation.CenterScreen
                            };
                            dialog.ShowDialog();
                        });
                    }
                }
            }
            catch
            {
            }
        });
    }

    public void SetMainWindow(MainWindow window)
    {
        _mainWindow = window;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppSettings.Save();
        SystemService.CloseHardwareMonitor();
        base.OnExit(e);
    }
}
