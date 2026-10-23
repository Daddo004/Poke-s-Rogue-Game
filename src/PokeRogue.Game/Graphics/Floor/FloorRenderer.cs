using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>A raised decoration standing on the floor, drawn y-sorted with entities.</summary>
public readonly record struct FloorProp(Texture2D Texture, Vector2 Feet, int ShadowWidth);

/// <summary>
/// Infinite, non-repeating procedural ground. Everything is a pure function of world coordinates,
/// so 128 px chunks are generated on demand, cached, evicted, and always join seamlessly.
/// </summary>
public sealed class FloorRenderer : IDisposable
{
    public const int ChunkSize = 128;
    private const int TallGrassTile = 16;
    private const int PropCell = 64;
    private const int MaxCachedChunks = 64;

    private static readonly float[,] Bayer4 =
    {
        { 0f, 8f, 2f, 10f },
        { 12f, 4f, 14f, 6f },
        { 3f, 11f, 1f, 9f },
        { 15f, 7f, 13f, 5f },
    };

    private readonly GraphicsDevice _device;
    private readonly Dictionary<(int X, int Y), (Texture2D Texture, long LastUsed)> _chunks = [];
    private readonly Color[] _tallGrassTile;
    private readonly Texture2D[] _props;
    private long _frame;

    public FloorRenderer(GraphicsDevice device, FloorStyle style)
    {
        _device = device;
        Style = style;
        _tallGrassTile = BuildTallGrassTile();
        _props = [FloorProps.Rock(device), FloorProps.Bush(device), FloorProps.Stump(device)];
    }

    public FloorStyle Style { get; }

    public int CachedChunks => _chunks.Count;

    /// <summary>Draws the chunks covering <paramref name="visibleWorld"/>; call inside a batch using the camera transform.</summary>
    public void Draw(SpriteBatch batch, Rectangle visibleWorld)
    {
        _frame++;
        var x0 = FloorDiv(visibleWorld.Left, ChunkSize);
        var y0 = FloorDiv(visibleWorld.Top, ChunkSize);
        var x1 = FloorDiv(visibleWorld.Right - 1, ChunkSize);
        var y1 = FloorDiv(visibleWorld.Bottom - 1, ChunkSize);

        for (var cy = y0; cy <= y1; cy++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                if (!_chunks.TryGetValue((cx, cy), out var chunk))
                    chunk = (GenerateChunk(cx, cy), 0);

                _chunks[(cx, cy)] = (chunk.Texture, _frame);
                batch.Draw(chunk.Texture, new Vector2(cx * ChunkSize, cy * ChunkSize), Color.White);
            }
        }

