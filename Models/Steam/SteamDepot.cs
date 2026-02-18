namespace wsteam.Models.Steam;

public class SteamDepot
{
    public DepotManifests? manifests { get; set; }
    public DepotConfig? config { get; set; }
}

public class DepotConfig
{
    public required string? language { get; set; }
    public required string? oslist { get; set; }
}

public class DepotManifests
{
    public required DepotManifest @public { get; set; }
}

public class DepotManifest
{
    public required string gid { get; set; }
}
