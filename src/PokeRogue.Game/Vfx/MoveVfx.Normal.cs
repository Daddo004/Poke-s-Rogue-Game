using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Normal-type styles: Comete's homing stars and Granvoce's sound waves.</summary>
public sealed partial class MoveVfx
{
    private static readonly Color StarGold = new(255, 214, 64);
    private static readonly Color SoundLilac = new(214, 196, 255);

    /// <summary>Comete: sparkles left behind each spinning star.</summary>
    private void StarTrail(Vector2 at)
    {
        if (_tick % 2 == 0)
            _glow.Emit(at + Jitter(2), new Vector2(Rand(-8, 8), Rand(-8, 8)), Color.White, StarGold, 12, 1f, drag: 0.92f);
    }

    private void StarImpact(Vector2 at, int big)
    {
        _timed.Add(new TimedFx(FxKind.Star, at, Vector2.Zero, 1f, StarGold, Life: 8));
        _glow.Burst(at, 6 * big, 120f, Color.White, StarGold, 12, _random);
    }

    /// <summary>Granvoce: the wave and two fading echoes behind it, broken into dashes like a shout. Additive.</summary>
    private void DrawSonicRing(SpriteBatch batch, MoveRing ring)
    {
        var center = ring.Center.ToXna() - new Vector2(0, BodyHeight * 0.5f);
        var fade = MathF.Sqrt(MathHelper.Clamp(1f - ring.Radius / ring.MaxRadius, 0f, 1f));
        for (var echo = 0; echo < 3; echo++)
        {
            var radius = ring.Radius - echo * 8f;
            if (radius < 3f)
                break;

            var alpha = fade * (1f - echo * 0.35f);
            DashedRing(batch, center, radius, SoundLilac * (0.55f * alpha), 3, ring.Serial + echo);
            DashedRing(batch, center, radius, Color.White * alpha, 1, ring.Serial + echo);
        }
    }

    /// <summary>A circle cut into twelve arcs, three drawn and one skipped; <paramref name="phase"/> turns the pattern.</summary>
    private void DashedRing(SpriteBatch batch, Vector2 center, float radius, Color color, int thickness, int phase)
    {
        var p = _services.Primitives;
        var steps = Math.Max(24, (int)(MathF.Tau * radius / 2f));
        for (var i = 0; i < steps; i++)
        {
            if ((i * 12 / steps + phase) % 4 == 3)
                continue;

            var angle = i * MathF.Tau / steps;
            var x = (int)MathF.Round(center.X + MathF.Cos(angle) * radius - thickness / 2f);
            var y = (int)MathF.Round(center.Y + MathF.Sin(angle) * radius - thickness / 2f);
            p.Rect(batch, new Rectangle(x, y, thickness, thickness), color);
        }
    }
}
