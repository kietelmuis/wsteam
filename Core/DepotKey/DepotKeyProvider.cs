namespace wsteam.Core.DepotKey;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DepotKeyProvider
{
    private readonly IDepotKeySource[] sources;
    private readonly Dictionary<uint, byte[]> depotKeys = [];
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public DepotKeyProvider(params IDepotKeySource[] sources)
    {
        this.sources = sources;
    }

    public async Task<byte[]?> GetDepotKeyAsync(uint appId, uint depotId)
    {
        if (depotKeys.TryGetValue(depotId, out var cached))
            return cached;

        await semaphore.WaitAsync();
        try
        {
            if (depotKeys.TryGetValue(depotId, out var cached2))
                return cached2;

            foreach (var source in sources)
            {
                Console.WriteLine($"[depotkey] Trying source {source.GetType().Name} for app {appId}, depot {depotId}");

                var key = await source.GetDepotKeyAsync(appId, depotId);
                if (key is not null)
                {
                    depotKeys[depotId] = key;
                    return key;
                }
            }

            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }
}
