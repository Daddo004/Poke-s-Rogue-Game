using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 3.1: the wave director and the special events.</summary>
public class WaveTests
{
    private static readonly Vector2 View = new(640, 360);

    private static RunState NewRun(WaveTimeline waves)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, waves, View);
        run.Player.Invincible = true;
        return run;
    }

    private static WaveTimeline Timeline(IReadOnlyList<WaveSpec>? waves = null, IReadOnlyList<WaveEventSpec>? events = null, float hpPerMinute = 0f) =>
        WaveTimeline.From(new WaveFile { Id = "prova", Minutes = 5, HpPerMinute = hpPerMinute, Waves = waves ?? [], Events = events ?? [] },
            TestContent.Pokedex.Value.Species);

    private static WaveSpec Wave(string from, string to, string pokemon, int minAlive = 0, float spawnsPerSecond = 0f, int maxAlive = 300) =>
        new() { From = from, To = to, MinAlive = minAlive, SpawnsPerSecond = spawnsPerSecond, MaxAlive = maxAlive, Species = [new WaveSpeciesSpec { Pokemon = pokemon }] };

    private static int Alive(RunState run) => run.Enemies.Items.ToArray().Count(e => e.IsAlive);

    [Fact]
    public void Times_are_minutes_and_seconds()
    {
        Assert.Equal(150 * RunClock.TicksPerClockSecond, WaveTimeline.ParseTime("2:30"));
        Assert.Equal(0, WaveTimeline.ParseTime("0:00"));
        Assert.Equal(14 * 60 * RunClock.TicksPerClockSecond, WaveTimeline.ParseTime("14:00"));
        Assert.Throws<InvalidDataException>(() => WaveTimeline.ParseTime("2:3"));
        Assert.Throws<InvalidDataException>(() => WaveTimeline.ParseTime("1:75"));
        Assert.Throws<InvalidDataException>(() => WaveTimeline.ParseTime("90"));
    }

    [Fact]
    public void Bosco_Smeraldo_covers_fifteen_minutes_with_all_three_special_events()
    {
        var waves = WaveTimeline.Load(TestContent.Root, "bosco-smeraldo", TestContent.Pokedex.Value.Species, TestContent.Moves.Value);

        Assert.Equal(15 * 60 * RunClock.TicksPerClockSecond, waves.DurationTicks);
        Assert.Equal(127, waves.Boss?.SpeciesId);
        Assert.Equal(3, waves.Boss!.Phases.Count);
        Assert.Equal(0, waves.Waves[0].FromTick);
        Assert.Equal(waves.DurationTicks, waves.Waves[^1].ToTick);
        for (var i = 1; i < waves.Waves.Count; i++)
            Assert.Equal(waves.Waves[i - 1].ToTick, waves.Waves[i].FromTick);
        Assert.Equal([WaveEventKind.Swarm, WaveEventKind.Ring, WaveEventKind.Stampede, WaveEventKind.Dominant], waves.Events.Select(e => e.Kind).Distinct());
        Assert.Equal([ChestTier.Poke, ChestTier.Poke, ChestTier.Great, ChestTier.Great, ChestTier.Ultra],
            waves.Events.Where(e => e.Kind == WaveEventKind.Dominant).Select(e => e.Chest));
        Assert.Contains(10, waves.WaveAt(0)!.Species.Select(s => s.SpeciesId));
        Assert.Contains(waves.Events, e => e.SpeciesId == 41 && e.Kind == WaveEventKind.Swarm);
    }

    [Fact]
    public void Bad_timelines_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => Timeline([Wave("0:00", "1:00", "missingno")]));
        Assert.Throws<InvalidDataException>(() => Timeline([Wave("0:00", "1:00", "rattata"), Wave("0:30", "2:00", "pidgey")]));
        Assert.Throws<InvalidDataException>(() => Timeline([Wave("1:00", "1:00", "rattata")]));
        Assert.Throws<InvalidDataException>(() => Timeline([Wave("0:00", "1:00", "rattata", minAlive: 50, maxAlive: 10)]));
        Assert.Throws<InvalidDataException>(() => Timeline(events: [new WaveEventSpec { At = "0:10", Every = "0:00", Kind = WaveEventKind.Swarm, Pokemon = "zubat" }]));
    }

    [Fact]
    public void A_wave_refills_to_its_minimum_a_few_enemies_per_tick()
    {
        var run = NewRun(Timeline([Wave("0:00", "1:00", "rattata", minAlive: 30)]));

        run.Tick(default);
        Assert.Equal(4, run.Enemies.AliveCount);

        for (var i = 0; i < 20; i++)
            run.Tick(default);
        Assert.Equal(30, run.Enemies.AliveCount);
    }

    [Fact]
    public void The_trickle_stops_at_max_alive()
    {
        var run = NewRun(Timeline([Wave("0:00", "1:00", "rattata", spawnsPerSecond: 120, maxAlive: 20)]));

        for (var i = 0; i < 120; i++)
            run.Tick(default);

        Assert.Equal(20, run.Enemies.AliveCount);
    }

    [Fact]
    public void Species_follow_the_timeline_and_the_last_wave_keeps_going()
    {
        var run = NewRun(Timeline([Wave("0:00", "0:01", "rattata", minAlive: 4), Wave("0:01", "0:02", "pidgey", minAlive: 8)]));

        run.Tick(default);
        Assert.All(run.Enemies.Items.ToArray().Where(e => e.IsAlive), e => Assert.Equal(19, e.SpeciesId));

        run.AdvanceTime(RunClock.TicksPerClockSecond * 5);
        run.Tick(default);
        Assert.Equal(4, run.Enemies.Items.ToArray().Count(e => e.IsAlive && e.SpeciesId == 16));
    }

    [Fact]
    public void Enemies_hit_harder_minute_by_minute()
    {
        var waves = WaveTimeline.From(new WaveFile
        {
            Id = "prova",
            Minutes = 5,
            DamagePerMinute = 0.5f,
            Waves = [Wave("0:00", "5:00", "rattata", minAlive: 1)],
        }, TestContent.Pokedex.Value.Species);
        var run = NewRun(waves);
        run.Tick(default);
        var early = run.Enemies.Items[0].ContactDamage;

        run.AdvanceTime(RunClock.TicksPerClockSecond * 120);
        run.DamageEnemy(0, float.MaxValue);
        run.Tick(default);
        var late = run.Enemies.Items.ToArray().First(e => e.IsAlive).ContactDamage;

        Assert.Equal(early * 2f, late, 1);
    }

    [Fact]
    public void Enemies_grow_sturdier_minute_by_minute()
    {
        var run = NewRun(Timeline([Wave("0:00", "5:00", "rattata", minAlive: 1)], hpPerMinute: 0.5f));
        run.Tick(default);
        var early = run.Enemies.Items[0].MaxHp;

        run.AdvanceTime(RunClock.TicksPerClockSecond * 120);
        run.DamageEnemy(0, float.MaxValue);
        run.Tick(default);
        var late = run.Enemies.Items.ToArray().First(e => e.IsAlive).MaxHp;

        Assert.Equal(early * 2f, late, 1);
    }

    [Fact]
    public void A_swarm_flies_straight_across_and_leaves_without_drops()
    {
        var run = NewRun(Timeline(events: [new WaveEventSpec { At = "0:00", Kind = WaveEventKind.Swarm, Pokemon = "zubat", Count = 12, Speed = 2.2f }]));

        run.Tick(default);
        Assert.Contains(run.Events, e => e.Type == RunEventType.WaveEventStarted && e.SpeciesId == 41);
        var swarm = run.Enemies.Items.ToArray().Where(e => e.IsAlive).ToList();
        Assert.Equal(12, swarm.Count);
        Assert.All(swarm, e =>
        {
            Assert.Equal(EnemyBehavior.Straight, e.Behavior);
            Assert.Equal(swarm[0].Heading, e.Heading);
            Assert.True(MathF.Abs(e.Position.X) > View.X / 2 || MathF.Abs(e.Position.Y) > View.Y / 2, "a swarm starts off screen");
        });

        // It passes close to the player, then is gone for good.
        var closest = float.MaxValue;
        for (var i = 0; i < RunClock.TicksPerSecond * 20 && Alive(run) > 0; i++)
        {
            run.Tick(default);
            foreach (var e in run.Enemies.Items)
            {
                if (e.IsAlive)
                    closest = MathF.Min(closest, e.Position.Length());
            }
        }

        Assert.Equal(0, Alive(run));
        Assert.True(closest < 140f, $"closest pass {closest:0} px");
        Assert.Equal(0, run.Stats.Kills);
        Assert.Equal(0, run.Pickups.Gems.ActiveCount);
    }

    [Fact]
    public void A_stampede_charges_as_a_wall_abreast()
    {
        var run = NewRun(Timeline(events: [new WaveEventSpec { At = "0:00", Kind = WaveEventKind.Stampede, Pokemon = "tauros", Count = 6, Speed = 2.6f }]));

        run.Tick(default);

        var wall = run.Enemies.Items.ToArray().Where(e => e.IsAlive).ToList();
        var heading = wall[0].Heading;
        var depths = wall.Select(e => Vector2.Dot(e.Position, heading)).ToList();
        var widths = wall.Select(e => Vector2.Dot(e.Position, new Vector2(-heading.Y, heading.X))).ToList();
        Assert.Equal(6, wall.Count);
        Assert.True(depths.Max() - depths.Min() <= 8.01f, "the wall is one row deep");
        Assert.True(widths.Max() - widths.Min() > 150f, "the wall is wide");
        Assert.All(wall, e => Assert.Equal(StatFormulas.EnemyMoveSpeed(TestContent.Species(128).BaseStats) * 2.6f, e.Speed, 3));
    }

    [Fact]
    public void A_ring_surrounds_the_player_then_burrows_away_without_drops()
    {
        var run = NewRun(Timeline(events: [new WaveEventSpec { At = "0:00", Kind = WaveEventKind.Ring, Pokemon = "diglett", Count = 16, Radius = 150, Speed = 0.5f, Lifetime = 1 }]));

        run.Tick(default);
        var ring = run.Enemies.Items.ToArray().Where(e => e.IsAlive).ToList();
        Assert.Equal(16, ring.Count);
        Assert.All(ring, e => Assert.Equal(150f, e.Position.Length(), 2));
        Assert.All(ring, e => Assert.Equal(EnemyBehavior.Chase, e.Behavior));

        // It closes in...
        for (var i = 0; i < RunClock.TicksPerSecond / 2; i++)
            run.Tick(default);
        Assert.All(run.Enemies.Items.ToArray().Where(e => e.IsAlive), e => Assert.True(e.Position.Length() < 145f));

        // ...and once its time is up the survivors leave, with no KO credit and no gems.
        for (var i = 0; i < RunClock.TicksPerSecond; i++)
            run.Tick(default);
        Assert.Equal(0, Alive(run));
        Assert.Equal(0, run.Stats.Kills);
        Assert.Equal(0, run.Pickups.Gems.ActiveCount);
    }

    [Fact]
    public void Events_repeat_until_their_last_time_and_a_time_jump_fires_them_once()
    {
        var run = NewRun(Timeline(events: [new WaveEventSpec { At = "0:10", Every = "0:10", Until = "0:30", Kind = WaveEventKind.Ring, Pokemon = "diglett", Count = 1, Lifetime = 1 }]));

        var fired = new List<int>();
        for (var i = 0; i < RunClock.TicksPerClockSecond * 60; i++)
        {
            run.Tick(default);
            if (run.Events.Any(e => e.Type == RunEventType.WaveEventStarted))
                fired.Add(run.Ticks / RunClock.TicksPerClockSecond);
        }

        Assert.Equal([10, 20, 30], fired);

        var jumped = NewRun(Timeline(events: [new WaveEventSpec { At = "0:10", Every = "0:10", Kind = WaveEventKind.Ring, Pokemon = "diglett", Count = 1 }]));
        jumped.AdvanceTime(RunClock.TicksPerClockSecond * 45);
        var firedAfterJump = 0;
        while (jumped.Ticks < RunClock.TicksPerClockSecond * 50 - 1)
        {
            jumped.Tick(default);
            firedAfterJump += jumped.Events.Count(e => e.Type == RunEventType.WaveEventStarted);
        }

        Assert.Equal(1, firedAfterJump);
        jumped.Tick(default);
        Assert.Contains(jumped.Events, e => e.Type == RunEventType.WaveEventStarted);
    }

    [Fact]
    public void A_run_started_later_skips_the_events_already_past()
    {
        var run = NewRun(Timeline(events:
        [
            new WaveEventSpec { At = "0:05", Kind = WaveEventKind.Swarm, Pokemon = "zubat", Count = 1 },
            new WaveEventSpec { At = "0:10", Every = "0:10", Kind = WaveEventKind.Ring, Pokemon = "diglett", Count = 1, Lifetime = 1 },
        ]));
        run.AdvanceTime(RunClock.TicksPerClockSecond * 15);
        run.Spawner.SkipEventsBefore(run.Ticks);

        var fired = new List<(int Second, int Species)>();
        while (run.Ticks < RunClock.TicksPerClockSecond * 25)
        {
            run.Tick(default);
            fired.AddRange(run.Events.Where(e => e.Type == RunEventType.WaveEventStarted).Select(e => (run.Ticks / RunClock.TicksPerClockSecond, e.SpeciesId)));
        }

        Assert.Equal([(20, 50)], fired);
    }

    /// <summary>Milestone C groundwork: the whole level on autopilot, as the balance simulator plays it.</summary>
    [Fact]
    public void A_full_Bosco_Smeraldo_run_brings_every_special_event_and_ends_in_victory()
    {
        var waves = WaveTimeline.Load(TestContent.Root, "bosco-smeraldo", TestContent.Pokedex.Value.Species, TestContent.Moves.Value);
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, waves, View, seed: 7)
        {
            TimeLimitTicks = waves.DurationTicks,
            MovePool = TestContent.Moves.Value.Enabled,
            ItemPool = TestContent.Items.Value.Enabled,
            LevelUpRules = TestContent.LevelUpRules.Value,
            StatusRules = TestContent.Statuses.Value,
            EnemyRules = TestContent.Enemies.Value,
            LootRules = TestContent.Loot.Value,
        };
        run.Player.Moves.Add(new Core.Run.Moves.MoveInstance(TestContent.Move("thundershock")));
        run.Player.Invincible = true;

        var events = new List<WaveEventKind>();
        var fighting = new HashSet<RunEventType>();
        var mostAlive = 0;
        // Up to 10 minutes past the time limit for the boss fight.
        while (!run.IsOver && run.Ticks < waves.DurationTicks + RunClock.TicksPerSecond * 600)
        {
            if (run.IsPaused)
            {
                run.ChooseLevelUp(AutoPilot.ChooseLevelUp(run));
                continue;
            }

            run.Tick(new PlayerInput(AutoPilot.Input(run)));
            events.AddRange(run.Events.Where(e => e.Type == RunEventType.WaveEventStarted).Select(e => (WaveEventKind)(int)e.Value));
            fighting.UnionWith(run.Events.Select(e => e.Type));
            mostAlive = Math.Max(mostAlive, run.Enemies.AliveCount);
        }

        // Phase 3.4: the boss arrives when the time runs out, goes through all its phases, and its fall wins the level.
        Assert.Equal(RunOutcome.Victory, run.Outcome);
        Assert.True(run.Ticks > waves.DurationTicks + BossAi.VictoryDelayTicks);
        Assert.Superset(new HashSet<RunEventType> { RunEventType.BossAppeared, RunEventType.BossPhaseChanged, RunEventType.BossDefeated, RunEventType.HazardLanded }, fighting);

        // Phase 3.5: the autopilot breaks grass and boulders on its way and picks up what they leave, coins included.
        Assert.Superset(new HashSet<RunEventType> { RunEventType.PropBroken, RunEventType.LootCollected }, fighting);
        Assert.True(run.Player.Coins > 0);
        Assert.Equal(waves.Events.Sum(e => e.EveryTicks == 0 ? 1 : (e.UntilTick - e.AtTick) / e.EveryTicks + 1), events.Count);
        Assert.True(run.Player.Level >= 30, $"Level {run.Player.Level} at the end");
        Assert.InRange(mostAlive, 200, 1000);

        // Phase 3.2: the level's shooters, chargers, Voltorb and Abra all get to act.
        Assert.Superset(new HashSet<RunEventType> { RunEventType.EnemyShotFired, RunEventType.EnemyCharged, RunEventType.EnemyExploded, RunEventType.EnemyTeleported }, fighting);
    }
}
