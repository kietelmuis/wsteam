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
    public required string? Language { get; set; }
    public required string? OsList { get; set; }
    public required string? OsArch { get; set; }
}

public class Manifest
{
    public required string Gid { get; set; }
    public long Size { get; set; }
    public long Download { get; set; }
}

public class Depot
{
    public int? Dlcappid { get; set; }
    public required DepotConfig Config { get; set; }
    public required Dictionary<string, Manifest> Manifests { get; set; }
}

public class Branch
{
    public required string BuildId { get; set; }
    public long TimeUpdated { get; set; }
}
