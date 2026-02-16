using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Data.APIs;

namespace wsteam.Data.Download;

public class DownloadManager
{
    private SteamSession steamSession;
    private IManifestApi manifestApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;

    public DownloadManager(SteamSession steamSession, IManifestApi manifestApi, SteamCMDApi steamCMDApi, DepotKeyProvider depotKeyProvider)
    {
        this.steamSession = steamSession;
        this.manifestApi = manifestApi;
        this.steamCMDApi = steamCMDApi;
        this.depotKeyProvider = depotKeyProvider;
    }

    private class ManifestWrapper
    {
        public required DepotManifest Manifest;
        public required uint DepotId;
    }

    public async Task DownloadAppAsync(uint appId, string path)
    {
        await steamSession.WaitLoggedOnAsync();

        var game = await steamCMDApi.GetInfoAsync(appId);
        var depots = game.depots.DepotObjects.ToList();

        Console.WriteLine($"downloading game {game.config.installdir}");
        Console.WriteLine($"found {depots.Count()} depots");

        var gameDirectory = Path.Combine(path, game.config.installdir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServer = (await steamSession.SteamContent.GetServersForSteamPipe())
            .ToList()
            .First();

        using var cdnClient = new Client(steamSession.SteamClient);

        Console.WriteLine($"connecting to cdn {cdnServer.Host}");

        var manifestDownloadTasks = depots.Select(async depot =>
        {
            if (depot.Value.manifests is null) return null;

            var depotId = uint.Parse(depot.Key);
            var manifestId = ulong.Parse(depot.Value.manifests.@public.gid);

            var manifest = await manifestApi.GetManifestAsync(depotId, manifestId);
            if (manifest is null) return null;

            return new ManifestWrapper
            {
                DepotId = depotId,
                Manifest = manifest,
            };
        });

        var manifestFileResults = await Task.WhenAll(manifestDownloadTasks);
        var manifestFiles = manifestFileResults
            .Where(m => m != null)
            .ToList()
            .Cast<ManifestWrapper>();

        Console.WriteLine($"downloading {manifestFiles.Count()} manifests from {cdnServer.Host}");
        await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServer);

        Console.WriteLine("Download finished!");
    }

    private async Task DownloadManifestsAsync(IEnumerable<ManifestWrapper> manifestFiles, string gameDirectory, uint appId, Client cdnClient, Server cdnServer)
    {
        foreach (var manifestInfo in manifestFiles)
        {
            var depotId = manifestInfo.DepotId;
            var manifest = manifestInfo.Manifest;
            var depotName = $"depot {manifest.DepotID}";

            var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, depotId);
            if (depotKey is null) continue;
            if (manifest.Files is null) continue;

            if (manifest.FilenamesEncrypted) manifest.DecryptFilenames(depotKey);

            Console.WriteLine($"[app {appId}] downloading {depotName} of {manifest.TotalUncompressedSize}");

            await Parallel.ForEachAsync(
                manifest.Files,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (file, ct) => await DownloadFileAsync(file, depotId, depotKey, depotName, gameDirectory, cdnClient, cdnServer)
            );
        }
    }

    private async Task DownloadFileAsync(DepotManifest.FileData file, uint depotId, byte[] depotKey, string depotName, string gameDirectory, Client cdnClient, Server cdnServer)
    {
        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(gameDirectory, file.FileName);

        if (File.Exists(filePath))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            var fileHash = await SHA1.HashDataAsync(fileStream);

            if (Enumerable.SequenceEqual(file.FileHash, fileHash))
            {
                Console.WriteLine($"[file {fileName}] file verified");
                return;
            }
        }

        var chunkCount = file.Chunks.Count();
        if (chunkCount == 0)
        {
            Console.WriteLine($"[{depotName}] creating directory {file.FileName}");
            Directory.CreateDirectory(filePath);
            return;
        }

        Console.WriteLine($"[file {fileName}] downloading file; {chunkCount} chunks, {file.TotalSize} bytes");

        try
        {
            using var writer = new ChunkedFileWriter(filePath);

            await Parallel.ForEachAsync(file.Chunks, async (chunk, ct) =>
                await DownloadChunkAsync(chunk, depotId, depotKey, writer, cdnClient, cdnServer));

            writer.Flush();
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }
    }

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, byte[] depotKey, ChunkedFileWriter writer, Client cdnClient, Server cdnServer)
    {
        var length = checked((int)chunk.UncompressedLength);
        byte[] buffer = new byte[length];

        var bytes = await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer, depotKey);
        await writer.WriteChunkAsync(chunk, buffer);

        Console.WriteLine($"[depot {depotId}] written {bytes} bytes");
    }
}
