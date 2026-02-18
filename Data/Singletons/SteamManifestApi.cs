namespace wsteam.Data.Singletons;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using SteamKit2;
using wsteam.Data.Manifests;

public sealed class SteamManifestApi : ILuaApi, IManifestApi
{
    private readonly HttpClient httpClient;

    private readonly MemoryCache zipCache = new("sm-zip");
    private static readonly TimeSpan ZipCacheTtl = TimeSpan.FromMinutes(30);

    public SteamManifestApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://steammanifest.com/");
        this.httpClient = httpClient;
    }

    private async Task<byte[]?> GetZipBytesAsync(uint appId)
    {
        var key = appId.ToString();

        if (zipCache.Get(key) is byte[] cached)
            return cached;

        var zipResponse = await httpClient.GetAsync(
            $"proxy.php?url=https://steamgames554.s3.us-east-1.amazonaws.com/{appId}.zip"
        );

        if (!zipResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"SteamManifest error fetching zip for app {appId}: {zipResponse.StatusCode}");
            return null;
        }

        var zipBytes = await zipResponse.Content.ReadAsByteArrayAsync();

        zipCache.Set(
            key,
            zipBytes,
            new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.Add(ZipCacheTtl),
            }
        );

        return zipBytes;
    }

    private async Task<ZipArchive?> OpenZipAsync(uint appId)
    {
        var zipBytes = await GetZipBytesAsync(appId).ConfigureAwait(false);
        if (zipBytes is null) return null;

        var stream = new MemoryStream(zipBytes, false);
        return new ZipArchive(stream, ZipArchiveMode.Read, false);
    }

    public async Task<string?> GetLuaAsync(uint appId)
    {
        using var zip = await OpenZipAsync(appId);
        if (zip is null) return null;

        var luaEntry =
            zip.Entries.First(e => e.Name.Contains("lua"));

        if (luaEntry is null)
        {
            Console.WriteLine($"SteamManifest: no lua file found in zip for app {appId}");
            return null;
        }

        await using var luaStream = luaEntry.Open();
        using var reader = new StreamReader(luaStream);

        return await reader.ReadToEndAsync();
    }

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        using var zip = await OpenZipAsync(appId);
        if (zip is null) return null;

        var entry =
            zip.Entries.First(e =>
                e.Name.Contains("manifest") &&
                e.Name.Contains(manifestId.ToString())
            );

        if (entry is null)
        {
            Console.WriteLine($"SteamManifest: no manifest entry found in app {appId} zip (depot {depotId}, manifest {manifestId})");
            return null;
        }

        try
        {
            await using var manifestStream = entry.Open();
            var manifest = DepotManifest.Deserialize(manifestStream);

            if (manifest.DepotID != depotId)
            {
                Console.WriteLine(
                    $"SteamManifest: manifest entry '{entry.FullName}' deserialized to depot {manifest.DepotID}, expected {depotId}"
                );
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SteamManifest: failed to deserialize manifest from entry '{entry.FullName}': {ex.Message}");
            return null;
        }
    }
}
