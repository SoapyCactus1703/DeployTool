using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeployTool.Models;

public class MenuItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _hasChildren;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConfirmMessage { get; set; } = string.Empty;
    public int? SubMenuId { get; set; }
    public int? TaskId { get; set; }
    public List<int>? TaskIds { get; set; }
    public List<MenuItem>? Children { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool HasChildren
    {
        get => _hasChildren;
        set { _hasChildren = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class TopMenuItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MenuItem> MenuItems { get; set; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
