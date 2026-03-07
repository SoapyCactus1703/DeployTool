using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeployTool.Models;
using DeployTool.Services;
using LibreHardwareMonitor.Hardware;

namespace DeployTool;

public partial class SystemDetailWindow : Window
{
    private readonly DispatcherTimer _refreshTimer;
    private readonly SystemService _systemService;
    private readonly ObservableCollection<TemperaturePoint> _tempHistory = new();
    private const int MaxHistoryPoints = 60;
    private double _minTemp = double.MaxValue;
    private double _maxTemp = double.MinValue;

    public SystemDetailWindow()
    {
        InitializeComponent();
        _systemService = SystemService.Instance;
        DataContext = new SystemDetailViewModel(_systemService);
        
        RefreshIntervalText.Text = $"{AppSettings.CpuTempRefreshInterval}秒";
        
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AppSettings.CpuTempRefreshInterval)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        
        Loaded += SystemDetailWindow_Loaded;
        Closed += SystemDetailWindow_Closed;
    }

    private void SystemDetailWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAllData();
        _refreshTimer.Start();
    }

    private void SystemDetailWindow_Closed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        UpdateAllData();
    }

    private void UpdateAllData()
    {
        try
        {
            UpdateTemperatureDisplay();
            UpdateFrequencies();
            UpdateFanSpeeds();
            UpdateGpuTemperature();
            UpdateMemoryFrequency();
            UpdateStorageHealth();
            UpdateNetworkSpeeds();
            UpdateBatteryInfo();
            
            if (DataContext is SystemDetailViewModel vm)
            {
                vm.CpuLoad = _systemService.GetCpuLoad();
                vm.GpuLoad = _systemService.GetGpuLoad();
            }
        }
        catch
        {
        }
    }

    private void UpdateTemperatureDisplay()
    {
        try
        {
            var tempInfo = _systemService.GetCpuTemperature();
            
            if (tempInfo.IsAvailable)
            {
                var temp = tempInfo.Temperature;
                
                if (temp < _minTemp) _minTemp = temp;
                if (temp > _maxTemp) _maxTemp = temp;
                
                CurrentTempText.Text = $"{temp:F1}°C";
                MinTempText.Text = $"{_minTemp:F1}°C";
                MaxTempText.Text = $"{_maxTemp:F1}°C";
                
                _tempHistory.Add(new TemperaturePoint { Time = DateTime.Now, Temperature = temp });
                while (_tempHistory.Count > MaxHistoryPoints)
                {
                    _tempHistory.RemoveAt(0);
                }
                
                DrawTemperatureChart();
            }
            else
            {
                CurrentTempText.Text = "不支持";
                MinTempText.Text = "--";
                MaxTempText.Text = "--";
            }
        }
        catch { }
    }

    private void UpdateFrequencies()
    {
        try
        {
            var freq = _systemService.GetCpuFrequencies();
            CurrentFreqText.Text = freq.current > 0 ? $"{freq.current:F0} MHz" : "-- MHz";
            MaxFreqText.Text = freq.max > 0 ? $"{freq.max:F0} MHz" : "-- MHz";
        }
        catch { }
    }

    private void UpdateFanSpeeds()
    {
        try
        {
            var fans = _systemService.GetFanSpeeds();
            if (fans.Count > 0)
            {
                FanSpeedItems.ItemsSource = fans;
                NoFanText.Visibility = Visibility.Collapsed;
            }
            else
            {
                FanSpeedItems.ItemsSource = null;
                NoFanText.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void UpdateGpuTemperature()
    {
        try
        {
            var gpuTemp = _systemService.GetGpuTemperature();
            GpuTempText.Text = gpuTemp > 0 ? $"{gpuTemp:F1}°C" : "--°C";
        }
        catch { }
    }

    private void UpdateMemoryFrequency()
    {
        try
        {
            var memFreq = _systemService.GetMemoryFrequency();
            MemoryFreqText.Text = memFreq > 0 ? $"频率: {memFreq:F0} MHz" : "频率: -- MHz";
        }
        catch { }
    }

    private void UpdateStorageHealth()
    {
        try
        {
            var health = _systemService.GetStorageHealth();
            if (health.Count > 0)
            {
                StorageHealthItems.ItemsSource = health;
                NoStorageHealthText.Visibility = Visibility.Collapsed;
            }
            else
            {
                StorageHealthItems.ItemsSource = null;
                NoStorageHealthText.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void UpdateNetworkSpeeds()
    {
        try
        {
            var speeds = _systemService.GetNetworkSpeeds();
            if (speeds.Count > 0)
            {
                NetworkSpeedItems.ItemsSource = speeds;
                NoNetworkText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NetworkSpeedItems.ItemsSource = null;
                NoNetworkText.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void UpdateBatteryInfo()
    {
        try
        {
            var battery = _systemService.GetBatteryInfo();
            if (DataContext is SystemDetailViewModel vm)
            {
                vm.UpdateBattery(battery);
                BatteryGrid.Visibility = battery.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
                NoBatteryText.Visibility = battery.IsAvailable ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        catch { }
    }

    private void DrawTemperatureChart()
    {
        var canvas = TempChartCanvas;
        if (canvas == null || _tempHistory.Count < 2) return;

        canvas.Children.Clear();

        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var temps = _tempHistory.Select(t => t.Temperature).ToList();
        var minTemp = Math.Floor(temps.Min() / 10) * 10 - 5;
        var maxTemp = Math.Ceiling(temps.Max() / 10) * 10 + 5;
        var tempRange = maxTemp - minTemp;
        if (tempRange == 0) tempRange = 20;

        var points = new PointCollection();
        for (var i = 0; i < temps.Count; i++)
        {
            var x = width * i / (MaxHistoryPoints - 1);
            var y = height - (height * (temps[i] - minTemp) / tempRange);
            points.Add(new Point(x, y));
        }

        if (points.Count >= 2)
        {
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3182CE")),
                StrokeThickness = 2
            };
            canvas.Children.Add(polyline);

            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(points[^1].X, height));
            fillPoints.Add(new Point(points[0].X, height));
            
            var polygon = new Polygon
            {
                Points = fillPoints,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#3182CE"), 0),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#3182CE00"), 1)
                    }
                },
                Opacity = 0.3
            };
            canvas.Children.Add(polygon);
        }

        if (temps.Count > 0)
        {
            var lastTemp = temps[^1];
            var lastX = width * (temps.Count - 1) / (MaxHistoryPoints - 1);
            var lastY = height - (height * (lastTemp - minTemp) / tempRange);

            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3182CE")),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, lastX - 3);
            Canvas.SetTop(dot, lastY - 3);
            canvas.Children.Add(dot);
        }
    }
}

public class SystemDetailViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private readonly SystemService _systemService;
    private double _cpuLoad;
    private double _gpuLoad;
    private ObservableCollection<NetworkSpeedInfo> _networkSpeeds = new();
    private BatteryInfo _battery = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public CpuInfo Cpu => _systemService.GetCpuInfo();
    public MemoryInfo Memory => _systemService.GetMemoryInfo();
    public GpuInfo Gpu => _systemService.GetGpuInfo();
    public MotherboardInfo Motherboard => _systemService.GetMotherboardInfo();
    public ObservableCollection<DiskInfo> Disks { get; }
    public ObservableCollection<NetworkInfo> Networks { get; }
    public string ComputerName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string WindowsVersion => _systemService.GetWindowsVersion();
    public string WindowsBuild => _systemService.GetWindowsBuild();
    public string SystemUptime => _systemService.GetSystemUptime();
    
    public bool IsWindowsActivated
    {
        get
        {
            var (activated, _) = _systemService.CheckWindowsActivationStatus();
            return activated;
        }
    }
    
    public bool IsOfficeActivated
    {
        get
        {
            var (activated, _) = _systemService.CheckOfficeActivationStatus();
            return activated;
        }
    }

    public double CpuLoad
    {
        get => _cpuLoad;
        set { _cpuLoad = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CpuLoad))); }
    }

    public double GpuLoad
    {
        get => _gpuLoad;
        set { _gpuLoad = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(GpuLoad))); }
    }

    public ObservableCollection<NetworkSpeedInfo> NetworkSpeeds
    {
        get => _networkSpeeds;
        set { _networkSpeeds = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NetworkSpeeds))); }
    }

    public BatteryInfo Battery
    {
        get => _battery;
        set { _battery = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Battery))); }
    }

    public SystemDetailViewModel(SystemService systemService)
    {
        _systemService = systemService;
        Disks = new ObservableCollection<DiskInfo>(_systemService.GetDiskInfo());
        Networks = new ObservableCollection<NetworkInfo>(_systemService.GetNetworkInfo());
        _cpuLoad = systemService.GetCpuLoad();
        _gpuLoad = systemService.GetGpuLoad();
    }

    public void UpdateNetworkSpeeds(List<NetworkSpeedInfo> speeds)
    {
        NetworkSpeeds = new ObservableCollection<NetworkSpeedInfo>(speeds);
    }

    public void UpdateBattery(BatteryInfo battery)
    {
        Battery = battery;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

public class TemperaturePoint
{
    public DateTime Time { get; set; }
    public double Temperature { get; set; }
}
