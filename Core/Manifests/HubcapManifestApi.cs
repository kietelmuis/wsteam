namespace wsteam.Core.Manifests;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using wsteam.Models.Steam;

/// <summary>
/// Provides up-to-date manifests, limited to 25 manifests a day
/// </summary>
public class HubcapManifestApi : IManifestApi
{
    public HubcapManifestApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://hubcapmanifest.com/api/v1/");

        this.httpClient = httpClient;
        this.manifestCache = Path.Combine(Directory.GetCurrentDirectory(), "manifests", "steam");
        Directory.CreateDirectory(manifestCache);
    }

    private readonly string manifestCache;
    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim semaphore = new(1, 1);

    private readonly string apiKey =
        Environment.GetEnvironmentVariable("HUBCAP_API_KEY")
        ?? throw new InvalidOperationException("No hubcap api key");

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        if (!await HubcapHealth())
            throw new InvalidOperationException("Hubcap manifest API is not healthy");

        var fileDirectory = Path.Combine(manifestCache, appId.ToString());
        var fileLocation = Path.Combine(fileDirectory, $"{depotId}.manifest");
        if (File.Exists(fileLocation))
            return DepotManifest.LoadFromFile(fileLocation);

        await semaphore.WaitAsync();
        try
        {
            if (File.Exists(fileLocation))
                return DepotManifest.LoadFromFile(fileLocation);

            using var response = await httpClient.GetAsync($"manifest/{appId}?api_key={apiKey}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Hubcap key invalid or expired");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("Hubcap daily limit exceeded");
                    return null;
                }

                Console.WriteLine($"Hubcap error {response.ReasonPhrase} (app {appId}): {response.ReasonPhrase}");
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var zip = new ZipArchive(stream);

            return zip.Entries
                .Where(e => e.Name.Contains("manifest"))
                .Select(m =>
                    {
                        var manifest = DepotManifest.Deserialize(m.Open());
                        var filePath = Path.Combine(fileDirectory, $"{manifest.ManifestGID}.manifest");

                        Directory.CreateDirectory(fileDirectory);
                        manifest.SaveToFile(filePath);

                        return manifest;
                    })
                .FirstOrDefault(m => m.DepotID == depotId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> HubcapHealth()
    {
        using var response = await httpClient.GetAsync($"health");
        return response.IsSuccessStatusCode;
    }
}
