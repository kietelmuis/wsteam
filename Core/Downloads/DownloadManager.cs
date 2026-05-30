using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using wsteam.Core.Common;
using wsteam.Core.DepotKey;
using wsteam.Core.Manifests;
using wsteam.Core.Steam;
using wsteam.Models.Steam;

namespace wsteam.Core.Downloads;

public enum SteamOperatingSystem
{
    Windows,
    Linux
}

public class DownloadManager(
    SteamSession steamSession,
    SteamPicsClient picsClient,
    DepotKeyProvider depotKeyProvider
)
{
    private const string ChunkParallelismEnv = "WSTEAM_CHUNK_PARALLELISM";

    private static int GetChunkParallelism()
    {
        var env = Environment.GetEnvironmentVariable(ChunkParallelismEnv);
        if (int.TryParse(env, out var parsed) && parsed > 0)
            return Math.Clamp(parsed, 1, 256);

        return Math.Clamp(Environment.ProcessorCount * 4, 8, 64);
    }

    private int cdnServerCounter = 0;

    private Server PickCdnServer(Server[] servers, int retryCount)
    {
        if (servers.Length == 0)
            throw new InvalidOperationException("No CDN servers available");

        var n = Interlocked.Increment(ref cdnServerCounter);
        var baseIndex = (int)((uint)n % (uint)servers.Length);
        return servers[(baseIndex + retryCount) % servers.Length];
    }

    private readonly SteamSession steamSession = steamSession;
    private readonly SteamPicsClient picsClient = picsClient;
    private readonly DepotKeyProvider depotKeyProvider = depotKeyProvider;

    public readonly System.Timers.Timer SpeedTimer = new();

    private SteamApp? currentApp;

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
        var elapsedSeconds = SpeedTimer.Interval / 1000.0;
        var speedMbps = byteAccumulator / 1024.0 / 1024.0 / elapsedSeconds;
        return $"{speedMbps:F2} MB/s";
    }

    public async Task DownloadAppAsync(uint appId, string path, SteamOperatingSystem os, IManifestApi manifestApi, uint[]? excludedDepots = null)
    {
        await steamSession.WaitLoggedOnAsync();

        var game = await picsClient.GetAppInfoAsync(appId);
        currentApp = game;

        Console.WriteLine($"[downloader] found app {game.AppId}");

        var depots = game.Depots.Depots.ToList()
            .Where(d => !Redistributables.Contains(int.Parse(d.Key)))
            .Where(d => (d.Value?.Config?.OsList ?? nameof(os)).Contains(nameof(os)))
            .Where(d => (d.Value?.Config?.Language ?? "english") == "english")
            .Where(d => excludedDepots is null || !excludedDepots.Contains(uint.Parse(d.Key)))
            .ToList();

        depots.ForEach(d => Console.WriteLine($"[{d.Key}] dlc:{d.Value.DlcAppId?.ToString() ?? "null"} os:{d.Value.Config?.OsList}"));

        Console.WriteLine($"[downloader] downloading game {game.Config.InstallDir}");
        Console.WriteLine($"[downloader] found {depots.Count} depots");

        var gameDirectory = Path.Combine(path, game.Config.InstallDir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServers = (await steamSession.SteamContent.GetServersForSteamPipe()).ToArray();
        using var cdnClient = new Client(steamSession.SteamClient);

        Console.WriteLine($"[downloader] using cdn {cdnServers.First().Host} (+{cdnServers.Count()} others)");

        var manifestDownloadTasks = depots.Select(async depot =>
        {
            if (depot.Value.Manifests is null) return null;

            var publicManifest = depot.Value.Manifests["public"];
            if (publicManifest is null) return null;

            var depotId = uint.Parse(depot.Key);
            var manifestId = ulong.Parse(publicManifest.Gid);

            var manifest = await manifestApi.GetManifestAsync(appId, depotId, manifestId);
            if (manifest is null) return null;

            var depotKey = await depotKeyProvider.GetDepotKeyAsync(appId, depotId);
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
            Console.WriteLine($"[downloader] downloading {manifestCount} manifests from {cdnServers[0].Host}");
            await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServers);

            Console.WriteLine($"[downloader] downloaded {manifestCount} depots");
            Console.WriteLine($"[downloader] download finished in {sw.Elapsed}!");
        }

        SpeedTimer.Dispose();
        currentApp = null;
    }

    private async Task DownloadManifestsAsync(IEnumerable<ManifestWrapper> manifestFiles, string gameDirectory, uint appId, Client cdnClient, Server[] cdnServers)
    {
        foreach (var manifestInfo in manifestFiles)
        {
            var manifest = manifestInfo.Manifest;
            var depotName = $"depot {manifest.DepotID}";

            if (manifest.Files is null)
                continue;

            var allFiles = manifest.Files
                .Where(f => f.Chunks.Any())
                .ToList();

            var filePaths = allFiles.ToDictionary(
                f => f,
                f => Path.Combine(gameDirectory, f.FileName.Replace('\\', Path.DirectorySeparatorChar))
            );

            foreach (var dir in filePaths.Values
                .Select(Path.GetDirectoryName)
                .Distinct())
            {
                if (dir != null)
                    Directory.CreateDirectory(dir);
            }

            var files = new HashSet<ChunkedFile>();
            var chunkJobs = new List<(ChunkedFile file, DepotManifest.ChunkData chunk)>();

            foreach (var f in allFiles)
            {
                var path = filePaths[f];

                var exists = File.Exists(path);
                if (!exists)
                {
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                    fs.SetLength((long)f.TotalSize);
                }

                var file = new ChunkedFile(path, exists);

                files.Add(file);

                foreach (var chunk in f.Chunks)
                    chunkJobs.Add((file, chunk));
            }

            Console.WriteLine(
                $"[app {appId}] downloading {depotName} of {ByteSizeFormatter.FormatBytes(manifest.TotalUncompressedSize)}"
            );

            try
            {
                var dop = GetChunkParallelism();
                Console.WriteLine($"[downloader] chunk parallelism={dop} (override via {ChunkParallelismEnv})");

                await Parallel.ForEachAsync(
                    chunkJobs,
                    new ParallelOptions { MaxDegreeOfParallelism = dop },
                    async (job, ct) =>
                    {
                        await DownloadChunkAsync(
                            job.chunk,
                            manifestInfo.DepotId,
                            manifestInfo.DepotKey,
                            job.file,
                            cdnClient,
                            cdnServers
                        );
                    });
            }
            finally
            {
                foreach (var f in files)
                    f.Dispose();
            }
        }
    }

    private async Task DownloadChunkAsync(
        DepotManifest.ChunkData chunk,
        uint depotId,
        byte[]? depotKey,
        ChunkedFile file,
        Client cdnClient,
        Server[] cdnServers,
        int retryCount = 0)
    {
        bool isValid = false;

        if (file.alreadyExists)
        {
            var uncompressedLength = (int)chunk.UncompressedLength;
            byte[] verifyBuffer = RentBuffer(uncompressedLength);

            try
            {
                var read = await file.ReadChunkAsync(chunk, verifyBuffer);

                if (read == uncompressedLength)
                {
                    var computed = Adler32.Calculate(1, verifyBuffer.AsSpan(0, read));
                    isValid = computed == chunk.Checksum;
                }
            }
            catch
            {
                isValid = false;
            }
            finally
            {
                ReturnBuffer(verifyBuffer);
            }
        }

        if (isValid)
        {
            Interlocked.Add(ref byteAccumulator, chunk.UncompressedLength);
            Interlocked.Add(ref totalAccumulator, chunk.UncompressedLength);
            return;
        }

        var cdnServer = PickCdnServer(cdnServers, retryCount);
        try
        {
            var length = checked((int)Math.Max(chunk.CompressedLength, chunk.UncompressedLength));

            byte[] buffer = RentBuffer(length);
            try
            {
                await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer, depotKey);
                await file.WriteChunkAsync(chunk, buffer.AsMemory(0, (int)chunk.UncompressedLength));

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
                Console.WriteLine($"[depot {depotId}] error downloading chunk after {MaxRetries} retries: {ex}");
                throw;
            }

            Console.WriteLine($"[depot {depotId}] error downloading chunk: {ex.Message}, retrying in {RetryDelayMs}ms (attempt {retryCount + 1}/{MaxRetries})");

            await Task.Delay(RetryDelayMs);
            await DownloadChunkAsync(chunk, depotId, depotKey, file, cdnClient, cdnServers, retryCount + 1);
        }
    }
}
