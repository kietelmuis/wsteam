using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wsteam.Models.Steam;

public class SteamDepots
{
    [JsonPropertyName("baselanguages")]
    public string? BaseLanguages { get; set; }

    [JsonPropertyName("branches")]
    public Dictionary<string, Branch>? Branches { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? RawDepots { get; set; }
}

public class DepotConfig
{
    public required string Language { get; set; }
    public required string OsList { get; set; }
    public required string OsArch { get; set; }
}

public class Manifest
{
    public required string Gid { get; set; }
    public long Size { get; set; }
    public long Download { get; set; }
}

public class Depot
{
    public required DepotConfig Config { get; set; }
    public required Dictionary<string, Manifest> Manifests { get; set; }
}

public class Branch
{
    public required string BuildId { get; set; }
    public long TimeUpdated { get; set; }
}
