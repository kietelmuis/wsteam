using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using wsteam.Core.Common;
using wsteam.Core.DepotKey;
using wsteam.Core.Downloads;
using wsteam.Core.Manifests;
using wsteam.Core.Steam;

namespace wsteam;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        var rootCommand = new RootCommand("A CLI tool installing Steam games with manifests.");

        var targetArg = new Argument<string>("target")
        {
            Description = "Steam app query (e.g. \"STEINS;GATE\")."
        };

        Option<ManifestSource> sourceOption = new("--source", "-s")
        {
            Description = "The manifest source (manifesthub or morrenus).",
            DefaultValueFactory = _ => ManifestSource.ManifestHub,
        };
        Option<string> locationOption = new("--location", ["-l"])
        {
            Description = "Custom location, default is Steam directory",
        };

        Option<string> apiKeyOption = new("--key", "-k")
        {
            Description = "The API key for the selected source. If omitted, the corresponding environment variable will be used.",
            Required = true,
        };

        apiKeyOption.Validators.Add(result =>
        {
            var value = result.GetValue(apiKeyOption);
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("API key must be valid");
                return;
            }

            switch (result.GetValue(sourceOption))
            {
                case ManifestSource.ManifestHub:
                    if (value.Length != 64)
                        result.AddError("ManifestHub's API key should be 64 characters");
                    break;
                case ManifestSource.Hubcap:
                    if (!value.StartsWith("smm"))
                        result.AddError("Hubcap's API key should start with smm");
                    break;
            }
        });

        Option<SteamOperatingSystem> osOption = new("--os")
        {
            Description = "The operation system to filter",
            DefaultValueFactory = (_) => SteamOperatingSystem.Windows
        };

        Option<uint[]> depotsOption = new("--depots")
        {
            Description = "The depots to filter",
            DefaultValueFactory = (_) => []
        };

        Command installCommand = new("install", "Install a game.")
        {
            sourceOption,
            apiKeyOption,
            locationOption,
            targetArg,
            osOption,
            depotsOption
        };
        rootCommand.Subcommands.Add(installCommand);

        // services.AddMemoryCache();

        services.AddHttpClient<ManifestHubApi>();
        services.AddHttpClient<HubcapManifestApi>();
        services.AddHttpClient<ToolsSiteApi>();

        services.AddSingleton<ToolsSiteApi>();
        services.AddSingleton<HubcapManifestApi>();
        services.AddSingleton<ManifestHubApi>();
        services.AddSingleton<LuaKeySource>();
        services.AddSingleton<IDepotKeySource>(sp => sp.GetRequiredService<ManifestHubApi>());
        services.AddSingleton<IDepotKeySource>(sp => sp.GetRequiredService<LuaKeySource>());
        services.AddSingleton<ApiKeyHolder>();

        services.AddSingleton(sp =>
            new DepotKeyProvider([.. sp.GetServices<IDepotKeySource>()]));

        services.AddSingleton<SteamPicsClient>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<SLSSteamApi>();
        services.AddSingleton<SteamSession>();
        var provider = services.BuildServiceProvider();

        installCommand.SetAction(async parseResult =>
        {
            var folder = parseResult.GetValue(locationOption);
            var gameDirectory = folder is null ? Path.Combine(
                GetSteamDirectory()
                    ?? throw new InvalidOperationException("Steam directory not found."),
                "steamapps",
                "common"
            ) : folder;

            var source = parseResult.GetValue(sourceOption);
            var userProvidedKey = parseResult.GetValue(apiKeyOption);

            if (!string.IsNullOrWhiteSpace(userProvidedKey))
            {
                var apiKeyHolder = provider.GetRequiredService<ApiKeyHolder>();
                apiKeyHolder.AddApiKey(source, userProvidedKey);

                Console.WriteLine(source);
                Console.WriteLine(userProvidedKey);

                switch (source)
                {
                    case ManifestSource.Hubcap:
                        Environment.SetEnvironmentVariable("HUBCAP_API_KEY", userProvidedKey);
                        break;
                    case ManifestSource.ManifestHub:
                        Environment.SetEnvironmentVariable("MANIFEST_API_KEY", userProvidedKey);
                        break;
                }
            }

            var toolSiteApi = provider.GetRequiredService<ToolsSiteApi>();
            var queryResults = await toolSiteApi.GetAppResultsAsync(parseResult.GetValue(targetArg)!);
            var game = queryResults.FirstOrDefault()
                ?? throw new InvalidOperationException("Game not found");

            IManifestApi manifestApi = source switch
            {
                ManifestSource.Hubcap => provider.GetRequiredService<HubcapManifestApi>(),
                _ => provider.GetRequiredService<ManifestHubApi>()
            };

            await provider.GetRequiredService<DownloadManager>().DownloadAppAsync(
                game.Id,
                gameDirectory,
                parseResult.GetValue(osOption),
                manifestApi,
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
