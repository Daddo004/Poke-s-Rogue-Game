using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>
/// The obstacles of a level's map (phase 7.2), drawn in code like the floor: four variants of the tileset's kind, lit from
/// the top left as the rest of the game, with a dark outline so they read over any floor. The body that blocks (the
/// core's <see cref="Obstacle"/>) is the trunk or the base; crowns and tops stand above it, y-sorted with the Pokémon.
/// </summary>
public sealed class ObstacleArt : IDisposable
{
    private readonly Texture2D[] _variants;

    public ObstacleArt(GraphicsDevice device, Tileset tileset)
    {
        Tileset = tileset;
        _variants = Enumerable.Range(0, 4).Select(v => Build(device, tileset.Obstacle, tileset.ObstacleTones, v)).ToArray();
    }

    public Tileset Tileset { get; }

    public Texture2D Variant(int index) => _variants[index & 3];

    /// <summary>Where the sprite's bottom stands for an obstacle: a little below the centre of its body.</summary>
    public static Vector2 Feet(in Obstacle o) => new(o.Position.X, o.Position.Y + o.Radius * 0.45f);

    public void Draw(SpriteBatch batch, in Obstacle o, float alpha = 1f)
    {
        var texture = Variant(o.Variant);
        var feet = Feet(o);
        batch.Draw(texture, new Vector2(MathF.Round(feet.X - texture.Width / 2f), MathF.Round(feet.Y - texture.Height + 1)), Color.White * alpha);
    }

    public void DrawShadow(SpriteBatch batch, Primitives p, in Obstacle o) =>
        p.Ellipse(batch, Feet(o) + new Vector2(0, -1), (int)(o.Radius * 2.6f), (int)MathF.Max(4, o.Radius * 0.9f), Color.Black * 0.3f);

    public void Dispose()
    {
        foreach (var texture in _variants)
            texture.Dispose();
    }

