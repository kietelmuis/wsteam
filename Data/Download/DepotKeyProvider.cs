using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;

public class DepotKeyProvider
{
    private ILuaApi luaApi;
    private LuaState state = LuaState.Create();

    private Dictionary<uint, byte[]> depotKeys = [];

    public DepotKeyProvider(ILuaApi luaApi)
    {
        this.luaApi = luaApi;

        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            var depotId = context.GetArgument<int>(0);
            var depotKey = context.HasArgument(2)
                ? context.GetArgument<string>(2) : null;

            if (depotKey is null) return new(0);

            depotKeys[(uint)depotId] = Convert.FromHexString(depotKey);
            return new(0);
        });
    }

    public async Task<byte[]?> GetDepotKeysAsync(uint appId, uint depotId)
    {
        if (depotKeys.TryGetValue(depotId, out var cached))
            return cached;

        var lua = await luaApi.GetLuaAsync(appId)
            ?? throw new Exception("Could not get lua");
        await state.DoStringAsync(lua);

        if (!depotKeys.TryGetValue(depotId, out var depotKey))
        {
            Console.WriteLine($"[provider] could not get depotKey for depot {depotId}");
            return null;
        }

        return depotKey;
    }
}
