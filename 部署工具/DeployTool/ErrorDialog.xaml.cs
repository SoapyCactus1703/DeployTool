using System.Windows;

namespace DeployTool;

public partial class ErrorDialog : Window
{
    public ErrorDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    public ErrorDialog(string title, string message) : this(message)
    {
        TitleText.Text = title;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
