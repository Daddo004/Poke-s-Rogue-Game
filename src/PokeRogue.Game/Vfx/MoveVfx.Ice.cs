using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Ice-type styles beyond Raggiaurora: Geloscheggia's orbiting shards and Geloraggio's frozen beam.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette IcePalette = TypeFx.For(PokemonType.Ice);

    /// <summary>Geloraggio: frost flakes drifting off the beam.</summary>
    private void UpdateIceBeam(MoveBeam beam)
    {
        var (start, end, direction) = IceBeamPoints(beam);
        var along = Rand(0f, Vector2.Distance(start, end));
        _dust.Emit(start + direction * along + Jitter(beam.HalfWidth), new Vector2(Rand(-10, 10), Rand(-14, -4)), Color.White, IcePalette.Main, 18, 1f, drag: 0.95f);
    }

    /// <summary>The beam is wide: it leaves a full width away from Pikachu, so it never covers its body or face.</summary>
    private static (Vector2 Start, Vector2 End, Vector2 Direction) IceBeamPoints(MoveBeam beam)
    {
        var (start, end, direction) = JetPoints(beam);
        return (start + direction * beam.HalfWidth, end, direction);
    }

    /// <summary>
    /// Geloraggio: a pale blue band with a dark rim and a white core, ice crystals glinting in turn along its edges,
    /// and a bigger crystal where it lands. Alpha batch; the soft halo is additive (<see cref="DrawIceGlow"/>).
    /// </summary>
    private void DrawIceBeam(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var (start, end, direction) = IceBeamPoints(beam);
        var side = new Vector2(-direction.Y, direction.X);
        var length = Vector2.Distance(start, end);
        var alpha = IceBeamAlpha(beam);
        var width = beam.HalfWidth * 2f;

        UiDraw.Line(batch, p, start, end, IcePalette.Dark * alpha, width + 2f);
        UiDraw.Line(batch, p, start, end, IcePalette.Main * alpha, width);
        UiDraw.Line(batch, p, start, end, Color.White * alpha, MathF.Max(2f, width / 3f));

        for (var d = 10f; d < length - 4f; d += 14f)
        {
            var k = (int)(d / 14f);
            var at = start + direction * d + side * ((k % 2 == 0 ? 1f : -1f) * (width / 2f + 1f));
            Crystal(at, (_tick / 3 + k) % 5 == 0 ? 3 : 2);
        }

        p.Ellipse(batch, end, (int)(width + 6), (int)(width + 6), IcePalette.Main * alpha);
        Crystal(end, 5);

        void Crystal(Vector2 at, int arm)
        {
            p.Rect(batch, new Rectangle((int)at.X - arm, (int)at.Y, arm * 2 + 1, 1), Color.White * alpha);
            p.Rect(batch, new Rectangle((int)at.X, (int)at.Y - arm, 1, arm * 2 + 1), Color.White * alpha);
        }
    }

    /// <summary>Geloraggio's cold halo. Additive.</summary>
    private void DrawIceGlow(SpriteBatch batch, MoveBeam beam)
    {
        var (start, end, _) = IceBeamPoints(beam);
        UiDraw.Line(batch, _services.Primitives, start, end, IcePalette.Main * (0.22f * IceBeamAlpha(beam)), beam.HalfWidth * 2f + 8f);
    }

    private static float IceBeamAlpha(MoveBeam beam) =>
        MathHelper.Clamp((beam.Lifetime - beam.TicksLeft) / 3f, 0f, 1f) * MathHelper.Clamp(beam.TicksLeft / 5f, 0f, 1f);

    /// <summary>Geloscheggia: frost specks shed by the shards.</summary>
    private void ShardTrail(Vector2 at)
    {
        if (_tick % 3 == 0)
            _glow.Emit(at + Jitter(2), Jitter(6), Color.White, IcePalette.Main, 10, 1f);
    }

    private void ShardImpact(Vector2 at, int big)
    {
        _dust.Burst(at, 5 * big, 90f, Color.White, IcePalette.Main, 12, _random);
        _glow.Burst(at, 3 * big, 60f, IcePalette.Hot, IcePalette.Main * 0.4f, 8, _random);
    }
}
