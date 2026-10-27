using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Characters;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// What the game draws itself for the collectible cards (phase 10), in card units (<see cref="CardArt.Width"/>×<see cref="CardArt.Height"/>)
/// into a target <c>scale</c> times larger: the back every card shares, and the sealed packs. The cards' faces are the
/// TCG cards' own pictures from the API.
/// </summary>
public static class CardFace
{
    /// <summary>A pack is as wide as a card and a little taller.</summary>
    public const int PackHeight = 168;

    private static readonly Color Ink = new(24, 20, 32);
    private static readonly Color Silver = new(206, 210, 220);
    private static readonly Color SilverDark = new(132, 138, 154);
    private static readonly Color Gold = new(232, 196, 92);

    /// <summary>The back: a deep blue card with a ball emblem and the game's name.</summary>
    public static void DrawBack(SpriteBatch batch, CardArt art, int scale)
    {
        var s = art.Services;
        var p = s.Primitives;
        Begin(batch, scale);
        batch.Draw(art.Rounded(1), new Rectangle(0, 0, CardArt.Width, CardArt.Height), Silver);
        var inner = new Rectangle(4, 4, CardArt.Width - 8, CardArt.Height - 8);
        p.Rect(batch, inner, new Color(28, 46, 112));
        batch.Draw(art.Glow, new Rectangle(-40, -30, 180, 200), new Color(70, 110, 220) * 0.9f);
        for (var x = -140; x < 100; x += 6)
            UiDraw.Line(batch, p, new Vector2(inner.X + x, inner.Bottom), new Vector2(inner.X + x + inner.Height, inner.Y), Color.White * 0.05f, 1.5f);
        p.Outline(batch, new Rectangle(8, 8, CardArt.Width - 16, CardArt.Height - 16), new Color(120, 150, 230) * 0.6f, 1);

        // A ball: red top, white bottom, a dark band and button.
        var center = new Vector2(50, 62);
        Disc(batch, art, center, 25f, Ink);
        Disc(batch, art, center, 23f, new Color(236, 236, 240));
        var disc = art.Disc;
        batch.Draw(disc, new Vector2(center.X - 23f, center.Y - 23f), new Rectangle(0, 0, disc.Width, disc.Height / 2), new Color(214, 46, 46),
            0f, Vector2.Zero, 46f / disc.Width, SpriteEffects.None, 0f);
        p.Rect(batch, new Vector2(center.X - 23, center.Y - 2.5f), new Vector2(46, 5), Ink);
        Disc(batch, art, center, 8f, Ink);
        Disc(batch, art, center, 5f, new Color(236, 236, 240));

        CenteredOutlined(batch, Body(s, 10f), "POKÉROGUE", 50, 98, Gold);
        UiDraw.TextCentered(batch, Small(s, 3.2f), "CARTE COLLEZIONABILI", 50, 114, new Color(170, 190, 240));
        batch.End();
    }

