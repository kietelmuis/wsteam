using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        var depots = game.depots.DepotObjects.ToList();

        Console.WriteLine($"downloading {game.config.installdir}");
        Console.WriteLine($"found {depots.Count()} depots");

        var gameDirectory = Path.Combine(path, game.config.installdir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServer = (await steamContent.GetServersForSteamPipe())
            .ToList()
            .First();

        using var cdnClient = new Client(steamClient);

        Console.WriteLine($"connecting to cdn {cdnServer.Host}");

        var manifestTasks = depots.Select(async d =>
        {
            var depotId = uint.Parse(d.Key);
            var manifest = await DownloadManifestAsync(d.Value, depotId);
            if (manifest is null) return (KeyValuePair<uint, DepotManifest>?)null;

            return new KeyValuePair<uint, DepotManifest>(depotId, manifest);
        });

        var manifestResults = await Task.WhenAll(manifestTasks);
        var manifests = manifestResults
            .Where(m => m.HasValue)
            .Select(m => m!.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Console.WriteLine($"downloading {manifests.Count()} manifests from {cdnServer.Host}");
        await DownloadManifestsAsync(manifests, gameDirectory, appId, cdnClient, cdnServer);
    }

    private async Task DownloadManifestsAsync(Dictionary<uint, DepotManifest> manifests, string gameDirectory, uint appId, Client cdnClient, Server cdnServer)
    {
        manifests.ToList().ForEach(async m =>
        {
            var depotId = m.Key;
            var manifest = m.Value;

            var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, depotId);
            if (depotKey is null) return;

            manifest.DecryptFilenames(depotKey);

            if (manifest.Files is null) return;
            Console.WriteLine($"[app {appId}] downloading manifest {manifest.ManifestGID}");

            manifest.Files.ForEach(async file =>
                await DownloadFileAsync(file, appId, gameDirectory, cdnClient, cdnServer));
        });
    }

    private async Task DownloadFileAsync(DepotManifest.FileData file, uint depotId, string gameDirectory, Client cdnClient, Server cdnServer)
    {
        var fileName = Path.GetFileName(file.FileName);

        using var writer = new FileWriter(Path.Combine(gameDirectory, file.FileName));
        Console.WriteLine($"[depot {depotId}] downloading file {fileName}, {file.Chunks.Count()} chunks");

        file.Chunks.ForEach(async chunk =>
            await DownloadChunkAsync(chunk, depotId, writer, cdnClient, cdnServer));
    }

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, FileWriter writer, Client cdnClient, Server cdnServer)
    {
        var chunkId = Convert.ToHexStringLower(SHA256.HashData(chunk.ChunkID ?? []));

        var length = checked((int)chunk.UncompressedLength);
        byte[] buffer = new byte[length];

        var bytes = await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer);
        Console.WriteLine($"[chunk {chunkId}] downloaded {bytes}b");

        await writer.WriteChunkAsync(chunk, buffer);
        await writer.FlushAsync();
    }

    private async Task<DepotManifest?> DownloadManifestAsync(SteamDepot depot, uint depotId)
    {
        if (depot.manifests is null) return null;

        return await manifestApi.GetManifestAsync(depotId, ulong.Parse(depot.manifests.@public.gid));
    }
}
