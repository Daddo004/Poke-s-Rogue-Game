using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Water-type styles: Pistolacqua's jet and Bolla's bubbles. Water is alpha-blended, like fire, so it stays blue.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette Water = TypeFx.For(PokemonType.Water);

    /// <summary>Pistolacqua: spray flying off the far end of the jet, and a little mist along it.</summary>
    private void UpdateJet(MoveBeam beam)
    {
        var (origin, end, direction) = JetPoints(beam);
        for (var i = 0; i < 2; i++)
            _flames.Emit(end + Jitter(3), direction * Rand(20f, 60f) + new Vector2(Rand(-30, 30), Rand(-55, -15)), Water.Hot, Water.Main, 14, 2f, drag: 0.9f, gravity: 6f);

        if (_tick % 3 == 0)
            _glow.Emit(origin + direction * Rand(0f, beam.Length) + Jitter(beam.HalfWidth), Jitter(6), Water.Hot * 0.5f, Water.Main * 0.2f, 10, 1f);
    }

    /// <summary>The jet: a blue band with a dark edge, a pale rippling core and foam streaming along it. Alpha batch.</summary>
    private void DrawJet(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var (start, end, direction) = JetPoints(beam);
        var side = new Vector2(-direction.Y, direction.X);
        var length = Vector2.Distance(start, end);
        var age = beam.Lifetime - beam.TicksLeft;
        var alpha = MathHelper.Clamp(age / 3f, 0f, 1f) * MathHelper.Clamp(beam.TicksLeft / 5f, 0f, 1f);
        var width = beam.HalfWidth * 2f;

        UiDraw.Line(batch, p, start, end, Water.Dark * (0.85f * alpha), width + 2f);
        UiDraw.Line(batch, p, start, end, Water.Main * alpha, width);

        // The core ripples sideways, the ripple running away from Pikachu.
        const float Segment = 3f;
        for (var d = 0f; d < length; d += Segment)
        {
            var next = MathF.Min(length, d + Segment);
            var a = start + direction * d + side * Ripple(d);
            var b = start + direction * next + side * Ripple(next);
            UiDraw.Line(batch, p, a, b, Water.Hot * alpha, MathF.Max(1f, width / 3f));
        }

        // Foam flecks carried along by the water.
        for (var k = 0; k < (int)(length / 9f); k++)
        {
            var d = (k * 9f + _tick * 5f) % length;
            var offset = side * ((k % 2 == 0 ? 1f : -1f) * width * 0.3f);
            var at = start + direction * d + offset;
            p.Rect(batch, new Rectangle((int)at.X, (int)at.Y, 2, 1), Color.White * (0.8f * alpha));
        }

        p.Ellipse(batch, end, (int)(width + 7), (int)(width + 5), Water.Hot * (0.85f * alpha));
        p.Ellipse(batch, start, (int)(width + 1), (int)(width + 1), Water.Main * alpha);

        float Ripple(float d) => MathF.Sin(d * 0.35f - _tick * 0.6f) * MathF.Max(1f, width * 0.2f);
    }

    /// <summary>The jet leaves just in front of Pikachu, at mouth height, so it never covers the face.</summary>
    private static (Vector2 Start, Vector2 End, Vector2 Direction) JetPoints(MoveBeam beam)
    {
        var origin = beam.Origin.ToXna() - new Vector2(0, BodyHeight);
        var direction = beam.Direction.ToXna();
        return (origin + direction * 12f, origin + direction * beam.Length, direction);
    }

    /// <summary>Bolla: a wobbling see-through bubble with a bright rim and a glint. Alpha batch.</summary>
    private void DrawBubble(SpriteBatch batch, float radius, Vector2 at, int serial)
    {
        var p = _services.Primitives;
        var wobble = MathF.Sin((_tick + serial * 7) * 0.3f);
        var width = (int)MathF.Round(radius * 2f + 1f + wobble);
        var height = (int)MathF.Round(radius * 2f + 1f - wobble);
        p.Ellipse(batch, at, width, height, Water.Spark * 0.45f);
        SolidRing(batch, at, width / 2f, height / 2f, new Color(225, 245, 255) * 0.95f);

        var glint = new Point((int)(at.X - width * 0.3f), (int)(at.Y - height * 0.3f));
        p.Rect(batch, new Rectangle(glint.X, glint.Y, 2, 2), Color.White);
        p.Rect(batch, new Rectangle(glint.X + 3, glint.Y - 1, 1, 1), Color.White * 0.8f);
    }

    /// <summary>An unbroken one-pixel ellipse (<see cref="Primitives.Ring"/> spaces its dots for big rings).</summary>
    private void SolidRing(SpriteBatch batch, Vector2 center, float radiusX, float radiusY, Color color)
    {
        var p = _services.Primitives;
        var steps = Math.Max(24, (int)(MathF.Tau * MathF.Max(radiusX, radiusY) * 1.5f));
        var last = new Point(int.MinValue, int.MinValue);
        for (var i = 0; i < steps; i++)
        {
            var angle = i * MathF.Tau / steps;
            var point = new Point((int)MathF.Round(center.X + MathF.Cos(angle) * radiusX), (int)MathF.Round(center.Y + MathF.Sin(angle) * radiusY));
            if (point == last)
                continue;

            p.Rect(batch, new Rectangle(point.X, point.Y, 1, 1), color);
            last = point;
        }
    }

    /// <summary>Water hitting something: droplets, and a ring flash when a bubble pops.</summary>
    private void Splash(Vector2 at, int big, bool pop)
    {
        if (pop)
            _timed.Add(new TimedFx(FxKind.Pop, at, Vector2.Zero, 0f, Water.Hot, Life: 7));

        for (var i = 0; i < 5 * big; i++)
            _flames.Emit(at + Jitter(2), new Vector2(Rand(-60, 60), Rand(-80, -20)), Water.Hot, Water.Main, 14, 2f, drag: 0.9f, gravity: 6f);
    }
}
