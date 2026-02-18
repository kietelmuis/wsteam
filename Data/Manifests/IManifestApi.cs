using System.Threading.Tasks;
using SteamKit2;

namespace wsteam.Data.Manifests;

public interface IManifestApi
{
    public Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId);
}
