namespace wsteam.Core.Manifests;

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

/// <summary>
/// Provides up-to-date manifests, may fail unexpectedly
/// </summary>
public class ManifestHubApi(HttpClient httpClient) : IManifestApi
{
    private readonly HttpClient httpClient = httpClient;

    private readonly string apiKey =
        Environment.GetEnvironmentVariable("MANIFEST_API_KEY")
        ?? throw new InvalidOperationException("No manifest api key");

    private const int MaxRetries = 5;
    private const int RequestIntervalMs = 500;

    private DateTime lastRequestTime = DateTime.MinValue;
    private DateTime backoffUntil = DateTime.MinValue;

    private readonly SemaphoreSlim semaphoreSlim = new(1, 1);

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
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

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var now = DateTime.UtcNow;
                if (now < backoffUntil)
                    await Task.Delay(backoffUntil - now);

                var elapsed = (DateTime.UtcNow - lastRequestTime).TotalMilliseconds;
                var wait = RequestIntervalMs - elapsed;
                if (wait > 0) await Task.Delay((int)wait);

                using var response = await httpClient.GetAsync(url);
                lastRequestTime = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    Console.WriteLine(response.ReasonPhrase);

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                        throw new InvalidOperationException("ManifestHub key invalid or expired");

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryAfter =
                                    response.Headers.RetryAfter?.Delta
                                    ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                        backoffUntil = DateTime.UtcNow + retryAfter;
                        semaphoreSlim.Release();
                        await Task.Delay(retryAfter);
                        await semaphoreSlim.WaitAsync();
                        continue;
                    }

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
            Console.WriteLine($"failed to download depot {depotId} after {MaxRetries} tries");
            return null;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}
