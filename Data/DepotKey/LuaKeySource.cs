namespace wsteam.Data.DepotKey;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;

public class LuaKeySource : IDepotKeySource
{
    private readonly ILuaApi luaApi;
    private readonly LuaState state = LuaState.Create();
    private readonly Dictionary<uint, byte[]> tempKeys = [];

    public LuaKeySource(ILuaApi luaApi)
    {
        this.luaApi = luaApi;
        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            var depotId = (uint)context.GetArgument<int>(0);
            var depotKey = context.HasArgument(2)
                ? context.GetArgument<string>(2)
                : null;

            if (depotKey is null) return new(0);

            tempKeys[depotId] = Convert.FromHexString(depotKey);
            return new(0);
        });
    }

    public async Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId)
    {
        tempKeys.Clear();
        var lua = await luaApi.GetLuaAsync(appId);
        if (lua == null) return null;

        await state.DoStringAsync(lua);
        tempKeys.TryGetValue(depotId, out var key);
        return key;
    }
}
