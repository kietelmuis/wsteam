using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class SearchResult
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("id")]
    public required uint Id { get; set; }
}

public interface ISearchApi
{
    public Task<List<SearchResult?>> GetAppResultsAsync(string query);
}
