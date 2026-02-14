using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Models.Steam;
using static SteamKit2.DepotManifest;

namespace wsteam.Data;

public class DownloadManager
{
    private SteamClient steamClient;
    private SteamUser steamUser;
    private SteamContent steamContent;

    private ManifestHubApi manifestApi;
    private SteamCMDApi steamCMDApi;

    public DownloadManager(ManifestHubApi manifestApi, SteamCMDApi steamCMDApi)
    {
        this.manifestApi = manifestApi;
        this.steamCMDApi = steamCMDApi;

        this.steamClient = new SteamClient();
        this.steamUser = this.steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.steamContent = this.steamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
    }

    public async Task DownloadAppAsync(uint appId)
    {
        DepotManifest[] manifests = [];

        var game = await steamCMDApi.GetInfoAsync(appId);
        Console.WriteLine(game.depots.DepotObjects.Count());

        game.depots.DepotObjects.ToList().ForEach(async d =>
        {
            if (d.Value.manifests is null) return;

            var depotId = int.Parse(d.Key);
            var manifestId = ulong.Parse(d.Value.manifests.@public.gid);
            Console.WriteLine($"depotId: {depotId} manifestId: {manifestId}");

            var manifest = await manifestApi.GetManifestAsync(depotId, manifestId);
            Console.WriteLine($"received manifest with depotid {manifest.DepotID}");
        });
    }

    private async Task DownloadDepotsAsync(IEnumerable<DepotData> depots)
    {
        depots.AsParallel().ForAll(async depot =>
        {
            // await DownloadDepotAsync(depot.DepotId);
        });
    }

    private async Task<DepotManifest> DownloadManifestAsync(int depotId)
    {
        return null;
    }

    private async Task DownloadDepotAsync(DepotManifest manifest, Server cdnServer, Client cdnClient)
    {
        manifest.Files.AsParallel().ForAll(async f => await DownloadDepotChunksAsync(f.Chunks, cdnServer, cdnClient));
    }

    private async Task DownloadDepotChunksAsync(List<ChunkData> chunks, Server cdnServer, Client cdnClient)
    {
        // chunks.AsParallel().ForAll(c => cdnClient.DownloadDepotChunkAsync());
    }
}
