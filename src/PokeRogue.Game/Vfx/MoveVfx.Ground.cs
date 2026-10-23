using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Ground-type evolutions (phase 2.6): Ossoraffica's dust, Geoforza's eruptions and Abisso's chasm.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette GroundPalette = TypeFx.For(PokemonType.Ground);
    private static readonly Color Chasm = new(34, 22, 16);
    private static readonly Color Magma = new(255, 150, 50);

    /// <summary>Points on Abisso's crack; fixed, so the jagged shape stays the same while the ring grows.</summary>
    private const int FissurePoints = 72;

    /// <summary>Ossoraffica: dust kicked up on the ground under each bone.</summary>
    private void BoneRushDust(Vector2 feet) =>
        _dust.Emit(feet + Jitter(3), new Vector2(Rand(-10, 10), Rand(-18, -8)), GroundPalette.Spark * 0.8f, GroundPalette.Dark * 0.3f, 16, 2f, drag: 0.92f);

    /// <summary>Geoforza: each time the zone hits, a column of earth and glowing embers shoots out of the cracks.</summary>
    private void UpdateEruption(MoveZone zone, bool fresh)
    {
        var age = zone.Lifetime - zone.TicksLeft;
        if (zone.TicksLeft < 6 || (!fresh && age % Math.Max(1, zone.HitIntervalTicks) != 0))
            return;

        var center = zone.Position.ToXna();
        for (var i = 0; i < 7; i++)
        {
            var color = i % 3 == 0 ? GroundPalette.Dark : GroundPalette.Main;
            _dust.Emit(center + Jitter(zone.Radius * 0.35f) * new Vector2(1f, 0.5f), new Vector2(Rand(-25, 25), Rand(-150, -90)), color, color * 0.5f, 22, i % 2 == 0 ? 3f : 2f, drag: 0.95f, gravity: 8f);
        }

        for (var i = 0; i < 3; i++)
            _flames.Emit(center + Jitter(zone.Radius * 0.3f) * new Vector2(1f, 0.5f), new Vector2(Rand(-20, 20), Rand(-120, -70)), Magma, FirePalette.Dark, 16, 2f, drag: 0.95f, gravity: 6f);

        if (fresh)
            Shake.Add(0.05f);
    }

    /// <summary>Geoforza: a dark hollow crossed by cracks that glow brightest right after each eruption, then cool. Alpha batch.</summary>
    private void DrawEruptionGround(SpriteBatch batch, MoveZone zone, float alpha)
    {
        var p = _services.Primitives;
        var center = zone.Position.ToXna();
        var r = zone.Radius;
        var interval = Math.Max(1, zone.HitIntervalTicks);
        var heat = 1f - (zone.Lifetime - zone.TicksLeft) % interval / (float)interval;

        p.Ellipse(batch, center, (int)(r * 2), (int)(r * 1.1f), Chasm * (0.45f * alpha));
        var crack = Color.Lerp(Chasm, Magma, 0.35f + 0.65f * heat) * alpha;
        for (var k = 0; k < 6; k++)
        {
            var h = Hash(k, zone.Serial);
            var angle = k * MathF.Tau / 6f + h % 100 / 100f;
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f);
            var mid = center + outward * r * 0.5f + new Vector2(-outward.Y, outward.X) * ((h >> 8) % 7 - 3);
            var tip = center + outward * r * (0.85f + (h >> 4) % 20 / 100f);
            UiDraw.Line(batch, p, center, mid, crack, 2f);
            UiDraw.Line(batch, p, mid, tip, crack);
        }

        p.Ellipse(batch, center, (int)(r * 0.5f), (int)(r * 0.3f), Color.Lerp(Chasm, Magma, heat) * (0.8f * alpha));
    }

    /// <summary>Abisso: rubble thrown up along the opening crack, and a hard jolt as each wave starts.</summary>
    private void UpdateFissure(MoveRing ring, bool fresh)
    {
        if (fresh)
            Shake.Add(0.3f);

        for (var i = 0; i < 2; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var color = i == 0 ? GroundPalette.Dark : GroundPalette.Main;
            _dust.Emit(ring.Center.ToXna() + dir * ring.Radius, dir * 20f + new Vector2(0, -45), color, color * 0.4f, 16, 2f, drag: 0.92f, gravity: 5f);
        }
    }

    /// <summary>Abisso: a jagged black crack opening along the ring, with a pale sandy lip on both sides. Alpha batch.</summary>
    private void DrawFissure(SpriteBatch batch, MoveRing ring)
    {
        var p = _services.Primitives;
        var center = ring.Center.ToXna();
        var fade = MathF.Sqrt(MathHelper.Clamp(1f - ring.Radius / ring.MaxRadius, 0f, 1f));

        Span<Vector2> points = stackalloc Vector2[FissurePoints];
        for (var i = 0; i < FissurePoints; i++)
        {
            var angle = i * MathF.Tau / FissurePoints;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (ring.Radius + Hash(i, ring.Serial) % 7 - 3);
        }

        for (var i = 0; i < FissurePoints; i++)
            UiDraw.Line(batch, p, points[i], points[(i + 1) % FissurePoints], GroundPalette.Spark * (0.8f * fade), 5f);
        for (var i = 0; i < FissurePoints; i++)
            UiDraw.Line(batch, p, points[i], points[(i + 1) % FissurePoints], Chasm * fade, 3f);
    }
}
