using SteamKit2.CDN;
using static SteamKit2.DepotManifest;

namespace wsteam.Models.Steam;

public class DepotData
{
    public uint DepotId { get; set; }
    public uint ManifestId { get; set; }
    public required ChunkData[] Chunks { get; set; }
    public required byte[] DepotKey { get; set; }
}
