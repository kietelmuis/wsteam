using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class ToolsSiteApi : ISearchApi
{
    private HttpClient httpClient;

    public ToolsSiteApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://steamtools.site/");

        this.httpClient = httpClient;
    }

    public async Task<List<SearchResult>> GetAppResultsAsync(string query)
    {
        var response = await httpClient.GetAsync($"search?query={query}");
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>()
            ?? throw new InvalidOperationException("Failed to read JSON response");

        if (!json.RootElement.TryGetProperty("results", out var resultsEl) ||
        resultsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return resultsEl.Deserialize<List<SearchResult>>() ?? [];
    }
}
