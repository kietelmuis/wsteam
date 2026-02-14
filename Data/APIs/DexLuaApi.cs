namespace wsteam.Data.APIs;

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

public class DexLuaApi
{
    private readonly HttpClient httpClient;

    public DexLuaApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://lua.dexpie.web.id/api/");

        this.httpClient = httpClient;
    }

    public async Task<string> DownloadLuaAsync(uint appId)
    {
        var response = await httpClient.GetAsync($"download?id={appId}");
        if (!response.IsSuccessStatusCode) throw new Exception("Failed to get lua");

        return await response.Content.ReadAsStringAsync();
    }
}
