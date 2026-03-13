using System.Collections.Generic;
using System.Threading.Tasks;
using SteamKit2;

namespace wsteam.Data.Manifests;

public interface IBatchManifestApi : IManifestApi
{
    public Task<Dictionary<uint, DepotManifest?>?> GetManifestsAsync(uint appId);
}
