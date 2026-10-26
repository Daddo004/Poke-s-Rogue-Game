using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// What enemies do that is not a move (phase 3.2): the line a charger is about to take, the dust it raises, the blast
/// circle and the explosion of a kamikaze, the pink sparkles of a teleport. Warnings use the red of the strike warnings;
/// coloured light is alpha-blended so it keeps its colour on the grass.
/// </summary>
public sealed class EnemyVfx(GameServices services, ScreenShake shake)
{
    public static readonly Color WarningRed = new(214, 40, 40);
    public static readonly Color PsyPink = new(248, 120, 200);

    /// <summary>The Dominanti's aura (phase 3.3), the red-orange of the Totem Pokémon.</summary>
    public static readonly Color AuraRed = new(255, 84, 40);

    private static readonly Color AuraDark = new(150, 20, 20);

    /// <summary>Shiny Pokémon (phase 3.5): their glints and the arrow pointing at them.</summary>
    public static readonly Color ShinyGold = new(255, 232, 120);

    private static readonly Color HealGreen = new(120, 230, 120);
    private static readonly Color ShareBlue = new(120, 190, 255);
    private static readonly Color CandyPink = new(255, 150, 210);

    private static readonly Color Dust = new(200, 184, 140);
    private static readonly Color DustDark = new(120, 100, 70);
    private static readonly Color Smoke = new(70, 66, 76);
    private static readonly Color BlastOrange = new(255, 150, 40);
    private static readonly Color BlastYellow = new(255, 236, 120);
    private static readonly Color BlastRed = new(200, 40, 20);

    /// <summary>How long a blast's flash and dust ring last.</summary>
    private const int BlastLife = 22;

    /// <summary>How long the light left where a teleporter vanished lasts.</summary>
    private const int StreakLife = 14;

    private readonly Random _random = new(33);
    private readonly Particles _dust = new(1024);
    private readonly Particles _flames = new(1024);
    private readonly Particles _sparks = new(512);
    private readonly List<Timed> _blasts = [];
    private readonly List<Timed> _streaks = [];
    private readonly List<Timed> _roars = [];

    /// <summary>Rings sent across the screen by Repellente and Condividi Esp.</summary>
    private readonly List<Timed> _waves = [];

    private const int WaveLife = 28;
    private readonly MoveShapes _shapes = new(services.Device);
    private int _tick;

    /// <summary>How long the ring of dust of a boss' landing or roar lasts.</summary>
    private const int RoarLife = 30;

    public int ParticleCount => _dust.Active + _flames.Active + _sparks.Active;

    public void Reset()
    {
        _blasts.Clear();
        _streaks.Clear();
        _roars.Clear();
        _waves.Clear();
    }

    public void OnEvent(in RunEvent e)
    {
        var at = e.Position.ToXna();
        switch (e.Type)
        {
            case RunEventType.EnemyCharged:
            {
                // Hooves dig in: a spray of dirt kicked backwards.
                var back = -e.Target.ToXna();
                for (var i = 0; i < 8; i++)
                    _dust.Emit(at + Jitter(3), back * Rand(40, 90) + Jitter(25) + new Vector2(0, -20), Dust, DustDark * 0.3f, 16 + _random.Next(8), 2f, drag: 0.9f, gravity: 4f);
                break;
            }

            case RunEventType.EnemyExploded:
                Explode(at, e.Value);
                break;

            case RunEventType.HazardLanded:
            {
                // A rock smashes into the ground: chips and dust thrown out, a small jolt.
                _blasts.Add(new Timed(at, e.Value * 0.6f, Age: 5));
                for (var i = 0; i < 12; i++)
                {
                    var angle = Rand(0f, MathF.Tau);
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
                    _dust.Emit(at + dir * 4f, dir * Rand(50, 110) + new Vector2(0, -60), i % 3 == 0 ? DustDark : Dust, DustDark * 0.3f, 20, 2f, drag: 0.92f, gravity: 7f);
                }

                shake.Add(0.15f);
                break;
            }

            case RunEventType.BossPhaseChanged:
                BossRoared(at);
                break;

            case RunEventType.PropBroken:
                PropBroken(at, (BreakableKind)(int)e.Value);
                break;

            case RunEventType.LootCollected:
                LootCollected(at, (Core.Run.Pickups.LootKind)(int)e.Value);
                break;

            case RunEventType.BossDefeated:
                BossFell(at);
                break;

            case RunEventType.EnemyTeleported:
                _streaks.Add(new Timed(at, 0f));
                Sparkle(at, 10, outward: true);
                Sparkle(e.Target.ToXna(), 10, outward: false);
                break;
        }
    }

