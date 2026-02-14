using System;
using System.IO;
using System.Threading.Tasks;
using SteamKit2;

public class FileWriter(string fileDirectory) : IDisposable
{
    private readonly FileStream file = File.Create(fileDirectory);

    public async Task WriteChunkAsync(DepotManifest.ChunkData chunk, byte[] data)
    {
        var offset = (long)checked(chunk.Offset);
        var size = (int)checked(chunk.UncompressedLength);

        file.Seek(offset, SeekOrigin.Begin);
        await file.WriteAsync(
            data.AsMemory(0, size)
        );

        Console.WriteLine($"Written chunk {chunk.ChunkID} of {data.Length}");
    }

    public Task FlushAsync() => file.FlushAsync();

    public void Dispose()
    {
        file.Dispose();
    }
}
