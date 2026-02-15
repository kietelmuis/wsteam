using System.IO;
using System.Threading.Tasks;
using wsteam.Data;
using wsteam.Data.APIs;

public partial class MainViewModel : ViewModelBase
{
    private ManifestHubApi manifestHubApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;

    private readonly DownloadManager downloadManager;

    private readonly string downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

    public bool IsGamesSelected { get; set; }
    public bool IsSourcesSelected { get; set; }
    public bool IsSettingsSelected { get; set; }

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
