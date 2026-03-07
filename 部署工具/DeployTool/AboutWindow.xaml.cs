using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DeployTool;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadAuthorAvatar();
    }

    private void LoadAuthorAvatar()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("40768.ico");
            
            if (stream != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                AuthorAvatar.Source = bitmap;
            }
        }
        catch
        {
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_ManipulationBoundaryFeedback(object sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        e.Handled = true;
    }
}
