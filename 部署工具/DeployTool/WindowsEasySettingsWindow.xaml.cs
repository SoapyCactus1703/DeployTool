using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeployTool.Services;

namespace DeployTool;

public partial class WindowsEasySettingsWindow : Window
{
    private readonly WindowsSettingsService _settingsService = new();
    private readonly LogService _log = LogService.Instance;
    private DispatcherTimer _statusTimer = null!;
    private bool _isLoading = true;

    private readonly Dictionary<string, SettingItem> _settingItems = new();

    public WindowsEasySettingsWindow()
    {
        try
        {
            InitializeComponent();
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _statusTimer.Tick += StatusTimer_Tick;
            
            Loaded += WindowsEasySettingsWindow_Loaded;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化窗口失败: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            _log.Error($"初始化Windows轻松设置窗口失败: {ex.Message}", "Windows设置");
        }
    }

    private void WindowsEasySettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadAllSettings();
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        _statusTimer.Stop();
        try
        {
            var statusText = StatusText.Text;
            if (statusText.Contains("需重启资源管理器"))
            {
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
            }
            else if (statusText.Contains("需重启电脑"))
            {
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD6B20"));
            }
            else if (statusText.Contains("成功"))
            {
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"));
            }
        }
        catch { }
    }

    private void LoadAllSettings()
    {
        try
        {
            LoadingText.Text = "正在检测系统状态...";
            _isLoading = true;

            Task.Run(() =>
            {
                try
                {
                    var systemSettings = new[]
                    {
                        ("SystemRestore", "系统还原", "管理系统还原功能"),
                        ("SearchHighlights", "显示搜索热点", "控制搜索框中的热点资讯显示"),
                        ("Widgets", "小组件", "控制任务栏小组件功能"),
                        ("AutoUpdateDriver", "自动更新硬件驱动", "控制Windows自动更新硬件驱动程序"),
                        ("Ceip", "微软客户体验改善计划", "控制是否参与微软客户体验改善计划"),
                        ("WindowsUpdate", "Windows更新", "控制Windows自动更新服务"),
                        ("Pca", "程序兼容性助手", "控制程序兼容性助手服务"),
                        ("Dps", "诊断策略服务", "控制诊断策略服务")
                    };

                    var securitySettings = new[]
                    {
                        ("MeltdownSpectre", "Meltdown与Spectre保护", "控制CPU漏洞缓解措施"),
                        ("MemoryIntegrity", "内存完整性", "核心隔离内存完整性保护"),
                        ("Wdac", "WDAC", "Windows Defender应用程序控制"),
                        ("Vbs", "应用基于虚拟化的安全", "启用虚拟化安全功能"),
                        ("TcpBbr2", "TCP BBR2", "TCP拥塞控制算法优化"),
                        ("Dep", "数据执行保护(DEP)", "数据执行保护策略"),
                        ("Uac", "用户账户控制(UAC)", "用户账户控制级别"),
                        ("ReservedStorage", "Windows系统保留存储", "系统保留存储空间")
                    };

                    var explorerSettings = new[]
                    {
                        ("ShowFileExtensions", "显示已知文件扩展名", "在资源管理器中显示文件扩展名"),
                        ("ShowFullPathInTitleBar", "在标题栏显示完整路径", "在资源管理器标题栏显示完整路径"),
                        ("ShowHiddenFiles", "显示隐藏文件", "在资源管理器中显示隐藏文件"),
                        ("OneDrive", "OneDrive", "控制OneDrive文件同步功能"),
                        ("ContextMenuOptimization", "右键菜单优化", "使用旧版右键菜单"),
                        ("TaskbarCombine", "任务栏合并模式", "控制任务栏按钮合并方式")
                    };

                    var performanceSettings = new[]
                    {
                        ("Hibernate", "系统休眠", "控制系统休眠功能"),
                        ("FastStartup", "快速启动", "控制Windows快速启动功能"),
                        ("RemoteDesktop", "远程桌面", "控制远程桌面服务"),
                        ("Firewall", "防火墙", "控制Windows防火墙"),
                        ("SysMain", "SysMain服务", "SuperFetch/SysMain服务"),
                        ("MemoryCompression", "内存压缩", "系统内存压缩功能"),
                        ("Prefetch", "预取", "系统预取功能"),
                        ("DnsCache", "DNS缓存", "DNS客户端缓存服务")
                    };

                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            SystemSettingsPanel.Children.Clear();
                            SecuritySettingsPanel.Children.Clear();
                            ExplorerSettingsPanel.Children.Clear();
                            PerformanceSettingsPanel.Children.Clear();
                            _settingItems.Clear();
                        }
                        catch { }
                    });

                    foreach (var (id, name, desc) in systemSettings)
                    {
                        try
                        {
                            var status = GetSettingStatus(id);
                            Dispatcher.Invoke(() => 
                            {
                                try { AddSettingItem(SystemSettingsPanel, id, name, desc, status); }
                                catch { }
                            });
                        }
                        catch { }
                    }

                    foreach (var (id, name, desc) in securitySettings)
                    {
                        try
                        {
                            var status = GetSettingStatus(id);
                            Dispatcher.Invoke(() => 
                            {
                                try { AddSettingItem(SecuritySettingsPanel, id, name, desc, status); }
                                catch { }
                            });
                        }
                        catch { }
                    }

                    foreach (var (id, name, desc) in explorerSettings)
                    {
                        try
                        {
                            var status = GetSettingStatus(id);
                            Dispatcher.Invoke(() => 
                            {
                                try { AddSettingItem(ExplorerSettingsPanel, id, name, desc, status); }
                                catch { }
                            });
                        }
                        catch { }
                    }

                    foreach (var (id, name, desc) in performanceSettings)
                    {
                        try
                        {
                            var status = GetSettingStatus(id);
                            Dispatcher.Invoke(() => 
                            {
                                try { AddSettingItem(PerformanceSettingsPanel, id, name, desc, status); }
                                catch { }
                            });
                        }
                        catch { }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            LoadingText.Text = "状态检测完成";
                            _isLoading = false;
                        }
                        catch { }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            LoadingText.Text = "检测失败";
                            _log.Error($"加载设置状态失败: {ex.Message}", "Windows设置");
                        }
                        catch { }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private WindowsSettingsService.SettingStatus GetSettingStatus(string settingId)
    {
        try
        {
            return settingId switch
            {
                "SystemRestore" => _settingsService.GetSystemRestoreStatus(),
                "SearchHighlights" => _settingsService.GetSearchHighlightsStatus(),
                "Widgets" => _settingsService.GetWidgetsStatus(),
                "AutoUpdateDriver" => _settingsService.GetAutoUpdateDriverStatus(),
                "Ceip" => _settingsService.GetCeipStatus(),
                "WindowsUpdate" => _settingsService.GetWindowsUpdateStatus(),
                "Pca" => _settingsService.GetPcaStatus(),
                "Dps" => _settingsService.GetDpsStatus(),
                "MeltdownSpectre" => _settingsService.GetMeltdownSpectreStatus(),
                "MemoryIntegrity" => _settingsService.GetMemoryIntegrityStatus(),
                "Wdac" => _settingsService.GetWdacStatus(),
                "Vbs" => _settingsService.GetVbsStatus(),
                "TcpBbr2" => _settingsService.GetTcpBbr2Status(),
                "Dep" => _settingsService.GetDepStatus(),
                "Uac" => _settingsService.GetUacStatus(),
                "ReservedStorage" => _settingsService.GetReservedStorageStatus(),
                "ShowFileExtensions" => _settingsService.GetShowFileExtensionsStatus(),
                "ShowFullPathInTitleBar" => _settingsService.GetShowFullPathInTitleBarStatus(),
                "ShowHiddenFiles" => _settingsService.GetShowHiddenFilesStatus(),
                "OneDrive" => _settingsService.GetOneDriveStatus(),
                "ContextMenuOptimization" => _settingsService.GetContextMenuOptimizationStatus(),
                "TaskbarCombine" => _settingsService.GetTaskbarCombineStatus(),
                "Hibernate" => _settingsService.GetHibernateStatus(),
                "FastStartup" => _settingsService.GetFastStartupStatus(),
                "RemoteDesktop" => _settingsService.GetRemoteDesktopStatus(),
                "Firewall" => _settingsService.GetFirewallStatus(),
                "SysMain" => _settingsService.GetSysMainStatus(),
                "MemoryCompression" => _settingsService.GetMemoryCompressionStatus(),
                "Prefetch" => _settingsService.GetPrefetchStatus(),
                "DnsCache" => _settingsService.GetDnsCacheStatus(),
                _ => new WindowsSettingsService.SettingStatus { Name = settingId, StatusText = "未知" }
            };
        }
        catch
        {
            return new WindowsSettingsService.SettingStatus { Name = settingId, StatusText = "检测失败" };
        }
    }

    private void AddSettingItem(StackPanel panel, string id, string name, string description, WindowsSettingsService.SettingStatus status)
    {
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7FAFC")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftPanel = new StackPanel();
        var titleBlock = new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748"))
        };
        leftPanel.Children.Add(titleBlock);

        var descBlock = new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")),
            Margin = new Thickness(0, 2, 0, 0)
        };
        leftPanel.Children.Add(descBlock);

        Grid.SetColumn(leftPanel, 0);
        grid.Children.Add(leftPanel);

        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var statusBlock = new TextBlock
        {
            Text = status.StatusText,
            FontSize = 12,
            Foreground = status.IsEnabled 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E")),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        rightPanel.Children.Add(statusBlock);

        var toggleBtn = new Button
        {
            Content = status.IsEnabled ? "开" : "关",
            Width = 50,
            Height = 26,
            FontSize = 11,
            Tag = id,
            Background = status.IsEnabled 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E0")),
            Foreground = status.IsEnabled 
                ? Brushes.White 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568")),
            BorderThickness = new Thickness(0)
        };

        var settingItem = new SettingItem
        {
            Id = id,
            IsEnabled = status.IsEnabled,
            RequiresRestart = status.RequiresRestart,
            RequiresExplorerRestart = status.RequiresExplorerRestart,
            StatusBlock = statusBlock,
            ToggleButton = toggleBtn
        };
        _settingItems[id] = settingItem;

        toggleBtn.Click += ToggleButton_Click;
        toggleBtn.Template = CreateButtonTemplate();
        rightPanel.Children.Add(toggleBtn);

        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(rightPanel);

        border.Child = grid;
        panel.Children.Add(border);
    }

    private ControlTemplate CreateButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "border";
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        
        template.VisualTree = borderFactory;

        var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.OpacityProperty, 0.85, borderFactory.Name));
        template.Triggers.Add(trigger);

        return template;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is string settingId)
            {
                if (_isLoading) return;

                var item = _settingItems[settingId];
                var newState = !item.IsEnabled;

                Task.Run(() =>
                {
                    try
                    {
                        var success = ApplySetting(settingId, newState);
                        var status = GetSettingStatus(settingId);

                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (success)
                                {
                                    item.IsEnabled = status.IsEnabled;
                                    item.RequiresRestart = status.RequiresRestart;
                                    item.RequiresExplorerRestart = status.RequiresExplorerRestart;

                                    btn.Content = status.IsEnabled ? "开" : "关";
                                    btn.Background = status.IsEnabled 
                                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"))
                                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E0"));
                                    btn.Foreground = status.IsEnabled 
                                        ? Brushes.White 
                                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));

                                    item.StatusBlock.Text = status.StatusText;
                                    item.StatusBlock.Foreground = status.IsEnabled 
                                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"))
                                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));

                                    UpdateStatusText(status);
                                }
                                else
                                {
                                    StatusText.Text = "操作失败，请检查权限";
                                    StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
                                    _statusTimer.Start();
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                });
            }
        }
        catch { }
    }

    private bool ApplySetting(string settingId, bool enable)
    {
        try
        {
            return settingId switch
            {
                "SystemRestore" => _settingsService.SetSystemRestore(enable),
                "SearchHighlights" => _settingsService.SetSearchHighlights(enable),
                "Widgets" => _settingsService.SetWidgets(enable),
                "AutoUpdateDriver" => _settingsService.SetAutoUpdateDriver(enable),
                "Ceip" => _settingsService.SetCeip(enable),
                "WindowsUpdate" => _settingsService.SetWindowsUpdate(enable),
                "Pca" => _settingsService.SetPca(enable),
                "Dps" => _settingsService.SetDps(enable),
                "MeltdownSpectre" => _settingsService.SetMeltdownSpectre(enable),
                "MemoryIntegrity" => _settingsService.SetMemoryIntegrity(enable),
                "Wdac" => _settingsService.SetWdac(enable),
                "Vbs" => _settingsService.SetVbs(enable),
                "TcpBbr2" => _settingsService.SetTcpBbr2(enable),
                "Dep" => _settingsService.SetDep(enable),
                "Uac" => _settingsService.SetUac(enable),
                "ReservedStorage" => _settingsService.SetReservedStorage(enable),
                "ShowFileExtensions" => _settingsService.SetShowFileExtensions(enable),
                "ShowFullPathInTitleBar" => _settingsService.SetShowFullPathInTitleBar(enable),
                "ShowHiddenFiles" => _settingsService.SetShowHiddenFiles(enable),
                "OneDrive" => _settingsService.SetOneDrive(enable),
                "ContextMenuOptimization" => _settingsService.SetContextMenuOptimization(enable),
                "TaskbarCombine" => _settingsService.SetTaskbarCombine(enable ? 1 : 2),
                "Hibernate" => _settingsService.SetHibernate(enable),
                "FastStartup" => _settingsService.SetFastStartup(enable),
                "RemoteDesktop" => _settingsService.SetRemoteDesktop(enable),
                "Firewall" => _settingsService.SetFirewall(enable),
                "SysMain" => _settingsService.SetSysMain(enable),
                "MemoryCompression" => _settingsService.SetMemoryCompression(enable),
                "Prefetch" => _settingsService.SetPrefetch(enable),
                "DnsCache" => _settingsService.SetDnsCache(enable),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private void UpdateStatusText(WindowsSettingsService.SettingStatus status)
    {
        try
        {
            if (status.RequiresRestart)
            {
                StatusText.Text = "修改成功，需重启电脑生效";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DD6B20"));
            }
            else if (status.RequiresExplorerRestart)
            {
                StatusText.Text = "修改成功，需重启资源管理器生效";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
            }
            else
            {
                StatusText.Text = "修改成功";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169"));
            }
            _statusTimer.Start();
        }
        catch { }
    }

    private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (CategoryListBox.SelectedItem is ListBoxItem item && item.Tag is string tag)
            {
                SystemPanel.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
                SecurityPanel.Visibility = tag == "Security" ? Visibility.Visible : Visibility.Collapsed;
                ExplorerPanel.Visibility = tag == "Explorer" ? Visibility.Visible : Visibility.Collapsed;
                PerformancePanel.Visibility = tag == "Performance" ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch { }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadAllSettings();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }
}

public class SettingItem
{
    public string Id { get; set; } = "";
    public bool IsEnabled { get; set; }
    public bool RequiresRestart { get; set; }
    public bool RequiresExplorerRestart { get; set; }
    public TextBlock StatusBlock { get; set; } = null!;
    public Button ToggleButton { get; set; } = null!;
}
