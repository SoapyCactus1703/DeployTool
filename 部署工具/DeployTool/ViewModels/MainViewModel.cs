using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DeployTool.Models;
using DeployTool.Services;

namespace DeployTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SystemService _systemService = SystemService.Instance;
    private readonly ProcessService _processService = new();
    private readonly OptimizationService _optimizationService = new();
    private readonly LogService _log = LogService.Instance;

    private SystemInfo _systemInfo = new();
    private string _currentCategory = "功能导航";
    private bool _isExecuting;
    private TopMenuItem _selectedTopMenu = null!;
    private CheckpointState? _currentCheckpoint;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ConfirmEventArgs>? ConfirmRequested;
    public event EventHandler<InputEventArgs>? InputRequested;
    public event Action<LogEntry>? LogAdded;
    public event EventHandler<CheckpointResumeEventArgs>? CheckpointResumeRequested;

    public SystemInfo SystemInfo
    {
        get => _systemInfo;
        set { _systemInfo = value; OnPropertyChanged(); }
    }

    public string CurrentCategory
    {
        get => _currentCategory;
        set { _currentCategory = value; OnPropertyChanged(); }
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        set { _isExecuting = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TopMenuItem> TopMenuItems { get; } = new();
    public ObservableCollection<MenuItem> CurrentMenuItems { get; } = new();
    public ObservableCollection<LogEntry> LogEntries => _log.LogEntries;

    public ICommand ClearLogCommand { get; }
    public ICommand RefreshSystemInfoCommand { get; }

    public MainViewModel()
    {
        ClearLogCommand = new RelayCommand(ClearLog);
        RefreshSystemInfoCommand = new RelayCommand(RefreshSystemInfo);

        _log.LogAdded += entry => LogAdded?.Invoke(entry);

        InitializeMenus();
        LoadSystemInfo();
    }
    
    public void CheckForCheckpointOnStartup()
    {
        var checkpoint = _systemService.LoadCheckpoint();
        if (checkpoint != null && checkpoint.HasMoreTasks)
        {
            CheckpointResumeRequested?.Invoke(this, new CheckpointResumeEventArgs { Checkpoint = checkpoint });
        }
    }

    private void InitializeMenus()
    {
        var topMenus = new List<TopMenuItem>
        {
            new TopMenuItem
            {
                Id = 1, Name = "流程部署",
                MenuItems = new List<MenuItem>
                {
                    new MenuItem 
                    { 
                        Id = 101, Name = "教学部署", Description = "教学环境部署方案",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 11, Name = "标准教学环境 (改名seewo)", Description = "安装seewo相关软件，更改用户名为seewo，激活系统", ConfirmMessage = "即将执行【标准教学环境部署】：\n\n1. 将 Administrator 重命名为 seewo\n2. 安装 .NET SDK\n3. 安装显卡驱动（自动检测CPU代数）\n4. 安装 seewo 教学应用\n5. 安装办公应用\n6. 安装 360 极速浏览器X\n7. 激活系统与 Office\n8. 执行系统优化\n\n此操作可能需要较长时间，确定继续吗？", TaskIds = new List<int> { 101, 301, 302, 401, 402, 404, 501, 502 } },
                            new MenuItem { Id = 12, Name = "标准教学环境 (带迁移)", Description = "安装seewo相关软件，迁移用户文件夹到D盘", ConfirmMessage = "即将执行【标准教学环境部署（带迁移）】：\n\n1. 迁移用户文件夹到 D 盘\n2. 重启资源管理器\n3. 安装 .NET SDK\n4. 安装显卡驱动\n5. 安装 seewo 教学应用\n6. 安装办公应用\n7. 安装 360 极速浏览器X\n8. 激活系统与 Office\n9. 执行系统优化\n\n注意：迁移用户文件夹需要 D 盘存在！\n确定继续吗？", TaskIds = new List<int> { 612, 613, 301, 302, 401, 402, 404, 501, 502 } },
                            new MenuItem { Id = 13, Name = "完整教学环境", Description = "改名seewo + 迁移用户文件夹 + 完整部署", ConfirmMessage = "即将执行【完整教学环境部署】：\n\n1. 迁移用户文件夹到 D 盘\n2. 重启资源管理器\n3. 将 Administrator 重命名为 seewo\n4. 安装 .NET SDK\n5. 安装显卡驱动\n6. 安装 seewo 教学应用\n7. 安装办公应用\n8. 安装 360 极速浏览器X\n9. 激活系统与 Office\n10. 执行系统优化\n\n这是最完整的部署流程，确定继续吗？", TaskIds = new List<int> { 612, 613, 101, 301, 302, 401, 402, 404, 501, 502 } }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 102, Name = "办公部署", Description = "办公环境部署方案",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 21, Name = "基础办公环境", Description = "安装办公软件、输入法、安全软件", ConfirmMessage = "即将执行【基础办公环境部署】：\n\n1. 安装办公应用（Apps2 目录）\n2. 安装搜狗输入法\n3. 安装 360 极速浏览器X\n4. 激活系统与 Office\n5. 执行系统优化\n\n确定继续吗？", TaskIds = new List<int> { 402, 403, 404, 501, 502 } },
                            new MenuItem { Id = 22, Name = "基础办公 (带迁移)", Description = "安装办公软件，迁移用户文件夹", ConfirmMessage = "即将执行【基础办公环境部署（带迁移）】：\n\n1. 迁移用户文件夹到 D 盘\n2. 重启资源管理器\n3. 安装办公应用\n4. 安装 360 极速浏览器X\n5. 激活系统与 Office\n6. 执行系统优化\n\n确定继续吗？", TaskIds = new List<int> { 612, 613, 402, 404, 501, 502 } },
                            new MenuItem { Id = 23, Name = "精简办公环境", Description = "仅安装基础办公软件", ConfirmMessage = "即将执行【精简办公环境部署】：\n\n1. 安装办公应用\n2. 安装 360 极速浏览器X\n3. 激活系统与 Office\n4. 执行系统优化\n\n确定继续吗？", TaskIds = new List<int> { 402, 404, 501, 502 } }
                        }
                    }
                }
            },
            new TopMenuItem
            {
                Id = 2, Name = "系统优化",
                MenuItems = new List<MenuItem>
                {
                    new MenuItem 
                    { 
                        Id = 201, Name = "优化套餐", Description = "一键优化方案",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 31, Name = "快速优化套餐", Description = "执行安全的快速优化", ConfirmMessage = "即将执行【快速优化套餐】：\n\n1. 清理临时文件和系统缓存\n2. 根据磁盘类型执行 TRIM 或碎片整理\n3. 切换到高性能电源计划\n4. 关闭休眠、禁用 SuperFetch\n5. 优化网络设置\n\n确定继续吗？", TaskId = 701 },
                            new MenuItem { Id = 32, Name = "完整优化套餐", Description = "执行全面的系统优化", ConfirmMessage = "即将执行【完整优化套餐】：\n\n1. 清理临时文件\n2. 磁盘性能优化\n3. 高性能模式\n4. 性能优化调整\n5. 系统服务优化\n6. 移除预装应用\n7. 网络优化\n8. 隐私设置调整\n9. 安全加固\n10. UAC 优化\n11. BIOS 优化\n12. 游戏模式增强\n\n确定继续吗？", TaskId = 702 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 202, Name = "磁盘与性能", Description = "磁盘清理与性能优化",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 33, Name = "磁盘清理", Description = "清理临时文件、系统缓存", ConfirmMessage = "即将执行【磁盘清理】：\n\n清理以下位置的临时文件：\n- Windows 临时文件夹\n- 用户临时文件夹\n- Windows Update 缓存\n\n确定继续吗？", TaskId = 601 },
                            new MenuItem { Id = 34, Name = "磁盘性能优化", Description = "根据磁盘类型执行TRIM或碎片整理", ConfirmMessage = "即将执行【磁盘性能优化】：\n\n自动检测系统盘类型：\n- SSD：执行 TRIM 优化\n- HDD：执行碎片整理\n\n确定继续吗？", TaskId = 602 },
                            new MenuItem { Id = 35, Name = "高性能模式", Description = "切换到高性能电源计划", ConfirmMessage = "即将执行【高性能模式】：\n\n将电源计划切换为【高性能】模式，\n以获得最佳系统性能。\n\n确定继续吗？", TaskId = 604 },
                            new MenuItem { Id = 36, Name = "性能优化调整", Description = "关闭搜索索引、SuperFetch等服务", ConfirmMessage = "即将执行【性能优化调整】：\n\n1. 关闭系统休眠\n2. 禁用 Windows Search 服务\n3. 禁用 SuperFetch/SysMain 服务\n4. 优化内存管理设置\n\n确定继续吗？", TaskId = 605 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 203, Name = "系统服务", Description = "服务与隐私设置",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 37, Name = "系统服务优化", Description = "禁用不必要的后台服务", ConfirmMessage = "即将执行【系统服务优化】：\n\n将禁用以下服务：\n- Fax（传真服务）\n- 地理位置服务\n- 地图下载服务\n- 分布式链接跟踪\n- 平板电脑输入服务\n\n确定继续吗？", TaskId = 607 },
                            new MenuItem { Id = 38, Name = "移除预装应用", Description = "清理Microsoft预装应用", ConfirmMessage = "即将执行【移除预装应用】：\n\n将移除以下 Microsoft 预装应用：\n- Bing 新闻\n- Xbox 相关应用\n- Skype\n- 获取帮助\n- 开始菜单\n\n确定继续吗？", TaskId = 610 },
                            new MenuItem { Id = 39, Name = "网络优化", Description = "优化TCP参数和DNS缓存", ConfirmMessage = "即将执行【网络优化】：\n\n1. 优化 TCP/IP 参数\n2. 清除 DNS 缓存\n\n确定继续吗？", TaskId = 617 },
                            new MenuItem { Id = 310, Name = "隐私设置调整", Description = "禁用诊断数据和活动历史", ConfirmMessage = "即将执行【隐私设置调整】：\n\n1. 禁用 Windows 遥测\n2. 禁用活动历史记录\n\n确定继续吗？", TaskId = 618 },
                            new MenuItem { Id = 311, Name = "安全加固", Description = "禁用SMBv1和LLMNR", ConfirmMessage = "即将执行【安全加固】：\n\n1. 禁用 SMBv1 协议（防止勒索病毒）\n2. 禁用 LLMNR（防止 DNS 欺骗）\n\n确定继续吗？", TaskId = 619 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 204, Name = "时间设置", Description = "NTP时间同步",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 312, Name = "设置NTP服务器", Description = "设置NTP服务器为国家授时中心", ConfirmMessage = "即将执行【设置NTP服务器】：\n\n将 NTP 服务器设置为：\n" + AppSettings.NtpServer + "\n\n并启用自动时间同步服务。\n确定继续吗？", TaskId = 720 },
                            new MenuItem { Id = 313, Name = "立即同步时间", Description = "立即同步系统时间", ConfirmMessage = "即将执行【立即同步时间】：\n\n将立即从 NTP 服务器同步系统时间。\n\n确定继续吗？", TaskId = 721 }
                        }
                    }
                }
            },
            new TopMenuItem
            {
                Id = 3, Name = "软件与驱动",
                MenuItems = new List<MenuItem>
                {
                    new MenuItem 
                    { 
                        Id = 301, Name = "驱动安装", Description = "安装系统驱动",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 41, Name = "安装显卡驱动", Description = "根据CPU代数自动选择并安装显卡驱动", ConfirmMessage = "即将执行【安装显卡驱动】：\n\n将根据 CPU 代数自动选择驱动：\n- 7-10 代 CPU：gfx_win_101.2137.exe\n- 11-14 代 CPU：gfx_win_101.7076.exe\n\n安装完成后可能需要重启系统。\n确定继续吗？", TaskId = 302 },
                            new MenuItem { Id = 42, Name = "安装 .NET SDK", Description = "安装 .NET 运行时与开发工具包", ConfirmMessage = "即将执行【安装 .NET SDK】：\n\n从 Appx 目录安装 .NET SDK 运行时。\n\n确定继续吗？", TaskId = 301 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 302, Name = "教学软件", Description = "安装教学相关软件",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 51, Name = "安装seewo教学应用", Description = "静默安装 Apps/ 下所有 .exe 应用", ConfirmMessage = "即将执行【安装 seewo 教学应用】：\n\n将静默安装 Apps 目录下所有 .exe 应用程序。\n\n确定继续吗？", TaskId = 401 },
                            new MenuItem { Id = 56, Name = "安装希沃白板5", Description = "从 Appx/ 安装希沃白板5", ConfirmMessage = "即将执行【安装希沃白板5】：\n\n从 Appx 目录安装希沃白板5。\n\n确定继续吗？", TaskId = 406 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 303, Name = "办公软件", Description = "安装办公相关软件",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 52, Name = "安装办公应用", Description = "静默安装 Apps2/ 下所有 .exe 应用", ConfirmMessage = "即将执行【安装办公应用】：\n\n将静默安装 Apps2 目录下所有 .exe 应用程序。\n\n确定继续吗？", TaskId = 402 },
                            new MenuItem { Id = 53, Name = "安装搜狗输入法", Description = "从 Appx/ 安装搜狗输入法", ConfirmMessage = "即将执行【安装搜狗输入法】：\n\n从 Appx 目录安装搜狗输入法。\n\n确定继续吗？", TaskId = 403 },
                            new MenuItem { Id = 54, Name = "安装360极速浏览器X", Description = "静默安装360极速浏览器X", ConfirmMessage = "即将执行【安装 360 极速浏览器X】：\n\n静默安装 360 极速浏览器X。\n\n确定继续吗？", TaskId = 404 },
                            new MenuItem { Id = 55, Name = "安装360安全卫士", Description = "从 Appx/ 安装360安全卫士极速版", ConfirmMessage = "即将执行【安装 360 安全卫士】：\n\n从 Appx 目录安装 360 安全卫士极速版。\n\n确定继续吗？", TaskId = 405 }
                        }
                    }
                }
            },
            new TopMenuItem
            {
                Id = 4, Name = "网络与用户",
                MenuItems = new List<MenuItem>
                {
                    new MenuItem 
                    { 
                        Id = 401, Name = "网络设置", Description = "IP和DNS配置",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 91, Name = "自动获取IP地址", Description = "设置网卡为DHCP自动获取IP", ConfirmMessage = "即将执行【自动获取IP地址】：\n\n将所有活动网卡设置为 DHCP 自动获取 IP 地址。\n\n确定继续吗？", TaskId = 801 },
                            new MenuItem { Id = 92, Name = "自动获取DNS", Description = "设置DNS为自动获取", ConfirmMessage = "即将执行【自动获取DNS】：\n\n将所有活动网卡的 DNS 设置为自动获取。\n\n确定继续吗？", TaskId = 802 },
                            new MenuItem { Id = 93, Name = "手动设置IP地址", Description = "手动指定IP地址、子网掩码、网关", ConfirmMessage = "INPUT_IP", TaskId = 803 },
                            new MenuItem { Id = 94, Name = "手动设置DNS", Description = "手动指定首选和备用DNS", ConfirmMessage = "INPUT_DNS", TaskId = 804 },
                            new MenuItem { Id = 95, Name = "使用预设网络配置", Description = "应用设置中保存的网络预设", ConfirmMessage = "即将执行【使用预设网络配置】：\n\n将应用设置中保存的网络预设配置。\n\n确定继续吗？", TaskId = 805 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 402, Name = "用户管理", Description = "用户账户设置",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 61, Name = "更改用户名为 seewo", Description = "将 Administrator 重命名为 seewo", ConfirmMessage = "即将执行【更改用户名为 seewo】：\n\n将 Administrator 账户重命名为 seewo。\n\n注意：此操作需要重启系统后生效！\n确定继续吗？", TaskId = 101 },
                            new MenuItem { Id = 62, Name = "自定义用户名", Description = "输入新的用户名进行重命名", ConfirmMessage = "INPUT", TaskId = 102 },
                            new MenuItem { Id = 63, Name = "迁移用户文件夹", Description = "将用户文件夹迁移到D盘", ConfirmMessage = "即将执行【迁移用户文件夹】：\n\n将以下文件夹迁移到 D:\\User\\用户名：\n- 文档\n- 下载\n- 音乐\n- 图片\n- 视频\n- 桌面\n\n注意：此操作需要 D 盘存在！\n迁移后需要重启资源管理器生效。\n确定继续吗？", TaskId = 612 },
                            new MenuItem { Id = 64, Name = "重启资源管理器", Description = "重启资源管理器使迁移生效", ConfirmMessage = "即将执行【重启资源管理器】：\n\n将重启 Windows 资源管理器，\n使用户文件夹迁移生效。\n\n确定继续吗？", TaskId = 613 }
                        }
                    }
                }
            },
            new TopMenuItem
            {
                Id = 5, Name = "系统管理",
                MenuItems = new List<MenuItem>
                {
                    new MenuItem 
                    { 
                        Id = 501, Name = "常用工具", Description = "系统配置与激活",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 81, Name = "Windows轻松设置", Description = "可视化调整Windows系统设置", ConfirmMessage = "WINDOW_EASY_SETTINGS" },
                            new MenuItem { Id = 71, Name = "激活系统与Office", Description = "使用 HEU KMS 激活", ConfirmMessage = "即将执行【激活系统与 Office】：\n\n使用 HEU KMS 工具激活 Windows 和 Office。\n\n确定继续吗？", TaskId = 501 }
                        }
                    },
                    new MenuItem 
                    { 
                        Id = 502, Name = "系统操作", Description = "优化与重启",
                        HasChildren = true,
                        Children = new List<MenuItem>
                        {
                            new MenuItem { Id = 72, Name = "系统优化", Description = "时间同步、隐藏图标、清理右键、硬盘优化", ConfirmMessage = "即将执行【系统优化】：\n\n1. 同步系统时间到国家授时中心\n2. 隐藏控制面板桌面图标\n3. 清理右键菜单多余项\n4. 根据硬盘类型优化设置\n5. 优化文件系统行为\n\n确定继续吗？", TaskId = 502 },
                            new MenuItem { Id = 73, Name = "重启系统", Description = "重启计算机", ConfirmMessage = "即将执行【重启系统】：\n\n系统将在 3 秒后自动重启！\n请确保已保存所有工作。\n\n确定继续吗？", TaskId = 202 },
                            new MenuItem { Id = 74, Name = "取消重启任务", Description = "删除重启标记文件", ConfirmMessage = "即将执行【取消重启任务】：\n\n删除所有重启相关的标记文件。\n\n确定继续吗？", TaskId = 203 }
                        }
                    }
                }
            }
        };

        foreach (var menu in topMenus)
        {
            TopMenuItems.Add(menu);
        }

        _selectedTopMenu = TopMenuItems.First();
        _selectedTopMenu.IsSelected = true;
        UpdateCurrentMenuItems(_selectedTopMenu);
    }

    private void UpdateCurrentMenuItems(TopMenuItem topMenu)
    {
        CurrentMenuItems.Clear();
        foreach (var item in topMenu.MenuItems)
        {
            CurrentMenuItems.Add(item);
        }
        CurrentCategory = topMenu.Name;
    }

    public void SelectTopMenu(TopMenuItem topMenu)
    {
        if (_selectedTopMenu != null)
            _selectedTopMenu.IsSelected = false;

        _selectedTopMenu = topMenu;
        _selectedTopMenu.IsSelected = true;
        UpdateCurrentMenuItems(topMenu);
    }

    public void ExecuteMenuItem(MenuItem item)
    {
        if (IsExecuting) return;

        if (item.ConfirmMessage == "INPUT")
        {
            OnInputRequested(item);
        }
        else if (!string.IsNullOrEmpty(item.ConfirmMessage))
        {
            OnConfirmRequested(item);
        }
        else
        {
            ExecuteMenuItemDirect(item);
        }
    }

    private void OnConfirmRequested(MenuItem item)
    {
        var args = new ConfirmEventArgs { Item = item, Message = item.ConfirmMessage };
        ConfirmRequested?.Invoke(this, args);
        if (args.Confirmed)
        {
            ExecuteMenuItemDirect(item);
        }
    }

    private void OnInputRequested(MenuItem item)
    {
        var args = new InputEventArgs { Item = item, Prompt = "请输入新的用户名：", Title = "自定义用户名" };
        InputRequested?.Invoke(this, args);
        if (args.Confirmed && !string.IsNullOrWhiteSpace(args.InputText))
        {
            ExecuteRenameCustomUser(args.InputText);
        }
    }

    public void ExecuteMenuItemDirect(MenuItem item)
    {
        if (IsExecuting) return;

        if (item.TaskId.HasValue)
        {
            ExecuteTask(item.TaskId.Value);
        }
        else if (item.TaskIds != null && item.TaskIds.Count > 0)
        {
            IsExecuting = true;
            _currentCheckpoint = new CheckpointState
            {
                ProcessName = item.Name,
                TaskIds = new List<int>(item.TaskIds),
                CurrentTaskIndex = -1,
                StartTime = DateTime.Now,
                LogSessionId = Guid.NewGuid().ToString("N")[..8]
            };
            _systemService.SaveCheckpoint(_currentCheckpoint);
            
            Task.Run(() =>
            {
                try
                {
                    ExecuteTaskList(_currentCheckpoint);
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsExecuting = false;
                    });
                }
            });
        }
    }
    
    public void ResumeFromCheckpoint(CheckpointState checkpoint)
    {
        if (IsExecuting) return;
        
        IsExecuting = true;
        _currentCheckpoint = checkpoint;
        _log.Info($"继续执行流程: {checkpoint.ProcessName}", "断点续传");
        _log.Info($"从任务 [{checkpoint.NextTaskId}] 继续 (进度: {checkpoint.NextTaskIndex + 1}/{checkpoint.TaskIds.Count})", "断点续传");
        
        Task.Run(() =>
        {
            try
            {
                ExecuteTaskList(_currentCheckpoint, true);
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsExecuting = false;
                });
            }
        });
    }
    
    private void ExecuteTaskList(CheckpointState checkpoint, bool isResuming = false)
    {
        var startIndex = isResuming ? checkpoint.NextTaskIndex : 0;
        
        for (int i = startIndex; i < checkpoint.TaskIds.Count; i++)
        {
            var taskId = checkpoint.TaskIds[i];
            checkpoint.CurrentTaskIndex = i;
            checkpoint.CurrentTaskId = taskId;
            
            _systemService.SaveCheckpoint(checkpoint);
            
            if (IsRebootTask(taskId))
            {
                checkpoint.RequiresReboot = true;
                checkpoint.RebootTaskIndex = i;
                _systemService.SaveCheckpoint(checkpoint);
                
                _log.Warning($"即将重启系统，断点已保存", "断点管理");
                _log.Warning($"重启后将自动继续执行: {checkpoint.ProcessName}", "断点管理");
                _log.Warning($"重启后剩余任务数: {checkpoint.TaskIds.Count - i - 1}", "断点管理");
                
                Thread.Sleep(1000);
                
                ExecuteTaskById(taskId);
                return;
            }
            
            ExecuteTaskById(taskId);
        }
        
        _systemService.MarkCheckpointCompleted();
        _log.Success($"流程 [{checkpoint.ProcessName}] 已完成!", "断点管理");
    }
    
    private bool IsRebootTask(int taskId)
    {
        return taskId == 201 || taskId == 202;
    }

    public void LoadSystemInfo()
    {
        Task.Run(() =>
        {
            try
            {
                var info = _systemService.GetSystemInfo();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SystemInfo = info;
                });
            }
            catch (Exception ex)
            {
                _log.Error($"加载系统信息失败: {ex.Message}", "系统信息");
            }
        });
    }

    private void RefreshSystemInfo()
    {
        LoadSystemInfo();
        _log.Info("系统信息已刷新", "系统信息");
    }

    public void RefreshCpuTemperature()
    {
        Task.Run(() =>
        {
            try
            {
                var tempInfo = _systemService.GetCpuTemperature();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (SystemInfo != null)
                    {
                        SystemInfo.CpuTemperature = tempInfo;
                        OnPropertyChanged(nameof(SystemInfo));
                    }
                });
            }
            catch { }
        });
    }

    private void ClearLog()
    {
        _log.Clear();
    }

    private void ExecuteTask(int taskId)
    {
        if (IsExecuting) return;

        IsExecuting = true;
        Task.Run(() =>
        {
            try
            {
                ExecuteTaskById(taskId);
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsExecuting = false;
                });
            }
        });
    }

    private void ExecuteTaskById(int taskId)
    {
        _log.Info($"开始执行任务: [{taskId}]", "任务执行");

        bool success;
        switch (taskId)
        {
            case 101:
                success = _systemService.RenameUserAccount("seewo");
                break;
            case 102:
                success = true;
                break;
            case 201:
                success = ExecuteRebootWithFlag();
                break;
            case 202:
                success = ExecuteReboot();
                break;
            case 203:
                success = CancelReboot();
                break;
            case 301:
                success = _processService.InstallDotNetSdk();
                break;
            case 302:
                success = _processService.InstallGraphicsDriver();
                break;
            case 401:
                success = _processService.InstallAllFromDirectory(@"Data\Apps") > 0;
                break;
            case 402:
                success = _processService.InstallAllFromDirectory(@"Data\apps2") > 0;
                break;
            case 403:
                success = _processService.InstallSoftware(@"Data\Appx\搜狗输入法.exe", "/S");
                break;
            case 404:
                success = _processService.InstallSoftware(@"Data\Appx\360极速浏览器X.exe", "--silent-install=3_1_1");
                break;
            case 405:
                success = _processService.InstallSoftware(@"Data\Appx\360安全卫士极速版.exe", "/s");
                break;
            case 406:
                success = _processService.InstallSoftware(@"Data\Appx\希沃白板5.exe", "/S");
                break;
            case 501:
                success = _processService.ActivateWindows();
                break;
            case 502:
                success = _optimizationService.SystemOptimize();
                break;
            case 601:
                success = _optimizationService.DiskCleanup();
                break;
            case 602:
                success = _optimizationService.DiskOptimize();
                break;
            case 604:
                success = _optimizationService.SetHighPerformanceMode();
                break;
            case 605:
                success = _optimizationService.OptimizePerformance();
                break;
            case 607:
                success = _optimizationService.OptimizeSystemServices();
                break;
            case 610:
                success = _optimizationService.RemoveBloatware();
                break;
            case 612:
                success = _systemService.MoveUserFolders("D");
                break;
            case 613:
                _systemService.RestartExplorer();
                success = true;
                break;
            case 617:
                success = _optimizationService.OptimizeNetwork();
                break;
            case 618:
                success = _optimizationService.EnhancePrivacy();
                break;
            case 619:
                success = _optimizationService.EnhanceSecurity();
                break;
            case 701:
                success = _optimizationService.QuickOptimize();
                break;
            case 702:
                success = _optimizationService.FullOptimize();
                break;
            case 720:
                success = _optimizationService.SetNtpServer(AppSettings.NtpServer);
                break;
            case 721:
                success = _optimizationService.SyncTime();
                break;
            case 801:
                success = _optimizationService.SetDhcpIp();
                break;
            case 802:
                success = _optimizationService.SetDhcpDns();
                break;
            case 803:
                success = true;
                break;
            case 804:
                success = true;
                break;
            case 805:
                success = _optimizationService.ApplyNetworkPreset();
                break;
            case 901:
                _systemService.DeleteFlagFile("DEPLOY_SUCCESS.flag");
                success = true;
                break;
            case 902:
                _systemService.DeleteFlagFile("USER_RENAMED_REBOOT.flag");
                success = true;
                break;
            case 903:
                _systemService.DeleteFlagFile("USER_FOLDERS_MOVED.flag");
                success = true;
                break;
            case 904:
                _systemService.DeleteFlagFile("DOTNET_INSTALLED.flag");
                success = true;
                break;
            case 905:
                _systemService.DeleteFlagFile("DRIVER_INSTALLED.flag");
                success = true;
                break;
            case 906:
                success = ClearAllFlags();
                break;
            default:
                success = false;
                break;
        }

        if (success)
        {
            _log.Success($"任务 [{taskId}] 执行成功", "任务执行");
        }
        else
        {
            _log.Warning($"任务 [{taskId}] 执行失败或被跳过", "任务执行");
        }
    }

    private void ExecuteRenameCustomUser(string newUserName)
    {
        if (IsExecuting) return;

        IsExecuting = true;
        Task.Run(() =>
        {
            try
            {
                _log.Info($"开始执行任务: 将用户名更改为 {newUserName}", "任务执行");
                var success = _systemService.RenameUserAccount(newUserName);
                if (success)
                {
                    _log.Success($"用户名已更改为: {newUserName}", "任务执行");
                }
                else
                {
                    _log.Warning("用户名更改失败", "任务执行");
                }
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsExecuting = false;
                });
            }
        });
    }

    private bool ExecuteRebootWithFlag()
    {
        if (_systemService.CheckFlagFile("Restarted.flag"))
        {
            _log.Warning("检测到重启标记已存在，跳过重启", "系统管理");
            return false;
        }

        _systemService.CreateFlagFile("Restarted.flag", $"任务201已执行 - {DateTime.Now}");
        _log.Warning("3秒后重启系统...", "系统管理");
        _systemService.RestartComputer(3);
        return true;
    }

    private bool ExecuteReboot()
    {
        if (_currentCheckpoint != null)
        {
            _log.Warning($"重启系统后将继续执行流程: {_currentCheckpoint.ProcessName}", "断点管理");
            _log.Warning($"重启后剩余任务数: {_currentCheckpoint.TaskIds.Count - _currentCheckpoint.CurrentTaskIndex - 1}", "断点管理");
        }
        _log.Warning("3秒后重启系统...", "系统管理");
        _systemService.RestartComputer(3);
        return true;
    }

    private bool CancelReboot()
    {
        _systemService.DeleteFlagFile("Restarted.flag");
        _systemService.ClearCheckpoint();
        _log.Success("已删除重启标记和断点文件", "系统管理");
        return true;
    }

    private bool ClearAllFlags()
    {
        var labelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Label");
        if (Directory.Exists(labelDir))
        {
            var files = Directory.GetFiles(labelDir, "*.flag");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    _log.Info($"已删除: {Path.GetFileName(file)}", "清理标记");
                }
                catch (Exception ex)
                {
                    _log.Warning($"删除失败: {Path.GetFileName(file)} - {ex.Message}", "清理标记");
                }
            }
            _log.Success($"共删除 {files.Length} 个标记文件", "清理标记");
        }
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ConfirmEventArgs : EventArgs
{
    public MenuItem Item { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

public class InputEventArgs : EventArgs
{
    public MenuItem Item { get; set; } = new();
    public string Prompt { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string InputText { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
}

public class CheckpointResumeEventArgs : EventArgs
{
    public CheckpointState Checkpoint { get; set; } = new();
    public bool Confirmed { get; set; }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);
}
