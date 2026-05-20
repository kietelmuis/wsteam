namespace wsteam.Core.DepotKey;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lua;

public class LuaKeySource : IDepotKeySource
{
    private readonly LuaState state = LuaState.Create();
    private readonly Dictionary<uint, byte[]> tempKeys = [];

    public bool Available => true;

    public LuaKeySource()
    {
        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            var depotId = (uint)context.GetArgument<int>(0);
            var depotKey = context.HasArgument(2)
                ? context.GetArgument<string>(2)
                : null;

            if (depotKey is null) return new(0);

            Console.WriteLine($"[luakeysrc] adding depot key for depot {depotId}");
            tempKeys[depotId] = Convert.FromHexString(depotKey);
            return new(0);
        });
        state.Environment["setManifestid"] = new LuaFunction((context, ct) =>
            new(0)
        );
    }

    public async Task RunLuaAsync(string lua)
        => await state.DoStringAsync(lua);

    public async Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId)
    {
        tempKeys.TryGetValue(depotId, out var key);
        return key;
    }
}
