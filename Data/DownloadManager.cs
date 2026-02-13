using System;
using System.Collections.Generic;
using System.IO;
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

    private Client cdnClient;

    public DownloadManager()
    {
        this.steamClient = new SteamClient();
        this.steamUser = this.steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.steamContent = this.steamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
    }

    public async Task DownloadAppAsync(uint appId, DepotData[] depots)
    {
        var cdns = await steamContent.GetServersForSteamPipe();
    }

    private async Task DownloadDepotsAsync(DepotData[] depots)
    {
        depots.AsParallel().ForAll(async depot =>
        {
            await DownloadDepotAsync("manifestFile.vdf", depot);
        });
    }

    private async Task<DepotManifest> DownloadDepotAsync(string manifestFile, Server cdnServer, Client cdnClient)
    {
        var manifest = DepotManifest.LoadFromFile(manifestFile)
            ?? throw new FileNotFoundException($"Manifest file not found: {manifestFile}");

        manifest.Files.AsParallel().ForAll(async f => await DownloadDepotChunksAsync(f.Chunks, cdnServer, cdnClient));
    }

    private async Task DownloadDepotChunksAsync(List<ChunkData> chunks, Server cdnServer, Client cdnClient)
    {
        chunks.AsParallel().ForAll(c => cdnClient.DownloadDepotChunkAsync());
    }
}
