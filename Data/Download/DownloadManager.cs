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
    private SteamClient steamClient;
    private SteamUser steamUser;
    private SteamContent steamContent;

    private IManifestApi manifestApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;
    private CallbackManager callbackManager;

    private bool loggedIn = false;

    public DownloadManager(IManifestApi manifestApi, SteamCMDApi steamCMDApi, DepotKeyProvider depotKeyProvider)
    {
        this.manifestApi = manifestApi;
        this.steamCMDApi = steamCMDApi;
        this.depotKeyProvider = depotKeyProvider;

        this.steamClient = new SteamClient();
        this.steamUser = this.steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.steamContent = this.steamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
        this.callbackManager = new CallbackManager(steamClient);

        callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ =>
        {
            Console.WriteLine("SteamClient connected, logging on");
            steamUser.LogOnAnonymous();
        });

        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(cb =>
        {
            Console.WriteLine($"SteamClient disconnected: forced={!cb.UserInitiated}");
        });

        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(cb =>
        {
            Console.WriteLine($"SteamUser logged off: {cb.Result}");
        });

        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);

        steamClient.Connect();

        while (!loggedIn) callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
    }

    private void LogOnCallback(SteamUser.LoggedOnCallback loggedOn)
    {
        if (loggedOn.Result == EResult.OK)
        {
            Console.WriteLine("Logged in!");
            loggedIn = true;
        }
        else
        {
            Console.WriteLine($"Error upon logging in: {loggedOn.Result}");
        }
    }

    private class ManifestWrapper
    {
        public required DepotManifest Manifest;
        public required uint DepotId;
    }

    public async Task DownloadAppAsync(uint appId, string path)
    {
        var game = await steamCMDApi.GetInfoAsync(appId);
        var depots = game.depots.DepotObjects.ToList();

        Console.WriteLine($"downloading game {game.config.installdir}");
        Console.WriteLine($"found {depots.Count()} depots");

        var gameDirectory = Path.Combine(path, game.config.installdir);
        Directory.CreateDirectory(gameDirectory);

        var cdnServer = (await steamContent.GetServersForSteamPipe())
            .ToList()
            .First();

        using var cdnClient = new Client(steamClient);

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

        var chunkCount = file.Chunks.Count();
        if (chunkCount == 0)
        {
            Directory.CreateDirectory(filePath);
            Console.WriteLine($"[{depotName}] created directory {file.FileName}");
            return;
        }

        Console.WriteLine($"[{depotName}] downloading file {fileName}, {file.Chunks.Count()} chunks");

        using var writer = new FileWriter(filePath);

        try
        {
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

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, byte[] depotKey, FileWriter writer, Client cdnClient, Server cdnServer)
    {
        var chunkId = Convert.ToHexStringLower(SHA256.HashData(chunk.ChunkID ?? []));

        var length = checked((int)chunk.UncompressedLength);
        byte[] buffer = new byte[length];

        var bytes = await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer, depotKey);
        await writer.WriteChunkAsync(chunk, buffer);
        Console.WriteLine($"[{chunkId}] written {bytes} bytes");
    }
}
