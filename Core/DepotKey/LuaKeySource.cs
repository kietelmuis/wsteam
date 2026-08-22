namespace wsteam.Core.DepotKey;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Lua;

public class LuaKeySource : IDepotKeySource, IDisposable
{
    private readonly LuaState state = LuaState.Create();
    private readonly SemaphoreSlim luaLock = new(1, 1);
    private readonly ConcurrentDictionary<uint, byte[]> tempKeys = new();

    public bool Available => true;

    public LuaKeySource()
    {
        state.Environment["addappid"] = new LuaFunction((context, ct) =>
        {
            if (!context.HasArgument(0))
                return new(0);

            double rawDepotId = context.GetArgument<double>(0);
            var depotId = (uint)rawDepotId;

            if (!context.HasArgument(2))
                return new(0);

            string? depotKey;
            try
            {
                depotKey = context.GetArgument<string>(2);
            }
            catch (LuaRuntimeException)
            {
                Console.WriteLine($"[luakeysrc] addappid: argument #3 is not a string for depot {depotId}");
                return new(0);
            }

            if (string.IsNullOrEmpty(depotKey))
                return new(0);

            try
            {
                Console.WriteLine($"[luakeysrc] adding depot key for depot {depotId}");
                tempKeys[depotId] = Convert.FromHexString(depotKey);
            }
            catch (FormatException)
            {
                Console.WriteLine($"[luakeysrc] addappid: invalid hex string for depot {depotId}");
            }

            return new(0);
        });
        state.Environment["addtoken"] = new LuaFunction((context, ct) =>
            new(0)
        );
        state.Environment["setManifestid"] = new LuaFunction((context, ct) =>
            new(0)
        );
    }

    public async Task RunLuaAsync(string lua)
    {
        await luaLock.WaitAsync();
        try
        {
            await state.DoStringAsync(lua);
        }
        finally
        {
            luaLock.Release();
        }
    }

    public async Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId)
    {
        tempKeys.TryGetValue(depotId, out var key);
        return key;
    }

    public void Dispose()
    {
        state.Dispose();
        luaLock.Dispose();
    }
}
