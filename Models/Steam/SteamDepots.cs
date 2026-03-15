using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace wsteam.Models.Steam;

public class SteamDepots
{
    [JsonProperty("baselanguages")]
    public string? BaseLanguages { get; set; }

    [JsonProperty("branches")]
    public Dictionary<string, Branch>? Branches { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JToken>? RawDepots { get; set; }

    [JsonIgnore]
    public Dictionary<string, Depot> Depots => RawDepots?
        .Where(kvp => long.TryParse(kvp.Key, out _))
        .ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToObject<Depot>()!
        ) ?? [];
}

public class DepotConfig
{
    [JsonProperty("language")]
    public string? Language { get; set; }

    [JsonProperty("oslist")]
    public string? OsList { get; set; }

    [JsonProperty("osarch")]
    public string? OsArch { get; set; }
}

public class Manifest
{
    [JsonProperty("gid")]
    public required string Gid { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("download")]
    public long Download { get; set; }
}

public class Depot
{
    [JsonProperty("dlcappid")]
    public int? DlcAppId { get; set; }

    [JsonProperty("config")]
    public DepotConfig? Config { get; set; }

    [JsonProperty("manifests")]
    public Dictionary<string, Manifest>? Manifests { get; set; }
}

public class Branch
{
    [JsonProperty("buildid")]
    public required string BuildId { get; set; }

    [JsonProperty("timeupdated")]
    public long TimeUpdated { get; set; }
}
