using System.IO;
using System.Threading.Tasks;
using wsteam.Data.APIs;
using wsteam.Data.Download;

public partial class MainViewModel(
    SteamSession steamSession,
    IManifestApi manifestApi,
    SteamCMDApi steamCMDApi,
    DepotKeyProvider depotKeyProvider) : ViewModelBase
{
    private readonly DownloadManager downloadManager =
        new(steamSession, manifestApi, steamCMDApi, depotKeyProvider);

    private readonly string downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "games");

    public bool IsGamesSelected { get; set; }
    public bool IsSourcesSelected { get; set; }
    public bool IsSettingsSelected { get; set; }

    public Task DownloadAsync(uint appId)
        => downloadManager.DownloadAppAsync(appId, downloadDirectory);
}
