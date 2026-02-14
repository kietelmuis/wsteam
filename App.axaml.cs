using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using wsteam.Data;
using wsteam.Data.APIs;

namespace wsteam;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<DexLuaApi>();
        services.AddHttpClient<ManifestHubApi>();
        services.AddHttpClient<SteamCMDApi>();

        services.AddSingleton<ManifestHubApi>();
        services.AddSingleton<SteamCMDApi>();
        services.AddSingleton<DepotKeyProvider>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<MainViewModel>();

        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = provider.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
