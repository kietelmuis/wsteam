namespace wsteam.Data;

using System;
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

    public async Task<SteamApp[]> GetInfoAsync(int appId)
    {
        var response = await httpClient.GetAsync($"https://api.steamcmd.net/v1/info/{appId}");
        return await response.Content.ReadFromJsonAsync<SteamApp[]>()
            ?? throw new Exception("Failed to retrieve Steam app information");
    }
}
