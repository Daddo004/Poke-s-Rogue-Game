using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Electric-type styles beyond Thundershock's bolts: Tuononda's static field and Tuono's bolts from the sky.</summary>
public sealed partial class MoveVfx
{
    private static readonly Color Scorch = new(70, 60, 44);

    /// <summary>Tuono: where a bolt lands, sparks fly off the feet, a puff of scorched earth rises, the camera jolts.</summary>
    private void ThunderStrike(Vector2 feet)
    {
        ElectricFx.ImpactSparks(_glow, feet - new Vector2(0, 2), _random, 6);
        _dust.Burst(feet, 3, 40f, Scorch, Scorch * 0.3f, 16, _random, size: 2f);
        Shake.AddFromMove(0.04f);
    }

    /// <summary>Tuono: a bolt falls from the sky onto each enemy of the chain, with a flash where it lands. Additive.</summary>
    private void DrawThunder(SpriteBatch batch, ArcFx arc)
    {
        var p = _services.Primitives;
        var fade = 1f - arc.Age / (float)ArcFx.Life;
        var target = arc.To;
        var sky = target + new Vector2(arc.Seed % 13 - 6, -170f);

        UiDraw.Line(batch, p, sky, target, ElectricFx.Glow * (0.16f * fade), 12f);
        ElectricFx.Bolt(batch, p, sky, target, arc.Seed + arc.Age / 2, fade);
        ElectricFx.Bolt(batch, p, sky + new Vector2(2, 0), target, arc.Seed / 3 + 1 + arc.Age / 2, 0.5f * fade);

        var feet = target + new Vector2(0, BodyHeight);
        p.Ellipse(batch, feet, 20 + arc.Age * 3, 8 + arc.Age, ElectricFx.Core * (0.45f * fade));
        p.Ellipse(batch, target, 14, 14, ElectricFx.Hot * (0.5f * fade));
    }

    /// <summary>Tuononda: a faint field on the ground, its edge dotted with charge. Alpha batch.</summary>
    private void DrawStaticField(SpriteBatch batch, Vector2 center, float radius)
    {
        var p = _services.Primitives;
        p.Ellipse(batch, center, (int)(radius * 2), (int)(radius * 2), ElectricFx.Glow * 0.08f);

        var dots = Math.Max(12, (int)(radius / 3f));
        for (var k = 0; k < dots; k++)
        {
            var angle = k * MathF.Tau / dots + _tick * 0.02f;
            var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            p.Rect(batch, new Rectangle((int)at.X, (int)at.Y, 1, 1), ElectricFx.Core * 0.5f);
        }
    }

    /// <summary>Little bolts crawling along the field's edge, re-rolled every other tick. Additive.</summary>
    private void DrawStaticSparks(SpriteBatch batch, Vector2 center, float radius)
    {
        var p = _services.Primitives;
        var random = new Random(_tick / 2 * 7919);
        for (var k = 0; k < 3; k++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-outward.Y, outward.X);
            var a = center + outward * radius - tangent * 4f;
            var mid = center + outward * (radius + random.Next(-3, 4));
            var b = center + outward * radius + tangent * 4f;
            UiDraw.Line(batch, p, a, mid, ElectricFx.Core * 0.8f);
            UiDraw.Line(batch, p, mid, b, ElectricFx.Core * 0.8f);
            p.Rect(batch, new Rectangle((int)mid.X, (int)mid.Y, 1, 1), ElectricFx.Hot);
        }
    }

    /// <summary>Each pulse: a jagged ring of electricity spreading out, and sparks shooting off.</summary>
    private void StaticPulse(Vector2 center, float radius)
    {
        _timed.Add(new TimedFx(FxKind.StaticRing, center, Vector2.Zero, radius, ElectricFx.Core, Life: 12));
        for (var i = 0; i < 8; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _glow.Emit(center + outward * radius * 0.5f, outward * Rand(60, 110), ElectricFx.Hot, ElectricFx.Glow, 10, 1f, drag: 0.88f);
        }
    }

    /// <summary>A ring whose points jitter anew every other tick, so it crackles as it grows. Additive.</summary>
    private void DrawStaticRing(SpriteBatch batch, Vector2 center, float radius, int age, float alpha)
    {
        var p = _services.Primitives;
        var random = new Random(age / 2 * 31 + (int)center.X * 7 + (int)center.Y);
        var count = Math.Clamp((int)(MathF.Tau * radius / 6f), 16, 96);
        Span<Vector2> points = stackalloc Vector2[count];
        for (var k = 0; k < count; k++)
        {
            var angle = k * MathF.Tau / count;
            points[k] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius + random.Next(-3, 4));
        }

        for (var k = 0; k < count; k++)
            UiDraw.Line(batch, p, points[k], points[(k + 1) % count], ElectricFx.Glow * (0.4f * alpha), 3f);
        for (var k = 0; k < count; k++)
            UiDraw.Line(batch, p, points[k], points[(k + 1) % count], ElectricFx.Core * alpha);
    }
}
