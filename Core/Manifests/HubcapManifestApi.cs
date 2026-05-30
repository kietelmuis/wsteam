namespace wsteam.Core.Manifests;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using wsteam.Core.Common;
using wsteam.Core.DepotKey;

/// <summary>
/// Provides up-to-date manifests, limited to 25 manifests a day
/// </summary>
public class HubcapManifestApi : IManifestApi
{
    public HubcapManifestApi(HttpClient httpClient, LuaKeySource luaSrc, ApiKeyHolder apiKeyHolder)
    {
        httpClient.BaseAddress = new Uri("https://hubcapmanifest.com/api/v1/");

        this.httpClient = httpClient;
        this.luaSrc = luaSrc;
        this.apiKeyHolder = apiKeyHolder;
        this.manifestCache = Path.Combine(Directory.GetCurrentDirectory(), "manifests", "steam", "hubcap");
        Directory.CreateDirectory(manifestCache);
    }

    private readonly string manifestCache;
    private readonly HttpClient httpClient;
    private readonly LuaKeySource luaSrc;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly ApiKeyHolder apiKeyHolder;

    public bool Available => apiKeyHolder.GetApiKey(ManifestSource.Hubcap) is not null;

    public async Task<DepotManifest?> GetManifestAsync(uint appId, uint depotId, ulong manifestId)
    {
        var apiKey = apiKeyHolder.GetApiKey(ManifestSource.Hubcap)
            ?? throw new InvalidOperationException("No hubcap api key");

        if (!await HubcapHealth())
            throw new InvalidOperationException("Hubcap manifest API is not healthy");

        var fileDirectory = Path.Combine(manifestCache, appId.ToString());
        Directory.CreateDirectory(fileDirectory);

        var fileLocation = Path.Combine(fileDirectory, $"{depotId}.manifest");
        var luaFileLocation = Path.Combine(fileDirectory, $"{appId}.lua");

        if (File.Exists(luaFileLocation))
        {
            Console.WriteLine($"Loading lua script from {luaFileLocation}");
            await luaSrc.RunLuaAsync(File.ReadAllText(luaFileLocation));
        }

        if (File.Exists(fileLocation))
        {
            Console.WriteLine($"Loading manifest from cache for depot {depotId}");
            return DepotManifest.LoadFromFile(fileLocation);
        }

        await semaphore.WaitAsync();
        try
        {
            if (File.Exists(fileLocation))
                return DepotManifest.LoadFromFile(fileLocation);

            using var response = await httpClient.GetAsync($"manifest/{appId}?api_key={apiKey}");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Hubcap key invalid or expired");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("Hubcap daily limit exceeded");
                    return null;
                }

                Console.WriteLine($"Hubcap error {response.ReasonPhrase} (app {appId}): {response.ReasonPhrase}");
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var zip = new ZipArchive(stream);

            string? luaScript = null;
            var lua = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));
            if (lua is not null)
            {
                Console.WriteLine($"Saving lua script to {luaFileLocation} (from {lua.Name})");

                using var luaStream = lua.Open();
                using var streamReader = new StreamReader(luaStream);
                luaScript = await streamReader.ReadToEndAsync();

                await File.WriteAllTextAsync(luaFileLocation, luaScript);
            }

            DepotManifest? requestedManifest = null;
            foreach (var m in zip.Entries.Where(e => e.Name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)))
            {
                using var manifestStream = m.Open();
                var manifest = DepotManifest.Deserialize(manifestStream);
                var filePath = Path.Combine(fileDirectory, $"{manifest.DepotID}.manifest");

                Directory.CreateDirectory(fileDirectory);
                manifest.SaveToFile(filePath);

                if (manifest.DepotID == depotId)
                    requestedManifest = manifest;
            }

            if (luaScript is not null)
            {
                Console.WriteLine($"Loading lua script from {luaFileLocation}");
                await luaSrc.RunLuaAsync(luaScript);
            }

            return requestedManifest;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> HubcapHealth()
    {
        using var response = await httpClient.GetAsync($"health");
        return response.IsSuccessStatusCode;
    }
}
