namespace wsteam.Data;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using SteamKit2;

public class ManifestHubApi
{
    private HttpClient httpClient;
    private readonly string apiKey;

    public ManifestHubApi(HttpClient httpClient)
    {
        apiKey = Environment.GetEnvironmentVariable("MANIFEST_API_KEY")
            ?? throw new Exception("No manifest api key");

        this.httpClient = httpClient;
    }

    public async Task<DepotManifest> GetManifestAsync(int depotId, ulong manifestId)
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

        Console.WriteLine(url);

        using var stream = await httpClient.GetStreamAsync(url);
        return DepotManifest.Deserialize(stream);
    }
}
