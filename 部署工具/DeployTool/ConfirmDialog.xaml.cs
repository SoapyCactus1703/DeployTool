using System.Windows;

namespace DeployTool;

public partial class ConfirmDialog : Window
{
    public bool IsConfirmed { get; private set; }

    public ConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }
}
