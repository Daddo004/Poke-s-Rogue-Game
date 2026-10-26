using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Tests.Run;

public class ItemEffectTests
{
    private static RunState NewRun()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            MovePool = TestContent.Moves.Value.Enabled,
            ItemPool = TestContent.Items.Value.All,
        };
        run.Spawner.Enabled = false;
        return run;
    }

    private static void Hold(RunState run, string id, int level)
    {
        run.Player.Items.Add(new ItemInstance(TestContent.Item(id), level));
        run.Player.RecalculateStats();
    }

    [Fact]
    public void Ferro_softens_physical_touches_and_Zinco_special_ones()
    {
        var bare = NewRun().Player;
        var iron = NewRun();
        Hold(iron, "iron", 5);
        var zinc = NewRun();
        Hold(zinc, "zinc", 5);

        var physical = bare.TakeContactDamage(40f);
        Assert.Equal(MathF.Round(physical * 0.7f), iron.Player.TakeContactDamage(40f), 0);
        Assert.Equal(physical, zinc.Player.TakeContactDamage(40f));

        var special = NewRun().Player.TakeContactDamage(40f, special: true);
        var zincTarget = NewRun();
        Hold(zincTarget, "zinc", 5);
        Assert.Equal(MathF.Round(special * 0.7f), zincTarget.Player.TakeContactDamage(40f, special: true), 0);
    }

    [Theory]
    [InlineData(92, true)]
    [InlineData(63, true)]
    [InlineData(19, false)]
    [InlineData(16, false)]
    public void Pokemon_with_more_special_attack_touch_specially(int species, bool special)
    {
        var run = NewRun();
        var index = run.Enemies.Spawn(TestContent.Species(species), new Vector2(300, 0));

        Assert.Equal(special, run.Enemies[index].HitsSpecially);
    }

    [Fact]
    public void Mirino_adds_five_percent_critical_chance_per_level()
    {
        var run = NewRun();
        Hold(run, "scope-lens", 3);

        Assert.Equal(Player.BaseCritChance + 0.15f, run.Player.Attacker.CritChance, 3);
    }

    [Fact]
    public void Focalnastro_brings_the_player_back_once_per_level()
    {
        var run = NewRun();
        Hold(run, "focus-sash", 1);
        run.Player.Hp = 0f;

        run.Tick(default);

        Assert.Equal(RunOutcome.None, run.Outcome);
        Assert.Equal(MathF.Round(run.Player.MaxHp * 0.5f), run.Player.Hp);
        Assert.Contains(run.Events, e => e.Type == RunEventType.PlayerRevived);

        run.Player.Hp = 0f;
        run.Tick(default);
        Assert.Equal(RunOutcome.Defeat, run.Outcome);
    }

    [Fact]
    public void Conchinella_heals_a_share_of_the_damage_dealt()
    {
        var run = NewRun();
        Hold(run, "shell-bell", 3);
        run.Player.Hp -= 40f;
        var before = run.Player.Hp;
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0), hpMultiplier: 100f);

        run.HitEnemy(index, new MoveInstance(TestContent.Move("ember")), Vector2.UnitX);

        var dealt = run.Events.Single(e => e.Type == RunEventType.EnemyHit).Value;
        Assert.Equal(before + dealt * 0.015f, run.Player.Hp, 3);
    }

    [Fact]
    public void Healing_from_damage_dealt_is_capped_per_second()
    {
        var run = NewRun();
        Hold(run, "shell-bell", 3);
        run.Player.Hp = 10f;
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0), hpMultiplier: 10000f);
        var drain = new MoveInstance(TestContent.Move("leech-life") with { Drain = 1f });

        // Fifty big draining hits in one tick heal at most a second's worth of the cap.
        for (var i = 0; i < 50; i++)
            run.HitEnemy(index, drain, Vector2.UnitX);

        Assert.Equal(10f + run.Player.MaxHp * Player.DamageHealCapPerSecond, run.Player.Hp, 2);
    }

    [Fact]
    public void Monetamuleto_adds_coins()
    {
        var run = NewRun();
        Hold(run, "amulet-coin", 2);

        run.AddCoins(25);

        Assert.Equal(35, run.Player.Coins);
        Assert.Equal(35, run.Stats.CoinsCollected);
    }

    [Fact]
    public void Grandelente_Creta_Luce_and_Metronomo_reach_the_moves()
    {
        var run = NewRun();
        Hold(run, "wide-lens", 2);
        Hold(run, "light-clay", 2);
        Hold(run, "metronome", 1);
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("ember")));
        run.Enemies.Spawn(TestContent.Species(19), new Vector2(100, 0), hpMultiplier: 100f);

        run.Tick(default);

        Assert.Equal(1.2f, run.Player.AreaMultiplier, 3);
        Assert.Equal(1.2f, run.Player.DurationMultiplier, 3);
        Assert.Equal(2, run.MoveSystem.Projectiles.ActiveCount);
    }

    [Fact]
    public void Zinco_and_Monetamuleto_are_offered_now_that_they_matter()
    {
        var enabled = TestContent.Items.Value.Enabled.Select(i => i.Id).ToList();

        // Special attackers arrived with the waves of phase 3.1 and coins drop since phase 3.5.
        Assert.Equal(17, enabled.Count);
        Assert.Contains("zinc", enabled);
        Assert.Contains("amulet-coin", enabled);
    }
}
