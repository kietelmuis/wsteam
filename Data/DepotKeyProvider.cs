using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;
using wsteam.Data.APIs;

public class DepotKeyProvider
{
    private DexLuaApi dexLuaApi;
    private LuaState state = LuaState.Create();

    private Dictionary<int, byte[]> depotKeys = [];

    public DepotKeyProvider(DexLuaApi dexLuaApi)
    {
        this.dexLuaApi = dexLuaApi;

        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            var depotId = context.GetArgument<int>(0);
            var depotKey = context.HasArgument(2)
                ? context.GetArgument<string>(2) : null;

            if (depotKey is null)
            {
                Console.WriteLine($"[lua] no depotKey found for depot {depotId}");
                return new(0);
            }

            depotKeys[depotId] = Convert.FromHexString(depotKey);
            return new(0);
        });
    }

    public async Task<byte[]?> GetDepotKeysAsync(uint appId, int depotId)
    {
        if (depotKeys.TryGetValue(depotId, out var cached))
            return cached;

        var lua = await dexLuaApi.DownloadLuaAsync(appId);
        await state.DoStringAsync(lua);

        if (!depotKeys.TryGetValue(depotId, out var depotKey)) return null;

        return depotKey;
    }
}
