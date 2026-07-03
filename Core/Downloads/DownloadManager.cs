using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SteamKit2;
using SteamKit2.CDN;
using SteamKit2.Internal;
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

        int dynamicDefault = Environment.ProcessorCount + 4;
        int minThreads = Math.Max(4, Environment.ProcessorCount);
        return Math.Clamp(dynamicDefault, minThreads, 32);
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

    private ulong _lastTotal = 0;
    private double _smoothedBytesPerSec = 0;
    private const double Alpha = 0.2;

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

    public string GetEstimatedRemainingTime()
    {
        var currentTotal = Interlocked.Read(ref totalAccumulator);
        var delta = currentTotal - _lastTotal;
        _lastTotal = currentTotal;

        _smoothedBytesPerSec = Alpha * delta + (1 - Alpha) * _smoothedBytesPerSec;

        var remaining = appSize - currentTotal;
        return _smoothedBytesPerSec > 0
            ? TimeSpan.FromSeconds(remaining / _smoothedBytesPerSec).ToString(@"hh\:mm\:ss")
            : "?";
    }

    public string GetDownloadSpeed()
    {
        var elapsedSeconds = SpeedTimer.Interval / 1000.0;
        var speedMbps = byteAccumulator / 1024.0 / 1024.0 / elapsedSeconds;
        return $"{speedMbps:F2} MB/s";
    }

    public async Task DownloadPubFileAsync(uint appId, PublishedFileID pubId, string steamDirectory = "")
    {
        await steamSession.WaitLoggedOnAsync();

        var pubFileRequest = new CPublishedFile_GetDetails_Request
        {
            appid = appId,
            includechildren = true,
        };
        pubFileRequest.publishedfileids.Add(pubId);

        var cdnServers = (await steamSession.SteamContent.GetServersForSteamPipe()).ToArray();
        using var cdnClient = new Client(steamSession.SteamClient);

        var details = await steamSession.PublishedFile.GetDetails(pubFileRequest);
        if (details.Result is not EResult.OK)
            throw new InvalidOperationException("Failed to get UGC details");

        string? downloadedTitle = null;
        List<ulong> contentFileIds = [];
        List<(string, string)> fileUrls = [];

        foreach (var file in details.Body.publishedfiledetails)
        {
            if (file is null) continue;

            Console.WriteLine($"[workshop] found file {file.title} ({file.publishedfileid}) of type {(EWorkshopFileType)file.file_type}");
            switch ((EWorkshopFileType)file.file_type)
            {
                case EWorkshopFileType.Collection:
                    foreach (var child in file.children)
                    {
                        await DownloadPubFileAsync(appId, child.publishedfileid);
                    }
                    break;
                case EWorkshopFileType.First:
                    if (!string.IsNullOrEmpty(file.file_url))
                    {
                        fileUrls.Add((file.filename, file.file_url));
                    }
                    else if (file.hcontent_file > 0)
                    {
                        downloadedTitle = file.title;
                        Console.WriteLine($"[workshop] {file.title} has content file id {file.hcontent_file}");
                        contentFileIds.Add(file.hcontent_file);
                    }
                    break;
            }
        }

        var steamWorkshopDirectory = Path.Combine(steamDirectory, "steamapps", "workshop", "content", appId.ToString());
        Directory.CreateDirectory(steamWorkshopDirectory);

        Console.WriteLine($"[workshop] {fileUrls.Count()} files to download from URLs and {contentFileIds.Count()} files to download from content servers");

        foreach (var fileId in contentFileIds)
        {
            var workshopItemDirectory = Path.Combine(steamWorkshopDirectory, pubId.ToString());
            Directory.CreateDirectory(workshopItemDirectory);

            var app = await picsClient.GetAppInfoAsync(appId);
            currentApp = app;

            Console.WriteLine($"[downloader] found app {app.AppId}");

            if (app.Depots.WorkshopDepot is null && app.Depots.WorkshopDepot != 0)
            {
                Console.WriteLine($"[workshop] no workshop depot found! cannot download content file {fileId}");
                continue;
            }
            Console.WriteLine($"[workshop] found workshop depot {app.Depots.WorkshopDepot}");

            var keyResponse = await steamSession.SteamApps.GetDepotDecryptionKey((uint)app.Depots.WorkshopDepot, appId);
            if (keyResponse.Result is not EResult.OK)
            {
                Console.WriteLine($"[workshop] failed to get depot key for workshop depot {app.Depots.WorkshopDepot}: {keyResponse.Result}");
                continue;
            }
            Console.WriteLine($"[workshop] got depot key {keyResponse.DepotKey} for workshop depot {app.Depots.WorkshopDepot}");

            var requestCode = await steamSession.SteamContent.GetManifestRequestCode((uint)app.Depots.WorkshopDepot, appId, fileId);
            if (requestCode == 0)
            {
                Console.WriteLine($"[workshop] failed to get manifest request code for content file {fileId}");
                continue;
            }
            Console.WriteLine($"[workshop] got manifest request code {requestCode} for content file {fileId}");

            var manifest = await cdnClient.DownloadManifestAsync(
                keyResponse.DepotID,
                fileId,
                requestCode,
                cdnServers.First()
            );

            if (manifest.FilenamesEncrypted)
                manifest.DecryptFilenames(keyResponse.DepotKey);

            var manifestWrapper = new ManifestWrapper
            {
                DepotId = (uint)app.Depots.WorkshopDepot,
                DepotKey = keyResponse.DepotKey,
                Manifest = manifest
            };

            appSize = manifest.TotalUncompressedSize;

            SpeedTimer.Interval = 1000;
            SpeedTimer.Elapsed += (sender, e) =>
            {
                var progressPercent = GetDownloadPercentage();
                var speedMbps = GetDownloadSpeed();
                var eta = GetEstimatedRemainingTime();

                Console.WriteLine($"[dl] {progressPercent:F1}% | {speedMbps} | {eta}");
                byteAccumulator = 0;
            };
            SpeedTimer.Start();

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[downloader] downloading workshop item from cellid {cdnServers[0].CellID}");
            await DownloadManifestsAsync([manifestWrapper], workshopItemDirectory, appId, cdnClient, cdnServers);

            Console.WriteLine($"[downloader] download finished in {sw.Elapsed.ToString(@"hh\:mm\:ss\.ff")}!");

            SpeedTimer.Dispose();
            currentApp = null;
        }

        foreach (var file in fileUrls)
        {
            var workshopItemDirectory = Path.Combine(steamWorkshopDirectory, pubId.ToString());
            Directory.CreateDirectory(workshopItemDirectory);

            using var client = new HttpClient();

            var filePath = Path.Combine(workshopItemDirectory, file.Item1);
            Console.WriteLine($"[workshop] downloading {file.Item1} to {filePath}");

            var response = await client.GetAsync(file.Item2);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[workshop] failed to download {file.Item1} from {file.Item2}: {response.StatusCode}");
                continue;
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);

            await fs.FlushAsync();
            fs.Close();
        }

        Console.WriteLine($"[workshop] finished downloading \"{downloadedTitle ?? pubId.ToString()}\" ({pubId}) to {Path.Combine(steamWorkshopDirectory, pubId.ToString())}");
    }

    public async Task DownloadAppAsync(uint appId, string path, SteamOperatingSystem os, IManifestApi manifestApi, uint[]? excludedDepots = null)
    {
        await steamSession.WaitLoggedOnAsync();

        var game = await picsClient.GetAppInfoAsync(appId);
        currentApp = game;

        Console.WriteLine($"[downloader] found app {game.AppId}");

        var depots = game.Depots.Depots.ToList()
            .Where(d => !Redistributables.Contains(int.Parse(d.Key)))
            .Where(d => (d.Value?.Config?.OsList ?? "").Contains(os.ToString(), StringComparison.OrdinalIgnoreCase))
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

        Console.WriteLine($"[downloader] using cellid {cdnServers.First().CellID} (+{cdnServers.Count()} cells)");

        ulong totalSize = 0;

        var manifestDownloadTasks = depots.Select(async depot =>
        {
            if (depot.Value.Manifests is null) return null;

            var publicManifest = depot.Value.Manifests.TryGetValue("public", out var pManifest) ? pManifest : null;
            if (publicManifest is null)
            {
                Console.WriteLine($"[manifest] no public manifest for depot {depot.Key}! cannot download depot");
                return null;
            }

            var depotId = uint.Parse(depot.Key);
            var manifestId = ulong.Parse(publicManifest.Gid);

            var manifest = await manifestApi.GetManifestAsync(appId, depotId, manifestId);
            if (manifest is null)
            {
                Console.WriteLine($"[manifest] failed to download manifest for depot {depot.Key}! cannot download depot");
                return null;
            }

            var depotKey = await depotKeyProvider.GetDepotKeyAsync(appId, depotId);
            if (depotKey is null)
            {
                Console.WriteLine($"[manifest] no depot key for depot {depot.Key}! cannot decrypt");
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
            totalSize += manifest.TotalUncompressedSize;

            return wrappedManifest;
        });

        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.AvailableFreeSpace < (long)totalSize)
        {
            Console.WriteLine($"[downloader] not enough space on drive {drive.Name} (available={drive.AvailableFreeSpace}, required={totalSize})");
            return;
        }

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
                var eta = GetEstimatedRemainingTime();

                Console.WriteLine($"[dl] {progressPercent:F1}% | {speedMbps} | {eta}");
                byteAccumulator = 0;
            };
            SpeedTimer.Start();

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[downloader] downloading {manifestCount} manifests from cellid {cdnServers[0].CellID}");
            await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServers);

            Console.WriteLine($"[downloader] downloaded {manifestCount} depot(s)");
            Console.WriteLine($"[downloader] download finished in {sw.Elapsed.ToString(@"hh\:mm\:ss\.ff")}!");
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
            {
                Console.WriteLine($"[manifest] Files is null for depot {manifest.DepotID}");
                continue;
            }

            Console.WriteLine($"[manifest] depot {manifest.DepotID} has {manifest.Files.Count} total files, {manifest.Files.Count(f => f.Chunks.Any())} with chunks, filenamesEncrypted={manifest.FilenamesEncrypted}");

            var allFiles = manifest.Files
                .Where(f => f.Chunks.Any())
                .ToList();

            var filePaths = allFiles.ToDictionary(
                f => f,
                f => Path.Combine(gameDirectory, f.FileName.TrimStart('\\', '/').Replace('\\', Path.DirectorySeparatorChar))
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
                var read = await file.ReadChunkAsync(chunk, verifyBuffer.AsMemory(0, uncompressedLength));

                if (read == uncompressedLength)
                {
                    var computed = Adler32.Calculate(0, verifyBuffer.AsSpan(0, uncompressedLength));
                    isValid = computed == chunk.Checksum;
                    if (!isValid)
                        Console.WriteLine($"[verify] Chunk INVALID for {file.fileName} offset {chunk.Offset} expected {chunk.Checksum} got {computed}");
                }
                else
                {
                    Console.WriteLine($"[verify] Could not read full chunk for {file.fileName} offset {chunk.Offset}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[verify] Exception while verifying chunk: {ex}");
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
