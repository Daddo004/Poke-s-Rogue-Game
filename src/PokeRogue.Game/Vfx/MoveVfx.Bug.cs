using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Bug-type styles: Forbice X's crossed blades and Sanguisuga's draining bite.</summary>
public sealed partial class MoveVfx
{
    private static readonly Color BloodDeep = new(140, 16, 36);
    private static readonly Color BloodBright = new(255, 70, 90);
    private static readonly Color HealGreen = new(140, 255, 150);

    /// <summary>Forbice X: two blades crossed at right angles, spinning together. Alpha batch.</summary>
    private void DrawScissor(SpriteBatch batch, Vector2 at, float spin, float alpha)
    {
        MoveShapes.DrawRotated(batch, _shapes.Blade, at, spin, Color.White * alpha);
        MoveShapes.DrawRotated(batch, _shapes.Blade, at, spin + MathF.PI / 2f, Color.White * alpha);
    }

    /// <summary>
    /// Sanguisuga: a wavering dark-red thread between the bites, with bright beads running back towards Pikachu
    /// (the life it drains). Alpha batch: additive would turn the red pale.
    /// </summary>
    private void DrawDrainArc(SpriteBatch batch, ArcFx arc)
    {
        var p = _services.Primitives;
        var fade = 1f - arc.Age / (float)ArcFx.Life;
        var delta = arc.To - arc.From;
        var length = delta.Length();
        if (length < 1f)
            return;

        var along = delta / length;
        var side = new Vector2(-along.Y, along.X);
        const int Steps = 10;
        var previous = arc.From;
        for (var i = 1; i <= Steps; i++)
        {
            var u = i / (float)Steps;
            var point = arc.From + delta * u + side * (MathF.Sin(u * MathF.PI * 3f + arc.Seed + arc.Age * 0.8f) * 3f * MathF.Sin(u * MathF.PI));
            UiDraw.Line(batch, p, previous, point, BloodDeep * (0.9f * fade), 2f);
            previous = point;
        }

        for (var k = 0; k < 3; k++)
        {
            var u = 1f - (k / 3f + arc.Age * 0.11f) % 1f;
            var bead = arc.From + delta * u;
            p.Ellipse(batch, bead, 3, 3, BloodBright * fade);
        }
    }

    /// <summary>A bite: red droplets, a few drops of life flying back to Pikachu, and a green glint where it heals.</summary>
    private void DrainImpact(Vector2 at, Vector2 playerAt, int big)
    {
        _flames.Burst(at, 4 * big, 70f, BloodBright, BloodDeep, 12, _random);

        var toPlayer = playerAt - at;
        var distance = toPlayer.Length();
        if (distance > 1f)
        {
            const float Speed = 200f;
            var life = Math.Clamp((int)(distance / (Speed / RunClock.TicksPerSecond)), 4, 40);
            for (var k = 0; k < 3; k++)
                _flames.Emit(at + Jitter(3), toPlayer / distance * Speed + Jitter(15), BloodBright, BloodDeep, life, 2f, drag: 1f);
        }

        _glow.Burst(playerAt, 3, 40f, HealGreen, new Color(60, 200, 80), 14, _random);
    }
}
