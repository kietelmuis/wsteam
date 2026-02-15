using System.IO;
using System.Threading.Tasks;
using wsteam.Data.APIs;
using wsteam.Data.Download;

public partial class MainViewModel : ViewModelBase
{
    private IManifestApi manifestApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;

    private readonly DownloadManager downloadManager;

    private readonly string downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

    public bool IsGamesSelected { get; set; }
    public bool IsSourcesSelected { get; set; }
    public bool IsSettingsSelected { get; set; }

    public MainViewModel(IManifestApi manifestApi, SteamCMDApi steamCMDApi, DepotKeyProvider depotKeyProvider)
    {
        this.manifestApi = manifestApi;
        this.steamCMDApi = steamCMDApi;
        this.depotKeyProvider = depotKeyProvider;

        this.downloadManager = new(manifestApi, steamCMDApi, depotKeyProvider);
    }

    public Task DownloadAsync(uint appId)
        => downloadManager.DownloadAppAsync(appId, downloadDirectory);
}
