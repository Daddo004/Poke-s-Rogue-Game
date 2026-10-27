using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Kecleon the merchant in the field (phase 9.2): it stands on a striped mat with its wares, camouflaged (a faint shimmer)
/// until the player comes close, turns to face it, and a bubble with a coin bobs over its head. It arrives and leaves in a
/// puff of smoke, and blinks in its last seconds.
/// </summary>
public sealed class MerchantVfx(GameServices services)
{
    public const int SpeciesId = 352;

    /// <summary>Fully seen within this distance; camouflaged beyond twice it.</summary>
    private const float SeenDistance = 90f;

    private const int ArriveTicks = 20;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color MatLight = new(236, 200, 96);
    private static readonly Color MatDark = new(186, 72, 96);
    private static readonly Color Smoke = new(220, 220, 230);
    public static readonly Color Green = new(120, 210, 110);

    private readonly Particles _particles = new(128);
    private readonly Random _random = new(352);
    private SpriteAnimator _animator;
    private int _tick;
    private float _seen;

    public void Reset()
    {
        _animator = default;
        _seen = 0f;
    }

    public void OnEvent(in RunEvent e)
    {
        if (e.Type is RunEventType.MerchantArrived or RunEventType.MerchantLeft)
            Puff(e.Position.ToXna());
    }

    public void Update(RunState run)
    {
        _tick++;
        _particles.Update();
        if (!run.Merchant.Present || services.Sprites.Get(SpeciesId) is not { } set)
            return;

        if (set.Resolve(PmdAnimations.Idle) is { } idle)
            _animator.Play(idle);
        _animator.Update();

        var distance = Vector2.Distance(run.Player.Position.ToXna(), run.Merchant.Position.ToXna());
        var target = MathHelper.Clamp(1f - (distance - SeenDistance) / SeenDistance, 0f, 1f);
        _seen += (target - _seen) * 0.12f;
    }

    /// <summary>The mat and the shadow. World space, alpha batch, under everyone.</summary>
    public void DrawGround(SpriteBatch batch, RunState run)
    {
        if (!run.Merchant.Present)
            return;

        var p = services.Primitives;
        var feet = run.Merchant.Position.ToXna();
        var mat = new Vector2(feet.X - 22, feet.Y - 2);
        var alpha = Appear(run) * (0.45f + 0.55f * _seen);
        p.Rect(batch, mat - Vector2.One, new Vector2(46, 12), Ink * (0.6f * alpha));
        for (var stripe = 0; stripe < 4; stripe++)
            p.Rect(batch, mat + new Vector2(0, stripe * 2.5f), new Vector2(44, 2.5f), (stripe % 2 == 0 ? MatDark : MatLight) * alpha);
        p.Ellipse(batch, feet + new Vector2(0, 1), 18, 6, Color.Black * (0.3f * alpha));

        // Two of its wares laid on the mat, small.
        var stock = run.Merchant.Stock;
        for (var i = 0; i < Math.Min(2, stock.Count); i++)
        {
            if (stock[i].Sold)
                continue;
            var at = new Vector2(i == 0 ? mat.X + 2 : mat.X + 32, mat.Y - 3);
            services.ItemIcons.Draw(batch, stock[i].IconKey, new Rectangle((int)at.X, (int)at.Y, 12, 12), alpha: alpha);
        }
    }

    /// <summary>Kecleon itself, its bubble and the smoke. World space, alpha batch.</summary>
    public void DrawObjects(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        if (run.Merchant.Present && services.Sprites.Get(SpeciesId) is { } set && _animator.Anim is { } anim)
        {
            var feet = run.Merchant.Position.ToXna();
            var toPlayer = run.Player.Position - run.Merchant.Position;
            var facing = Direction8Extensions.FromVector(toPlayer, Direction8.Down);

            // Camouflaged: a faint, wavering outline of the grass' colour; seen: itself. Its last seconds blink.
            var appear = Appear(run);
            var left = run.Merchant.LeavesTick - run.Ticks;
            if (left < RunClock.Seconds(8f) && _tick / 6 % 2 == 0)
                appear *= 0.5f;
            var shimmer = new Vector2(MathF.Sin(_tick * 0.2f) * (1f - _seen), 0f);
            if (_seen < 1f)
                set.Draw(batch, anim, _animator.Frame, facing, feet + shimmer, Green * (0.35f * (1f - _seen) * appear), silhouette: true);
            set.Draw(batch, anim, _animator.Frame, facing, feet, Color.White * (_seen * appear));

            // The bubble with a coin over its head: here is a shop.
            var head = set.HeadHeight;
            var bob = MathF.Round(MathF.Sin(_tick * 0.08f) * 1.5f);
            var bubble = new Vector2(feet.X + 8, feet.Y - head - 14 + bob);
            var bubbleAlpha = appear * (0.55f + 0.45f * _seen);
            p.Rect(batch, bubble - new Vector2(7, 6), new Vector2(14, 12), Ink * bubbleAlpha);
            p.Rect(batch, bubble - new Vector2(6, 5), new Vector2(12, 10), Color.White * bubbleAlpha);
            p.Rect(batch, bubble + new Vector2(-5, 5), new Vector2(3, 2), Color.White * bubbleAlpha);
            p.Rect(batch, bubble + new Vector2(-6, 5), new Vector2(1, 3), Ink * bubbleAlpha);
            var coin = services.Pickups.Coin;
            batch.Draw(coin, VirtualScreen.Snap(bubble - new Vector2(coin.Width / 2f, coin.Height / 2f)), Color.White * bubbleAlpha);
        }

        _particles.Draw(batch, p);
    }

    /// <summary>0 to 1 while it fades in after arriving.</summary>
    private static float Appear(RunState run) => MathHelper.Clamp((run.Ticks - run.Merchant.ArrivedTick) / (float)ArriveTicks, 0f, 1f);

    private void Puff(Vector2 at)
    {
        for (var i = 0; i < 16; i++)
        {
            var angle = i * MathF.Tau / 16f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
            _particles.Emit(at + direction * 4f - new Vector2(0, 8), direction * (50f + _random.Next(30)), Smoke, Smoke * 0f, 26 + _random.Next(10), 3f, 0.88f, gravity: -1f);
        }
    }
}
