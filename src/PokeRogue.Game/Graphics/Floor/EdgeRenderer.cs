using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>
/// What lies past the walls of a corridor or an arena (phase 7.2), drawn over the floor: the void, the sea or the sky
/// beyond, and along the wall its face. Seen from above at an angle, a wall at the top shows its whole face (a band of
/// rock, bricks or a stone rim), the other walls only their top edge.
/// </summary>
public sealed class EdgeRenderer(Primitives primitives, Tileset tileset)
{
    /// <summary>How tall the face of the top wall is.</summary>
    public const int FaceHeight = 26;

    private const int Rim = 5;

    public Tileset Tileset { get; } = tileset;

    /// <summary>Draws inside a batch with the camera transform; <paramref name="tick"/> moves the waves and the clouds.</summary>
    public void Draw(SpriteBatch batch, LevelMap map, Rectangle view, int tick)
    {
        if (!map.IsBounded)
            return;

        var left = float.IsFinite(map.Min.X) ? (int)map.Min.X : int.MinValue;
        var right = float.IsFinite(map.Max.X) ? (int)map.Max.X : int.MaxValue;
        var top = float.IsFinite(map.Min.Y) ? (int)map.Min.Y : int.MinValue;
        var bottom = float.IsFinite(map.Max.Y) ? (int)map.Max.Y : int.MaxValue;

        // Beyond, on each side the view reaches past.
        if (top > view.Top)
            Beyond(batch, Clip(view, view.Left, view.Top, view.Right, top), tick);
        if (bottom < view.Bottom)
            Beyond(batch, Clip(view, view.Left, bottom, view.Right, view.Bottom), tick);
        if (left > view.Left)
            Beyond(batch, Clip(view, view.Left, Math.Max(top, view.Top), left, Math.Min(bottom, view.Bottom)), tick);
        if (right < view.Right)
            Beyond(batch, Clip(view, right, Math.Max(top, view.Top), view.Right, Math.Min(bottom, view.Bottom)), tick);

        var x0 = Math.Max(left, view.Left - 8);
        var x1 = Math.Min(right, view.Right + 8);
        var y0 = Math.Max(top, view.Top - 8);
        var y1 = Math.Min(bottom, view.Bottom + 8);
        if (top > view.Top - FaceHeight && top < view.Bottom)
            TopFace(batch, x0, x1, top, tick);
        if (bottom > view.Top && bottom < view.Bottom + Rim)
            SideRim(batch, new Rectangle(x0, bottom, x1 - x0, Rim), horizontal: true, tick);
        if (left > view.Left - Rim && left < view.Right)
            SideRim(batch, new Rectangle(left - Rim, y0, Rim, y1 - y0), horizontal: false, tick);
        if (right > view.Left && right < view.Right + Rim)
            SideRim(batch, new Rectangle(right, y0, Rim, y1 - y0), horizontal: false, tick);
    }

    private static Rectangle Clip(Rectangle view, int x0, int y0, int x1, int y1)
    {
        x0 = Math.Max(x0, view.Left);
        y0 = Math.Max(y0, view.Top);
        x1 = Math.Min(x1, view.Right);
        y1 = Math.Min(y1, view.Bottom);
        return x1 > x0 && y1 > y0 ? new Rectangle(x0, y0, x1 - x0, y1 - y0) : Rectangle.Empty;
    }

    private void Beyond(SpriteBatch batch, Rectangle area, int tick)
    {
        if (area.IsEmpty)
            return;

        var p = primitives;
        p.Rect(batch, area, Tileset.Beyond);
        switch (Tileset.Edge)
        {
            case EdgeKind.Water:
                // Rows of short glints drifting along.
                var glint = Color.Lerp(Tileset.Beyond, Color.White, 0.25f);
                for (var y = area.Top - area.Top % 12; y < area.Bottom; y += 12)
                {
                    for (var x = area.Left - area.Left % 40 - 40; x < area.Right; x += 40)
                    {
                        var h = Hash(x, y);
                        var gx = x + h % 40 + (int)(MathF.Sin((tick + h % 97) * 0.03f) * 6f);
                        if (gx >= area.Left && gx + 6 <= area.Right && y >= area.Top)
                            p.Rect(batch, new Rectangle(gx, y, 6, 1), glint);
                    }
                }

                break;

            case EdgeKind.Clouds:
                // Slow clouds below, drifting with time.
                var drift = tick * 0.15f;
                for (var y = area.Top - area.Top % 60 - 60; y < area.Bottom + 60; y += 60)
                {
                    for (var x = area.Left - area.Left % 90 - 90; x < area.Right + 90; x += 90)
                    {
                        var h = Hash(x, y);
                        if (h % 3 == 0)
                            continue;
                        var at = new Vector2(x + h % 60 + drift % 90, y + h / 7 % 40);
                        if (!area.Contains(at.ToPoint()))
                            continue;
                        p.Ellipse(batch, at, 22 + h % 12, 8, Color.White * 0.55f);
                        p.Ellipse(batch, at + new Vector2(10, -4), 12, 6, Color.White * 0.6f);
                    }
                }

                break;

            default:
                // Darkness: a few dim specks.
                var speck = Color.Lerp(Tileset.Beyond, Tileset.WallDark, 0.6f);
                for (var y = area.Top - area.Top % 24; y < area.Bottom; y += 24)
                {
                    for (var x = area.Left - area.Left % 24; x < area.Right; x += 24)
                    {
                        var h = Hash(x, y);
                        if (h % 4 == 0 && area.Contains(x + h % 24, y + h / 5 % 24))
                            p.Rect(batch, new Rectangle(x + h % 24, y + h / 5 % 24, 1, 1), speck);
                    }
                }

                break;
        }
    }

