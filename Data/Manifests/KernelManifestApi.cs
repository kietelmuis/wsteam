namespace wsteam.Data.Manifests;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class KernelManifestApi : ILuaApi
{
    private HttpClient httpClient;

    public KernelManifestApi(HttpClient httpClient)
    {
        httpClient.BaseAddress = new Uri("https://kernelos.org/");

        this.httpClient = httpClient;
    }

    public async Task<string?> GetLuaAsync(uint appId)
    {
        using var downloadResponse = await httpClient.GetAsync($"games/download.php?gen=1&id={appId}");
        var jsonResponse = await downloadResponse.Content.ReadFromJsonAsync<JsonElement>();

        if (!downloadResponse.IsSuccessStatusCode)
        {
            var error = jsonResponse.GetProperty("error").ToString();
            Console.WriteLine($"KernelManifest error: {error}");
            return null;
        }

        var url = jsonResponse.GetProperty("url").ToString();
        using var manifestResponse = await httpClient.GetAsync(url);

        if (!manifestResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("KernelManifest url error");
            return null;
        }

        using var stream = await manifestResponse.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream);

        Console.WriteLine($"[zip] found {zip.Entries.Count()} zip entries");

        var luaFile = zip.Entries.First(z => z.Name.Contains("lua"))
            ?? throw new Exception("Could not find lua file");

        Console.WriteLine($"[zip] found lua file {luaFile.Name}");

        using var luaStream = await luaFile.OpenAsync();
        using var luaReader = new StreamReader(luaStream);
        return await luaReader.ReadToEndAsync();
    }
}
