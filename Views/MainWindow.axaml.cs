using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using wsteam.Data.Download;

namespace wsteam;

public partial class MainWindow : Window
{
    public required MainViewModel mainView;

    private readonly Button? gamesButton;
    private readonly Button? sourcesButton;
    private readonly Button? settingsButton;
    private readonly Button? addGameButton;

    public MainWindow()
    {
        InitializeComponent();

        gamesButton = this.FindControl<Button>("GamesButton");
        sourcesButton = this.FindControl<Button>("SourcesButton");
        settingsButton = this.FindControl<Button>("SettingsButton");
        addGameButton = this.FindControl<Button>("AddGameButton");

        addGameButton?.Click += async (o, r) =>
        {
            Console.WriteLine("Clicked");
            await mainView!.DownloadAsync(4164420);
        };
        gamesButton?.Click += OnNavigationClick;
        sourcesButton?.Click += OnNavigationClick;
        settingsButton?.Click += OnNavigationClick;
    }

    private void OnNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button clickedButton) return;

        gamesButton?.Classes.Remove("selected");
        sourcesButton?.Classes.Remove("selected");
        settingsButton?.Classes.Remove("selected");

        clickedButton.Classes.Add("selected");
        Console.WriteLine($"Switching to view {clickedButton.Name}");
    }
}
