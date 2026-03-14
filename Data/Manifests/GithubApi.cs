namespace wsteam.Data.Manifests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SteamKit2;

public class GithubApi : IManifestApi
{
    private HttpClient httpClient;
    private IMemoryCache memoryCache;
    private readonly ConcurrentDictionary<uint, Lazy<Task<DepotManifest?[]>>> _manifestLoads = new();

    public GithubApi(HttpClient httpClient, IMemoryCache memoryCache)
    {
        httpClient.BaseAddress = new Uri("https://codeload.github.com/SteamAutoCracks/ManifestHub/zip/refs/heads/");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0");

        this.httpClient = httpClient;
        this.memoryCache = memoryCache;
    }

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        var cacheKey = $"{appId}:{depotId}";
        if (memoryCache.Get(cacheKey) is DepotManifest manifest)
            return manifest;

        var lazyTask = _manifestLoads.GetOrAdd(appId,
            id => new Lazy<Task<DepotManifest?[]>>(() => CacheAllManifestsAsync(id)));

        var manifests = await lazyTask.Value;
        return manifests.FirstOrDefault(m => m is not null && m.DepotID == depotId);
    }

    public async Task<DepotManifest?[]> CacheAllManifestsAsync(uint appId)
    {
        Console.WriteLine($"Getting manifests for app {appId}");
        var response = await httpClient.GetAsync($"{appId}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get manifest zip: {response.StatusCode}");
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var zipfile = new ZipArchive(stream, ZipArchiveMode.Read);

        return await Task.WhenAll(
            zipfile.Entries
                .Where(e => e.Name.Contains("manifest"))
                .Select(async e =>
                {
                    try
                    {
                        var manifest = DepotManifest.Deserialize(await e.OpenAsync());
                        memoryCache.Set($"{appId}:{manifest.DepotID}", manifest);

                        return manifest;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing {e.Name}: {ex.Message}");
                        return null;
                    }
                })
        );
    }
}
