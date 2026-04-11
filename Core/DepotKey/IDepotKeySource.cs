namespace wsteam.Core.DepotKey;

using System.Threading.Tasks;

public interface IDepotKeySource
{
    Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId);
}
