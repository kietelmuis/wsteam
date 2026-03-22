using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using SteamKit2;
using wsteam.Models.Steam;
using static SteamKit2.SteamApps;

namespace wsteam.Data.Steam;

/// <summary>
/// Steam Product Information Control System
/// </summary>
public class SteamPicsClient
{
    private readonly SteamApps steamApps;
    private readonly string manifestCache;

    public SteamPicsClient(SteamSession steamSession)
    {
        this.steamApps = steamSession.SteamApps;
        this.manifestCache = manifestCache = Path.Combine(Directory.GetCurrentDirectory(), "manifests", "steam");

        Directory.CreateDirectory(manifestCache);
    }

    /// <summary>
    /// Request PICS access tokens for an app or package.
    /// </summary>
    private async Task<ulong?> GetAccessTokenAsync(uint appId)
    {
        var accessTokens = await steamApps.PICSGetAccessTokens(appId, null);

        return
            accessTokens.AppTokens.TryGetValue(appId, out var accessToken) ?
            accessToken :
            null;
    }

    /// <summary>
    /// Request product information for an app or package.
    /// </summary>
    public async Task<SteamApp> GetAppInfoAsync(uint appId)
    {
        var fileLocation = Path.Combine(manifestCache, $"{appId}.manifest");
        if (File.Exists(fileLocation))
            return VdfToSteamApp(KeyValue.LoadFromString(await File.ReadAllTextAsync(fileLocation)));

        var accessToken = await GetAccessTokenAsync(appId)
            ?? throw new Exception($"Failed to get PICS accessToken for app {appId}");

        var picsRequest = new PICSRequest(appId, accessToken);
        var data = await steamApps.PICSGetProductInfo(picsRequest, null);
        if (data.Failed)
            throw new Exception("Failed to get PICS data");

        if (data.Results is null || data.Results.Count() == 0)
            throw new Exception("No results found");

        var firstResult = data.Results.First();
        if (firstResult.Apps.Count() == 0)
            throw new Exception("No app results found");

        var firstApp = firstResult.Apps.First();
        var appVdf = firstApp.Value.KeyValues;
        appVdf.SaveToFile(fileLocation, false);

        return VdfToSteamApp(appVdf);
    }

    private SteamApp VdfToSteamApp(KeyValue appVdf)
    {
        using var vdfStream = new MemoryStream();
        appVdf.SaveToStream(vdfStream, false);
        vdfStream.Position = 0;

        var vdf = VdfConvert.Deserialize(new StreamReader(vdfStream));
        var vdfJson = vdf.ToJson()
            .FirstOrDefault()
            ?? throw new Exception("The app could not be found in the Steam result");

        return vdfJson.ToObject<SteamApp>()
            ?? throw new Exception("Failed to deserialize Steam PICS response");
    }
}
