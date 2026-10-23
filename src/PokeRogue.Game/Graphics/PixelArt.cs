using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>Builds small textures from text patterns: each character maps to a colour ('.' or ' ' = transparent).</summary>
public static class PixelArt
{
    public static Texture2D FromPattern(GraphicsDevice device, IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette)
    {
        var width = rows.Max(r => r.Length);
        var pixels = new Color[width * rows.Count];
        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
                pixels[y * width + x] = palette.TryGetValue(rows[y][x], out var c) ? c : Color.Transparent;
        }

        var texture = new Texture2D(device, width, rows.Count);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>
    /// Like <see cref="FromPattern"/>, plus a one-pixel <paramref name="outline"/> around every coloured pixel
    /// (8 neighbours), so small symbols read on any background. The texture is two pixels wider and taller.
    /// </summary>
    public static Texture2D FromPatternOutlined(GraphicsDevice device, IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette, Color outline)
    {
        var width = rows.Max(r => r.Length) + 2;
        var height = rows.Count + 2;
        var fill = new Color[width * height];
        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
                fill[(y + 1) * width + x + 1] = palette.TryGetValue(rows[y][x], out var c) ? c : Color.Transparent;
        }

        var pixels = (Color[])fill.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (fill[y * width + x].A > 0)
                    continue;

                for (var n = 0; n < 9; n++)
                {
                    int nx = x + n % 3 - 1, ny = y + n / 3 - 1;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && fill[ny * width + nx].A > 0)
                    {
                        pixels[y * width + x] = outline;
                        break;
                    }
                }
            }
        }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    private static readonly float[,] Bayer4 =
    {
        { 0f, 8f, 2f, 10f },
        { 12f, 4f, 14f, 6f },
        { 3f, 11f, 1f, 9f },
        { 15f, 7f, 13f, 5f },
    };

    /// <summary>Tileable grass: smooth value-noise patches in three tones with ordered dithering, small tufts and rare flowers.</summary>
    public static Texture2D Grass(GraphicsDevice device, int size, int seed)
    {
        var random = new Random(seed);
        Color[] tones = [new(76, 138, 66), new(88, 152, 72), new(102, 164, 80)];
        var tuft = new Color(62, 118, 56);
        var tuftLight = new Color(124, 184, 94);
        var pixels = new Color[size * size];

        const int cells = 6;
        var lattice = new float[cells, cells];
        for (var y = 0; y < cells; y++)
            for (var x = 0; x < cells; x++)
                lattice[x, y] = Hash(x, y, seed) % 1000 / 1000f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var n = ValueNoise(lattice, cells, x * cells / (float)size, y * cells / (float)size);
                var threshold = (Bayer4[y % 4, x % 4] / 16f - 0.5f) * 0.14f;
                var v = n + threshold;
                pixels[y * size + x] = tones[v < 0.4f ? 0 : v < 0.62f ? 1 : 2];
            }
        }

        for (var i = 0; i < size * size / 220; i++)
        {
            var x = random.Next(size);
            var y = random.Next(size);
            pixels[y * size + x] = tuft;
            pixels[(y + size - 1) % size * size + (x + size - 1) % size] = tuft;
            pixels[(y + size - 1) % size * size + (x + 1) % size] = tuft;
            if (random.Next(4) == 0)
                pixels[(y + size - 2) % size * size + x] = tuftLight;
        }

        Color[] flowers = [new(248, 248, 232), new(248, 200, 64), new(232, 112, 136)];
        for (var i = 0; i < size * size / 2600; i++)
        {
            var x = random.Next(1, size - 1);
            var y = random.Next(1, size - 1);
            var c = flowers[random.Next(flowers.Length)];
            pixels[y * size + x - 1] = c;
            pixels[y * size + x + 1] = c;
            pixels[(y - 1) * size + x] = c;
            pixels[(y + 1) * size + x] = c;
            pixels[y * size + x] = new Color(248, 224, 96);
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    private static float ValueNoise(float[,] lattice, int cells, float x, float y)
    {
        int x0 = (int)x, y0 = (int)y;
        float fx = Smooth(x - x0), fy = Smooth(y - y0);
        float a = lattice[x0 % cells, y0 % cells], b = lattice[(x0 + 1) % cells, y0 % cells];
        float c = lattice[x0 % cells, (y0 + 1) % cells], d = lattice[(x0 + 1) % cells, (y0 + 1) % cells];
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }
}
