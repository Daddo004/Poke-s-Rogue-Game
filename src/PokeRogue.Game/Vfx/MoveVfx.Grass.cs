using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Grass-type evolutions (phase 2.6): Verdebufera's whirl of leaves and Vigorcolpo's thorny vine.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette GrassPalette = TypeFx.For(PokemonType.Grass);
    private static readonly Color Thorn = new(28, 64, 28);

    /// <summary>Verdebufera: a flurry of leaves with a curved streak of wind behind it along the circle. Alpha batch.</summary>
    private void DrawLeafStorm(SpriteBatch batch, in Projectile projectile)
    {
        var p = _services.Primitives;
        var at = OrbitAt(projectile);
        var center = at - new Vector2(MathF.Cos(projectile.Angle), MathF.Sin(projectile.Angle)) * projectile.OrbitRadius;
        var turn = MathF.Sign(projectile.AngularSpeed);
        var previous = at;
        for (var k = 1; k <= 8; k++)
        {
            var angle = projectile.Angle - turn * k * 0.08f;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * projectile.OrbitRadius;
            UiDraw.Line(batch, p, previous, point, GrassPalette.Hot * (0.75f - k * 0.08f), k < 4 ? 2f : 1f);
            previous = point;
        }

        // A flurry of three small leaves tumbling around each other, rather than one big leaf.
        var flutter = MathF.Sin((_tick + projectile.Serial * 5) * 0.4f) * 0.4f;
        for (var k = 0; k < 3; k++)
        {
            var spin = k * MathF.Tau / 3f + (_tick + projectile.Serial * 3) * 0.2f;
            var offset = new Vector2(MathF.Cos(spin), MathF.Sin(spin) * 0.8f) * 4f;
            MoveShapes.DrawRotated(batch, _shapes.Leaf, at + offset, projectile.Angle + MathF.PI / 2f + flutter + k * 0.9f, Color.White);
        }
    }

    /// <summary>Verdebufera: small leaves torn off the whirl and blown outwards.</summary>
    private void LeafStormBits(in Projectile projectile)
    {
        var outward = new Vector2(MathF.Cos(projectile.Angle), MathF.Sin(projectile.Angle));
        var tangent = new Vector2(-outward.Y, outward.X) * MathF.Sign(projectile.AngularSpeed);
        _dust.Emit(OrbitAt(projectile), tangent * 50f + outward * 25f, GrassPalette.Main, GrassPalette.Dark * 0.4f, 18, 2f, drag: 0.93f);
    }

    /// <summary>Vigorcolpo: a thick dark vine with thorns sweeping a wide arc, a bright smear behind it. Alpha batch.</summary>
    private void DrawPowerWhip(SpriteBatch batch, SlashFx slash)
    {
        var p = _services.Primitives;
        var reach = slash.Reach.Length();
        var facing = MathF.Atan2(slash.Reach.Y, slash.Reach.X);
        var half = slash.SpreadDegrees * MathF.PI / 360f;
        var t = 1f - (1f - slash.Age / (float)SlashFx.Life) * (1f - slash.Age / (float)SlashFx.Life);
        var fade = slash.Age < SlashFx.Life - 3 ? 1f : (SlashFx.Life - slash.Age) / 3f;

        // Smear of the sweep: a fan of strokes, wider and brighter than Frustata's.
        for (var k = 8; k >= 1; k--)
        {
            var past = facing - half + 2f * half * MathF.Max(0f, t - k * 0.05f);
            UiDraw.Line(batch, p, slash.Origin, slash.Origin + new Vector2(MathF.Cos(past), MathF.Sin(past)) * reach, GrassPalette.Spark * ((0.5f - k * 0.05f) * fade), 4f);
        }

        var angle = facing - half + 2f * half * t;
        var tip = slash.Origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
        var normal = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
        var control = (slash.Origin + tip) / 2f + normal * reach * 0.3f * (1f - t);
        Vector2 Curve(float u) => (1 - u) * (1 - u) * slash.Origin + 2 * (1 - u) * u * control + u * u * tip;

        var previous = slash.Origin;
        for (var i = 1; i <= 14; i++)
        {
            var point = Curve(i / 14f);
            UiDraw.Line(batch, p, previous, point, GrassPalette.Dark * fade, 7f);
            UiDraw.Line(batch, p, previous, point, GrassPalette.Main * fade, 4f);
            UiDraw.Line(batch, p, previous, point, GrassPalette.Hot * (0.7f * fade), 1f);
            previous = point;
        }

        // Thorns on alternating sides.
        for (var i = 1; i <= 6; i++)
        {
            var u = i / 7f;
            var at = Curve(u);
            var along = Vector2.Normalize(Curve(MathF.Min(1f, u + 0.03f)) - at + new Vector2(0.001f, 0));
            var outward = new Vector2(-along.Y, along.X) * (i % 2 == 0 ? 1f : -1f);
            UiDraw.Line(batch, p, at + outward * 3f, at + outward * 7f - along * 2f, Thorn * fade, 2f);
        }

        MoveShapes.DrawRotated(batch, _shapes.Leaf, tip, angle, Color.White * fade, 2f);
    }
}