    private static Texture2D Build(GraphicsDevice device, ObstacleKind kind, Color[] t, int v)
    {
        var (dark, mid, light, accent) = (t[0], t[1], t[2], t[3]);
        Canvas c;
        switch (kind)
        {
            case ObstacleKind.Tree:
                c = new Canvas(34, 42);
                c.Rect(15, 26, 5, 15, accent);
                c.Rect(18, 26, 2, 15, Color.Lerp(accent, Color.Black, 0.35f));
                c.Ball(17 + (v == 1 ? -1 : 0), 15, 12, 11, t);
                c.Ball(9, 20 + (v == 2 ? 1 : 0), 8, 7, t);
                c.Ball(25, 20 - (v == 3 ? 1 : 0), 8, 7, t);
                c.Ball(17, 9, 8, 6, t);
                c.Speckle(v + 1, 6, light, 4, 2, 30, 26);
                break;

            case ObstacleKind.Hedge:
                c = new Canvas(30, 22);
                c.Ball(15, 12, 13, 9, t);
                c.Ball(9 + v, 9, 6, 5, t);
                c.Speckle(v + 3, 9, accent, 4, 5, 26, 18, plus: true);
                break;

            case ObstacleKind.Pine:
                c = new Canvas(28, 44);
                c.Rect(12, 36, 4, 7, accent);
                for (var tier = 0; tier < 3; tier++)
                {
                    var top = 2 + tier * 10 + (v == 2 ? 1 : 0);
                    var half = 6 + tier * 4;
                    c.Triangle(14, top, half, 14, mid, dark);
                    // Snow on each tier's top.
                    c.Triangle(14, top, half / 2 + 1, 5, light, Color.Lerp(light, mid, 0.4f));
                }

                break;

            case ObstacleKind.Palm:
                c = new Canvas(36, 46);
                for (var k = 0; k < 30; k++)
                {
                    var x = 16 + (int)(MathF.Sin(k / 30f * 1.4f) * (v % 2 == 0 ? 4 : -3));
                    c.Rect(x, 44 - k, 4, 1, k % 4 == 0 ? Color.Lerp(accent, Color.Black, 0.3f) : accent);
                }

                var crown = new Vector2(v % 2 == 0 ? 20 : 15, 14);
                for (var f = 0; f < 6; f++)
                {
                    var angle = -MathF.PI / 2 + (f - 2.5f) * 0.62f + (f >= 3 ? 0.4f : -0.4f);
                    for (var k = 0; k < 14; k++)
                    {
                        var droop = k * k * 0.035f;
                        var x = crown.X + MathF.Cos(angle) * k * 1.1f;
                        var y = crown.Y + MathF.Sin(angle) * k * 0.8f + droop;
                        c.Rect((int)x, (int)y, 2, 2, k < 5 ? light : f % 2 == 0 ? mid : dark);
                    }
                }

                c.Ball((int)crown.X - 1, (int)crown.Y + 3, 2, 2, [Color.Lerp(accent, Color.Black, 0.5f), accent, accent]);
                break;

            case ObstacleKind.Rock:
            case ObstacleKind.LavaRock:
                c = new Canvas(32, 24);
                c.Ball(16, 14, 14, 9, t);
                if (v >= 2)
                    c.Ball(v == 2 ? 7 : 25, 18, 6, 4, t);
                if (kind == ObstacleKind.Rock)
                    c.Speckle(v + 7, 5, accent, 8, 6, 24, 10);
                else
                {
                    c.Crack(9 + v, 10, 9, accent);
                    c.Crack(19 - v, 12, 7, accent);
                }

                break;

            case ObstacleKind.Stalagmite:
                c = new Canvas(24, 38);
                c.Spike(12, 2 + v, 9, 35, t);
                c.Spike(v % 2 == 0 ? 4 : 20, 20, 4, 35, t);
                break;

            case ObstacleKind.Crystal:
                c = new Canvas(28, 36);
                c.Shard(14, 2 + v, 6, 33, t);
                c.Shard(v % 2 == 0 ? 6 : 22, 14, 4, 33, t);
                c.Shard(v % 2 == 0 ? 21 : 7, 19, 3, 33, t);
                c.Set(12, 8 + v, accent);
                c.Set(13, 7 + v, accent);
                break;

            case ObstacleKind.Gravestone:
                c = new Canvas(20, 26);
                c.Rect(2, 21, 16, 4, dark);
                c.Rect(4, 9, 12, 13, mid);
                c.Ball(10, 9, 6, 5, [mid, mid, light]);
                c.Rect(4, 9, 2, 13, light);
                c.Rect(14, 9, 2, 13, dark);
                // The engraving: a cross, or two lines.
                if (v % 2 == 0)
                {
                    c.Rect(9, 8, 2, 9, dark);
                    c.Rect(7, 10, 6, 2, dark);
                }
                else
                {
                    c.Rect(7, 11, 6, 1, dark);
                    c.Rect(7, 14, 6, 1, dark);
                }

                if (v == 3)
                    c.Crack(12, 5, 6, dark);
                break;

            case ObstacleKind.Pillar:
            {
                c = new Canvas(20, 42);
                var broken = v >= 2;
                var top = broken ? 14 + v * 2 : 4;
                c.Rect(1, 36, 18, 5, dark);
                c.Rect(2, 35, 16, 2, mid);
                c.Rect(4, top + 4, 12, 32 - top, mid);
                c.Rect(5, top + 4, 2, 32 - top, light);
                c.Rect(13, top + 4, 2, 32 - top, dark);
                c.Rect(9, top + 6, 1, 28 - top, Color.Lerp(mid, dark, 0.4f));
                if (broken)
                {
                    for (var x = 4; x < 16; x++)
                        c.Rect(x, top + (x * 7 + v) % 4, 1, 4, x < 9 ? light : mid);
                    c.Rect(15, 37, 4, 3, mid);
                }
                else
                {
                    c.Rect(2, top, 16, 4, light);
                    c.Rect(2, top + 3, 16, 1, dark);
                    c.Rect(3, top + 4, 14, 1, accent);
                }

                break;
            }

            case ObstacleKind.Generator:
                c = new Canvas(30, 32);
                c.Rect(3, 10, 24, 20, mid);
                c.Rect(3, 10, 24, 3, light);
                c.Rect(3, 27, 24, 3, dark);
                for (var x = 3; x < 27; x++)
                    c.Rect(x, 20, 1, 4, (x + v) / 3 % 2 == 0 ? accent : Color.Lerp(dark, Color.Black, 0.3f));
                c.Rect(11, 3, 8, 8, dark);
                c.Rect(12, 2, 6, 3, light);
                for (var y = 4; y < 10; y += 2)
                    c.Rect(10, y, 10, 1, Color.Lerp(accent, Color.White, 0.3f));
                c.Rect(6 + v * 4, 14, 3, 2, Color.Lerp(accent, Color.White, 0.5f));
                break;

            case ObstacleKind.Beam:
                c = new Canvas(26, 34);
                c.Rect(9, 8, 8, 25, mid);
                c.Rect(9, 8, 2, 25, light);
                c.Rect(15, 8, 2, 25, dark);
                for (var x = 9; x < 17; x++)
                    c.Rect(x, 4 + (x * 5 + v) % 5, 1, 5, dark);
                if (v % 2 == 1)
                {
                    for (var k = 0; k < 16; k++)
                        c.Rect(1 + k, 26 + k / 3, 2, 3, k % 5 == 0 ? dark : mid);
                }

                c.Speckle(v + 11, 4, accent, 9, 10, 16, 28);
                break;

            case ObstacleKind.Cactus:
                c = new Canvas(24, 36);
                c.Rect(9, 6, 7, 29, mid);
                c.Ball(12, 6, 3, 3, [mid, mid, light]);
                c.Rect(9, 6, 2, 29, light);
                c.Rect(14, 6, 2, 29, dark);
                var arm = 12 + v * 2;
                c.Rect(3, arm, 6, 3, mid);
                c.Rect(3, arm - 7, 3, 8, mid);
                c.Rect(3, arm - 7, 1, 8, light);
                c.Rect(16, arm + 4, 5, 3, mid);
                c.Rect(18, arm - 2, 3, 7, mid);
                c.Rect(20, arm - 2, 1, 7, dark);
                c.Speckle(v + 13, 7, light, 9, 8, 16, 32);
                if (v == 1)
                    c.Ball(12, 3, 2, 2, [accent, accent, Color.White]);
                break;

            default:
                c = new Canvas(8, 8);
                break;
        }

        return c.Finish(device, Color.Lerp(dark, Color.Black, 0.55f));
    }

