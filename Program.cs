using System;
using System.CommandLine;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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

        var targetArg = new Argument<string>("target")
        {
            Description = "Steam app query (e.g. \"STEINS;GATE\")."
        };
        Option<string> manifestApiKeyOption = new("--manifestApiKey", ["-k"])
        {
            Description = "ManifestHub API key. Get it at https://manifesthub1.filegear-sg.me/",
            Required = Environment.GetEnvironmentVariable("MANIFEST_API_KEY") is null,
        };
        manifestApiKeyOption.Validators.Add(result =>
        {
            var value = result.GetValue(manifestApiKeyOption);

            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("ManifestHub API key is required. Get it at https://manifesthub1.filegear-sg.me/");
                return;
            }

            if (value.Length != 64)
            {
                result.AddError("ManifestHub API key must be 64 characters long.");
            }
        });

        Option<string> osOption = new("--os")
        {
            Description = "The operation system to filter",
            DefaultValueFactory = (_) => "windows"
        };
        Option<uint[]> depotsOption = new("--depots")
        {
            Description = "The depots to filter",
            DefaultValueFactory = (_) => []
        };

        Command installCommand = new("install", "Install a game.")
        {
            manifestApiKeyOption,
            targetArg,
            osOption,
            depotsOption
        };
        rootCommand.Subcommands.Add(installCommand);

        var parseResult = rootCommand.Parse(args);

        var services = new ServiceCollection();
        services.AddMemoryCache();

        services.AddHttpClient<ManifestHubApi>();
        services.AddHttpClient<MorrenusManifestApi>();
        services.AddHttpClient<GithubApi>();
        services.AddHttpClient<ToolsSiteApi>();

        services.AddSingleton<ToolsSiteApi>();
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
        services.AddSingleton<SLSSteamApi>();
        services.AddSingleton<SteamSession>();
        var provider = services.BuildServiceProvider();

        installCommand.SetAction(async parseResult =>
        {
            var steamDirectory = GetSteamDirectory()
                ?? throw new InvalidOperationException("Steam directory not found.");
            var gameDirectory = Path.Combine(steamDirectory, "steamapps", "common");

            var manifestApiKey = parseResult.GetValue(manifestApiKeyOption);
            if (string.IsNullOrWhiteSpace(manifestApiKey))
                Environment.SetEnvironmentVariable("MANIFEST_API_KEY", manifestApiKey);

            var toolSiteApi = provider.GetRequiredService<ToolsSiteApi>();
            var queryResults = await toolSiteApi.GetAppResultsAsync(parseResult.GetValue(targetArg)!);
            var game = queryResults.FirstOrDefault()
                ?? throw new InvalidOperationException("Game not found.");

            await provider.GetRequiredService<DownloadManager>().DownloadAppAsync(
                game.Id,
                gameDirectory,
                parseResult.GetValue(osOption)!,
                parseResult.GetValue(depotsOption)
            );

            provider.GetRequiredService<SLSSteamApi>().AddAppIdToAppList(game.Id);
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
        else if (OperatingSystem.IsWindows())
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                "InstallPath",
                @"C:\Program Files (x86)\Steam"
            ) as string;
        }

        return null;
    }
}
