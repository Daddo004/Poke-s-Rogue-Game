using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Dark-type styles: Morso's jaws and Neropulsar's waves of darkness. Darkness is not light, so both are alpha-blended.</summary>
public sealed partial class MoveVfx
{
    private static readonly Color DarkViolet = new(110, 60, 160);
    private static readonly Color Gum = new(70, 30, 50);

    /// <summary>
    /// Morso: an upper and a lower row of fangs snapping shut in front of Pikachu, then fading. The jaws are always
    /// horizontal, as a bite seen from the front reads in any direction. Alpha batch.
    /// </summary>
    private void DrawBite(SpriteBatch batch, SlashFx slash)
    {
        var p = _services.Primitives;
        var reach = slash.Reach.Length();
        if (reach < 1f)
            return;

        var t = slash.Age / (float)SlashFx.Life;
        var close = MathF.Min(1f, t * 2.5f);
        var fade = t < 0.6f ? 1f : (1f - t) / 0.4f;
        var center = slash.Origin + slash.Reach / reach * (reach * 0.65f);
        var gap = 10f * (1f - close * close) + 1f;

        foreach (var jaw in (ReadOnlySpan<float>)[-1f, 1f])
        {
            // jaw -1 is the upper row, its fangs pointing down; +1 the lower row, pointing up.
            var gum = new Vector2(center.X, center.Y + gap * jaw);
            UiDraw.Line(batch, p, gum - new Vector2(12f, 0), gum + new Vector2(12f, 0), Gum * fade, 4f);
            for (var k = 0; k < 5; k++)
            {
                // The lower fangs sit between the upper ones; each fang tapers to a point.
                var root = gum + new Vector2(-10f + k * 5f + (jaw > 0f ? 2.5f : 0f), 0);
                var middle = root - new Vector2(0, jaw * 4f);
                var tip = root - new Vector2(0, jaw * 6f);
                UiDraw.Line(batch, p, root, middle, Color.White * fade, 3f);
                UiDraw.Line(batch, p, middle, tip, Color.White * fade);
            }
        }
    }

    /// <summary>Neropulsar: three bands of darkness rushing out one after another, each edged in violet. Alpha batch.</summary>
    private void DrawDarkRings(SpriteBatch batch, TimedFx fx)
    {
        var p = _services.Primitives;
        for (var k = 0; k < 3; k++)
        {
            var age = fx.Age - k * 4;
            var t = age / (float)(fx.Life - 8);
            if (age < 0 || t > 1f)
                continue;

            var radius = fx.Value * (0.25f + 0.85f * t);
            var fade = 1f - t;
            p.Ring(batch, fx.Position, radius, radius, Night * (0.7f * fade), 3);
            p.Ring(batch, fx.Position, radius + 2f, radius + 2f, DarkViolet * (0.6f * fade));
        }
    }
}