    /// <summary>The top wall's face, seen whole: rock with streaks, bricks, the sea's foam, or a stone rim over the clouds.</summary>
    private void TopFace(SpriteBatch batch, int x0, int x1, int top, int tick)
    {
        var p = primitives;
        if (Tileset.Edge == EdgeKind.Water)
        {
            Foam(batch, new Rectangle(x0, top - 3, x1 - x0, 3), horizontal: true, tick);
            return;
        }

        var face = new Rectangle(x0, top - FaceHeight, x1 - x0, FaceHeight);
        p.Rect(batch, face, Tileset.WallDark);
        p.Rect(batch, new Rectangle(face.X, face.Y, face.Width, 2), Tileset.WallLight);
        p.Rect(batch, new Rectangle(face.X, face.Bottom - 2, face.Width, 2), Color.Lerp(Tileset.WallDark, Color.Black, 0.4f));
        switch (Tileset.Edge)
        {
            case EdgeKind.Wall:
                for (var row = 0; row < 3; row++)
                {
                    var y = face.Y + 4 + row * 7;
                    p.Rect(batch, new Rectangle(face.X, y + 6, face.Width, 1), Color.Lerp(Tileset.WallDark, Color.Black, 0.3f));
                    for (var x = face.X - face.X % 16 + row % 2 * 8; x < face.Right; x += 16)
                    {
                        if (x >= face.X)
                            p.Rect(batch, new Rectangle(x, y, 1, 6), Color.Lerp(Tileset.WallDark, Color.Black, 0.3f));
                        if (x + 1 >= face.X && x + 4 < face.Right)
                            p.Rect(batch, new Rectangle(x + 1, y, 3, 1), Color.Lerp(Tileset.WallDark, Tileset.WallLight, 0.5f));
                    }
                }

                break;

            default:
                // Rock (and the clouds' stone rim): uneven vertical streaks.
                for (var x = face.X - face.X % 5; x < face.Right; x += 5)
                {
                    var h = Hash(x, top);
                    if (x < face.X)
                        continue;
                    var length = 6 + h % 14;
                    p.Rect(batch, new Rectangle(x, face.Y + 3 + h % 5, 1, length), h % 2 == 0 ? Tileset.WallLight * 0.5f : Color.Lerp(Tileset.WallDark, Color.Black, 0.3f));
                }

                break;
        }
    }

    /// <summary>The top edge of a side or bottom wall.</summary>
    private void SideRim(SpriteBatch batch, Rectangle rim, bool horizontal, int tick)
    {
        if (rim.Width <= 0 || rim.Height <= 0)
            return;
        if (Tileset.Edge == EdgeKind.Water)
        {
            Foam(batch, rim, horizontal, tick);
            return;
        }

        var p = primitives;
        p.Rect(batch, rim, Tileset.WallDark);
        if (horizontal)
            p.Rect(batch, new Rectangle(rim.X, rim.Y, rim.Width, 1), Tileset.WallLight);
        else
            p.Rect(batch, new Rectangle(rim.X + rim.Width / 2, rim.Y, 1, rim.Height), Tileset.WallLight);
    }

    /// <summary>The shoreline: white foam that comes and goes.</summary>
    private void Foam(SpriteBatch batch, Rectangle line, bool horizontal, int tick)
    {
        var p = primitives;
        var length = horizontal ? line.Width : line.Height;
        var start = horizontal ? line.X : line.Y;
        for (var k = start - start % 6; k < start + length; k += 6)
        {
            var h = Hash(k, horizontal ? 1 : 2);
            var on = MathF.Sin((tick + h % 120) * 0.05f) > -0.3f;
            if (!on || k < start)
                continue;
            var piece = horizontal ? new Rectangle(k, line.Y, 4, 2) : new Rectangle(line.X, k, 2, 4);
            p.Rect(batch, piece, Tileset.WallLight);
        }
    }

    private static int Hash(int x, int y)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + 977;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }
}