    private void Explode(Vector2 at, float radius)
    {
        _blasts.Add(new Timed(at, radius));
        var body = at - new Vector2(0, 8);

        // Fire: orange balls cooling to red, a yellow core; smoke rising after; dark debris falling back.
        for (var i = 0; i < 26; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.7f);
            _flames.Emit(body + dir * Rand(2, 8), dir * Rand(40, 120), BlastOrange, BlastRed, 14 + _random.Next(10), 4f, drag: 0.86f);
        }

        for (var i = 0; i < 10; i++)
            _flames.Emit(body + Jitter(5), Jitter(40), BlastYellow, BlastOrange, 8 + _random.Next(5), 3f, drag: 0.85f);

        for (var i = 0; i < 10; i++)
            _dust.Emit(body + Jitter(radius * 0.4f), new Vector2(Rand(-10, 10), Rand(-40, -15)), Smoke * 0.9f, Smoke * 0.2f, 34 + _random.Next(14), 4f, drag: 0.94f);

        for (var i = 0; i < 8; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            _dust.Emit(body, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Rand(60, 120) + new Vector2(0, -60), DustDark, DustDark * 0.5f, 26, 2f, drag: 0.94f, gravity: 7f);
        }

        shake.Add(0.3f);
    }

    /// <summary>Pink sparkles around a teleporter: flying out where it leaves, gathering in where it lands.</summary>
    private void Sparkle(Vector2 feet, int count, bool outward)
    {
        var center = feet - new Vector2(0, 10);
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count + Rand(-0.2f, 0.2f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = outward ? center + dir * 3f : center + dir * 18f;
            var velocity = dir * (outward ? Rand(50, 80) : -Rand(60, 80));
            _flames.Emit(start, velocity, PsyPink, Color.White * 0.6f, 14, 2f, drag: 0.88f);
        }
    }

    public void Update(RunState run, bool paused)
    {
        _tick++;
        _dust.Update();
        _flames.Update();
        _sparks.Update();
        Age(_blasts, BlastLife);
        Age(_streaks, StreakLife);
        Age(_roars, RoarLife);
        Age(_waves, WaveLife);
        if (paused)
            return;

        foreach (ref readonly var e in run.Enemies.Items)
        {
            // A shiny Pokémon glints now and then, a little star appearing beside it.
            if (e.IsAlive && e.Shiny && (_tick + e.Serial * 7) % 18 == 0)
                _sparks.Emit(e.Position.ToXna() + new Vector2(Rand(-10, 10), -Rand(4, 22)), Vector2.Zero, Color.White, ShinyGold, 14, 2f);

            // A Dominante's aura gives off wisps that rise along its sides and fade (never across its face).
            if (e.IsAlive && e.Rank == EnemyRank.Dominant && _tick % 3 == 0)
            {
                var side = _random.Next(2) == 0 ? -1f : 1f;
                var around = e.Position.ToXna() + new Vector2(side * Rand(e.Radius * 0.8f, e.Radius * 1.1f), -Rand(0f, e.Radius * 0.8f));
                _flames.Emit(around, new Vector2(side * Rand(2, 6), Rand(-30, -16)), AuraRed * 0.9f, AuraDark * 0.3f, 16 + _random.Next(8), 2f, drag: 0.95f, stretch: 2f);
            }

            if (!e.IsAlive || e.Action == EnemyAction.None)
                continue;

            var feet = e.Position.ToXna();
            switch (e.Action)
            {
                case EnemyAction.WindUp when _tick % 4 == 0:
                {
                    // Pawing the ground: dirt kicked out behind.
                    var back = -e.ActionDirection.ToXna();
                    _dust.Emit(feet + back * 4 + Jitter(2), back * Rand(30, 50) + new Vector2(Rand(-10, 10), Rand(-30, -15)), Dust, DustDark * 0.3f, 14, 2f, drag: 0.9f, gravity: 4f);
                    break;
                }

                case EnemyAction.Dash when _tick % 2 == 0:
                    _dust.Emit(feet + Jitter(3), new Vector2(Rand(-12, 12), Rand(-18, -6)), Dust * 0.9f, DustDark * 0.2f, 18, 3f, drag: 0.92f);
                    break;

                case EnemyAction.Fuse when _tick % 5 == 0:
                    // Sparks fizzing on top, like a lit fuse.
                    _sparks.Emit(feet - new Vector2(Rand(-3, 3), 18), new Vector2(Rand(-25, 25), Rand(-45, -20)), Color.White, BlastYellow * 0.5f, 10, 1f, drag: 0.9f, gravity: 3f);
                    break;

                case EnemyAction.Vanish or EnemyAction.Appear when _tick % 3 == 0:
                    _flames.Emit(feet - new Vector2(0, 10) + Jitter(9), new Vector2(0, Rand(-30, -10)), PsyPink, Color.White * 0.5f, 10, 1f, drag: 0.9f);
                    break;
            }
        }
    }

    /// <summary>Warnings on the floor, under the Pokémon: a charger's lane and a kamikaze's blast circle.</summary>
    public void DrawGround(SpriteBatch batch, RunState run, Rectangle cull)
    {
        var p = services.Primitives;
        var bossAttack = run.Boss?.Current;
        foreach (ref readonly var e in run.Enemies.Items)
        {
            if (!e.IsAlive || !cull.Contains(e.Position.ToXna()))
                continue;

            // How long the warning lasts and how far the charge runs: from the boss' attack or the species' profile.
            int windUp;
            float dash;
            if (e.Rank == EnemyRank.Boss && bossAttack is not null)
            {
                windUp = bossAttack.WindUpTicks;
                dash = e.Speed * bossAttack.Spec.DashSpeed * bossAttack.Spec.DashTime;
            }
            else if (e.Profile is { } fighter)
            {
                windUp = fighter.WindUpTicks;
                dash = e.Speed * fighter.Spec.DashSpeed * fighter.Spec.DashTime;
            }
            else
            {
                continue;
            }

            var feet = e.Position.ToXna();
            switch (e.Action)
            {
                case EnemyAction.WindUp:
                {
                    // The lane it will charge along: a row of arrows lighting up one by one until it goes.
                    // Only lines: a red fill turns to mud brown on the grass.
                    var progress = 1f - e.ActionTicks / (float)windUp;
                    var direction = e.ActionDirection.ToXna();
                    var side = new Vector2(-direction.Y, direction.X);
                    var length = dash + e.Radius;
                    var half = MathF.Max(5f, e.Radius * 0.6f);
                    for (var d = e.Radius + 6f; d <= length; d += 14f)
                    {
                        var tip = feet + direction * d;
                        var lit = d <= e.Radius + 6f + (length - e.Radius) * progress;
                        var color = WarningRed * (lit ? 0.9f : 0.35f);
                        UiDraw.Line(batch, p, tip, tip - direction * 6f + side * half, color, 2f);
                        UiDraw.Line(batch, p, tip, tip - direction * 6f - side * half, color, 2f);
                    }

                    break;
                }

                case EnemyAction.Fuse when e.Profile is { } profile:
                {
                    // The edge of the blast, beating faster as the fuse burns down, and a ring growing out to meet it.
                    var progress = 1f - e.ActionTicks / (float)windUp;
                    var radius = profile.Spec.BlastRadius;
                    var beat = e.ActionTicks < windUp / 3 ? 4 : 8;
                    var rim = _tick / beat % 2 == 0 ? 0.85f : 0.5f;
                    p.Ring(batch, feet, radius, radius, WarningRed * rim, 2);
                    p.Ring(batch, feet, MathF.Max(1f, radius * progress), MathF.Max(1f, radius * progress), WarningRed * 0.5f);
                    break;
                }
            }
        }

        // A boss' rocks: a red circle where each will land, a ring growing to meet its edge, a shadow under the rock.
        foreach (var hazard in run.Hazards)
        {
            var progress = MathHelper.Clamp((run.Ticks - hazard.StartTick) / (float)Math.Max(1, hazard.DueTick - hazard.StartTick), 0f, 1f);
            var at = hazard.Position.ToXna();
            var rim = progress > 0.7f && _tick / 4 % 2 == 0 ? 0.95f : 0.65f;
            p.Ring(batch, at, hazard.Radius, hazard.Radius, WarningRed * rim, 2);
            p.Ring(batch, at, MathF.Max(1f, hazard.Radius * progress), MathF.Max(1f, hazard.Radius * progress), WarningRed * 0.5f);
            var shadow = (int)(hazard.Radius * (0.4f + 0.8f * progress));
            p.Ellipse(batch, at, shadow, shadow / 2, Color.Black * (0.1f + 0.3f * progress));
        }

        // Where the boss landed or roared: a ring of dust and two red shock waves running out across the ground.
        foreach (var roar in _roars)
        {
            var t = roar.Age / (float)RoarLife;
            var reach = roar.Value * (0.2f + 0.8f * t);
            p.Ring(batch, roar.Position, reach, reach * 0.6f, DustDark * (0.7f * (1f - t)), 2);
            for (var wave = 0; wave < 2; wave++)
            {
                var w = MathHelper.Clamp(t * 1.3f - wave * 0.25f, 0f, 1f);
                if (w <= 0f || w >= 1f)
                    continue;

                var r = roar.Value * w;
                p.Ring(batch, roar.Position, r, r * 0.6f, AuraRed * (0.8f * (1f - w)), 2);
            }
        }

        // A scorch mark and a ring of dust running out to the edge of the blast.
        foreach (var blast in _blasts)
        {
            var t = blast.Age / (float)BlastLife;
            var reach = blast.Value * (0.4f + 0.6f * t);
            p.Ellipse(batch, blast.Position, (int)(blast.Value * 0.8f), (int)(blast.Value * 0.45f), Smoke * (0.4f * (1f - t)));
            p.Ring(batch, blast.Position, reach, reach, DustDark * (0.8f * (1f - t)), 2);
        }
    }

    /// <summary>Smoke, fire, sparkles and the light of a teleport, over the Pokémon. Alpha batch.</summary>
    public void DrawObjects(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        DrawBossWarnings(batch, run);

        // The first instants of a blast: a small white flash where the Pokémon was.
        foreach (var blast in _blasts)
        {
            if (blast.Age > 4)
                continue;

            var size = 14 + blast.Age * 4;
            p.Ellipse(batch, blast.Position - new Vector2(0, 8), size, size, Color.White * (0.8f * (1f - blast.Age / 5f)));
        }

        // Where a teleporter vanished: a thin pink column of light, fading.
        foreach (var streak in _streaks)
        {
            var t = streak.Age / (float)StreakLife;
            var top = streak.Position - new Vector2(0, 34 + t * 10f);
            UiDraw.Line(batch, p, streak.Position, top, PsyPink * (0.7f * (1f - t)), 3f * (1f - t) + 1f);
            UiDraw.Line(batch, p, streak.Position, top, Color.White * (0.6f * (1f - t)));
        }

        _dust.Draw(batch, p);
        _flames.Draw(batch, p);
    }

    /// <summary>
    /// The boss' volley: thin red sight lines along each shot's path while it aims. Its rocks: falling into their
    /// circles during the last part of the warning.
    /// </summary>
    private void DrawBossWarnings(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        if (run.Boss is { Current: { Spec.Kind: BossAttackKind.Volley } attack } fight && run.Enemies[fight.Index] is { IsAlive: true, Action: EnemyAction.Aim } boss)
        {
            var spec = attack.Spec;
            var from = boss.Position.ToXna() - new Vector2(0, 14);
            var centre = MathF.Atan2(boss.ActionDirection.Y, boss.ActionDirection.X);
            var spread = spec.Spread * MathF.PI / 180f;
            var progress = 1f - boss.ActionTicks / (float)attack.WindUpTicks;
            var alpha = progress > 0.6f && _tick / 3 % 2 == 0 ? 0.85f : 0.45f;
            for (var k = 0; k < spec.Count; k++)
            {
                var angle = spec.Count == 1 ? centre : centre - spread / 2f + spread * k / (spec.Count - 1);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                UiDraw.Line(batch, p, from + direction * boss.Radius * 0.6f, from + direction * (boss.Radius + 30f + 110f * progress), WarningRed * alpha);
            }
        }

        foreach (var hazard in run.Hazards)
        {
            var progress = (run.Ticks - hazard.StartTick) / (float)Math.Max(1, hazard.DueTick - hazard.StartTick);
            if (progress < 0.55f)
                continue;

            // The rock drops the last part of the way, faster and faster.
            var u = MathHelper.Clamp((progress - 0.55f) / 0.45f, 0f, 1f);
            var at = hazard.Position.ToXna() - new Vector2(0, (1f - u) * (1f - u) * 150f + 6f);
            MoveShapes.DrawRotated(batch, _shapes.Rock, at, (hazard.StartTick % 4) * MathF.PI / 2f, Color.White, 2f);
        }
    }

    /// <summary>Cut grass: blades fly up and fall back. A shattered boulder: pebbles and a puff of dust.</summary>
    private void PropBroken(Vector2 feet, BreakableKind kind)
    {
        var (light, dark) = kind == BreakableKind.Rock ? (BreakableArt.RockLight, BreakableArt.RockDark) : (BreakableArt.GrassLight, BreakableArt.GrassDark);
        var count = kind == BreakableKind.Rock ? 10 : 12;
        for (var i = 0; i < count; i++)
        {
            var angle = Rand(MathF.PI * 1.1f, MathF.PI * 1.9f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var color = i % 2 == 0 ? light : dark;
            _dust.Emit(feet - new Vector2(0, 5) + Jitter(4), dir * Rand(50, 110), color, color * 0.6f, 20 + _random.Next(10),
                kind == BreakableKind.Rock ? 2f : 1f, drag: 0.93f, gravity: 7f, stretch: kind == BreakableKind.Rock ? 1f : 3f);
        }

        if (kind == BreakableKind.Rock)
        {
            _blasts.Add(new Timed(feet, 18f, Age: 5));
            shake.Add(0.05f);
        }
    }

    /// <summary>Picking something up: sparkles in its colour; Repellente and Condividi Esp. also send a ring across the screen.</summary>
    private void LootCollected(Vector2 at, Core.Run.Pickups.LootKind kind)
    {
        var body = at - new Vector2(0, 8);
        switch (kind)
        {
            case Core.Run.Pickups.LootKind.Coin:
                _sparks.Burst(body, 4, 60f, ShinyGold, ShinyGold * 0.3f, 10, _random);
                break;
            case Core.Run.Pickups.LootKind.Nugget or Core.Run.Pickups.LootKind.BigNugget:
                _sparks.Burst(body, 14, 110f, Color.White, ShinyGold, 20, _random, size: 2f);
                break;
            case Core.Run.Pickups.LootKind.HeldItem:
                _sparks.Burst(body, 16, 120f, Color.White, ShinyGold, 22, _random, size: 2f);
                break;
            case Core.Run.Pickups.LootKind.Potion or Core.Run.Pickups.LootKind.OranBerry:
                for (var i = 0; i < 10; i++)
                    _flames.Emit(at + new Vector2(Rand(-10, 10), Rand(-4, 2)), new Vector2(0, Rand(-40, -20)), HealGreen, Color.White * 0.4f, 24, 2f, drag: 0.95f);
                break;
            case Core.Run.Pickups.LootKind.RareCandy:
                _sparks.Burst(body, 12, 90f, Color.White, CandyPink, 20, _random, size: 2f);
                break;
            case Core.Run.Pickups.LootKind.ExpShare:
                _waves.Add(new Timed(at, 0f, Color: ShareBlue));
                break;
            case Core.Run.Pickups.LootKind.Repel:
                _waves.Add(new Timed(at, 0f, Color: Color.White));
                shake.Add(0.3f);
                break;
        }
    }

    /// <summary>The boss lands: dust and debris thrown out, a ring of dust, a hard jolt of the camera.</summary>
    public void BossLanded(Vector2 feet)
    {
        _roars.Add(new Timed(feet, 120f));
        for (var i = 0; i < 24; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
            _dust.Emit(feet + dir * 10f, dir * Rand(60, 140) + new Vector2(0, -50), i % 3 == 0 ? DustDark : Dust, DustDark * 0.3f, 24 + _random.Next(10), i % 4 == 0 ? 3f : 2f, drag: 0.92f, gravity: 6f);
        }

        shake.Add(0.55f);
    }

    /// <summary>A new phase: the boss roars, a ring of dust runs out and the camera shakes.</summary>
    private void BossRoared(Vector2 feet)
    {
        _roars.Add(new Timed(feet, 140f));
        shake.Add(0.4f);
    }

    /// <summary>The boss falls: a white flash, gold and white sparks, the camera shaking hard.</summary>
    private void BossFell(Vector2 feet)
    {
        var body = feet - new Vector2(0, 24);
        _blasts.Add(new Timed(feet, 60f));
        for (var i = 0; i < 40; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _sparks.Emit(body + dir * 6f, dir * Rand(80, 200), Color.White, i % 2 == 0 ? BlastYellow : Color.White * 0.5f, 26 + _random.Next(14), 2f, drag: 0.9f);
        }

        shake.Add(0.6f);
    }

    /// <summary>White sparks of a burning fuse, glints of shinies, and the rings of Repellente and Condividi Esp. Additive batch.</summary>
    public void DrawGlow(SpriteBatch batch)
    {
        var p = services.Primitives;
        foreach (var wave in _waves)
        {
            var t = wave.Age / (float)WaveLife;
            var reach = 20f + (1f - (1f - t) * (1f - t)) * 380f;
            p.Ring(batch, wave.Position, reach, reach * 0.7f, wave.Color * (0.7f * (1f - t)), 3);
            p.Ring(batch, wave.Position, reach * 0.85f, reach * 0.6f, wave.Color * (0.35f * (1f - t)), 2);
        }

        _sparks.Draw(batch, p);
    }

    private static void Age(List<Timed> list, int life)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            list[i] = list[i] with { Age = list[i].Age + 1 };
            if (list[i].Age >= life)
                list.RemoveAt(i);
        }
    }

    private float Rand(float min, float max) => min + _random.NextSingle() * (max - min);

    private Vector2 Jitter(float amount) => new(Rand(-amount, amount), Rand(-amount, amount));

    private readonly record struct Timed(Vector2 Position, float Value, int Age = 0, Color Color = default);
}
