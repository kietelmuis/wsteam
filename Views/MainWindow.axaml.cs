using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using wsteam.Data;

namespace wsteam;

public partial class MainWindow : Window
{
    private DownloadManager downloadManager;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.DownloadAsync(1808500);
        }
    }
}
