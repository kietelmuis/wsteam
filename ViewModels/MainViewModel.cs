using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using wsteam.Data;
using wsteam.Data.APIs;

public partial class MainViewModel
{
    private ManifestHubApi manifestHubApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;

    private readonly DownloadManager downloadManager;

    private readonly string downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

    public MainViewModel(ManifestHubApi manifestHubApi, SteamCMDApi steamCMDApi, DepotKeyProvider depotKeyProvider)
    {
        this.manifestHubApi = manifestHubApi;
        this.steamCMDApi = steamCMDApi;
        this.depotKeyProvider = depotKeyProvider;

        this.downloadManager = new(manifestHubApi, steamCMDApi, depotKeyProvider);
    }

    public Task DownloadAsync(uint appId)
        => downloadManager.DownloadAppAsync(appId, downloadDirectory);
}
