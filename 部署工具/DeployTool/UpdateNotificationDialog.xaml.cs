using System.Windows;
using DeployTool.Services;

namespace DeployTool;

public partial class UpdateNotificationDialog : Window
{
    private readonly UpdateInfo _updateInfo;

    public UpdateNotificationDialog(UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        
        VersionText.Text = $"v{updateInfo.Version}";
        DateText.Text = updateInfo.ReleaseDate.HasValue 
            ? $"发布日期: {updateInfo.ReleaseDate.Value:yyyy-MM-dd}" 
            : "";
        NotesText.Text = updateInfo.ReleaseNotes;
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        
        var updateWindow = new UpdateWindow(false)
        {
            Owner = Owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        updateWindow.ShowDialog();
    }
}
