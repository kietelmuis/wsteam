namespace wsteam.Models.Steam;

public class SteamApp
{
    public required uint AppId { get; set; }
    public required SteamDepots Depots { get; set; }
    public required SteamConfig Config { get; set; }
}
