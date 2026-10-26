using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 3.4: the level boss.</summary>
public class BossTests
{
    private static WaveTimeline Level(params BossPhaseSpec[] phases) => WaveTimeline.From(new WaveFile
    {
        Id = "prova",
        Minutes = 1,
        Boss = new BossSpec { Pokemon = "pinsir", Hp = 50, Phases = phases },
    }, TestContent.Pokedex.Value.Species, TestContent.Moves.Value);

    /// <summary>A long rest, so each test sees a single attack.</summary>
    private static BossPhaseSpec Phase(float below, params BossAttackSpec[] attacks) => new() { Below = below, Rest = 5f, Attacks = attacks };

    private static RunState NewRun(WaveTimeline level)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, level, new Vector2(640, 360))
        {
            TimeLimitTicks = level.DurationTicks,
        };
        run.Spawner.Enabled = false;
        return run;
    }

    private static readonly BossAttackSpec Charge = new() { Kind = BossAttackKind.Charge };

    [Fact]
    public void When_time_runs_out_the_boss_arrives_instead_of_victory()
    {
        var run = NewRun(Level(Phase(1f, Charge)));
        run.Player.Invincible = true;

        run.AdvanceTime(run.TimeLimitTicks!.Value);
        run.Tick(default);
        run.Tick(default);

        Assert.False(run.IsOver);
        Assert.NotNull(run.Boss);
        var boss = run.Enemies[run.Boss.Index];
        Assert.Equal(EnemyRank.Boss, boss.Rank);
        Assert.Equal(0f, boss.KnockbackScale);
        Assert.Contains(run.Events, e => e.Type == RunEventType.BossAppeared);
    }

    [Fact]
    public void A_level_without_a_boss_still_ends_at_the_time_limit()
    {
        var run = NewRun(WaveTimeline.From(new WaveFile { Id = "prova", Minutes = 1 }, TestContent.Pokedex.Value.Species));
        run.AdvanceTime(run.TimeLimitTicks!.Value);
        run.Tick(default);

        Assert.Equal(RunOutcome.Victory, run.Outcome);
    }

    [Fact]
    public void A_new_phase_brings_a_roar_that_shields_the_boss()
    {
        var run = NewRun(Level(Phase(1f, Charge), Phase(0.5f, Charge)));
        run.Player.Invincible = true;
        run.SummonBoss();
        var index = run.Boss!.Index;
        ref var boss = ref run.Enemies[index];
        boss.Hp = boss.MaxHp * 0.49f;

        run.Tick(default);

        Assert.Equal(1, run.Boss.Phase);
        Assert.Equal(EnemyAction.Roar, run.Enemies[index].Action);
        Assert.Contains(run.Events, e => e.Type == RunEventType.BossPhaseChanged);
        var hp = run.Enemies[index].Hp;
        run.DamageEnemy(index, 1000f);
        Assert.Equal(hp, run.Enemies[index].Hp);

        for (var i = 0; i < BossAi.RoarTicks + 1; i++)
            run.Tick(default);
        Assert.NotEqual(EnemyAction.Roar, run.Enemies[index].Action);
    }

    [Fact]
    public void A_volley_fires_its_whole_fan()
    {
        var volley = new BossAttackSpec { Kind = BossAttackKind.Volley, Move = "x-scissor", Count = 7, WindUp = 0.1f };
        var run = NewRun(Level(Phase(1f, volley)));
        run.Player.Invincible = true;
        run.SummonBoss();
        run.Enemies[run.Boss!.Index].CooldownTicks = 0;

        var fired = 0;
        for (var i = 0; i < 30; i++)
        {
            run.Tick(default);
            fired += run.Events.Count(e => e.Type == RunEventType.EnemyShotFired);
        }

        Assert.Equal(7, fired);
    }

    [Fact]
    public void Rocks_land_where_the_player_stood_and_hurt_it()
    {
        var rocks = new BossAttackSpec { Kind = BossAttackKind.Rockfall, Count = 3, WindUp = 0.1f, Delay = 0.5f };
        var run = NewRun(Level(Phase(1f, rocks)));
        run.SummonBoss();
        run.Enemies[run.Boss!.Index].CooldownTicks = 0;
        run.Enemies[run.Boss.Index].Position = new Vector2(400, 0);

        var hurt = false;
        var landed = 0;
        for (var i = 0; i < 60; i++)
        {
            run.Tick(default);
            if (i == 10)
                Assert.Equal(3, run.Hazards.Count);
            hurt |= run.Events.Any(e => e.Type == RunEventType.PlayerHurt);
            landed += run.Events.Count(e => e.Type == RunEventType.HazardLanded);
        }

        Assert.Equal(3, landed);
        Assert.True(hurt);
    }

    [Fact]
    public void Beating_the_boss_wins_the_level_after_its_fall()
    {
        var run = NewRun(Level(Phase(1f, Charge)));
        run.Player.Invincible = true;
        run.SummonBoss();
        run.Tick(default);

        run.DamageEnemy(run.Boss!.Index, float.MaxValue);
        Assert.Contains(run.Events, e => e.Type == RunEventType.BossDefeated);
        for (var i = 0; i < BossAi.VictoryDelayTicks - 1; i++)
            run.Tick(default);
        Assert.False(run.IsOver);

        run.Tick(default);
        run.Tick(default);
        Assert.Equal(RunOutcome.Victory, run.Outcome);
    }

    [Fact]
    public void Bad_bosses_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => Level());
        Assert.Throws<InvalidDataException>(() => Level(Phase(0.8f, Charge)));
        Assert.Throws<InvalidDataException>(() => Level(Phase(1f, Charge), Phase(1f, Charge)));
        Assert.Throws<InvalidDataException>(() => Level(Phase(1f, new BossAttackSpec { Kind = BossAttackKind.Volley })));
    }
}
