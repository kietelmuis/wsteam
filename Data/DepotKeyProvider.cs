using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;
using wsteam.Data.APIs;

public class DepotKeyProvider
{
    private DexLuaApi dexLuaApi;
    private LuaState state = LuaState.Create();

    private Dictionary<uint, string> appKeys = [];

    public DepotKeyProvider(DexLuaApi dexLuaApi)
    {
        this.dexLuaApi = dexLuaApi;

        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            var depotId = context.GetArgument<uint>(0);
            var depotKey = context.GetArgument<string?>(2);

            if (depotKey is null)
            {
                Console.WriteLine($"[lua] no depotKey found for depot {depotId}");
                return new(0);
            }

            appKeys[depotId] = depotKey;
            return new(0);
        });
    }

    public async Task<string> GetDepotKeysAsync(uint appId, uint depotId)
    {
        if (appKeys.TryGetValue(depotId, out var cached))
            return cached;

        var lua = await dexLuaApi.DownloadLuaAsync(appId);
        await state.DoStringAsync(lua);

        return appKeys[depotId]
            ?? throw new Exception("Depot key was not found");
    }
}
