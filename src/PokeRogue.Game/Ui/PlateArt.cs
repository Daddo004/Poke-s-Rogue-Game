using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Plates;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// How a Plate shows in the menus and windows (phase 9.1): a card with the tablet large on a glow of its type's colour,
/// its name, the type it strengthens, and what it does. A Plate not found yet is a dark tablet with a question mark.
/// </summary>
public static class PlateArt
{
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Locked = new(46, 40, 68);

    /// <summary>A Plate's square cell: the tablet on a disc of its colour, or a locked silhouette.</summary>
    public static void DrawCell(SpriteBatch batch, GameServices services, Rectangle cell, PlateDefinition plate, bool found, int tick)
    {
        var p = services.Primitives;
        var color = TypePalette.Of(plate.Type);
        p.Rect(batch, cell, Ink);
        p.Rect(batch, new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2), found ? Color.Lerp(color, Ink, 0.7f) : new Color(28, 24, 44));
        p.Outline(batch, new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2), found ? color : Locked);

        var icon = new Rectangle(cell.Center.X - cell.Width / 2 + 2, cell.Center.Y - cell.Height / 2 + 2, cell.Width - 4, cell.Height - 4);
        if (found)
        {
            var beat = 0.25f + 0.1f * MathF.Sin(tick * 0.08f);
            p.Ellipse(batch, cell.Center.ToVector2(), cell.Width - 10, cell.Height - 10, color * beat);
            services.ItemIcons.Draw(batch, plate.Icon, icon);
        }
        else
        {
            services.ItemIcons.Draw(batch, plate.Icon, icon, alpha: 0.18f);
            UiDraw.TextCentered(batch, services.Fonts.Body(services.Style), "?", cell.Center.X, cell.Center.Y - 7, Dim, Ink);
        }
    }

    /// <summary>
    /// A Plate's card: the tablet, its name, the type it strengthens and its effect, wrapped to the card. <paramref name="typePower"/>
    /// is the boost to its type (plates.json's typePower).
    /// </summary>
    public static void DrawCard(SpriteBatch batch, GameServices services, Rectangle card, PlateDefinition plate, float typePower, int tick, bool highlight)
    {
        var p = services.Primitives;
        var color = TypePalette.Of(plate.Type);
        var body = services.Fonts.Body(services.Style);
        var small = services.Fonts.Small(services.Style);
        var numbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);

        UiDraw.Frame(batch, p, card, services.Style.Theme with { Border = highlight ? color : services.Style.Theme.Border });
        var top = new Rectangle(card.X + 4, card.Y + 4, card.Width - 8, 46);
        p.Rect(batch, top, Color.Lerp(color, Ink, 0.62f));
        for (var k = 0; k < 6; k++)
        {
            // Slow rays of the type's colour behind the tablet.
            var angle = k * MathF.Tau / 6f + tick * 0.01f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            UiDraw.Line(batch, p, top.Center.ToVector2(), top.Center.ToVector2() + direction * 30f, color * 0.25f, 3f);
        }

        var bob = MathF.Round(MathF.Sin(tick * 0.07f) * 1.5f);
        services.ItemIcons.Draw(batch, plate.Icon, new Rectangle(top.Center.X - 18, top.Center.Y - 18 + (int)bob, 36, 36));

        UiDraw.TextCentered(batch, body, plate.NameIt, card.Center.X, top.Bottom + 4, Paper, Ink);
        var pill = new Rectangle(card.Center.X - 30, top.Bottom + 20, 60, 11);
        UiDraw.Pill(batch, p, small, pill, color, TypePalette.NameIt(plate.Type).ToUpperInvariant());

        var y = pill.Bottom + 6;
        var boost = $"Mosse {TypePalette.NameIt(plate.Type)}: danni +{MathF.Round(typePower * 100f)}%, e compaiono più spesso.";
        foreach (var text in new[] { plate.DescriptionIt, boost })
        {
            foreach (var line in UiDraw.Wrap(small, text, card.Width - 14))
            {
                if (y > card.Bottom - 12)
                    break;
                UiDraw.TextWithNumbers(batch, small, numbers, line, new Vector2(card.X + 7, y), text == boost ? color : Paper);
                y += 10;
            }

            y += 4;
        }
    }
}
