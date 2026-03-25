namespace wsteam.Data.DepotKey;

using System.Threading.Tasks;

public interface IDepotKeySource
{
    Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId);
}
