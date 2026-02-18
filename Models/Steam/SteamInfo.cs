using System.Collections.Generic;

namespace wsteam.Models.Steam;

public class SteamInfo
{
    public required Dictionary<int, SteamApp> data { get; set; }
    public required string status { get; set; }
}
