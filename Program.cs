using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using wsteam.Core.DepotKey;
using wsteam.Core.Downloads;
using wsteam.Core.Manifests;
using wsteam.Core.Singletons;
using wsteam.Core.Steam;

namespace wsteam;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("A CLI tool installing Steam games with manifests.");

        Option<string> manifestApiKeyOption = new("--manifestApiKey")
        {
            Description = "ManifestHub API key",
            Required = true,
        };
        Option<uint> appOption = new("--appId")
        {
            Description = "The app to install",
            Required = true,
        };
        Option<string> filteredOsOption = new("--os")
        {
            Description = "The operation system to filter",
            DefaultValueFactory = (_) => "windows"
        };
        Option<uint[]> filteredDepotsOption = new("--depots")
        {
            Description = "The depots to filter",
            DefaultValueFactory = (_) => []
        };

        Command installCommand = new("install", "Install a game.")
        {
            manifestApiKeyOption,
            appOption,
            filteredOsOption,
            filteredDepotsOption,
        };
        rootCommand.Subcommands.Add(installCommand);

        var parseResult = rootCommand.Parse(args);

        var services = new ServiceCollection();
        services.AddMemoryCache();

        services.AddHttpClient<ManifestHubApi>();
        services.AddHttpClient<MorrenusManifestApi>();
        services.AddHttpClient<GithubApi>();

        services.AddSingleton<ManifestHubApi>();
        services.AddSingleton<MorrenusManifestApi>();
        services.AddSingleton<GithubApi>();

        services.AddSingleton<ILuaApi, SteamManifestApi>();
        services.AddSingleton<IManifestApi, ManifestHubApi>();

        services.AddSingleton<IDepotKeySource>
            (sp => sp.GetRequiredService<GithubApi>());
        services.AddSingleton<IDepotKeySource>
            (sp => new LuaKeySource(sp.GetRequiredService<ILuaApi>()));
        services.AddSingleton(sp =>
            new DepotKeyProvider([.. sp.GetServices<IDepotKeySource>()]));

        services.AddSingleton<SteamPicsClient>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<SteamSession>();
        var provider = services.BuildServiceProvider();

        installCommand.SetAction(async parseResult =>
        {
            var steamDirectory = GetSteamDirectory()
                ?? throw new InvalidOperationException("Steam directory not found.");
            var gameDirectory = Path.Combine(steamDirectory, "steamapps", "common");

            Environment.SetEnvironmentVariable("MANIFEST_API_KEY", parseResult.GetValue(manifestApiKeyOption));

            await provider.GetRequiredService<DownloadManager>().DownloadAppAsync(
                parseResult.GetValue(appOption),
                gameDirectory,
                parseResult.GetValue(filteredOsOption)!,
                parseResult.GetValue(filteredDepotsOption)
            );
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static string? GetSteamDirectory()
    {
        if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string[] paths =
            [
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam")
            ];

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }

        return Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "InstallPath",
            @"C:\Program Files (x86)\Steam"
        ) as string;
    }
}
