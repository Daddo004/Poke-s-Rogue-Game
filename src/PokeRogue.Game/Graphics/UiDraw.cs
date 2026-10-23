using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>Colours of a UI frame style.</summary>
public sealed record UiTheme(Color Fill, Color Border, Color Highlight, Color Shadow, Color Text, Color TextDim)
{
    /// <summary>Dark panels with a light rim (Vampire Survivors-like).</summary>
    public static readonly UiTheme Dark = new(new Color(32, 28, 52, 235), new Color(210, 200, 255), new Color(92, 84, 140), new Color(8, 6, 16), new Color(244, 240, 228), new Color(160, 152, 190));

    /// <summary>Light panels with a coloured rim (DS Pokémon menus).</summary>
    public static readonly UiTheme Light = new(new Color(248, 248, 240), new Color(72, 112, 176), new Color(168, 200, 232), new Color(24, 32, 56), new Color(56, 56, 64), new Color(120, 120, 136));
}

public static class UiDraw
{
    public static void Text(SpriteBatch batch, SpriteFontBase font, string text, Vector2 position, Color color) =>
        font.DrawText(batch, text, Round(position), color);

    /// <summary>Text with a 1 px outline on all eight sides, readable over busy gameplay.</summary>
    public static void TextOutlined(SpriteBatch batch, SpriteFontBase font, string text, Vector2 position, Color fill, Color outline)
    {
        position = Round(position);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx != 0 || dy != 0)
                    font.DrawText(batch, text, position + new Vector2(dx, dy), outline);
            }
        }

        font.DrawText(batch, text, position, fill);
    }

    public static Vector2 Measure(SpriteFontBase font, string text) => font.MeasureString(text);

    /// <summary>Greedy word wrap to <paramref name="maxWidth"/> pixels.</summary>
    public static List<string> Wrap(SpriteFontBase font, string text, float maxWidth)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(font, candidate).X > maxWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    /// <summary>Text centred horizontally on <paramref name="centerX"/>.</summary>
    public static void TextCentered(SpriteBatch batch, SpriteFontBase font, string text, float centerX, float y, Color color, Color? outline = null)
    {
        var x = centerX - Measure(font, text).X / 2f;
        if (outline is { } o)
            TextOutlined(batch, font, text, new Vector2(x, y), color, o);
        else
            Text(batch, font, text, new Vector2(x, y), color);
    }

    /// <summary>Panel with cut corners, a 1 px rim and an inner highlight line.</summary>
    public static void Frame(SpriteBatch batch, Primitives p, Rectangle r, UiTheme theme)
    {
        p.Rect(batch, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), theme.Fill);
        p.Rect(batch, new Rectangle(r.X + 2, r.Y, r.Width - 4, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.X + 2, r.Bottom - 1, r.Width - 4, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.X, r.Y + 2, 1, r.Height - 4), theme.Border);
        p.Rect(batch, new Rectangle(r.Right - 1, r.Y + 2, 1, r.Height - 4), theme.Border);
        p.Rect(batch, new Rectangle(r.X + 1, r.Y + 1, 1, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.Right - 2, r.Y + 1, 1, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.X + 1, r.Bottom - 2, 1, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.Right - 2, r.Bottom - 2, 1, 1), theme.Border);
        p.Rect(batch, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 1), theme.Highlight);
        p.Rect(batch, new Rectangle(r.X + 2, r.Bottom, r.Width - 4, 1), theme.Shadow);
    }

    public static void Bar(SpriteBatch batch, Primitives p, Rectangle r, float value, Color fill, Color back, Color border)
    {
        p.Rect(batch, r, border);
        var inner = new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        p.Rect(batch, inner, back);
        var w = (int)MathF.Round(inner.Width * Math.Clamp(value, 0f, 1f));
        if (w <= 0)
            return;

        p.Rect(batch, new Rectangle(inner.X, inner.Y, w, inner.Height), fill);
        if (inner.Height >= 3)
            p.Rect(batch, new Rectangle(inner.X, inner.Y, w, 1), Color.Lerp(fill, Color.White, 0.45f));
    }

    /// <summary>Small coloured label with cut corners, e.g. a type ("ELETTRO") or a category ("STRUMENTO").</summary>
    public static void Pill(SpriteBatch batch, Primitives p, SpriteFontBase font, Rectangle r, Color color, string text)
    {
        var rim = Color.Lerp(color, new Color(16, 12, 28), 0.45f);
        p.Rect(batch, new Rectangle(r.X + 1, r.Y, r.Width - 2, r.Height), rim);
        p.Rect(batch, new Rectangle(r.X, r.Y + 1, r.Width, r.Height - 2), rim);
        p.Rect(batch, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), color);
        var size = Measure(font, text);
        TextOutlined(batch, font, text, new Vector2(r.Center.X - size.X / 2f, r.Y + (r.Height - size.Y) / 2f), Color.White, Color.Lerp(color, new Color(16, 12, 28), 0.6f));
    }

    /// <summary>HP colour thresholds from the main series: green, yellow under 50%, red under 20%.</summary>
    public static Color HpColor(float ratio) => ratio > 0.5f ? new Color(88, 208, 96) : ratio > 0.2f ? new Color(248, 200, 40) : new Color(232, 72, 56);

    public static void Line(SpriteBatch batch, Primitives p, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        batch.Draw(p.Pixel, from, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X), MathF.Round(v.Y));
}
