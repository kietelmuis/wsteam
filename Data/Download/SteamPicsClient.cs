using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using SteamKit2;
using wsteam.Models.SteamCMD;
using static SteamKit2.SteamApps;

namespace wsteam.Data.Download;

public class SteamPicsClient(SteamApps steamApps)
{
    private readonly SteamApps steamApps = steamApps;

    public async Task GetAppDataAsync(uint appId)
    {
        var accessToken = (await steamApps.PICSGetAccessTokens(appId, null)).AppTokens.First().Value;
        var data = await steamApps.PICSGetProductInfo(new PICSRequest(appId, accessToken), null);
        // data.Results?.First().Apps.First().Value.KeyValues.SaveToFile("./app.vdf", false);

        using var vdfStream = new MemoryStream();
        data.Results?.First().Apps.First().Value.KeyValues.SaveToStream(vdfStream, false);

        vdfStream.Position = 0;

        var vdf = VdfConvert.Deserialize(new StreamReader(vdfStream));
        var vdfJson = vdf.ToJson().First();
        Console.WriteLine(vdfJson);
        var appModel = vdfJson.ToObject<SteamInfo>();
    }
}
