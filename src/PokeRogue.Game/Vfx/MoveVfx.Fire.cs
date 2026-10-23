using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Fire-type evolutions (phase 2.6): Lanciafiamme's stream and Fuococarica's blaze. Fire is alpha-blended, like
/// Braciere and Ruotafuoco, so it stays orange on the grass.
/// </summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette FirePalette = TypeFx.For(PokemonType.Fire);

    /// <summary>Lanciafiamme: embers and smoke rising where the stream ends.</summary>
    private void UpdateFlamethrower(MoveBeam beam)
    {
        if (beam.TicksLeft < 4)
            return;

        var (_, end, direction) = JetPoints(beam);
        _flames.Emit(end + Jitter(4), direction * Rand(10f, 40f) + new Vector2(Rand(-15, 15), Rand(-40, -20)), FirePalette.Main, FirePalette.Dark, 12 + _random.Next(6), 3f, drag: 0.9f, stretch: 1.6f);
        if (_tick % 3 == 0)
            _dust.Emit(end + Jitter(4), new Vector2(Rand(-8, 8), Rand(-30, -18)), Smoke * 0.7f, Smoke * 0.1f, 24, 2f, drag: 0.95f);
    }

    /// <summary>
    /// Lanciafiamme: puffs of fire racing away from Pikachu, growing and cooling from yellow to red as they go.
    /// Drawn in three passes (dark edge, body, hot core) so the puffs merge into one flame. Alpha batch.
    /// </summary>
    private void DrawFlamethrower(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var (start, end, direction) = JetPoints(beam);
        var side = new Vector2(-direction.Y, direction.X);
        var length = Vector2.Distance(start, end);
        var age = beam.Lifetime - beam.TicksLeft;
        var reach = length * MathHelper.Clamp(age / 6f, 0f, 1f);
        var alpha = MathHelper.Clamp(beam.TicksLeft / 6f, 0f, 1f);
        const float Spacing = 4f;
        var scroll = _tick * 5f % Spacing;

        for (var pass = 0; pass < 3; pass++)
        {
            for (var d = scroll; d < reach; d += Spacing)
            {
                var u = d / length;
                var at = start + direction * d + side * (MathF.Sin(d * 0.21f - _tick * 0.55f + beam.Serial) * (0.5f + u * 3f));
                var size = 5f + u * beam.HalfWidth * 2.2f;
                switch (pass)
                {
                    case 0:
                        p.Ellipse(batch, at, (int)(size + 3), (int)(size + 3), FirePalette.Dark * (0.85f * alpha));
                        break;
                    case 1:
                        p.Ellipse(batch, at, (int)(size + 1), (int)(size + 1), Color.Lerp(FirePalette.Main, FirePalette.Dark, MathF.Max(0f, u - 0.5f) * 1.2f) * alpha);
                        break;
                    case 2 when u < 0.7f:
                        p.Ellipse(batch, at, (int)(size * 0.5f + 1), (int)(size * 0.5f + 1), Color.Lerp(FirePalette.Hot, FirePalette.Spark, u / 0.7f) * alpha);
                        break;
                }
            }
        }
    }

    /// <summary>Fuococarica: flames licking up all around Pikachu's feet, never higher than its belly.</summary>
    private void FlareCloak(Vector2 feet)
    {
        for (var i = 0; i < 3; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var spot = feet + new Vector2(MathF.Cos(angle) * 12f, MathF.Sin(angle) * 5f);
            _flames.Emit(spot, new Vector2(Rand(-4, 4), Rand(-20, -10)), FirePalette.Main, FirePalette.Dark, 10 + _random.Next(5), 3f, drag: 0.9f, stretch: 2f);
        }

        var hot = Rand(0f, MathF.Tau);
        _flames.Emit(feet + new Vector2(MathF.Cos(hot) * 10f, MathF.Sin(hot) * 4f), new Vector2(0, -14), FirePalette.Hot, FirePalette.Main, 7, 2f, drag: 0.9f, stretch: 2f);
    }

    /// <summary>Fuococarica: the ground glows orange under Pikachu, with a yellow heart. Alpha batch.</summary>
    private void DrawFlareGround(SpriteBatch batch, Vector2 feet)
    {
        var p = _services.Primitives;
        var flicker = _tick % 6 < 3 ? 1 : 0;
        p.Ellipse(batch, feet, 36 + flicker * 2, 15 + flicker, FirePalette.Dark * 0.35f);
        p.Ellipse(batch, feet, 26, 10, FirePalette.Main * 0.45f);
        p.Ellipse(batch, feet, 14, 5, FirePalette.Spark * 0.5f);
    }

    /// <summary>Fuococarica's trail: denser flames than Ruotafuoco's over a wider patch, each with a yellow core.</summary>
    private void UpdateFlareTrail(MoveZone zone)
    {
        var center = zone.Position.ToXna();
        var strength = MathHelper.Clamp(zone.TicksLeft / 20f, 0f, 1f);
        if (_random.NextSingle() > 0.9f * strength + 0.1f)
            return;

        var spot = center + Jitter(zone.Radius * 0.6f) * new Vector2(1f, 0.5f);
        _flames.Emit(spot, new Vector2(Rand(-4, 4), Rand(-30, -16)), FirePalette.Main, FirePalette.Dark, 14 + _random.Next(8), 4f, drag: 0.93f, stretch: 2.2f);
        _flames.Emit(spot + new Vector2(0, 1), new Vector2(Rand(-3, 3), Rand(-24, -12)), FirePalette.Hot, FirePalette.Main, 9, 2f, drag: 0.93f, stretch: 2f);
        if (_random.NextSingle() < 0.1f)
            _glow.Emit(center + Jitter(zone.Radius * 0.5f), new Vector2(Rand(-12, 12), Rand(-60, -36)), FirePalette.Spark, FirePalette.Dark, 24, 1f, drag: 0.97f);
    }
}
