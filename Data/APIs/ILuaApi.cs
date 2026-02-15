using System.Threading.Tasks;

public interface ILuaApi
{
    public Task<string?> GetLuaAsync(uint appId);
}
