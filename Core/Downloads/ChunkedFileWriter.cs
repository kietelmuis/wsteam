using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using SteamKit2;

namespace wsteam.Core.Downloads;

public class ChunkedFile : IDisposable
{
    private readonly SafeFileHandle file;
    public readonly string fileName;
    public readonly bool alreadyExists;

    public ChunkedFile(string fileDirectory, bool alreadyExists)
    {
        var directory = Path.GetDirectoryName(fileDirectory)
            ?? throw new DirectoryNotFoundException("Invalid fileDirectory");

        fileName = Path.GetFileName(fileDirectory);
        Directory.CreateDirectory(directory);

        this.alreadyExists = alreadyExists;
        file = File.OpenHandle(
            fileDirectory,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read
        );
    }

    public async Task WriteChunkAsync(DepotManifest.ChunkData chunk, Memory<byte> data)
    {
        var offset = (long)checked(chunk.Offset);
        await RandomAccess.WriteAsync(file, data, offset);
    }

    public async Task<int> ReadChunkAsync(DepotManifest.ChunkData chunk, Memory<byte> data)
    {
        var offset = (long)checked(chunk.Offset);
        return await RandomAccess.ReadAsync(file, data, offset);
    }

    public void Flush()
        => RandomAccess.FlushToDisk(file);

    public void Dispose()
        => file.Dispose();
}
