using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Photino.NET;
using wsteam.Data.Downloads;
using wsteam.Data.Manifests;
using wsteam.Data.Steam;

namespace wsteam;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();

        services.AddHttpClient<KernelManifestApi>();
        services.AddHttpClient<GithubApi>();

        services.AddSingleton<ILuaApi, KernelManifestApi>();
        services.AddSingleton<IManifestApi, GithubApi>();

        services.AddSingleton<SteamPicsClient>();
        services.AddSingleton<DepotKeyProvider>();
        services.AddSingleton<DownloadManager>();
        services.AddSingleton<SteamSession>();
        var provider = services.BuildServiceProvider();

        var window = new PhotinoWindow()
            .SetTitle("Steam Downloader")
            .SetDevToolsEnabled(true)
            .SetContextMenuEnabled(true)
            .SetWidth(1200)
            .SetHeight(700)
            .SetMinWidth(900)
            .SetMinHeight(600)
            .RegisterWebMessageReceivedHandler(async (sender, message) =>
            {
                Console.WriteLine(message);
                var window = (PhotinoWindow?)sender;
                if (window is null)
                    return;

                var appId = uint.Parse(message);
                var downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

                var downloadManager = provider.GetRequiredService<DownloadManager>();
                var currentApp = downloadManager.GetCurrentApp();

                downloadManager.SpeedTimer.Elapsed += async (sender, e) =>
                {
                    var downloadSpeed = downloadManager.GetDownloadSpeed();
                    var downloadPercentage = downloadManager.GetDownloadPercentage();

                    await window.SendWebMessageAsync(JsonConvert.SerializeObject(new
                    {
                        appId = appId,
                        name = currentApp?.Config.InstallDir,
                        speed = downloadSpeed,
                        percentage = downloadPercentage,
                    }));
                };

                await downloadManager.DownloadAppAsync(appId, downloadDirectory, "windows", []);

                await window.SendWebMessageAsync(JsonConvert.SerializeObject(new
                {
                    appId = appId,
                    appName = currentApp?.Config.InstallDir,
                    speed = "Finished",
                    percentage = 100,
                }));
            })
            .Load("wwwroot/index.html");

        window.WaitForClose();
    }
}
