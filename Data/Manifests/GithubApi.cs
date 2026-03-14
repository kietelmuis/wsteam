namespace wsteam.Data.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SteamKit2;
using wsteam.Models.Github;

public class GithubApi : IManifestApi
{
    private HttpClient httpClient;
    private IMemoryCache memoryCache;

    public GithubApi(HttpClient httpClient, IMemoryCache memoryCache)
    {
        httpClient.BaseAddress = new Uri("https://api.github.com/repos/SteamAutoCracks/ManifestHub/");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0");

        this.httpClient = httpClient;
        this.memoryCache = memoryCache;
    }

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        if (memoryCache.Get($"{appId}:{depotId}") is DepotManifest manifest)
            return manifest;
        Console.WriteLine($"Cache miss for {appId}:{depotId}:{manifestId}");

        Console.WriteLine($"Getting manifests for app {appId}");
        var response = await httpClient.GetAsync($"git/trees/{appId}?recursive=1");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get manifest tree: {response.StatusCode}");
            return null;
        }

        var manifestResponse = await response.Content.ReadFromJsonAsync<ManifestResponse>()
            ?? throw new Exception("Failed to parse manifest response");

        Console.WriteLine($"Found {manifestResponse.Tree.Count} manifest files");

        var manifests = await Task.WhenAll(
            manifestResponse.Tree
                .Where(m => m.Path.Contains("manifest"))
                .ToList()
                .Select(async m =>
                {
                    var fileResponse = await httpClient.GetAsync($"https://raw.githubusercontent.com/SteamAutoCracks/ManifestHub/{appId}/{m.Path}");
                    if (!fileResponse.IsSuccessStatusCode) return null;

                    var manifest = DepotManifest.Deserialize(await fileResponse.Content.ReadAsByteArrayAsync());
                    memoryCache.Set($"{appId}:{manifest.DepotID}", manifest);

                    return manifest;
                })
        );

        return manifests.FirstOrDefault(m =>
            m is not null &&
            m.DepotID == depotId &&
            m.ManifestGID == manifestId
        );
    }
}
