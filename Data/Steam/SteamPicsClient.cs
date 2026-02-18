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

public class SteamPicsClient(SteamSession steamSession)
{
    private readonly SteamApps steamApps = steamSession.SteamApps;

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
    public async Task<SteamInfo> GetInfoAsync(uint appId)
    {
        var accessToken = await GetAccessTokenAsync(appId)
            ?? throw new Exception($"Failed to get PICS accessToken for app {appId}");

        var data = await steamApps.PICSGetProductInfo(new PICSRequest(appId, accessToken), null);
        data.Results?.First().Apps.First().Value.KeyValues.SaveToFile("./app.vdf", false);

        using var vdfStream = new MemoryStream();
        data.Results?.First().Apps.First().Value.KeyValues.SaveToStream(vdfStream, false);

        vdfStream.Position = 0;

        var vdf = VdfConvert.Deserialize(new StreamReader(vdfStream));
        var vdfJson = vdf.ToJson().First();
        Console.WriteLine(vdfJson);

        return vdfJson.ToObject<SteamInfo>()
            ?? throw new Exception("Failed to deserialize Steam PICS response");
    }
}
