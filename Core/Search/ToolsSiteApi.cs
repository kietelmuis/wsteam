using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed partial class ToolsSiteApi : ISearchApi
{
    private readonly HttpClient httpClient;

    public ToolsSiteApi(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.httpClient.BaseAddress ??= new Uri("https://steamtools.site/");
    }

    public async Task<List<SearchResult>> GetAppResultsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var response = await httpClient.GetAsync(
            $"search?query={Uri.EscapeDataString(query)}"
        );

        var json = await response.Content.ReadFromJsonAsync<ToolsSiteSearchResponse>();
        return json?.Results ?? [];
    }

    private sealed class ToolsSiteSearchResponse
    {
        [JsonPropertyName("results")]
        public List<SearchResult>? Results { get; set; }
    }
}
