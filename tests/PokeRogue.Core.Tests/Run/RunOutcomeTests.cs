using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Tests.Run;

public class RunOutcomeTests
{
    private static RunState NewRun(int? timeLimitTicks = null) =>
        new(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, WaveTimeline.Constant([new SpawnEntry(19, 1)], spawnsPerSecond: 30), new Vector2(640, 360))
        {
            TimeLimitTicks = timeLimitTicks,
            ItemPool = TestContent.Items.Value.Enabled,
        };

    [Fact]
    public void Kills_are_counted_per_species()
    {
        var run = NewRun();
        run.Spawner.Enabled = false;
        var rattata = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0));
        var pidgey = run.Enemies.Spawn(TestContent.Species(16), new Vector2(-300, 0));
        var rattata2 = run.Enemies.Spawn(TestContent.Species(19), new Vector2(0, 300));

        run.DamageEnemy(rattata, 999);
        run.DamageEnemy(pidgey, 999);
        run.DamageEnemy(rattata2, 999);

        Assert.Equal(3, run.Stats.Kills);
        Assert.Equal([(19, 2), (16, 1)], run.Stats.TopKills(5));
    }

    [Fact]
    public void Fainting_ends_the_run_in_defeat_and_freezes_time()
    {
        var run = NewRun();
        run.Spawner.Enabled = false;
        run.Player.Hp = 1;
        run.Enemies.Spawn(TestContent.Species(19), new Vector2(1, 0));

        run.Tick(default);
        var ticks = run.Ticks;
        run.Tick(default);

        Assert.Equal(RunOutcome.Defeat, run.Outcome);
        Assert.True(run.Stats.DamageTaken >= 1);
        Assert.Equal(ticks, run.Ticks);
    }

    [Fact]
    public void Surviving_the_time_limit_is_a_victory_and_the_horde_faints()
    {
        var run = NewRun(timeLimitTicks: 120);
        run.Player.Invincible = true;

        for (var i = 0; i < 120; i++)
            run.Tick(default);

        Assert.Equal(RunOutcome.Victory, run.Outcome);
        Assert.Contains(run.Events, e => e.Type == RunEventType.RunEnded && (int)e.Value == (int)RunOutcome.Victory);
        Assert.Equal(0, run.Enemies.AliveCount);
        Assert.Equal(0, run.Stats.Kills);

        // Aftermath ticks let every faint animation finish.
        for (var i = 0; i < Enemy.FaintTicks; i++)
            run.Tick(default);
        Assert.All(run.Enemies.Items.ToArray(), e => Assert.Equal(EnemyState.Inactive, e.State));
    }

    [Fact]
    public void Acquisition_time_and_healing_are_recorded()
    {
        var run = NewRun();
        run.AdvanceTime(600);
        run.Player.Hp = 10;

        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.NewItem, "leftovers", "", 0, 1, "", ""));
        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.Heal, "oran-berry", "", 0, 0, "", ""));
        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.Coins, "nugget", "", 0, 0, "", ""));

        Assert.Equal(600, run.Player.Items[0].AcquiredTick);
        Assert.Equal(LevelUp.HealAmount, run.Player.HpRecovered);
        Assert.Equal(LevelUp.CoinAmount, run.Stats.CoinsCollected);
    }

    [Fact]
    public void Healing_never_exceeds_max_hp_or_revives()
    {
        var player = NewRun().Player;

        Assert.Equal(0f, player.Heal(50));
        player.Hp = 0;
        Assert.Equal(0f, player.Heal(50));
        Assert.True(player.IsDown);
    }
}
