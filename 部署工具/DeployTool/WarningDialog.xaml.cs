using System.Windows;

namespace DeployTool;

public partial class WarningDialog : Window
{
    public bool IsConfirmed { get; private set; }

    public WarningDialog()
    {
        InitializeComponent();
    }

    public WarningDialog(string title, string message, string confirmButtonText = "确认执行") : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmButtonText;
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

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }
}