    /// <summary>A sealed pack of a line's cards: foil in the line's type colour, its first species' HOME render, crimped ends.</summary>
    public static void DrawPack(SpriteBatch batch, CardArt art, CharacterDefinition line, int scale)
    {
        var s = art.Services;
        var p = s.Primitives;
        var type = TypePalette.Of(line.First.Types[0]);
        Begin(batch, scale);

        var body = new Rectangle(0, 8, CardArt.Width, PackHeight - 16);
        p.Rect(batch, body, Color.Lerp(type, Ink, 0.35f));
        batch.Draw(art.Glow, new Rectangle(-50, 0, 200, PackHeight), Color.Lerp(type, Color.White, 0.35f));
        for (var x = -170; x < 100; x += 7)
            UiDraw.Line(batch, p, new Vector2(x, body.Bottom), new Vector2(x + 152, body.Y), Color.White * 0.07f, 2f);

        // Crimped ends: silver strips with teeth.
        foreach (var y in new[] { 0, PackHeight - 10 })
        {
            p.Rect(batch, new Rectangle(0, y + 1, CardArt.Width, 9), Silver);
            for (var x = 0; x < CardArt.Width; x += 3)
                p.Rect(batch, new Rectangle(x, y + 2, 1, 7), SilverDark);
        }

        if (art.Picture(line.First.Id, "home") is { } picture)
            DrawClipped(batch, picture, new Vector2(50, 84), 96f, body);

        Shade(batch, p, new Rectangle(0, 10, CardArt.Width, 26), top: true);
        Shade(batch, p, new Rectangle(0, 122, CardArt.Width, 36), top: false);
        CenteredOutlined(batch, Body(s, 11f), "POKÉROGUE", 50, 14, Gold);
        UiDraw.TextCentered(batch, Small(s, 3.4f), "CARTE COLLEZIONABILI", 50, 29, Color.White);
        UiDraw.TextCentered(batch, Small(s, 3.4f), "BUSTINA DELLA LINEA DI", 50, 132, Silver);
        CenteredOutlined(batch, Body(s, 9f), line.NameIt.ToUpperInvariant(), 50, 138, Color.White);
        batch.End();
    }

    /// <summary>A dark fade over a band of the picture, darkest at the edge.</summary>
    private static void Shade(SpriteBatch batch, Primitives p, Rectangle band, bool top)
    {
        const int steps = 12;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)steps;
            var alpha = 0.7f * (1f - t) * (1f - t);
            var y = top ? band.Y + band.Height * t : band.Bottom - band.Height * (t + 1f / steps);
            p.Rect(batch, new Vector2(band.X, y), new Vector2(band.Width, band.Height / (float)steps + 0.2f), Color.Black * alpha);
        }
    }

    /// <summary>A square picture <paramref name="size"/> units wide centred on <paramref name="center"/>, cut to <paramref name="clip"/>.</summary>
    private static void DrawClipped(SpriteBatch batch, Texture2D picture, Vector2 center, float size, Rectangle clip)
    {
        var units = size / picture.Width;
        var left = center.X - size / 2f;
        var top = center.Y - size / 2f;
        var x0 = MathF.Max(left, clip.X);
        var y0 = MathF.Max(top, clip.Y);
        var x1 = MathF.Min(left + size, clip.Right);
        var y1 = MathF.Min(top + size, clip.Bottom);
        if (x1 <= x0 || y1 <= y0)
            return;

        var source = new Rectangle((int)((x0 - left) / units), (int)((y0 - top) / units), (int)((x1 - x0) / units), (int)((y1 - y0) / units));
        batch.Draw(picture, new Vector2(x0, y0), source, Color.White, 0f, Vector2.Zero, units, SpriteEffects.None, 0f);
    }

    private static void Disc(SpriteBatch batch, CardArt art, Vector2 center, float radius, Color color) =>
        batch.Draw(art.Disc, center - new Vector2(radius), null, color, 0f, Vector2.Zero, radius * 2f / art.Disc.Width, SpriteEffects.None, 0f);

    private static void CenteredOutlined(SpriteBatch batch, SpriteFontBase font, string text, float centerX, float y, Color color)
    {
        var at = new Vector2(centerX - UiDraw.Measure(font, text).X / 2f, y);

        // A dark rim a third of a unit wide, finer than the screen's one-pixel outline.
        foreach (var (dx, dy) in new[] { (-0.35f, 0f), (0.35f, 0f), (0f, -0.35f), (0f, 0.35f) })
            UiDraw.Text(batch, font, text, at + new Vector2(dx, dy), Ink * 0.85f);
        UiDraw.Text(batch, font, text, at, color);
    }

    private static void Begin(SpriteBatch batch, int scale) =>
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null, Matrix.CreateScale(scale, scale, 1f));

    private static SpriteFontBase Body(GameServices s, float size) => s.Fonts.Get(s.Style.BodyFont, size);

    private static SpriteFontBase Small(GameServices s, float size) => s.Fonts.Get(s.Style.SmallFont, size);
}
