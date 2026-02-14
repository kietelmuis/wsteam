using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using wsteam.Data;

namespace wsteam;

public partial class MainWindow : Window
{
    private DownloadManager downloadManager;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.DownloadAsync(1245620);
        }
    }
}
