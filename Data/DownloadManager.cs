using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Data.APIs;
using wsteam.Models.SteamCMD;

namespace wsteam.Data;

public class DownloadManager
{
    private SteamClient steamClient;
    private SteamUser steamUser;
    private SteamContent steamContent;

    private ManifestHubApi manifestApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;

    public DownloadManager(ManifestHubApi manifestApi, SteamCMDApi steamCMDApi, DepotKeyProvider depotKeyProvider)
    {
        this.manifestApi = manifestApi;
        this.steamCMDApi = steamCMDApi;
        this.depotKeyProvider = depotKeyProvider;

        this.steamClient = new SteamClient();
        steamClient.Connect();

        this.steamUser = this.steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.steamContent = this.steamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
    }

    public async Task DownloadAppAsync(uint appId, string path)
    {
        var game = await steamCMDApi.GetInfoAsync(appId);
        Console.WriteLine($"downloading {game.config.installdir}");
        Console.WriteLine($"found {game.depots.DepotObjects.Count()} manifests");

        var gameDirectory = Path.Combine(path, game.config.installdir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServer = (await steamContent.GetServersForSteamPipe())
            .ToList()
            .First();

        using var cdnClient = new Client(steamClient);

        var depots = game.depots.DepotObjects.ToList();
        Console.WriteLine("");

        var manifestTasks = depots.Select(async d =>
        {
            var depotId = uint.Parse(d.Key);
            var manifest = await DownloadManifestAsync(d.Value, depotId);
            if (manifest is null) return (KeyValuePair<uint, DepotManifest>?)null;

            return new KeyValuePair<uint, DepotManifest>(depotId, manifest);
        });

        var manifests = (await Task.WhenAll(manifestTasks))
            .Where(m => m is not null)
            .Cast<KeyValuePair<uint, DepotManifest>?>()
            .ToList();

        manifests.ForEach(async m =>
        {
            if (m is null) return;

            var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, (int)m.Value.Value.DepotID);
            var byteDepotKey = DepotKeyDecryptor.HexStringToBytes(depotKey);

            if (m is null || m.Value.Value.Files is null) return;
            Console.WriteLine($"downloading manifest {m.Value.Value.ManifestGID}");

            m.Value.Value.Files.ForEach(f =>
            {
                var decryptedFileName = DepotKeyDecryptor.DecryptFilename(f.FileName, byteDepotKey);

                using var writer = new FileWriter(Path.Combine(gameDirectory, f.FileName));
                Console.WriteLine($"[manifest {m.Value.Value.ManifestGID}] downloading file {f.FileName}");

                f.Chunks.ForEach(async c =>
                {
                    Console.WriteLine($"[file {f.FileName}] downloading chunk {c.ChunkID}");

                    var length = checked((int)c.UncompressedLength);
                    byte[] buffer = new byte[length];

                    var bytes = await cdnClient.DownloadDepotChunkAsync(m.Value.Key, c, cdnServer, buffer);
                    Console.WriteLine($"[chunk {c.ChunkID}] downloaded {bytes}b");

                    await writer.WriteChunkAsync(c, buffer);
                    await writer.FlushAsync();
                });

            });
        });
    }

    private async Task<DepotManifest?> DownloadManifestAsync(SteamDepot depot, uint depotId)
    {
        if (depot.manifests is null) return null;

        return await manifestApi.GetManifestAsync(depotId, ulong.Parse(depot.manifests.@public.gid));
    }
}
