using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Data.Manifests;
using wsteam.Data.Steam;

namespace wsteam.Data.Downloads;

public class DownloadManager(
    SteamSession steamSession,
    IManifestApi manifestApi,
    SteamPicsClient picsClient,
    DepotKeyProvider depotKeyProvider
)
{
    private readonly SteamSession steamSession = steamSession;
    private readonly IManifestApi manifestApi = manifestApi;
    private readonly SteamPicsClient picsClient = picsClient;
    private readonly DepotKeyProvider depotKeyProvider = depotKeyProvider;

    private readonly System.Timers.Timer speedTimer = new();

    private string? currentFileName;
    private int byteAccumulator = 0;
    private float appSize = 0;
    private int retryCount = 0;

    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    static readonly int[] Redistributables = [
        228983, // VC 2010 Redist
        228990 // DirectX Jun 2010 Redist
    ];

    internal class ManifestWrapper
    {
        public required DepotManifest Manifest;
        public byte[]? DepotKey;
        public required uint DepotId;
    }

    private byte[] RentBuffer(int length)
        => ArrayPool<byte>.Shared.Rent(length);

    private void ReturnBuffer(byte[] buffer)
        => ArrayPool<byte>.Shared.Return(buffer);

    public async Task DownloadAppAsync(uint appId, string path)
    {
        await steamSession.WaitLoggedOnAsync();

        var game = await picsClient.GetAppInfoAsync(appId);

        Console.WriteLine($"Found app {game.AppId}");

        var depots = game.Depots.Depots.ToList()
            .Where(d => !Redistributables.Contains(int.Parse(d.Key)))
            .Where(d => (d.Value.Config.OsList ?? "windows").Contains("windows"))
            .Where(d => (d.Value.Config.Language ?? "english") == "english")
            .ToList();

        depots.ForEach(d => Console.WriteLine($"[{d.Key}] dlc:{d.Value.Dlcappid.ToString() ?? "null"} os:{d.Value.Config.OsList}"));

        Console.WriteLine($"downloading game {game.Config.InstallDir}");
        Console.WriteLine($"found {depots.Count()} depots");

        var gameDirectory = Path.Combine(path, game.Config.InstallDir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServer = (await steamSession.SteamContent.GetServersForSteamPipe())
            .ToList()
            .First();

        using var cdnClient = new Client(steamSession.SteamClient);

        Console.WriteLine($"using cdn {cdnServer.Host}");

        var manifestDownloadTasks = depots.Select(async depot =>
        {
            var publicManifest = depot.Value.Manifests["public"];
            if (publicManifest is null) return null;

            var depotId = uint.Parse(depot.Key);
            var manifestId = ulong.Parse(publicManifest.Gid);

            var manifest = await manifestApi.GetManifestAsync(appId, depotId, manifestId);
            if (manifest is null) return null;

            var wrappedManifest = new ManifestWrapper
            {
                DepotId = depotId,
                Manifest = manifest,
            };

            if (manifest.FilenamesEncrypted)
            {
                var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, depotId);
                if (depotKey is null)
                {
                    Console.WriteLine($"no depot key for depot {depot.Key}");
                    return null;
                }

                var result = manifest.DecryptFilenames(depotKey);
                if (!result)
                {
                    Console.WriteLine($"failed to decrypt filenames for depot {depot.Key}");
                    return null;
                }

                wrappedManifest.DepotKey = depotKey;
            }

            Console.WriteLine($"successfully downloaded depot {depot.Key}");
            return wrappedManifest;
        });

        var manifestFileResults = await Task.WhenAll(manifestDownloadTasks);
        var manifestFiles = manifestFileResults
            .Where(m => m != null)
            .ToList()
            .Cast<ManifestWrapper>();

        if (manifestFiles.Any())
        {
            speedTimer.Interval = 1000;
            speedTimer.Elapsed += (sender, e) =>
            {
                Console.WriteLine($"current file: {currentFileName}");
                Console.WriteLine($"speed: {byteAccumulator / 1024.0 / 1024.0:F2} MB/s");
                Console.WriteLine($"progress: {byteAccumulator / (float)appSize * 100:F2}%");
                byteAccumulator = 0;
            };
            speedTimer.Start();

            Console.WriteLine($"downloading {manifestFiles.Count()} manifests from {cdnServer.Host}");
            await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServer);
        }

        Console.WriteLine("Download finished!");
    }

    private async Task DownloadManifestsAsync(IEnumerable<ManifestWrapper> manifestFiles, string gameDirectory, uint appId, Client cdnClient, Server cdnServer)
    {
        foreach (var manifestInfo in manifestFiles)
        {
            var manifest = manifestInfo.Manifest;
            var depotName = $"depot {manifest.DepotID}";

            if (manifest.Files is null) continue;

            appSize = manifest.Files.Sum(m => (float)m.TotalSize);

            Console.WriteLine($"[app {appId}] downloading {depotName} of {manifest.TotalUncompressedSize}");

            await Parallel.ForEachAsync(
                manifest.Files,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (file, ct) => await DownloadFileAsync(file, manifestInfo.DepotId, manifestInfo.DepotKey, depotName, gameDirectory, cdnClient, cdnServer)
            );
        }
    }

    private async Task DownloadFileAsync(DepotManifest.FileData file, uint depotId, byte[]? depotKey, string depotName, string gameDirectory, Client cdnClient, Server cdnServer)
    {
        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(gameDirectory, file.FileName);

        currentFileName = fileName;

        if (File.Exists(filePath))
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileHash = await SHA1.HashDataAsync(fileStream);

                if (file.FileHash.SequenceEqual(fileHash))
                {
                    Console.WriteLine($"[file {fileName}] file verified");
                    return;
                }
                else
                {
                    Console.WriteLine($"[file {fileName}] hash mismatch, re-downloading");
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[file {fileName}] verification failed: {ex.Message}, re-downloading");
                try { File.Delete(filePath); } catch { }
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

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, byte[]? depotKey, ChunkedFileWriter writer, Client cdnClient, Server cdnServer)
    {
        try
        {
            retryCount = 0;

            var length = depotKey is null
                ? checked((int)chunk.CompressedLength)
                : checked((int)chunk.UncompressedLength);

            byte[] buffer = RentBuffer(length);
            try
            {
                var bytes = await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer, depotKey);
                await writer.WriteChunkAsync(chunk, buffer);

                Interlocked.Add(ref byteAccumulator, bytes);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }
        catch (Exception ex)
        {
            if (retryCount >= MaxRetries)
            {
                Console.WriteLine($"[file {currentFileName}] error downloading chunk after {MaxRetries} retries: {ex}");
                throw;
            }

            Console.WriteLine($"[file {currentFileName}] error downloading chunk: {ex.Message}, retrying in {RetryDelayMs}ms (attempt {retryCount + 1}/{MaxRetries})");

            await Task.Delay(RetryDelayMs);
            retryCount += 1;

            await DownloadChunkAsync(chunk, depotId, depotKey, writer, cdnClient, cdnServer);
        }
    }
}
