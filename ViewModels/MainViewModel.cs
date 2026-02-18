using System.IO;
using System.Threading.Tasks;
using wsteam.Data.Downloads;
using wsteam.Data.Manifests;
using wsteam.Data.Steam;

public partial class MainViewModel(
    SteamSession steamSession,
    IManifestApi manifestApi,
    SteamPicsClient picsClient,
    DepotKeyProvider depotKeyProvider) : ViewModelBase
{
    private readonly DownloadManager downloadManager =
        new(steamSession, manifestApi, picsClient, depotKeyProvider);

    private readonly string downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

    public bool IsGamesSelected { get; set; }
    public bool IsSourcesSelected { get; set; }
    public bool IsSettingsSelected { get; set; }

    public Task DownloadAsync(uint appId)
        => downloadManager.DownloadAppAsync(appId, downloadDirectory);
}
