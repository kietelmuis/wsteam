using System.Threading.Tasks;
using SteamKit2;

public interface IManifestApi
{
    public Task<DepotManifest?> GetManifestAsync(uint depotId, ulong manifestId);
}
