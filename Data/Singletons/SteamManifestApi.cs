namespace wsteam.Data.Singletons;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SteamKit2;
using wsteam.Data.Manifests;

public sealed class SteamManifestApi : ILuaApi, IManifestApi
{
    private readonly HttpClient httpClient;
    private readonly IMemoryCache memoryCache;

    public SteamManifestApi(HttpClient httpClient, IMemoryCache memoryCache)
    {
        httpClient.BaseAddress = new Uri("https://steammanifest.com/");
        this.httpClient = httpClient;
        this.memoryCache = memoryCache;
    }

    private async Task<byte[]?> GetZipBytesAsync(uint appId)
    {
        var key = appId.ToString();

        if (memoryCache.Get(key) is byte[] cached)
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

        memoryCache.Set(
            key,
            zipBytes
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
            zip.Entries.FirstOrDefault(e => e.Name.Contains("lua")) ?? throw new Exception("Lua not found in zip");

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
            zip.Entries.FirstOrDefault(e =>
                e.Name.Contains("manifest") &&
                e.Name.Contains(manifestId.ToString())
            ) ?? throw new Exception($"Manifest {manifestId} (depot {depotId}) was not found in zip");

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
