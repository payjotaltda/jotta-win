using System.IO;
using System.Text.Json;
using System.Windows;

namespace JottaWin;

public partial class MainWindow : Window
{
    private readonly Func<string, Task>? _onSaved;

    public MainWindow(Func<string, Task>? onSaved = null)
    {
        _onSaved = onSaved;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JOTTA",
                "jotta-settings.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                if (doc.RootElement.TryGetProperty("serverUrl", out var u))
                    ServerUrlBox.Text = u.GetString() ?? ServerUrlBox.Text;
            }
            else
            {
                ServerUrlBox.Text = "https://storage-dev.tailf8d872.ts.net:8443";
            }
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var url = ServerUrlBox.Text.Trim();
        if (_onSaved is not null)
            await _onSaved(url);
        Close();
    }
}
