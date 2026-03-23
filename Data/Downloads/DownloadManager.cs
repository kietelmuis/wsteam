using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Data.Manifests;
using wsteam.Data.Steam;
using wsteam.Models.Steam;

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

    public readonly System.Timers.Timer SpeedTimer = new();

    private SteamApp? currentApp;
    private string? currentFileName;

    private ulong totalAccumulator = 0;
    private ulong byteAccumulator = 0;
    private ulong appSize = 0;

    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    static readonly int[] Redistributables = [
        228983, // VC 2010 Redist
        228990  // DirectX Jun 2010 Redist
    ];

    internal class ManifestWrapper
    {
        public required DepotManifest Manifest;
        public required byte[] DepotKey;
        public required uint DepotId;
    }

    private byte[] RentBuffer(int length)
        => ArrayPool<byte>.Shared.Rent(length);

    private void ReturnBuffer(byte[] buffer)
        => ArrayPool<byte>.Shared.Return(buffer);

    public string? GetDownloadFileName() =>
        currentFileName;

    public SteamApp? GetCurrentApp() =>
        currentApp;

    public ulong GetDownloadPercentage()
    {
        if (appSize is 0) return 0;
        var percentage = (double)totalAccumulator / appSize * 100;
        return (ulong)percentage;
    }

    public string GetDownloadSpeed()
    {
        var speedMbps = byteAccumulator / 1024.0 / 1024.0;
        return $"{speedMbps:F2} MB/s";
    }

    public async Task DownloadAppAsync(uint appId, string path, string os, uint[]? excludedDepots = null)
    {
        await steamSession.WaitLoggedOnAsync();

        var game = await picsClient.GetAppInfoAsync(appId);
        currentApp = game;

        Console.WriteLine($"found app {game.AppId}");

        var depots = game.Depots.Depots.ToList()
            .Where(d => !Redistributables.Contains(int.Parse(d.Key)))
            .Where(d => (d.Value?.Config?.OsList ?? os).Contains(os))
            .Where(d => (d.Value?.Config?.Language ?? "english") == "english")
            .Where(d => excludedDepots is null || !excludedDepots.Contains(uint.Parse(d.Key)))
            .ToList();

        depots.ForEach(d => Console.WriteLine($"[{d.Key}] dlc:{d.Value.DlcAppId?.ToString() ?? "null"} os:{d.Value.Config?.OsList}"));

        Console.WriteLine($"downloading game {game.Config.InstallDir}");
        Console.WriteLine($"found {depots.Count} depots");

        var gameDirectory = Path.Combine(path, game.Config.InstallDir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServers = (await steamSession.SteamContent.GetServersForSteamPipe()).ToList();
        using var cdnClient = new Client(steamSession.SteamClient);

        Console.WriteLine($"using cdn {cdnServers.First().Host} (+{cdnServers.Count()} others)");

        var manifestDownloadTasks = depots.Select(async depot =>
        {
            if (depot.Value.Manifests is null) return null;

            var publicManifest = depot.Value.Manifests["public"];
            if (publicManifest is null) return null;

            var depotId = uint.Parse(depot.Key);
            var manifestId = ulong.Parse(publicManifest.Gid);

            var manifest = await manifestApi.GetManifestAsync(appId, depotId, manifestId);
            if (manifest is null) return null;

            var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, depotId);
            if (depotKey is null)
            {
                Console.WriteLine($"[manifest] no depot key for depot {depot.Key}");
                return null;
            }

            var wrappedManifest = new ManifestWrapper
            {
                DepotId = depotId,
                Manifest = manifest,
                DepotKey = depotKey,
            };

            var result = manifest.DecryptFilenames(depotKey);
            if (!result)
            {
                Console.WriteLine($"[manifest] failed to decrypt filenames for depot {depot.Key}");
                return null;
            }

            Console.WriteLine($"[manifest] depotkey={wrappedManifest.DepotKey?.Length.ToString() ?? "null"}, isEncrypted={manifest.FilenamesEncrypted}");
            Console.WriteLine($"[manifest] successfully downloaded manifest for depot {manifest.DepotID}");
            return wrappedManifest;
        });

        var manifestFileResults = await Task.WhenAll(manifestDownloadTasks);
        var manifestFiles = manifestFileResults
            .Where(m => m != null)
            .Cast<ManifestWrapper>()
            .ToList();

        appSize = (ulong)manifestFiles.Sum(m => (long)m.Manifest.TotalUncompressedSize);

        var manifestCount = manifestFiles.Count;
        if (manifestCount > 0)
        {
            SpeedTimer.Interval = 1000;
            SpeedTimer.Elapsed += (sender, e) =>
            {
                var progressPercent = GetDownloadPercentage();
                var speedMbps = GetDownloadSpeed();

                Console.WriteLine($"[dl] {progressPercent:F1}% | {speedMbps}");
                byteAccumulator = 0;
            };
            SpeedTimer.Start();

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"downloading {manifestCount} manifests from {cdnServers[0].Host}");
            await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServers);

            Console.WriteLine($"downloaded {manifestCount} depots");
            Console.WriteLine($"download finished in {sw.Elapsed}!");
        }

        SpeedTimer.Dispose();
        currentApp = null;
    }

    private async Task DownloadManifestsAsync(IEnumerable<ManifestWrapper> manifestFiles, string gameDirectory, uint appId, Client cdnClient, List<Server> cdnServers)
    {
        foreach (var manifestInfo in manifestFiles)
        {
            var manifest = manifestInfo.Manifest;
            var depotName = $"depot {manifest.DepotID}";

            if (manifest.Files is null) continue;

            var allFiles = manifest.Files
                .Where(f => f.Chunks.Any())
                .ToList();

            foreach (var f in allFiles)
            {
                var filePath = Path.Combine(gameDirectory, f.FileName.Replace('\\', '/'));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                if (!File.Exists(filePath))
                {
                    using var fs = new FileStream(filePath, FileMode.Create);
                    fs.SetLength((long)f.TotalSize);
                }
            }

            var allChunks = allFiles.SelectMany(f => f.Chunks.Select(c => (
                filePath: Path.Combine(gameDirectory, f.FileName.Replace('\\', '/')),
                chunk: c
            )));

            Console.WriteLine($"[app {appId}] downloading {depotName} of {ByteSizeFormatter.FormatBytes(manifest.TotalUncompressedSize)}");

            var writers = new ConcurrentDictionary<string, ChunkedFileWriter>();
            try
            {
                await Parallel.ForEachAsync(
                    allChunks,
                    new ParallelOptions { MaxDegreeOfParallelism = 6 },
                    async (item, ct) =>
                    {
                        var writer = writers.GetOrAdd(item.filePath, p => new ChunkedFileWriter(p));
                        await DownloadChunkAsync(item.chunk, manifestInfo.DepotId, manifestInfo.DepotKey, writer, cdnClient, cdnServers.First());
                    }
                );
            }
            finally
            {
                foreach (var writer in writers.Values)
                    writer.Dispose();
                writers.Clear();
            }

            foreach (var f in allFiles)
            {
                var filePath = Path.Combine(gameDirectory, f.FileName.Replace('\\', '/'));
                var actual = new FileInfo(filePath).Length;
                if (actual != (long)f.TotalSize)
                    Console.WriteLine($"[verify] MISMATCH {f.FileName}: expected={f.TotalSize}, actual={actual}");
            }
        }
    }

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, byte[]? depotKey, ChunkedFileWriter writer, Client cdnClient, Server cdnServer, int retryCount = 0)
    {
        try
        {
            var length = checked((int)Math.Max(chunk.CompressedLength, chunk.UncompressedLength));

            byte[] buffer = RentBuffer(length);
            try
            {
                await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer, depotKey);
                await writer.WriteChunkAsync(chunk, buffer.AsMemory(0, (int)chunk.UncompressedLength));

                Interlocked.Add(ref byteAccumulator, chunk.UncompressedLength);
                Interlocked.Add(ref totalAccumulator, chunk.UncompressedLength);
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
            await DownloadChunkAsync(chunk, depotId, depotKey, writer, cdnClient, cdnServer, retryCount + 1);
        }
    }
}
