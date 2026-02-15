using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using SteamKit2;

public class FileWriter : IDisposable
{
    private readonly SafeFileHandle file;

    public FileWriter(string fileDirectory)
    {
        var directory = Path.GetDirectoryName(fileDirectory)
            ?? throw new DirectoryNotFoundException("Invalid fileDirectory");

        Directory.CreateDirectory(directory);

        file = File.OpenHandle(
            fileDirectory,
            FileMode.Create,
            FileAccess.Write
        );
    }

    public async Task WriteChunkAsync(DepotManifest.ChunkData chunk, Memory<byte> data)
    {
        var offset = (long)checked(chunk.Offset);
        await RandomAccess.WriteAsync(file, data, offset);
    }

    public void Flush() => RandomAccess.FlushToDisk(file);

    public void Dispose()
    {
        file.Dispose();
    }
}
