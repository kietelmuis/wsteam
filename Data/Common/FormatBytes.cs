using System;

public static class ByteSizeFormatter
{
    public static string FormatBytes(ulong bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        if (bytes == 0)
            return "0 B";

        int order = (int)Math.Floor(Math.Log(bytes, 1024));
        double num = bytes / Math.Pow(1024, order);

        return $"{num:0.##} {sizes[order]}";
    }
}
