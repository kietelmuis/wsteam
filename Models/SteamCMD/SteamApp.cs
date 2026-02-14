using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wsteam.Models.SteamCMD;

public class SteamApp
{
    public required DepotsContainer depots { get; set; }
    public required SteamConfig config { get; set; }
}

public class DepotsContainer
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> OtherDepots { get; set; }

    [JsonIgnore]
    public Dictionary<string, SteamDepot> DepotObjects
    {
        get
        {
            if (OtherDepots == null) return [];

            return OtherDepots
                .Where(kvp => kvp.Value.ValueKind == JsonValueKind.Object)
                .Where(kvp => kvp.Value.TryGetProperty("config", out _) ||
                             kvp.Value.TryGetProperty("manifests", out _))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => JsonSerializer.Deserialize<SteamDepot>(kvp.Value.GetRawText())
                );
        }
    }
}
