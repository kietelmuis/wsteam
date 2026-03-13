namespace wsteam.Data.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SteamKit2;

public class ManifestResponse
{
    public required List<ManifestUrlFile> Tree { get; set; }
}

public class ManifestUrlFile
{
    public string Url { get; set; }
    public string Path { get; set; }
}

public class ManifestFile
{
    public string Content { get; set; }
}

public class GithubApi : IBatchManifestApi
{
    private HttpClient httpClient;

    public GithubApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://api.github.com/repos/SteamAutoCracks/ManifestHub/");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0");

        this.httpClient = httpClient;
    }

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        var manifests = await GetManifestsAsync(appId);
        if (manifests is null)
            return null;

        manifests.TryGetValue(depotId, out var manifest);
        return manifest;
    }

    public async Task<Dictionary<uint, DepotManifest?>?> GetManifestsAsync(uint appId)
    {
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

        var manifestFiles = manifestResponse.Tree.Select(async m =>
        {
            if (!m.Path.Contains("manifest")) return ((uint)0, (DepotManifest?)null);

            var depotId = uint.Parse(m.Path.Split("_")[0]);

            var response = await httpClient.GetAsync($"https://raw.githubusercontent.com/SteamAutoCracks/ManifestHub/{appId}/{m.Path}");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to get manifest file: {response.StatusCode}");
                return (depotId, (DepotManifest?)null);
            }

            return (depotId, DepotManifest.Deserialize(await response.Content.ReadAsByteArrayAsync()));
        });

        var results = await Task.WhenAll(manifestFiles);
        return results
            .Where(r => r.Item1 != 0)
            .ToDictionary(r => r.Item1, r => r.Item2);
    }
}
