using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.CDN;
using SteamKit2.Internal;
using wsteam.Data.APIs;
using wsteam.Models.SteamCMD;
using static SteamKit2.SteamApps;

namespace wsteam.Data;

public class DownloadManager
{
    private SteamClient steamClient;
    private SteamUser steamUser;
    private SteamContent steamContent;
    private SteamApps steamApps;

    private ManifestHubApi manifestApi;
    private SteamCMDApi steamCMDApi;
    private DepotKeyProvider depotKeyProvider;
    private CallbackManager callbackManager;

    private bool loggedIn = false;

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
        this.steamApps = this.steamClient.GetHandler<SteamApps>()
            ?? throw new InvalidOperationException("SteamApps handler not found");
        this.callbackManager = new CallbackManager(steamClient);

        Console.WriteLine("Logging in");
        steamUser.LogOnAnonymous();

        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);
        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(LogOffCallback);
        while (!loggedIn) callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
    }

    private void LogOffCallback(SteamUser.LoggedOffCallback loggedOff)
    {
        Console.WriteLine($"Logged off: {loggedOff.Result}");
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
        public required string DepotName;
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

        // var accessToken = await steamApps.PICSGetAccessTokens(appId, null);
        // var data = await steamApps.PICSGetProductInfo(new PICSRequest(appId, accessToken.AppTokens.First().Value), null);
        // Console.WriteLine(data!.Results.First().Apps.First().Value.ID);

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
                DepotName = "hello"
            };
        });

        var manifestFileResults = await Task.WhenAll(manifestDownloadTasks);
        var manifestFiles = manifestFileResults
            .Where(m => m != null)
            .ToList()
            .Cast<ManifestWrapper>();

        Console.WriteLine($"downloading {manifestFiles.Count()} manifests from {cdnServer.Host}");
        await DownloadManifestsAsync(manifestFiles, gameDirectory, appId, cdnClient, cdnServer);
    }

    private async Task DownloadManifestsAsync(IEnumerable<ManifestWrapper> manifestFiles, string gameDirectory, uint appId, Client cdnClient, Server cdnServer)
    {
        foreach (var manifestInfo in manifestFiles)
        {
            uint depotId = manifestInfo.DepotId;
            DepotManifest manifest = manifestInfo.Manifest;
            string depotName = manifestInfo.DepotName ?? $"depot_{depotId}";

            var depotKey = await depotKeyProvider.GetDepotKeysAsync(appId, depotId);
            if (depotKey is null) continue;
            if (manifest.Files is null) continue;

            if (manifest.FilenamesEncrypted) manifest.DecryptFilenames(depotKey);

            Console.WriteLine($"[app {appId}] downloading {depotName} of {manifest.TotalUncompressedSize}");

            await Parallel.ForEachAsync(
                manifest.Files,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (file, ct) => await DownloadFileAsync(file, depotId, depotName, gameDirectory, cdnClient, cdnServer));
        }
    }

    private async Task DownloadFileAsync(DepotManifest.FileData file, uint depotId, string depotName, string gameDirectory, Client cdnClient, Server cdnServer)
    {
        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(gameDirectory, file.FileName);

        Console.WriteLine($"[{depotName}] downloading file {fileName}, {file.Chunks.Count()} chunks");

        using var writer = new FileWriter(filePath);

        try
        {
            await Parallel.ForEachAsync(file.Chunks, async (chunk, ct) =>
                await DownloadChunkAsync(chunk, depotId, writer, cdnClient, cdnServer));

            writer.Flush();
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }
    }

    private async Task DownloadChunkAsync(DepotManifest.ChunkData chunk, uint depotId, FileWriter writer, Client cdnClient, Server cdnServer)
    {
        var chunkId = Convert.ToHexStringLower(SHA256.HashData(chunk.ChunkID ?? []));

        var length = checked((int)chunk.UncompressedLength);
        byte[] buffer = new byte[length];

        var bytes = await cdnClient.DownloadDepotChunkAsync(depotId, chunk, cdnServer, buffer);
        Console.WriteLine($"[chunk {chunkId}] downloaded {bytes}b");

        await writer.WriteChunkAsync(chunk, buffer);
    }
}
