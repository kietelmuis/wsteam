using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Spectre.Console;
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
    Linux,
    MacOs
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

    private const int MaxServerFailures = 2;
    private readonly ConcurrentDictionary<Server, int> serverFailures = new();

    private bool IsServerFailed(Server server)
        => serverFailures.TryGetValue(server, out var failures) && failures >= MaxServerFailures;

    private Server PickCdnServer(Server[] servers, int retryCount)
    {
        if (servers.Length == 0)
            throw new InvalidOperationException("No CDN servers available");

        var n = Interlocked.Increment(ref cdnServerCounter);
        var baseIndex = (int)((uint)n % (uint)servers.Length);

        for (int i = 0; i < servers.Length; i++)
        {
            var server = servers[(baseIndex + i) % servers.Length];
            if (!IsServerFailed(server))
                return server;
        }

        return servers[(baseIndex + retryCount) % servers.Length];
    }

    private readonly SteamSession steamSession = steamSession;
    private readonly SteamPicsClient picsClient = picsClient;
    private readonly DepotKeyProvider depotKeyProvider = depotKeyProvider;

    private SteamApp? currentApp;

    private long invalidChunkCount = 0;
    private ulong appSize = 0;

    // chunk retry settings
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    static readonly uint[] Redistributables = [
        228983, // VC 2010 Redist
        228990  // DirectX Jun 2010 Redist
    ];

    private sealed class DepotProgress
    {
        public required uint DepotId;
        public required ulong TotalBytes;
        public long DownloadedBytes;
        public ProgressTask? Task;
    }

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
            if (requestCode is 0)
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

            var sw = Stopwatch.StartNew();

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new DownloadedColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn())
                .StartAsync(async progressCtx =>
                {
                    var task = progressCtx.AddTask(
                        $"[green]{downloadedTitle ?? pubId.ToString()}[/]",
                        maxValue: manifest.TotalUncompressedSize);

                    var dp = new DepotProgress
                    {
                        DepotId = manifestWrapper.DepotId,
                        TotalBytes = manifest.TotalUncompressedSize,
                        Task = task
                    };

                    await DownloadManifestsAsync(
                        [manifestWrapper], workshopItemDirectory, appId,
                        cdnClient, cdnServers, [dp], task);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] finished in {sw.Elapsed.ToString(@"hh\:mm\:ss\.ff")}");
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

    public async Task DownloadAppAsync(uint appId, string path, SteamOperatingSystem[] os, IManifestApi manifestApi, uint[]? excludedDepots = null)
    {
        SteamApp game = null!;
        Server[] cdnServers = null!;
        Client cdnClient = null!;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Logging into Steam...", async ctx =>
            {
                await steamSession.WaitLoggedOnAsync();

                ctx.Status($"Finding app {appId}...");
                game = await picsClient.GetAppInfoAsync(appId);
                currentApp = game;

                ctx.Status("Connecting to Steam CDN...");
                cdnServers = [.. await steamSession.SteamContent.GetServersForSteamPipe()];
                cdnClient = new Client(steamSession.SteamClient);
            });

        var depots = game.Depots.Depots
            .Where(d => uint.TryParse(d.Key, out var depotId) && !Redistributables.Contains(depotId))
            .Where(d => d.Value?.Manifests is not null)
            .Where(d => os.Any((o) => (d.Value?.Config?.OsList ?? "").Contains(o.ToString(), StringComparison.OrdinalIgnoreCase)))
            .Where(d => (d.Value?.Config?.Language ?? "english") == "english")
            .Where(d => excludedDepots is null || !excludedDepots.Contains(uint.Parse(d.Key)))
            .ToList();

        var selectedDepotKeys = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select depots to download:")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to start download)[/]")
                .AddChoices(depots.Select(d => $"Depot {d.Key} - {d.Value?.Config?.OsList ?? "unknown"}").ToList()));

        var selectedDepots = depots.Where(d =>
            selectedDepotKeys.Contains($"Depot {d.Key} - {d.Value?.Config?.OsList ?? "unknown"}")
        ).ToList();

        var selectedDepotIds = selectedDepots.Select(d => d.Key).ToList();

        var gameDirectory = Path.Combine(path, game.Config.InstallDir);
        Directory.CreateDirectory(gameDirectory);

        var manifestResults = new ConcurrentDictionary<uint, ManifestWrapper?>();
        var depotSizes = new ConcurrentDictionary<uint, ulong>();

        await AnsiConsole.Live(BuildManifestTable(selectedDepotIds, manifestResults, depotSizes))
                .StartAsync(async ctx =>
                {
                    var manifestTasks = selectedDepots.Select(async depot =>
                {
                    var depotId = uint.Parse(depot.Key);
                    AnsiConsole.MarkupLine($"[blue]Downloading manifest for depot {depotId}...[/]");

                    try
                    {
                        if (depot.Value.Manifests is null) throw new Exception("no manifests");

                        var publicManifest = (depot.Value.Manifests.TryGetValue("public", out var p) ? p : null)
                            ?? throw new Exception("no public manifest");

                        var manifestId = ulong.Parse(publicManifest.Gid);
                        var manifest = await manifestApi.GetManifestAsync(appId, depotId, manifestId)
                            ?? throw new Exception("download failed");

                        var depotKey = await depotKeyProvider.GetDepotKeyAsync(appId, depotId)
                            ?? throw new Exception("no key");

                        if (!manifest.DecryptFilenames(depotKey)) throw new Exception("decrypt failed");

                        manifestResults[depotId] = new ManifestWrapper
                        {
                            DepotId = depotId,
                            Manifest = manifest,
                            DepotKey = depotKey,
                        };
                        depotSizes[depotId] = manifest.TotalUncompressedSize;
                    }
                    catch
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to download manifest for depot {depotId}[/]");
                        manifestResults[depotId] = null;
                    }

                    ctx.UpdateTarget(BuildManifestTable(selectedDepotIds, manifestResults, depotSizes));
                });

                    await Task.WhenAll(manifestTasks);
                });

        var manifestFiles = manifestResults.Values.Where(m => m is not null).Cast<ManifestWrapper>().ToList();

        if (manifestFiles.Count is 0)
        {
            AnsiConsole.MarkupLine("[red]No manifests could be found.[/]");
            return;
        }

        appSize = (ulong)manifestFiles.Sum(m => (long)m.Manifest.TotalUncompressedSize);

        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.AvailableFreeSpace < (long)appSize)
        {
            AnsiConsole.MarkupLine("[red]Not enough disk space.[/]");
            return;
        }

        var depotProgressList = manifestFiles.Select(m => new DepotProgress
        {
            DepotId = m.DepotId,
            TotalBytes = m.Manifest.TotalUncompressedSize
        }).ToList();

        var sw = Stopwatch.StartNew();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new DownloadedColumn(),
                new TransferSpeedColumn(),
                new RemainingTimeColumn())
            .StartAsync(async progressCtx =>
            {
                var overallTask = progressCtx.AddTask("[bold]Overall[/]", maxValue: appSize);

                foreach (var dp in depotProgressList)
                    dp.Task = progressCtx.AddTask($"Depot {dp.DepotId}", maxValue: dp.TotalBytes);

                await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServers, depotProgressList, overallTask);
            });

        sw.Stop();
        cdnClient.Dispose();
        currentApp = null;

        var avgSpeed = appSize / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;

        var summary = new Table()
            .Border(TableBorder.Rounded)
            .Title("[green]✓ Download Complete[/]")
            .AddColumn("").AddColumn("");

        summary.AddRow("Application", game.Config.InstallDir);
        summary.AddRow("Depots", manifestFiles.Count.ToString());
        summary.AddRow("Downloaded", ByteSizeFormatter.FormatBytes(appSize));
        summary.AddRow("Time", sw.Elapsed.ToString(@"hh\:mm\:ss"));
        summary.AddRow("Average", $"{avgSpeed:F1} MB/s");
        summary.AddRow("Installed", gameDirectory);

        AnsiConsole.Write(summary);
    }

    private static Table BuildManifestTable(
        List<string> depotIds,
        ConcurrentDictionary<uint, ManifestWrapper?> results,
        ConcurrentDictionary<uint, ulong> sizes)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Manifests[/]")
            .AddColumn("Depot").AddColumn("Status").AddColumn("Size");

        foreach (var key in depotIds)
        {
            var depotId = uint.Parse(key);

            if (!results.TryGetValue(depotId, out var result))
                table.AddRow(key, "[yellow]⠋ Loading...[/]", "—");
            else if (result is not null)
                table.AddRow(key, "[green]✓ Ready[/]", ByteSizeFormatter.FormatBytes(sizes[depotId]));
            else
                table.AddRow(key, "[red]✗ Failed[/]", "—");
        }

        return table;
    }

    private async Task DownloadManifestsAsync(
        IEnumerable<ManifestWrapper> manifestFiles,
        string gameDirectory,
        uint appId,
        Client cdnClient,
        Server[] cdnServers,
        List<DepotProgress>? depotProgressList = null,
        ProgressTask? overallTask = null)
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

                var depotProgress = depotProgressList?.FirstOrDefault(dp => dp.DepotId == manifestInfo.DepotId);

                await Parallel.ForEachAsync(
                    chunkJobs,
                    new ParallelOptions { MaxDegreeOfParallelism = dop },
                    async (job, ct) =>
                    {
                        await DownloadChunkAsync(
                            job.chunk, manifestInfo.DepotId, manifestInfo.DepotKey,
                            job.file, cdnClient, cdnServers,
                            depotProgress, overallTask);
                    });

                if (depotProgress?.Task is not null)
                {
                    depotProgress.Task.Description = $"[green]Depot {depotProgress.DepotId} ✓[/]";
                    depotProgress.Task.StopTask();
                }
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
        DepotProgress? depotProgress = null,
        ProgressTask? overallTask = null,
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
                    {
                        var n = Interlocked.Increment(ref invalidChunkCount);
                        overallTask?.Description = $"[bold]Overall[/]  [yellow]{n} chunks re-queued[/]";
                    }
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
            depotProgress?.Task?.Increment(chunk.UncompressedLength);
            overallTask?.Increment(chunk.UncompressedLength);
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

                serverFailures.TryRemove(cdnServer, out _);
                depotProgress?.Task?.Increment(chunk.UncompressedLength);
                overallTask?.Increment(chunk.UncompressedLength);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }
        catch (Exception ex)
        {
            var failures = serverFailures.AddOrUpdate(cdnServer, 1, (_, count) => count + 1);
            var failed = IsServerFailed(cdnServer)
                ? " (server marked as failed)"
                : "";
            Console.WriteLine($"[depot {depotId}] error downloading chunk from {cdnServer.Host}: {ex.Message}{failed}, retrying in {RetryDelayMs}ms (attempt {retryCount + 1}/{MaxRetries}, server failures={failures})");

            if (retryCount >= MaxRetries)
            {
                Console.WriteLine($"[depot {depotId}] error downloading chunk after {MaxRetries} retries: {ex}");
                throw;
            }

            await Task.Delay(RetryDelayMs);
            await DownloadChunkAsync(chunk, depotId, depotKey, file, cdnClient, cdnServers, depotProgress, overallTask, retryCount + 1);
        }
    }
}
