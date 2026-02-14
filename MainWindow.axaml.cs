using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;
using wsteam.Data;
using wsteam.Data.APIs;

namespace wsteam;

public partial class MainWindow : Window
{
    private ManifestHubApi manifestHubApi;
    private SteamCMDApi steamCMDApi;
    private DownloadManager downloadManager;

    public MainWindow()
    {
        manifestHubApi = new(new HttpClient());
        steamCMDApi = new(new HttpClient());
        downloadManager = new(manifestHubApi, steamCMDApi);

        InitializeComponent();
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        await downloadManager.DownloadAppAsync(1808500, Directory.GetCurrentDirectory());
    }
}
