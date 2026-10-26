using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Rock-type styles beyond Frana's boulders: Rocciotomba's stone spikes and Pietrataglio's sharp stones.</summary>
public sealed partial class MoveVfx
{
    private static readonly TypeFxPalette RockPalette = TypeFx.For(PokemonType.Rock);
    private static readonly Color Crack = new(60, 48, 36);

    /// <summary>Pietrataglio: a shadow and cracks spreading on the ground where the stones will burst out. Alpha batch.</summary>
    private void DrawStoneEdgeWarning(SpriteBatch batch, PendingHit pending, float progress)
    {
        var p = _services.Primitives;
        var center = pending.Position.ToXna();
        var r = pending.Radius * (0.3f + 0.7f * progress);
        p.Ellipse(batch, center, (int)(r * 2), (int)r, Color.Black * (0.12f + 0.2f * progress));
        for (var k = 0; k < 5; k++)
        {
            var angle = k * MathF.Tau / 5f + pending.StartTick * 0.7f;
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.5f);
            UiDraw.Line(batch, p, center, center + outward * r, Crack * (0.5f + 0.4f * progress));
        }
    }

    /// <summary>Pietrataglio lands: the stones burst up, dust rings out, rubble flies.</summary>
    private void StoneEdgeImpact(Vector2 at, float radius)
    {
        _timed.Add(new TimedFx(FxKind.StoneSpikes, at, Vector2.Zero, radius, Color.White, Life: 26));
        _timed.Add(new TimedFx(FxKind.DustRing, at, Vector2.Zero, radius, RockPalette.Spark, Life: 14));
        for (var i = 0; i < 8; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
            var color = i % 3 == 0 ? RockPalette.Dark : RockPalette.Main;
            _dust.Emit(at + dir * 4, dir * Rand(40, 90) + new Vector2(0, -70), color, color * 0.6f, 20, 2f, drag: 0.93f, gravity: 7f);
        }

        Shake.AddFromMove(0.12f);
    }

    /// <summary>Pietrataglio: three sharp stones shooting out of the ground, a tall one in front, then sinking back. Alpha batch.</summary>
    private void DrawStoneSpikes(SpriteBatch batch, TimedFx fx)
    {
        var rise = MathHelper.Clamp(fx.Age / 3f, 0f, 1f) * MathHelper.Clamp((fx.Life - fx.Age) / 7f, 0f, 1f);
        if (rise <= 0f)
            return;

        var texture = _shapes.Spike;
        var spread = fx.Value * 0.45f;
        Span<(Vector2 Foot, float Scale)> stones = [(fx.Position + new Vector2(-spread, -2), 1f), (fx.Position + new Vector2(spread, -1), 1f), (fx.Position + new Vector2(0, 2), 2f)];
        foreach (var (foot, scale) in stones)
        {
            var visible = (int)MathF.Ceiling(texture.Height * rise);
            var topLeft = new Vector2(MathF.Round(foot.X - texture.Width * scale / 2f), MathF.Round(foot.Y - visible * scale));
            batch.Draw(texture, topLeft, new Rectangle(0, 0, texture.Width, visible), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // A glint on the tall stone's tip as it breaks through.
        if (fx.Age is >= 2 and < 6)
        {
            var tip = fx.Position + new Vector2(0, 2 - texture.Height * 2f * rise);
            _services.Primitives.Rect(batch, new Rectangle((int)tip.X, (int)tip.Y + 2, 1, 1), Color.White);
        }
    }

    /// <summary>Rocciotomba: a burst of dust as the stones break through, a few pebbles while they stand, rubble as they sink.</summary>
    private void UpdatePillars(MoveZone zone, bool fresh)
    {
        var center = zone.Position.ToXna();
        if (fresh)
        {
            Shake.AddFromMove(0.06f);
            for (var i = 0; i < 10; i++)
            {
                var angle = Rand(0f, MathF.Tau);
                var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
                _dust.Emit(center + outward * zone.Radius * 0.7f, outward * Rand(30, 70) + new Vector2(0, -40), RockPalette.Spark, RockPalette.Dark * 0.5f, 18, 2f, drag: 0.92f, gravity: 6f);
            }
        }

        if (zone.TicksLeft is > 3 and < 8 || (_tick % 20 == 0 && zone.TicksLeft > 10))
            _dust.Emit(center + Jitter(zone.Radius * 0.6f) * new Vector2(1f, 0.6f), new Vector2(Rand(-15, 15), Rand(-30, -10)), RockPalette.Main, RockPalette.Dark * 0.5f, 14, 2f, drag: 0.92f, gravity: 6f);
    }

    /// <summary>The broken ground around the stones: a dark hollow with a few cracks. Alpha batch.</summary>
    private void DrawCrater(SpriteBatch batch, MoveZone zone, float alpha)
    {
        var p = _services.Primitives;
        var center = zone.Position.ToXna();
        var r = zone.Radius;
        p.Ellipse(batch, center, (int)(r * 2), (int)(r * 1.1f), new Color(60, 48, 36) * (0.55f * alpha));
        for (var k = 0; k < 5; k++)
        {
            var angle = k * MathF.Tau / 5f + zone.Serial;
            var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f);
            UiDraw.Line(batch, p, center + outward * r * 0.5f, center + outward * r * 1.1f, new Color(48, 38, 30) * (0.8f * alpha));
        }
    }

    /// <summary>The stones: a ring of spikes and one in the middle, rising tip first, sinking at the end. Alpha batch.</summary>
    private void DrawPillars(SpriteBatch batch, MoveZone zone)
    {
        var center = zone.Position.ToXna();
        var age = zone.Lifetime - zone.TicksLeft;
        var rise = MathHelper.Clamp(age / 5f, 0f, 1f) * MathHelper.Clamp(zone.TicksLeft / 8f, 0f, 1f);
        if (rise <= 0f)
            return;

        var count = 5 + (int)(zone.Radius / 6f);
        Span<(Vector2 Foot, int Height)> spikes = stackalloc (Vector2, int)[count + 1];
        for (var k = 0; k < count; k++)
        {
            var angle = k * MathF.Tau / count + zone.Serial * 0.9f;
            var h = Hash(k, zone.Serial);
            var foot = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * zone.Radius * (0.7f + h % 20 / 100f);
            spikes[k] = (foot, 8 + h % 5);
        }

        spikes[count] = (center + new Vector2(0, 1), _shapes.Spike.Height);

        // Back to front, so nearer stones overlap the ones behind.
        spikes.Sort(static (a, b) => a.Foot.Y.CompareTo(b.Foot.Y));
        foreach (var (foot, height) in spikes)
        {
            var visible = (int)MathF.Ceiling(height * rise);
            var texture = _shapes.Spike;
            var source = new Rectangle(0, 0, texture.Width, Math.Min(texture.Height, visible));
            var scale = height / (float)texture.Height;
            var topLeft = new Vector2(MathF.Round(foot.X - texture.Width * scale / 2f), MathF.Round(foot.Y - visible * scale));
            batch.Draw(texture, topLeft, source, Color.White, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
        }
    }
}
