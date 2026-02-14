using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;
using wsteam.Data.APIs;

public class DepotKeyProvider
{
    private DexLuaApi dexLuaApi;
    private LuaState state = LuaState.Create();

    private Dictionary<uint, byte[]> depotKeys = [];

    public DepotKeyProvider(DexLuaApi dexLuaApi)
    {
        this.dexLuaApi = dexLuaApi;

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

        var lua = await dexLuaApi.DownloadLuaAsync(appId);
        await state.DoStringAsync(lua);

        if (!depotKeys.TryGetValue(depotId, out var depotKey))
        {
            Console.WriteLine($"[provider] could not get depotKey for depot {depotId}");
            return null;
        }

        return depotKey;
    }
}
