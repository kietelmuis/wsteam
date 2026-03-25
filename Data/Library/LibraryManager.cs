using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class LibraryManager
{
    private Dictionary<uint, string> library = [];

    private readonly string libraryFile = Path.Combine(Directory.GetCurrentDirectory(), "library.json");

    public LibraryManager()
    {
        if (File.Exists(libraryFile))
        {
            var json = File.ReadAllText(libraryFile);
            library = JsonSerializer.Deserialize<Dictionary<uint, string>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize library file");
        }
        else
        {
            File.Create(libraryFile);
        }
    }

    private async Task Save()
        => await File.WriteAllTextAsync(libraryFile, JsonSerializer.Serialize(library));

    public async Task AddApp(uint appId, string appDirectory)
    {
        library[appId] = appDirectory;
        await Save();
    }
}
