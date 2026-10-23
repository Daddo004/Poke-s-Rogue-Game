using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Steel-type styles: Vortexpalla's spinning ball and Cannonflash's silver beam.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette SteelPalette = TypeFx.For(PokemonType.Steel);
    private static readonly Color SteelDark = new(80, 86, 104);
    private static readonly Color SteelBody = new(168, 176, 194);
    private static readonly Color SteelShine = new(236, 240, 250);
    private static readonly Color SparkOrange = new(255, 190, 90);

    /// <summary>Vortexpalla: a steel ball with two dark bands turning fast across it under a fixed shine. Alpha batch.</summary>
    private void DrawGyro(SpriteBatch batch, Vector2 at, float radius, int serial, float alpha)
    {
        var p = _services.Primitives;
        var size = (int)(radius * 2f + 1f);
        p.Ellipse(batch, at, size + 2, size + 2, SteelDark * alpha);
        p.Ellipse(batch, at, size, size, SteelBody * alpha);

        // Shade the lower right, so the ball reads round.
        p.Ellipse(batch, at + new Vector2(radius * 0.25f, radius * 0.3f), (int)(size * 0.75f), (int)(size * 0.7f), SteelDark * (0.35f * alpha));
        p.Ellipse(batch, at + new Vector2(-radius * 0.1f, -radius * 0.1f), (int)(size * 0.7f), (int)(size * 0.7f), SteelBody * (0.8f * alpha));

        var spin = (_tick + serial * 7) * 0.6f;
        for (var k = 0; k < 2; k++)
        {
            var angle = spin + k * MathF.PI / 2f;
            var across = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.5f) * (radius * 0.8f);
            UiDraw.Line(batch, p, at - across, at + across, SteelDark * (0.9f * alpha), 2f);
        }

        p.Ellipse(batch, at + new Vector2(-radius * 0.35f, -radius * 0.4f), 3, 3, SteelShine * alpha);
    }

    /// <summary>Sparks thrown off sideways by the spinning ball.</summary>
    private void GyroSparks(Vector2 at, Vector2 velocity)
    {
        var forward = Vector2.Normalize(velocity + new Vector2(0.001f, 0));
        var side = new Vector2(-forward.Y, forward.X) * (_random.Next(2) == 0 ? 1f : -1f);
        _glow.Emit(at + side * 5f, side * Rand(60, 110) - forward * 20f, Color.White, SparkOrange, 10, 1f, drag: 0.88f);
    }

    /// <summary>Metal on body: a white star and a spray of hot sparks.</summary>
    private void GyroClang(Vector2 at, int big)
    {
        _timed.Add(new TimedFx(FxKind.Star, at, Vector2.Zero, 1f, Color.White, Life: 7));
        _glow.Burst(at, 6 * big, 130f, Color.White, SparkOrange, 10, _random);
    }

    /// <summary>Cannonflash: glints racing along the beam.</summary>
    private void UpdateFlash(MoveBeam beam)
    {
        var origin = beam.Origin.ToXna() - new Vector2(0, BodyHeight);
        var direction = beam.Direction.ToXna();
        _glow.Emit(origin + direction * Rand(12f, beam.Length) + Jitter(beam.HalfWidth), direction * 60f, Color.White, SteelPalette.Main, 8, 1f);
    }

    /// <summary>The beam: a silver glow, a flickering bright body and a white core, flaring at the muzzle and at the target. Additive.</summary>
    private void DrawFlash(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var origin = beam.Origin.ToXna() - new Vector2(0, BodyHeight);
        var direction = beam.Direction.ToXna();
        var start = origin + direction * 12f;
        var end = origin + direction * beam.Length;
        var age = beam.Lifetime - beam.TicksLeft;
        var alpha = MathHelper.Clamp(age / 3f, 0f, 1f) * MathHelper.Clamp(beam.TicksLeft / 5f, 0f, 1f);
        var width = beam.HalfWidth * 2f;
        var flicker = _tick % 4 < 2 ? 1f : 0.85f;

        UiDraw.Line(batch, p, start, end, SteelPalette.Main * (0.3f * alpha), width + 8f);
        UiDraw.Line(batch, p, start, end, SteelShine * (0.7f * alpha * flicker), width);
        UiDraw.Line(batch, p, start, end, Color.White * alpha, MathF.Max(1f, width / 3f));

        // A small muzzle flash, kept off Pikachu's face, and a bigger flare where the light lands.
        p.Ellipse(batch, start, (int)(width + 2), (int)(width + 2), Color.White * (0.4f * alpha));
        p.Ellipse(batch, end, (int)(width + 10), (int)(width + 10), SteelPalette.Main * (0.5f * alpha));
    }
}
