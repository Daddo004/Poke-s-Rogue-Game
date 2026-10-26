using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Dragon-type styles: Tornado's twisters and Dragobolide's meteors (orange, as in the games).</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette DragonPalette = TypeFx.For(PokemonType.Dragon);
    private static readonly Color MeteorOrange = new(255, 140, 40);
    private static readonly Color MeteorRed = new(170, 40, 20);
    private static readonly Color MeteorRock = new(90, 50, 40);
    private static readonly Color Dust = new(200, 190, 170);

    /// <summary>Where a falling meteor is: it comes down from the upper right, speeding up, and lands at the target.</summary>
    private static Vector2 MeteorAt(PendingHit pending, float progress)
    {
        var left = 1f - progress;
        return pending.Position.ToXna() + new Vector2(80f, -160f) * (left * left) - new Vector2(0, 4f);
    }

    /// <summary>Tornado: dust whirled up at the twister's foot.</summary>
    private void TwisterDust(Vector2 feet) =>
        _dust.Emit(feet + Jitter(4), new Vector2(Rand(-20, 20), Rand(-14, -4)), Dust * 0.8f, Dust * 0.2f, 12, 1f, drag: 0.9f);

    /// <summary>A small twister: stacked rings, narrow at the ground and wide at the top, each swaying on its own. Alpha batch.</summary>
    private void DrawTwister(SpriteBatch batch, Vector2 feet, int serial)
    {
        var p = _services.Primitives;
        const int Rings = 8;
        for (var level = 0; level < Rings; level++)
        {
            var sway = MathF.Sin((_tick + serial * 5) * 0.3f + level * 0.6f) * (1f + level * 0.4f);
            var at = new Vector2(feet.X + sway, feet.Y - level * 3f);
            var width = (int)(4 + level * 1.6f);
            var color = level % 2 == 0 ? DragonPalette.Main * 0.75f : Color.White * 0.6f;
            p.Ellipse(batch, at, width, 3, color);
        }
    }

    /// <summary>Fire peeling off a falling meteor.</summary>
    private void MeteorTrail(Vector2 at)
    {
        var back = Vector2.Normalize(new Vector2(80f, -160f));
        _flames.Emit(at + Jitter(2), back * Rand(30f, 60f) + Jitter(10), MeteorOrange, MeteorRed, 14 + _random.Next(6), 3f, drag: 0.9f);
        if (_tick % 2 == 0)
            _glow.Emit(at + Jitter(3), back * 40f + Jitter(15), new Color(255, 230, 150), MeteorOrange * 0.4f, 10, 1f);
    }

    /// <summary>The meteor: a molten rim around a dark rock with a white-hot spot, bigger for wider blasts. Alpha batch.</summary>
    private void DrawMeteor(SpriteBatch batch, Vector2 at, float radius)
    {
        var p = _services.Primitives;
        var scale = MathHelper.Clamp(radius / 22f, 0.8f, 1.6f);
        p.Ellipse(batch, at, (int)(13 * scale), (int)(13 * scale), MeteorOrange);
        p.Ellipse(batch, at + new Vector2(1, 1), (int)(9 * scale), (int)(9 * scale), MeteorRock);
        p.Ellipse(batch, at + new Vector2(-2, -2), (int)(3 * scale), (int)(3 * scale), new Color(255, 220, 120));
    }

    /// <summary>The landing spot: an orange ring tightening and brightening as the meteor nears. Alpha batch.</summary>
    private void DrawMeteorMark(SpriteBatch batch, PendingHit pending, float progress)
    {
        var p = _services.Primitives;
        var center = pending.Position.ToXna();
        var radius = pending.Radius * (1.15f - 0.3f * progress);
        p.Ring(batch, center, radius, radius * 0.55f, MeteorOrange * (0.35f + 0.5f * progress), 2);
        if (_tick / 4 % 2 == 0)
            p.Ring(batch, center, radius * 0.5f, radius * 0.28f, MeteorOrange * (0.3f * progress));
    }

    /// <summary>The blast: a ring of dust, fire and sparks thrown up, rubble raining back, and a strong shake.</summary>
    private void MeteorImpact(Vector2 at, float radius)
    {
        _timed.Add(new TimedFx(FxKind.DustRing, at, Vector2.Zero, radius * 1.2f, MeteorOrange, Life: 16));
        for (var i = 0; i < 16; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
            _flames.Emit(at + outward * 4f, outward * Rand(50, 120) + new Vector2(0, -50), MeteorOrange, MeteorRed, 16 + _random.Next(8), 3f, drag: 0.9f, gravity: 3f);
            if (i % 2 == 0)
                _dust.Emit(at + outward * 3f, outward * Rand(40, 90) + new Vector2(0, -70), MeteorRock, MeteorRock * 0.5f, 22, 2f, drag: 0.93f, gravity: 7f);
        }

        _glow.Burst(at - new Vector2(0, 6), 10, 140f, new Color(255, 240, 180), MeteorOrange, 14, _random, size: 2f);
        Shake.AddFromMove(0.3f);
    }
}
