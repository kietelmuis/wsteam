namespace wsteam.Models.SteamCMD;

public class SteamDepot
{
    public SteamManifests? manifests { get; set; }
}

public class SteamManifests
{
    public required SteamManifest @public { get; set; }
}

public class SteamManifest
{
    public required string gid { get; set; }
}
