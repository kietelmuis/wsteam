using System;

namespace wsteam.Core.Common;

public static class Adler
{
    private const uint ModAdler = 65521;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;

        foreach (byte value in data)
        {
            a += value;
            if (a >= ModAdler) a -= ModAdler;

            b += a;
            if (b >= ModAdler) b -= ModAdler;
        }

        return (b << 16) | a;
    }
}
