using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Fighting-type styles: Colpokarate's chop and Forzasfera's aura sphere (blue, as in the games).</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette FightingPalette = TypeFx.For(PokemonType.Fighting);
    private static readonly Color AuraBlue = new(90, 150, 255);
    private static readonly Color AuraPale = new(210, 235, 255);
    private static readonly Color AuraDeep = new(40, 70, 200);

    /// <summary>
    /// Colpokarate: a white crescent cut across the sector in a few frames, edged in the type's orange, with speed lines
    /// from Pikachu. Alpha batch.
    /// </summary>
    private void DrawChop(SpriteBatch batch, SlashFx slash)
    {
        var p = _services.Primitives;
        var reach = slash.Reach.Length();
        var facing = MathF.Atan2(slash.Reach.Y, slash.Reach.X);
        var half = slash.SpreadDegrees * MathF.PI / 360f;
        var t = slash.Age / (float)SlashFx.Life;
        var sweep = MathF.Min(1f, t * 2.5f);
        var fade = t < 0.5f ? 1f : (1f - t) / 0.5f;
        var radius = reach * 0.8f;

        const int Steps = 14;
        for (var i = 0; i < Steps; i++)
        {
            var a0 = facing - half + 2f * half * sweep * i / Steps;
            var a1 = facing - half + 2f * half * sweep * (i + 1) / Steps;
            var from = slash.Origin + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var to = slash.Origin + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;

            // A crescent: thickest in the middle of the cut.
            var thickness = 1f + 3f * MathF.Sin(MathF.PI * (i + 0.5f) / Steps);
            UiDraw.Line(batch, p, from, to, FightingPalette.Main * (0.8f * fade), thickness + 2f);
            UiDraw.Line(batch, p, from, to, FightingPalette.Hot * fade, thickness);
        }

        for (var k = -1; k <= 1; k++)
        {
            var angle = facing + k * half * 0.6f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            UiDraw.Line(batch, p, slash.Origin + direction * radius * 0.35f, slash.Origin + direction * radius * 0.7f, Color.White * (0.5f * fade));
        }
    }

    private void ChopImpact(Vector2 at, int big)
    {
        _timed.Add(new TimedFx(FxKind.Star, at, Vector2.Zero, 1f, Color.White, Life: 7));
        _glow.Burst(at, 5 * big, 130f, FightingPalette.Hot, FightingPalette.Main, 10, _random);
    }

    /// <summary>Forzasfera: blue wisps streaming off the sphere.</summary>
    private void SphereTrail(Vector2 at) =>
        _glow.Emit(at + Jitter(3), Jitter(10), AuraBlue * 0.8f, AuraDeep * 0.3f, 14, 2f, drag: 0.9f);

    /// <summary>The sphere: a pulsing blue halo, a bright body, a white core and two specks of aura circling it. Additive.</summary>
    private void DrawSphere(SpriteBatch batch, Vector2 at, int serial)
    {
        var p = _services.Primitives;
        var pulse = (_tick + serial) % 8 < 4 ? 2 : 0;
        p.Ellipse(batch, at, 19 + pulse, 19 + pulse, AuraBlue * 0.3f);
        p.Ellipse(batch, at, 12, 12, AuraBlue * 0.9f);
        p.Ellipse(batch, at, 7, 7, AuraPale);
        p.Ellipse(batch, at + new Vector2(-1, -1), 3, 3, Color.White);

        for (var k = 0; k < 2; k++)
        {
            var angle = (_tick + serial * 5) * 0.35f + k * MathF.PI;
            var speck = at + new Vector2(MathF.Cos(angle) * 8f, MathF.Sin(angle) * 4f);
            p.Rect(batch, new Rectangle((int)speck.X, (int)speck.Y, 2, 2), AuraPale);
        }
    }

    private void SphereImpact(Vector2 at, int big)
    {
        _timed.Add(new TimedFx(FxKind.Pop, at, Vector2.Zero, 0f, AuraPale, Life: 9));
        _glow.Burst(at, 8 * big, 120f, AuraPale, AuraBlue, 14, _random, size: 2f);
    }
}
