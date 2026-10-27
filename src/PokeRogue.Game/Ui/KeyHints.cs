using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The key hints along the bottom of a screen, "[INVIO] GIOCA     [ESC] INDIETRO" (phase 8.1): each one that names a
/// single key is also a button, lit under the pointer, that presses that key when clicked. "[SU/GIÙ] SCEGLI" and the
/// like stay plain text: the cells they move between are clicked directly.
/// </summary>
public static class KeyHints
{
    private const int Gap = 14;
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);

    /// <param name="centered">Centred on <paramref name="at"/>.X instead of starting there.</param>
    public static void Draw(SpriteBatch batch, GameServices services, string text, Vector2 at, Color color, bool centered = false, Color? outline = null)
    {
        var font = services.Fonts.Small(services.Style);
        var segments = Parse(text);
        var widths = segments.Select(s => UiDraw.Measure(font, s.Label).X).ToList();
        var total = widths.Sum() + Gap * (segments.Count - 1);
        var x = centered ? at.X - total / 2f : at.X;
        var input = services.Input;
        for (var i = 0; i < segments.Count; i++)
        {
            var (label, key) = segments[i];
            var position = new Vector2(MathF.Round(x), at.Y);
            var tint = color;
            if (key is { } k)
            {
                var box = new Rectangle((int)position.X - 3, (int)position.Y - 3, (int)MathF.Ceiling(widths[i]) + 6, 13);
                input.KeyRegion(box, k);
                if (input.PointerAwake && box.Contains(input.Pointer))
                {
                    services.Primitives.Rect(batch, box, Gold * 0.22f);
                    services.Primitives.Outline(batch, box, Gold, 1);
                    tint = Paper;
                }
            }

            if (outline is { } o)
                UiDraw.TextOutlined(batch, font, label, position, tint, o);
            else
                UiDraw.Text(batch, font, label, position, tint);
            x += widths[i] + Gap;
        }
    }

    /// <summary>The hint split at each "[": the label and the key it presses, if it names exactly one.</summary>
    public static List<(string Label, Keys? Key)> Parse(string text)
    {
        var segments = new List<(string, Keys?)>();
        var start = 0;
        while (start < text.Length)
        {
            var next = text.IndexOf('[', start + 1);
            var segment = (next < 0 ? text[start..] : text[start..next]).Trim();
            if (segment.Length > 0)
                segments.Add((segment, segment.StartsWith('[') && segment.IndexOf(']') is > 1 and var close ? KeyOf(segment[1..close]) : null));
            if (next < 0)
                break;
            start = next;
        }

        return segments;
    }

    /// <summary>"INVIO" → Enter, "ESC" → Escape, a letter or a digit → that key; null for "SU/GIÙ", "FRECCE" and the like.</summary>
    public static Keys? KeyOf(string name) => name switch
    {
        "INVIO" => Keys.Enter,
        "ESC" => Keys.Escape,
        "SPAZIO" => Keys.Space,
        "TAB" => Keys.Tab,
        { Length: 1 } when name[0] is >= 'A' and <= 'Z' => Keys.A + (name[0] - 'A'),
        { Length: 1 } when name[0] is >= '1' and <= '9' => Keys.D1 + (name[0] - '1'),
        _ => null,
    };

    /// <summary>Width the hint takes (for right-aligned layouts).</summary>
    public static float Measure(GameServices services, string text)
    {
        var font = services.Fonts.Small(services.Style);
        var segments = Parse(text);
        return segments.Sum(s => UiDraw.Measure(font, s.Label).X) + Gap * Math.Max(0, segments.Count - 1);
    }

    /// <summary>Ink, for hints drawn over the game (outlined).</summary>
    public static Color Outline => Ink;
}
