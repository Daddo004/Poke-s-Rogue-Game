using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Psychic-type styles: Confusione's bursts and Ipnosi's hypnotic waves. Their rings are alpha-blended (additive light
/// washes pinks and violets to white on grass); only the sparkles glow.
/// </summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette PsychicPalette = TypeFx.For(PokemonType.Psychic);
    private static readonly Color HypnoViolet = new(150, 90, 255);
    private static readonly Color HypnoPink = new(255, 150, 215);

    /// <summary>Confusione's warning: two pink rings closing in on the target until the burst.</summary>
    private void DrawPsyWarning(SpriteBatch batch, PendingHit pending, float progress)
    {
        var p = _services.Primitives;
        var center = pending.Position.ToXna() - new Vector2(0, BodyHeight * 0.5f);
        for (var k = 0; k < 2; k++)
        {
            var t = (progress + k * 0.5f) % 1f;
            var radius = pending.Radius * (1.6f - 1.2f * t);
            p.Ring(batch, center, radius, radius * 0.6f, PsychicPalette.Main * (0.7f * t));
        }
    }

    /// <summary>The burst: two rings rushing out (drawn as timed effects) and pink sparkles. No shake: the mind is silent.</summary>
    private void PsyBurst(Vector2 at, float radius)
    {
        var center = at - new Vector2(0, BodyHeight * 0.5f);
        _timed.Add(new TimedFx(FxKind.PsyRing, center, Vector2.Zero, radius, PsychicPalette.Main, Life: 14));
        _timed.Add(new TimedFx(FxKind.PsyRing, center, Vector2.Zero, radius * 0.6f, PsychicPalette.Hot, Life: 10));
        _glow.Burst(center, 10, 100f, PsychicPalette.Hot, PsychicPalette.Main, 16, _random);
    }

    /// <summary>Ipnosi: three rippling rings whose violets and pinks keep trading places as they spread.</summary>
    private void DrawHypnoRing(SpriteBatch batch, MoveRing ring)
    {
        var p = _services.Primitives;
        var center = ring.Center.ToXna() - new Vector2(0, BodyHeight * 0.5f);
        var fade = MathF.Sqrt(MathHelper.Clamp(1f - ring.Radius / ring.MaxRadius, 0f, 1f));
        ReadOnlySpan<Color> colors = [HypnoViolet, PsychicPalette.Main, HypnoPink];
        for (var band = 0; band < 3; band++)
        {
            var radius = ring.Radius - band * 6f;
            if (radius < 3f)
                break;

            var color = colors[(band + _tick / 8) % colors.Length] * (fade * (1f - band * 0.25f));
            var steps = Math.Max(24, (int)(MathF.Tau * radius / 2f));
            for (var i = 0; i < steps; i++)
            {
                var angle = i * MathF.Tau / steps;
                var r = radius + MathF.Sin(angle * 6f + _tick * 0.25f + band) * 2f;
                p.Rect(batch, new Rectangle((int)(center.X + MathF.Cos(angle) * r), (int)(center.Y + MathF.Sin(angle) * r), 2, 2), color);
            }
        }
    }

    /// <summary>Glittering motes rising off the hypnotic wave.</summary>
    private void UpdateHypno(MoveRing ring)
    {
        if (_tick % 2 != 0)
            return;

        var angle = Rand(0f, MathF.Tau);
        var at = ring.Center.ToXna() + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring.Radius - new Vector2(0, BodyHeight * 0.5f);
        _glow.Emit(at, new Vector2(0, -12), Color.White, HypnoViolet, 16, 1f);
    }
}
