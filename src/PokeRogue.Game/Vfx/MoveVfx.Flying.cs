using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Flying-type styles beyond Aeroassalto's blade: Raffica's gust and Eterelama's air blades.</summary>
public sealed partial class MoveVfx
{
    private static readonly Color GrassBits = new(150, 200, 110);
    private static readonly TypeFxPalette FlyingPalette = TypeFx.For(Core.Data.PokemonType.Flying);

    /// <summary>
    /// Eterelama: a thick crescent of air, convex side forward, pale blue with a darker rim and a white leading edge,
    /// with speed lines behind. Alpha batch, so the blue holds on the grass (the faint halo is in the glow layer).
    /// </summary>
    private void DrawAirBlade(SpriteBatch batch, Vector2 at, Vector2 velocity)
    {
        var p = _services.Primitives;
        var dir = Vector2.Normalize(velocity + new Vector2(0.001f, 0));
        var side = new Vector2(-dir.Y, dir.X);
        for (var pass = 0; pass < 3; pass++)
        {
            for (var s = -1f; s <= 1.001f; s += 0.125f)
            {
                var bend = 1f - s * s;
                var point = at + side * s * 9f + dir * bend * (pass == 2 ? 6f : 5f);
                var half = pass switch { 0 => 1 + (int)(bend * 2f), 1 => (int)(bend * 2f), _ => 0 };
                var color = pass switch { 0 => FlyingPalette.Dark, 1 => FlyingPalette.Main, _ => Color.White };
                p.Rect(batch, new Rectangle((int)point.X - half, (int)point.Y - half, half * 2 + 1, half * 2 + 1), color);
            }
        }

        for (var k = -1; k <= 1; k++)
            UiDraw.Line(batch, p, at - dir * 2 + side * k * 4, at - dir * (11 + Math.Abs(k) * 3) + side * k * 4, Color.White * 0.45f);
    }

    /// <summary>Eterelama: wisps of air peeling off behind each blade.</summary>
    private void AirBladeTrail(Vector2 at, Vector2 velocity)
    {
        var back = -Vector2.Normalize(velocity + new Vector2(0.001f, 0));
        _glow.Emit(at + back * 6f + Jitter(3), back * 25f, Color.White * 0.7f, FlyingPalette.Main * 0.2f, 9, 1f);
    }

    /// <summary>
    /// Raffica: curved streaks of wind on the ring, each spiralling outwards so the gust seems to turn as it spreads,
    /// and a faint ring just inside. Additive.
    /// </summary>
    private void DrawWindRing(SpriteBatch batch, MoveRing ring)
    {
        var p = _services.Primitives;
        var center = ring.Center.ToXna() - new Vector2(0, BodyHeight * 0.5f);
        var fade = MathF.Sqrt(MathHelper.Clamp(1f - ring.Radius / ring.MaxRadius, 0f, 1f));

        const int Streaks = 10;
        for (var k = 0; k < Streaks; k++)
        {
            var start = k * MathF.Tau / Streaks + ring.Serial * 0.7f + ring.Radius * 0.012f;
            var previous = Spot(start, 0f);
            for (var s = 1; s <= 6; s++)
            {
                var next = Spot(start + s * 0.07f, s / 6f);
                UiDraw.Line(batch, p, previous, next, Color.White * (fade * (0.3f + 0.12f * s)));
                previous = next;
            }
        }

        p.Ring(batch, center, ring.Radius * 0.85f, ring.Radius * 0.85f, Color.White * (0.15f * fade));

        Vector2 Spot(float angle, float along) => center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (ring.Radius - 6f + along * 8f);
    }

    /// <summary>Bits of grass and dust thrown outwards by the gust.</summary>
    private void UpdateWind(MoveRing ring)
    {
        for (var i = 0; i < 2; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var color = i == 0 ? GrassBits : new Color(230, 230, 210);
            _dust.Emit(ring.Center.ToXna() + outward * ring.Radius, outward * 60f + new Vector2(0, -10), color, color * 0.4f, 16, 1f, drag: 0.92f);
        }
    }
}
