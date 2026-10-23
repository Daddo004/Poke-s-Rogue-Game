namespace PokeRogue.Game.Preview;

/// <summary>
/// Minimal animated GIF89a writer for preview exports: one local 256-colour palette per frame
/// (exact when the frame has ≤256 colours, otherwise the most frequent colours plus nearest match).
/// </summary>
public static class GifEncoder
{
    /// <param name="frames">RGBA frames, row-major, all <paramref name="width"/>×<paramref name="height"/>.</param>
    /// <param name="delayCs">Delay per frame in hundredths of a second.</param>
    public static void Write(Stream output, int width, int height, IReadOnlyList<uint[]> frames, int delayCs)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
        w.Write("GIF89a"u8);
        w.Write((ushort)width);
        w.Write((ushort)height);
        w.Write((byte)0x00); // no global colour table
        w.Write((byte)0);
        w.Write((byte)0);

        // Loop forever (NETSCAPE2.0 application extension).
        w.Write([0x21, 0xFF, 0x0B]);
        w.Write("NETSCAPE2.0"u8);
        w.Write([0x03, 0x01, 0x00, 0x00, 0x00]);

        foreach (var frame in frames)
        {
            var (palette, indices) = Quantize(frame);

            w.Write([0x21, 0xF9, 0x04, 0x04]); // graphic control: disposal = do not dispose
            w.Write((ushort)delayCs);
            w.Write([0x00, 0x00]);

            w.Write((byte)0x2C);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)width);
            w.Write((ushort)height);
            w.Write((byte)0x87); // local colour table, 256 entries

            for (var i = 0; i < 256; i++)
            {
                var c = i < palette.Count ? palette[i] : 0u;
                w.Write((byte)(c & 0xFF));
                w.Write((byte)((c >> 8) & 0xFF));
                w.Write((byte)((c >> 16) & 0xFF));
            }

            WriteLzw(w, indices);
        }

        w.Write((byte)0x3B);
    }

    private static (List<uint> Palette, byte[] Indices) Quantize(uint[] frame)
    {
        var counts = new Dictionary<uint, int>();
        foreach (var px in frame)
        {
            var rgb = px & 0xFFFFFF;
            counts[rgb] = counts.GetValueOrDefault(rgb) + 1;
        }

        var palette = counts.OrderByDescending(kv => kv.Value).Take(256).Select(kv => kv.Key).ToList();
        var lookup = new Dictionary<uint, byte>(palette.Count);
        for (var i = 0; i < palette.Count; i++)
            lookup[palette[i]] = (byte)i;

        var indices = new byte[frame.Length];
        for (var i = 0; i < frame.Length; i++)
        {
            var rgb = frame[i] & 0xFFFFFF;
            if (!lookup.TryGetValue(rgb, out var index))
                lookup[rgb] = index = Nearest(palette, rgb);
            indices[i] = index;
        }

        return (palette, indices);
    }

    private static byte Nearest(List<uint> palette, uint rgb)
    {
        int r = (int)(rgb & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)((rgb >> 16) & 0xFF);
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Count; i++)
        {
            var p = palette[i];
            int dr = r - (int)(p & 0xFF), dg = g - (int)((p >> 8) & 0xFF), db = b - (int)((p >> 16) & 0xFF);
            var distance = dr * dr * 2 + dg * dg * 4 + db * db * 3;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return (byte)best;
    }

    private static void WriteLzw(BinaryWriter w, byte[] indices)
    {
        const int minCodeSize = 8;
        const int clearCode = 1 << minCodeSize;
        const int endCode = clearCode + 1;

        w.Write((byte)minCodeSize);
        var blocks = new SubBlockWriter(w);
        var dictionary = new Dictionary<int, int>();
        var codeSize = minCodeSize + 1;
        var next = endCode + 1;

        blocks.WriteCode(clearCode, codeSize);
        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var k = indices[i];
            var key = (prefix << 8) | k;
            if (dictionary.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }

            blocks.WriteCode(prefix, codeSize);
            if (next < 4096)
            {
                dictionary[key] = next++;
                if (next > 1 << codeSize && codeSize < 12)
                    codeSize++;
            }
            else
            {
                blocks.WriteCode(clearCode, codeSize);
                dictionary.Clear();
                codeSize = minCodeSize + 1;
                next = endCode + 1;
            }

            prefix = k;
        }

        blocks.WriteCode(prefix, codeSize);
        blocks.WriteCode(endCode, codeSize);
        blocks.Flush();
        w.Write((byte)0);
    }

    private sealed class SubBlockWriter(BinaryWriter writer)
    {
        private readonly byte[] _block = new byte[255];
        private int _blockLength;
        private int _bitBuffer;
        private int _bitCount;

        public void WriteCode(int code, int size)
        {
            _bitBuffer |= code << _bitCount;
            _bitCount += size;
            while (_bitCount >= 8)
            {
                Emit((byte)(_bitBuffer & 0xFF));
                _bitBuffer >>= 8;
                _bitCount -= 8;
            }
        }

        public void Flush()
        {
            if (_bitCount > 0)
                Emit((byte)(_bitBuffer & 0xFF));
            _bitBuffer = 0;
            _bitCount = 0;
            if (_blockLength > 0)
            {
                writer.Write((byte)_blockLength);
                writer.Write(_block, 0, _blockLength);
                _blockLength = 0;
            }
        }

        private void Emit(byte value)
        {
            _block[_blockLength++] = value;
            if (_blockLength == 255)
            {
                writer.Write((byte)255);
                writer.Write(_block);
                _blockLength = 0;
            }
        }
    }
}