        Prefetch(x0 - 1, y0 - 1, x1 + 1, y1 + 1);
        Evict();
    }

    /// <summary>
    /// Keeps the ring of chunks just outside the screen ready, generating at most one per frame. Without it, walking
    /// brought a whole row of chunks into view at once and generated them all in the same frame (a ~20 ms hitch).
    /// </summary>
    private void Prefetch(int x0, int y0, int x1, int y1)
    {
        var generated = false;
        for (var cy = y0; cy <= y1; cy++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                if (_chunks.TryGetValue((cx, cy), out var chunk))
                {
                    _chunks[(cx, cy)] = (chunk.Texture, _frame);
                }
                else if (!generated)
                {
                    _chunks[(cx, cy)] = (GenerateChunk(cx, cy), _frame);
                    generated = true;
                }
            }
        }
    }

    /// <summary>Raised decorations whose base lies inside <paramref name="area"/>.</summary>
    public IEnumerable<FloorProp> Props(Rectangle area)
    {
        if (Style.PropChance <= 0f)
            yield break;

        for (var gy = FloorDiv(area.Top, PropCell); gy <= FloorDiv(area.Bottom, PropCell); gy++)
        {
            for (var gx = FloorDiv(area.Left, PropCell); gx <= FloorDiv(area.Right, PropCell); gx++)
            {
                var h = Hash(gx, gy, Style.Seed + 909);
                if (h % 1000 / 1000f >= Style.PropChance)
                    continue;

                var feet = new Vector2(gx * PropCell + 8 + h / 7 % 48, gy * PropCell + 8 + h / 331 % 48);
                if (IsPath((int)feet.X, (int)feet.Y))
                    continue;

                var texture = _props[h / 13 % _props.Length];
                yield return new FloorProp(texture, feet, texture.Width - 2);
            }
        }
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks.Values)
            chunk.Texture.Dispose();
        _chunks.Clear();
        foreach (var prop in _props)
            prop.Dispose();
    }

    private Texture2D GenerateChunk(int cx, int cy)
    {
        var s = Style;
        var ox = cx * ChunkSize;
        var oy = cy * ChunkSize;
        var pixels = new Color[ChunkSize * ChunkSize];
        var covered = new bool[ChunkSize * ChunkSize];

        // Tall grass is decided per 16 px tile: once per tile instead of once per pixel (chunks are tile-aligned).
        const int Tiles = ChunkSize / TallGrassTile;
        var tallGrass = new bool[Tiles * Tiles];
        if (s.TallGrassThreshold > 0f)
        {
            for (var t = 0; t < tallGrass.Length; t++)
            {
                var tx = ox / TallGrassTile + t % Tiles;
                var ty = oy / TallGrassTile + t / Tiles;
                var g = Fbm((tx + 0.5f) * TallGrassTile / 90f, (ty + 0.5f) * TallGrassTile / 90f, s.Seed + 202);
                tallGrass[t] = g > s.TallGrassThreshold && !IsPath(tx * TallGrassTile + 8, ty * TallGrassTile + 8);
            }
        }

        for (var y = 0; y < ChunkSize; y++)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                int wx = ox + x, wy = oy + y;
                var dither = (Bayer4[wy & 3, wx & 3] / 16f - 0.5f) * 0.14f;
                var i = y * ChunkSize + x;

                if (s.PathWidth > 0f)
                {
                    var distance = PathDistance(wx, wy);
                    if (distance < s.PathWidth)
                    {
                        var shade = Fbm(wx / 24f, wy / 24f, s.Seed + 303) + dither;
                        var edge = distance > s.PathWidth - 1.6f + dither * 4f;
                        pixels[i] = edge ? s.PathEdge : s.PathTones[shade < 0.42f ? 0 : shade < 0.62f ? 1 : 2];
                        covered[i] = true;
                        continue;
                    }
                }

                if (tallGrass[y / TallGrassTile * Tiles + x / TallGrassTile])
                {
                    pixels[i] = _tallGrassTile[(wy & (TallGrassTile - 1)) * TallGrassTile + (wx & (TallGrassTile - 1))];
                    covered[i] = true;
                    continue;
                }

                var n = Fbm(wx / s.PatchScale, wy / s.PatchScale, s.Seed) + dither;
                pixels[i] = s.BaseTones[n < 0.4f ? 0 : n < 0.62f ? 1 : 2];
            }
        }

        Scatter(pixels, covered, ox, oy, cell: 15, s.TuftDensity, s.Seed + 11, (set, wx, wy, h) =>
        {
            set(wx, wy, s.Tuft);
            set(wx - 1, wy - 1, s.Tuft);
            set(wx + 1, wy - 1, s.Tuft);
            if (h % 4 == 0)
                set(wx, wy - 2, s.TuftLight);
        });

        Color[] flowers = [new(248, 248, 232), new(248, 200, 64), new(232, 112, 136), new(168, 176, 248)];
        Scatter(pixels, covered, ox, oy, cell: 50, s.FlowerDensity, s.Seed + 23, (set, wx, wy, h) =>
        {
            // Small clusters of 1–3 flowers.
            var count = 1 + h % 3;
            for (var k = 0; k < count; k++)
            {
                var fx = wx + (h >> (k * 3 + 2)) % 7 - 3;
                var fy = wy + (h >> (k * 3 + 5)) % 5 - 2;
                var c = flowers[(h >> (k + 9)) % flowers.Length];
                set(fx - 1, fy, c);
                set(fx + 1, fy, c);
                set(fx, fy - 1, c);
                set(fx, fy + 1, c);
                set(fx, fy, new Color(248, 224, 96));
            }
        });

        var texture = new Texture2D(_device, ChunkSize, ChunkSize);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Jittered-grid scattering in world space; marks are written only inside this chunk, so neighbours draw their own halves.</summary>
    private static void Scatter(Color[] pixels, bool[] covered, int ox, int oy, int cell, float densityPer10k, int seed, Action<Action<int, int, Color>, int, int, int> mark)
    {
        if (densityPer10k <= 0f)
            return;

        var chance = densityPer10k * cell * cell / 10000f;
        void Set(int wx, int wy, Color color)
        {
            int x = wx - ox, y = wy - oy;
            if (x < 0 || y < 0 || x >= ChunkSize || y >= ChunkSize || covered[y * ChunkSize + x])
                return;
            pixels[y * ChunkSize + x] = color;
        }

        for (var gy = FloorDiv(oy - 8, cell); gy <= FloorDiv(oy + ChunkSize + 8, cell); gy++)
        {
            for (var gx = FloorDiv(ox - 8, cell); gx <= FloorDiv(ox + ChunkSize + 8, cell); gx++)
            {
                var h = Hash(gx, gy, seed);
                if (h % 1000 / 1000f >= chance)
                    continue;

                mark(Set, gx * cell + h / 1000 % cell, gy * cell + h / 97 % cell, h);
            }
        }
    }

    private bool IsPath(int wx, int wy) => Style.PathWidth > 0f && PathDistance(wx, wy) < Style.PathWidth + 2f;

    /// <summary>Approximate pixel distance to the noise iso-line at 0.5 (value / gradient), which keeps the path a constant width.</summary>
    private float PathDistance(int wx, int wy)
    {
        const float scale = 1f / 170f;
        var seed = Style.Seed + 101;
        var n = Fbm(wx * scale, wy * scale, seed);
        var gx = Fbm((wx + 1) * scale, wy * scale, seed) - n;
        var gy = Fbm(wx * scale, (wy + 1) * scale, seed) - n;
        return Math.Abs(n - 0.5f) / MathF.Max(MathF.Sqrt(gx * gx + gy * gy), 1e-5f);
    }

    /// <summary>Classic route "tall grass": staggered rows of inverted-V blades on a dark base; tiles seamlessly.</summary>
    private static Color[] BuildTallGrassTile()
    {
        var dark = new Color(52, 108, 50);
        var mid = new Color(80, 150, 66);
        var light = new Color(132, 200, 94);
        var shadow = new Color(38, 84, 42);
        var tile = Enumerable.Repeat(dark, TallGrassTile * TallGrassTile).ToArray();

        void Set(int x, int y, Color c) => tile[(y & 15) * TallGrassTile + (x & 15)] = c;

        foreach (var (bx, by) in new[] { (2, 1), (7, 1), (12, 1), (4, 9), (9, 9), (14, 9) })
        {
            Set(bx, by, light);
            for (var dx = -1; dx <= 1; dx++) Set(bx + dx, by + 1, dx == 0 ? light : mid);
            for (var dx = -2; dx <= 2; dx++) Set(bx + dx, by + 2, dx == 0 ? light : mid);
            for (var dx = -2; dx <= 2; dx++) Set(bx + dx, by + 3, mid);
            for (var dx = -2; dx <= 2; dx++) Set(bx + dx, by + 5, shadow);
        }

        return tile;
    }

    private void Evict()
    {
        if (_chunks.Count <= MaxCachedChunks)
            return;

        foreach (var key in _chunks.OrderBy(kv => kv.Value.LastUsed).Take(_chunks.Count - MaxCachedChunks).Select(kv => kv.Key).ToList())
        {
            _chunks[key].Texture.Dispose();
            _chunks.Remove(key);
        }
    }

    private static float Fbm(float x, float y, int seed) => Noise(x, y, seed) * 0.7f + Noise(x * 2.3f, y * 2.3f, seed + 17) * 0.3f;

    private static float Noise(float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = Smooth(x - x0);
        var fy = Smooth(y - y0);
        var a = Hash(x0, y0, seed) % 1000 / 1000f;
        var b = Hash(x0 + 1, y0, seed) % 1000 / 1000f;
        var c = Hash(x0, y0 + 1, seed) % 1000 / 1000f;
        var d = Hash(x0 + 1, y0 + 1, seed) % 1000 / 1000f;
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }

    private static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

}
