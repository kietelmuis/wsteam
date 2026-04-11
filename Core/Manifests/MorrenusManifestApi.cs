namespace wsteam.Core.Manifests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using SteamKit2;

/// <summary>
/// Provides up-to-date manifests, limited to 25 manifests a day
/// </summary>
public class MorrenusManifestApi : IManifestApi
{
    public MorrenusManifestApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://manifest.morrenus.xyz/api/v1/");

        this.httpClient = httpClient;
        this.manifestCache = Path.Combine(Directory.GetCurrentDirectory(), "manifests", "steam");
        Directory.CreateDirectory(manifestCache);
    }

    private readonly string manifestCache;
    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim semaphore = new(1, 1);

    private readonly string apiKey =
        Environment.GetEnvironmentVariable("MORRENUS_API_KEY")
        ?? throw new InvalidOperationException("No morrenus api key");

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        if (!await MorrenusHealth())
            throw new InvalidOperationException("Morrenus manifest api is not healthy");

        var fileDirectory = Path.Combine(manifestCache, appId.ToString());
        var fileLocation = Path.Combine(fileDirectory, $"{manifestId}.manifest");
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
                    throw new InvalidOperationException("Morrenus key invalid or expired");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("Morrenus daily limit exceeded");
                    return null;
                }

                Console.WriteLine($"Morrenus error {response.ReasonPhrase} (app {appId}): {response.ReasonPhrase}");
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

    public async Task<bool> MorrenusHealth()
    {
        using var response = await httpClient.GetAsync($"health");
        return response.IsSuccessStatusCode;
    }
}
