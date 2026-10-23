using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Ghost-type styles: Palla Ombra's dark sphere and Ombra Notturna's long shadow. Darkness cannot be added as light, so both are alpha-blended.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette GhostPalette = TypeFx.For(PokemonType.Ghost);
    private static readonly Color Night = new(28, 12, 44);

    /// <summary>Palla Ombra: dark wisps trailing behind the sphere.</summary>
    private void ShadowTrail(Vector2 at) =>
        _dust.Emit(at + Jitter(4), Jitter(8) + new Vector2(0, -6), GhostPalette.Dark * 0.8f, Night * 0.3f, 16, 3f, drag: 0.92f);

    /// <summary>The sphere: a wobbling dark aura, a near-black body with a violet swirl turning inside, and a pale glint.</summary>
    private void DrawShadowBall(SpriteBatch batch, Vector2 at, float radius, int serial)
    {
        var p = _services.Primitives;
        var size = (int)(radius * 2f + 3f);
        var wobble = (_tick + serial) % 6 < 3 ? 1 : 0;
        p.Ellipse(batch, at, size + 4 + wobble, size + 4 - wobble, GhostPalette.Dark * 0.5f);
        p.Ellipse(batch, at, size, size, Night);

        var swirl = (_tick + serial * 5) * 0.25f;
        p.Ellipse(batch, at + new Vector2(MathF.Cos(swirl), MathF.Sin(swirl)) * (radius * 0.35f), (int)(size * 0.55f), (int)(size * 0.55f), GhostPalette.Main * 0.8f);
        p.Ellipse(batch, at + new Vector2(-radius * 0.35f, -radius * 0.4f), 3, 3, GhostPalette.Hot);
    }

    /// <summary>Darkness splashing off the target, with a few violet sparks.</summary>
    private void ShadowImpact(Vector2 at, int big)
    {
        _dust.Burst(at, 5 * big, 70f, GhostPalette.Dark, Night * 0.4f, 14, _random, size: 2f);
        _glow.Burst(at, 3 * big, 90f, GhostPalette.Spark, GhostPalette.Main, 10, _random);
    }

    /// <summary>
    /// Ombra Notturna: Pikachu's own shadow stretching along the ground to the target, widening towards its tip, then
    /// drawing back. Soft overlapping discs flattened on the ground, darker where they overlap. Ground layer, alpha.
    /// </summary>
    private void DrawShade(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var feet = beam.Origin.ToXna();
        var direction = beam.Direction.ToXna();
        var age = beam.Lifetime - beam.TicksLeft;
        var stretch = MathHelper.Clamp(age / 6f, 0f, 1f) * MathHelper.Clamp(beam.TicksLeft / 6f, 0f, 1f);
        var length = beam.Length * stretch;
        var width = beam.HalfWidth * 2f;

        for (var d = 0f; d <= length; d += 3f)
        {
            var size = MathHelper.Lerp(10f, width * 1.8f, length > 0f ? d / length : 0f);
            p.Ellipse(batch, feet + direction * d, (int)size, (int)(size * 0.6f), Night * 0.2f);
        }
    }
}
