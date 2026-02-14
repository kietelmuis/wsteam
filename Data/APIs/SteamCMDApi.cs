namespace wsteam.Data.APIs;

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using wsteam.Models.SteamCMD;

public class SteamCMDApi
{
    private HttpClient httpClient;

    public SteamCMDApi(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<SteamApp> GetInfoAsync(uint appId)
    {
        var response = await httpClient.GetAsync($"https://api.steamcmd.net/v1/info/{appId}");

        var deserializedResponse = await response.Content.ReadFromJsonAsync<SteamInfo>()
            ?? throw new Exception("Failed to retrieve Steam app information");

        return deserializedResponse.data.First().Value;
    }
}
