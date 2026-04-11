namespace wsteam.Core.Manifests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using SteamKit2;
using wsteam.Core.DepotKey;
using wsteam.Core.Downloads;

class AppData
{
    public required DepotManifest Manifest { get; set; }
    public byte[]? DepotKey { get; set; }
}

/// <summary>
/// Provides older manifests, more reliable than the ManifestHub
/// </summary>
public class GithubApi : IManifestApi, IDepotKeySource
{
    private HttpClient httpClient;
    private IMemoryCache memoryCache;

    private readonly ConcurrentDictionary<uint, Lazy<Task<List<AppData>>>> _appCache = new();

    public GithubApi(HttpClient httpClient, IMemoryCache memoryCache)
    {
        httpClient.BaseAddress = new Uri("https://codeload.github.com/SteamAutoCracks/ManifestHub/zip/refs/heads/");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0");

        this.httpClient = httpClient;
        this.memoryCache = memoryCache;
    }

    private async Task<List<AppData>> CacheAllAsync(uint appId)
    {
        Console.WriteLine($"Getting app data for {appId}");

        var response = await httpClient.GetAsync($"{appId}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get zip: {response.StatusCode}");
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var zipfile = new ZipArchive(stream, ZipArchiveMode.Read);

        VToken? vdf = null;
        var vdfEntry = zipfile.Entries.FirstOrDefault(e => e.Name == "key.vdf");
        if (vdfEntry is not null)
        {
            using var vdfStream = new StreamReader(vdfEntry.Open());
            vdf = VdfConvert.Deserialize(await vdfStream.ReadToEndAsync()).Value;
        }

        return [.. (await Task.WhenAll(
            zipfile.Entries
                .Where(e => e.Name.Contains("manifest"))
                .Select(async e =>
                {
                    try
                    {
                        var manifest = DepotManifest.Deserialize(await e.OpenAsync());
                        memoryCache.Set($"{appId}:{manifest.DepotID}", manifest);

                        if (vdf is null) return new AppData
                        {
                            Manifest = manifest
                        };

                        var depotKey = vdf[manifest.DepotID.ToString()]?["DecryptionKey"]?.ToString();
                        if (depotKey is null) return new AppData
                        {
                            Manifest = manifest
                        };

                        return new AppData {
                            Manifest = manifest,
                            DepotKey = Convert.FromHexString(depotKey)
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing {e.Name}: {ex.Message}");
                        return null;
                    }
                })
        ))
        .Where(m => m is not null)
        .Cast<AppData>()];
    }

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        var cacheKey = $"{appId}:{depotId}";
        if (memoryCache.Get(cacheKey) is DepotManifest manifest)
            return manifest;

        var lazyTask = _appCache.GetOrAdd(appId,
            id => new Lazy<Task<List<AppData>>>(() => CacheAllAsync(id)));

        var appList = await lazyTask.Value;
        return appList.FirstOrDefault(a => a is not null && a.Manifest.DepotID == depotId)?.Manifest;
    }

    public async Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId)
    {
        var lazyTask = _appCache.GetOrAdd(appId,
            id => new Lazy<Task<List<AppData>>>(() => CacheAllAsync(id)));

        var appList = await lazyTask.Value;
        if (appList is null)
            return null;

        var depot = appList.FirstOrDefault(a => a is not null && a.Manifest.DepotID == depotId);
        return depot?.DepotKey;
    }
}
