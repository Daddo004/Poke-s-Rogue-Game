using System.Numerics;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Waves;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 9.3: the endless mode and Darkrai the Reaper.</summary>
public class EndlessTests
{
    private static readonly Lazy<EndlessRules> Rules = new(() =>
        TestContent.Worlds.Value.Endless(TestContent.Pokedex.Value.Species, TestContent.Moves.Value)!);

    private static int Minutes(float minutes) => RunClock.Seconds(minutes * 60f);

    private static RunState NewRun(bool endless = true)
    {
        var level = TestContent.Worlds.Value.Timeline("bosco-smeraldo", TestContent.Pokedex.Value.Species, TestContent.Moves.Value);
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, level, new Vector2(640, 360))
        {
            TimeLimitTicks = level.DurationTicks,
            EndlessRules = endless ? Rules.Value : null,
        };
        run.Player.Invincible = true;
        return run;
    }

    /// <summary>Knocks out the boss on the field and lets its faint play.</summary>
    private static void BeatBoss(RunState run)
    {
        run.DamageEnemy(run.Boss!.Index, float.MaxValue);
        for (var i = 0; i <= BossAi.VictoryDelayTicks; i++)
            run.Tick(default);
    }

    [Fact]
    public void Darkrai_is_the_reaper_and_the_last_five_minutes_loop()
    {
        var rules = Rules.Value;
        Assert.Equal("darkrai", TestContent.Pokedex.Value.Species[rules.Reaper.SpeciesId - 1].Key);
        Assert.Equal(Minutes(30), rules.ReaperAtTicks);
        Assert.Contains(rules.Reaper.Phases.SelectMany(p => p.Attacks), a => a.Spec.Kind == BossAttackKind.DarkVoid && a.SleepTicks > 0);
        Assert.Contains(rules.Reaper.Phases.SelectMany(p => p.Attacks), a => a.Spec.Kind == BossAttackKind.Teleport);

        var duration = Minutes(15);
        Assert.Equal(Minutes(14), rules.Looped(Minutes(14), duration));
        Assert.Equal(Minutes(10), rules.Looped(Minutes(15), duration));
        Assert.Equal(Minutes(12.5f), rules.Looped(Minutes(17.5f), duration));
        Assert.Equal(Minutes(10), rules.Looped(Minutes(20), duration));
        Assert.Equal(2, rules.Cycle(Minutes(20), duration));
    }

    [Fact]
    public void Beating_the_boss_does_not_end_an_endless_run()
    {
        var run = NewRun();
        run.AdvanceTime(Minutes(15));
        run.Tick(default);
        Assert.NotNull(run.Boss);

        BeatBoss(run);
        Assert.False(run.IsOver);
        Assert.True(run.PastBoss);
        Assert.Null(run.Boss);

        // The time limit does not bring the boss back, and the waves go on.
        for (var i = 0; i < 120; i++)
            run.Tick(default);
        Assert.Null(run.Boss);
        Assert.True(run.Enemies.AliveCount > 0);

        var ordinary = NewRun(endless: false);
        ordinary.AdvanceTime(Minutes(15));
        ordinary.Tick(default);
        BeatBoss(ordinary);
        Assert.Equal(RunOutcome.Victory, ordinary.Outcome);
    }

    [Fact]
    public void Past_the_end_the_loops_events_come_round_again()
    {
        var run = NewRun();
        run.AdvanceTime(Minutes(15) - 1);
        run.Spawner.SkipEventsBefore(Minutes(15) - 1);
        var dominants = 0;
        while (run.Ticks < Minutes(21))
        {
            run.Tick(default);
            dominants += run.Events.Count(e => e.Type == RunEventType.WaveEventStarted && (WaveEventKind)(int)e.Value == WaveEventKind.Dominant);
            if (run.Boss is { DefeatedTick: null } boss)
                run.DamageEnemy(boss.Index, float.MaxValue);
        }

        // Bosco Smeraldo's Dominanti at 11:00 and 13:30 come back at 16:00 and 18:30 (then 21:00).
        Assert.InRange(dominants, 2, 3);
    }

    [Fact]
    public void Darkrai_comes_at_thirty_minutes_after_a_warning_and_beating_it_wins_and_unlocks_it()
    {
        var run = NewRun();
        run.AdvanceTime(Minutes(15));
        run.Tick(default);
        BeatBoss(run);

        run.AdvanceTime(Minutes(29) - run.Ticks);
        run.Tick(default);
        Assert.Contains(run.Events, e => e.Type == RunEventType.ReaperWarning);
        Assert.False(run.ReaperCame);

        run.AdvanceTime(Minutes(30) - run.Ticks);
        run.Tick(default);
        Assert.True(run.ReaperCame);
        Assert.True(run.Boss is { IsReaper: true, IsLegendary: true });

        BeatBoss(run);
        Assert.Equal(RunOutcome.Victory, run.Outcome);

        var meta = new MetaProgress(new SaveData(), ShopRules.Load(TestContent.Root), TestContent.Characters.Value, TrainerTests.Trainers.Value,
            MissionLibrary.Load(TestContent.Root, TestContent.Characters.Value, TrainerTests.Trainers.Value, TestContent.Worlds.Value),
            TestContent.Pokedex.Value.Species, worlds: TestContent.Worlds.Value);
        var report = meta.RecordRun(RunRecord.From(run, "bosco-smeraldo"));
        Assert.Contains(report.NewCharacters, c => c.Id == "darkrai");
        Assert.Equal(run.Ticks, meta.Data.EndlessBest["bosco-smeraldo"]);
    }

    [Fact]
    public void The_reaper_grows_angrier_with_every_minute()
    {
        var run = NewRun();
        run.AdvanceTime(Minutes(30));
        run.SummonReaper();
        run.Tick(default);
        var fight = run.Boss!;
        Assert.True(fight.IsReaper);

        // Its contact hurts more after two minutes: the same touch, measured on a fresh player.
        run.Player.Invincible = false;
        run.Enemies[fight.Index].Position = run.Player.Position;
        run.Enemies[fight.Index].CooldownTicks = 10_000;
        run.Tick(default);
        var early = run.Player.MaxHp - run.Player.Hp;

        var later = NewRun();
        later.AdvanceTime(Minutes(30));
        later.SummonReaper();
        later.Tick(default);
        later.AdvanceTime(Minutes(2));
        later.Player.Invincible = false;
        later.Enemies[later.Boss!.Index].Position = later.Player.Position;
        later.Enemies[later.Boss.Index].CooldownTicks = 10_000;
        later.Tick(default);
        Assert.True(later.Player.MaxHp - later.Player.Hp > early);
    }

    [Fact]
    public void Caught_in_a_dark_void_the_player_falls_asleep_and_cannot_move()
    {
        var run = NewRun();
        run.Spawner.Enabled = false;
        run.Player.Invincible = false;
        run.Hazards.Add(new Hazard(run.Player.Position, 30f, 1f, false, run.Ticks, run.Ticks + 1, Rules.Value.Reaper.SpeciesId, SleepTicks: 60));
        run.Tick(default);

        Assert.True(run.Player.IsAsleep);
        Assert.Contains(run.Events, e => e.Type == RunEventType.PlayerSlept);
        var at = run.Player.Position;
        run.Tick(new PlayerInput(new Vector2(1, 0)));
        Assert.Equal(at, run.Player.Position);

        for (var i = 0; i < 60; i++)
            run.Tick(new PlayerInput(new Vector2(1, 0)));
        Assert.False(run.Player.IsAsleep);
        Assert.True(run.Player.Position.X > at.X);
    }
}