    /// <summary>A small raster to draw an obstacle into, with lit shapes and an outline pass.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly Color[] _pixels = new Color[width * height];

        public void Set(int x, int y, Color color)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
                _pixels[y * width + x] = color;
        }

        public void Rect(int x, int y, int w, int h, Color color)
        {
            for (var yy = y; yy < y + h; yy++)
                for (var xx = x; xx < x + w; xx++)
                    Set(xx, yy, color);
        }

        /// <summary>A filled ellipse lit from the top left: dark, mid and light tones by where each pixel faces.</summary>
        public void Ball(int cx, int cy, int rx, int ry, Color[] tones)
        {
            for (var y = -ry; y <= ry; y++)
            {
                for (var x = -rx; x <= rx; x++)
                {
                    float nx = x / (float)rx, ny = y / (float)ry;
                    var d = nx * nx + ny * ny;
                    if (d > 1f)
                        continue;
                    var lit = -(nx + ny) * 0.6f + (1f - d) * 0.3f;
                    var dither = ((x + y) & 1) == 0 ? 0.06f : -0.06f;
                    Set(cx + x, cy + y, tones[lit + dither > 0.32f ? 2 : lit + dither < -0.12f ? 0 : 1]);
                }
            }
        }

        /// <summary>A triangle from an apex down to a base <paramref name="height"/> below, <paramref name="half"/> wide each side; its right half darker.</summary>
        public void Triangle(int apexX, int apexY, int half, int height, Color left, Color right)
        {
            for (var y = 0; y < height; y++)
            {
                var span = (int)MathF.Round(half * (y + 1) / (float)height);
                for (var x = -span; x <= span; x++)
                    Set(apexX + x, apexY + y, x <= 0 ? left : right);
            }
        }

        /// <summary>A cone of rock from a tip down to a base line: lit left side, dark right side.</summary>
        public void Spike(int tipX, int tipY, int half, int baseY, Color[] tones)
        {
            var height = baseY - tipY;
            for (var y = 0; y <= height; y++)
            {
                var span = half * y / (float)height;
                for (var x = (int)-span; x <= (int)span; x++)
                    Set(tipX + x, tipY + y, x < -span * 0.3f ? tones[2] : x > span * 0.4f ? tones[0] : tones[1]);
            }
        }

        /// <summary>A long crystal: a diamond from a tip down to a base, its faces in three tones.</summary>
        public void Shard(int tipX, int tipY, int half, int baseY, Color[] tones)
        {
            var height = baseY - tipY;
            for (var y = 0; y <= height; y++)
            {
                var t = y / (float)height;
                var span = half * MathF.Min(1f, t * 3f);
                for (var x = (int)-span; x <= (int)span; x++)
                    Set(tipX + x, tipY + y, x < 0 ? tones[2] : x == 0 ? tones[3] : tones[1]);
            }

            for (var y = (int)(height * 0.75f); y <= height; y++)
                Set(tipX + half - 1, tipY + y, tones[0]);
        }

        /// <summary>Single pixels (or small plus signs) scattered in an area, the same for the same seed.</summary>
        public void Speckle(int seed, int count, Color color, int x0, int y0, int x1, int y1, bool plus = false)
        {
            var random = new Random(seed);
            for (var i = 0; i < count; i++)
            {
                var x = random.Next(x0, x1);
                var y = random.Next(y0, y1);
                if (!plus && _pixels[Math.Clamp(y, 0, height - 1) * width + Math.Clamp(x, 0, width - 1)].A == 0)
                    continue;
                Set(x, y, color);
                if (plus)
                {
                    Set(x - 1, y, color);
                    Set(x + 1, y, color);
                    Set(x, y - 1, color);
                    Set(x, y + 1, Color.Lerp(color, Color.White, 0.6f));
                }
            }
        }

        /// <summary>A crooked crack down from a point, only over what is already drawn.</summary>
        public void Crack(int x, int y, int length, Color color)
        {
            for (var k = 0; k < length; k++)
            {
                if (x >= 0 && y >= 0 && x < width && y < height && _pixels[y * width + x].A > 0)
                    _pixels[y * width + x] = color;
                y++;
                x += (k * 7 + x) % 3 - 1;
            }
        }

        public Texture2D Finish(GraphicsDevice device, Color outline)
        {
            var result = (Color[])_pixels.Clone();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (_pixels[y * width + x].A > 0)
                        continue;
                    for (var n = 0; n < 9; n++)
                    {
                        int nx = x + n % 3 - 1, ny = y + n / 3 - 1;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && _pixels[ny * width + nx].A > 0)
                        {
                            result[y * width + x] = outline;
                            break;
                        }
                    }
                }
            }

            var texture = new Texture2D(device, width, height);
            texture.SetData(result);
            return texture;
        }
    }
}
