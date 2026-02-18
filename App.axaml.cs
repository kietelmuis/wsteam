using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using wsteam.Data.Downloads;
using wsteam.Data.Manifests;
using wsteam.Data.Singletons;
using wsteam.Data.Steam;

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
        services.AddHttpClient<SteamManifestApi>();

        services.AddSingleton<ILuaApi>(sp =>
            sp.GetRequiredService<SteamManifestApi>());
        services.AddSingleton<IManifestApi>(sp =>
            sp.GetRequiredService<SteamManifestApi>());

        services.AddSingleton<SteamPicsClient>();
        services.AddSingleton<DepotKeyProvider>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<SteamSession>();
        services.AddSingleton<MainViewModel>();

        var provider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = provider.GetRequiredService<MainViewModel>();
            var mainView = provider.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
                mainView = mainView
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
