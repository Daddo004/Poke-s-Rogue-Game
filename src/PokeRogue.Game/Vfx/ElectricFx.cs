using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Procedural pixel-art electricity (approved FX style A): zig-zag bolts, crackling orbs, sparks.</summary>
public static class ElectricFx
{
    public static readonly Color Glow = new(255, 200, 40);
    public static readonly Color Core = new(255, 240, 120);
    public static readonly Color Hot = new(255, 255, 230);

    /// <summary>Jagged bolt between two points; <paramref name="seed"/> changes the shape (re-roll it every few ticks to flicker).</summary>
    public static void Bolt(SpriteBatch batch, Primitives p, Vector2 from, Vector2 to, int seed, float intensity = 1f)
    {
        var random = new Random(seed);
        var delta = to - from;
        var length = delta.Length();
        if (length < 1f)
            return;

        var normal = new Vector2(-delta.Y, delta.X) / length;
        var segments = System.Math.Max(3, (int)(length / 12));
        Span<Vector2> points = stackalloc Vector2[segments + 1];
        points[0] = from;
        for (var i = 1; i < segments; i++)
            points[i] = from + delta * (i / (float)segments) + normal * random.Next(-6, 7);
        points[segments] = to;

        for (var i = 0; i < segments; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], Glow * (0.35f * intensity), 5f);
        for (var i = 0; i < segments; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], Core * (0.9f * intensity), 2f);
        for (var i = 0; i < segments; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], Hot * (0.8f * intensity), 1f);

        // A couple of short forks off the main bolt.
        for (var f = 0; f < 2; f++)
        {
            var start = points[random.Next(1, segments)];
            var dir = Vector2.Normalize(delta) * 0.6f + normal * (random.Next(2) == 0 ? -1f : 1f);
            UiDraw.Line(batch, p, start, start + dir * random.Next(5, 10), Core * (0.7f * intensity), 1f);
        }
    }

    /// <summary>Crackling ball for projectile Thundershock. Additive batch.</summary>
    public static void Orb(SpriteBatch batch, Primitives p, Vector2 center, int tick, int seed)
    {
        var pulse = 0.85f + 0.15f * MathF.Sin(tick * 0.6f);
        p.Ellipse(batch, center, 17, 17, Glow * (0.22f * pulse));
        p.Ellipse(batch, center, 10, 10, Core * 0.85f);
        p.Ellipse(batch, center, 5, 5, Hot);

        var random = new Random(seed * 31 + tick / 2);
        for (var i = 0; i < 3; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var mid = center + dir * 4f + new Vector2(-dir.Y, dir.X) * random.Next(-2, 3);
            UiDraw.Line(batch, p, center + dir * 3f, mid, Hot * 0.9f, 1f);
            UiDraw.Line(batch, p, mid, center + dir * random.Next(7, 10), Core * 0.8f, 1f);
        }
    }

    public static void ImpactSparks(Particles particles, Vector2 at, Random random, int count = 8) =>
        particles.Burst(at, count, 140f, Hot, Glow, 14, random, size: 2f);

    public static void Trail(Particles particles, Vector2 at, Random random) =>
        particles.Emit(at + new Vector2(random.Next(-2, 3), random.Next(-2, 3)), Vector2.Zero, Core, Glow, 10, 2f);
}
