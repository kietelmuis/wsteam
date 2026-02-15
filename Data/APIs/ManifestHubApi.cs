namespace wsteam.Data.APIs;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using SteamKit2;

public class ManifestHubApi : IManifestApi
{
    private HttpClient httpClient;
    private readonly string apiKey;
    private const int maxRetries = 5;

    private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

    public ManifestHubApi(HttpClient httpClient)
    {
        apiKey = Environment.GetEnvironmentVariable("MANIFEST_API_KEY")
            ?? throw new Exception("No manifest api key");

        this.httpClient = httpClient;
    }

    public async Task<DepotManifest?> GetManifestAsync(uint depotId, ulong manifestId)
    {
        await semaphoreSlim.WaitAsync();

        try
        {
            var query = new Dictionary<string, string?>
            {
                ["apikey"] = apiKey,
                ["depotid"] = depotId.ToString(),
                ["manifestid"] = manifestId.ToString(),
            };

            var url = QueryHelpers.AddQueryString(
                "https://api.manifesthub1.filegear-sg.me/manifest",
                query
            );

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                        throw new Exception("ManifestHub key invalid or expired");

                    var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
                    var error = jsonResponse.GetProperty("error");

                    Console.WriteLine($"ManifestHub error {response.ReasonPhrase} (depot {depotId}): {error}, retrying in 5s");

                    await Task.Delay(5000);
                    continue;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                return DepotManifest.Deserialize(stream);
            }

            Console.WriteLine(url);
            Console.WriteLine($"failed to download depot {depotId} after {maxRetries} tries");
            return null;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}
