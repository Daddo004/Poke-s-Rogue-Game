using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Poison-type styles beyond Smog's cloud: Velenospina's needles and Tossina's swamp.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette PoisonPalette = TypeFx.For(PokemonType.Poison);

    /// <summary>Tossina: a stain of poison on the ground around Pikachu, with a slowly breathing rim. Alpha batch, under the Pokémon.</summary>
    private void DrawToxicSwamp(SpriteBatch batch, Vector2 center, float radius)
    {
        var p = _services.Primitives;
        p.Ellipse(batch, center, (int)(radius * 2), (int)(radius * 2), PoisonPalette.Dark * 0.22f);
        p.Ellipse(batch, center, (int)(radius * 1.4f), (int)(radius * 1.4f), PoisonPalette.Main * 0.1f);
        var rim = radius + MathF.Sin(_tick * 0.1f) * 1.5f;
        SolidRing(batch, center, rim, rim, PoisonPalette.Main * 0.6f);
    }

    /// <summary>Tossina: bubbles welling up here and there in the swamp.</summary>
    private void ToxicBubbles(Vector2 center, float radius)
    {
        if (_random.NextSingle() >= 0.3f)
            return;

        var angle = Rand(0f, MathF.Tau);
        var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.9f * MathF.Sqrt(_random.NextSingle());
        _bubbles.Add(new Bubble(at, 0, 16 + _random.Next(10), Toxic: true));
    }

    /// <summary>Each pulse: a purple wave washing out to the swamp's edge, and drops of poison splashing up.</summary>
    private void ToxicPulse(Vector2 center, float radius)
    {
        _timed.Add(new TimedFx(FxKind.ToxicRing, center, Vector2.Zero, radius, PoisonPalette.Main, Life: 16));
        for (var i = 0; i < 10; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * Rand(0.3f, 0.95f);
            _flames.Emit(at, new Vector2(Rand(-15, 15), Rand(-70, -40)), PoisonPalette.Spark, PoisonPalette.Dark, 16, 2f, drag: 0.94f, gravity: 7f);
        }
    }

    /// <summary>Velenospina: a thin purple needle along its flight, white at the tip. Alpha batch.</summary>
    private void DrawNeedle(SpriteBatch batch, Vector2 at, Vector2 velocity)
    {
        var p = _services.Primitives;
        var direction = Vector2.Normalize(velocity + new Vector2(0.001f, 0));
        UiDraw.Line(batch, p, at - direction * 5f, at + direction * 3f, PoisonPalette.Dark, 2f);
        UiDraw.Line(batch, p, at - direction * 5f, at + direction * 3f, PoisonPalette.Main);
        p.Rect(batch, new Rectangle((int)MathF.Round(at.X + direction.X * 3f), (int)MathF.Round(at.Y + direction.Y * 3f), 1, 1), Color.White);
    }

    /// <summary>A faint purple wake behind each needle.</summary>
    private void NeedleTrail(Vector2 at)
    {
        if (_tick % 2 == 0)
            _glow.Emit(at + Jitter(1), Vector2.Zero, PoisonPalette.Spark * 0.7f, PoisonPalette.Dark * 0.2f, 8, 1f);
    }
}
