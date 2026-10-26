using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Statuses;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 3.3: Dominanti and the chests they drop.</summary>
public class DominantTests
{
    private static RunState NewRun(WaveTimeline? waves = null)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, waves ?? WaveTimeline.Constant([new SpawnEntry(19, 1)]), new Vector2(640, 360))
        {
            StatusRules = TestContent.Statuses.Value,
            Evolutions = TestContent.Moves.Value.Evolutions,
        };
        run.Spawner.Enabled = waves is not null;
        run.Player.Invincible = true;
        return run;
    }

    private static int Dominant(RunState run, ChestTier tier = ChestTier.Poke, Vector2? at = null)
    {
        var index = run.SpawnEnemy(17, at ?? new Vector2(200, 0), hpMultiplier: 20f);
        Spawner.MakeDominant(run, index, tier);
        return index;
    }

    [Fact]
    public void A_dominant_event_brings_one_big_sturdy_pokemon_and_announces_it()
    {
        var waves = WaveTimeline.From(new WaveFile
        {
            Id = "prova",
            Events = [new WaveEventSpec { At = "0:00", Kind = WaveEventKind.Dominant, Pokemon = "pidgeotto", Hp = 30, Count = 9, Chest = ChestTier.Great }],
        }, TestContent.Pokedex.Value.Species);
        var run = NewRun(waves);

        run.Tick(default);
        run.Tick(default);

        var dominant = run.Enemies.Items.ToArray().Single(e => e.IsAlive);
        var plain = run.Enemies[run.Enemies.Spawn(TestContent.Species(17), new Vector2(500, 0))];
        Assert.Equal(EnemyRank.Dominant, dominant.Rank);
        Assert.Equal(ChestTier.Great, dominant.Chest);
        Assert.Equal(plain.Radius * Spawner.DominantHitboxScale, dominant.Radius, 2);
        Assert.Equal(plain.MaxHp * 30f, dominant.MaxHp, 0);
        Assert.Equal(plain.XpValue * 10, dominant.XpValue);
        Assert.Contains(run.Events, e => e.Type == RunEventType.DominantAppeared && e.SpeciesId == 17);
    }

    [Fact]
    public void Dominants_shrug_off_sleep_freeze_and_one_hit_knockouts()
    {
        var run = NewRun();
        var index = Dominant(run);

        Assert.False(StatusSystem.Apply(run, index, StatusKind.Freeze));
        Assert.False(StatusSystem.Apply(run, index, StatusKind.Sleep));
        Assert.True(StatusSystem.Apply(run, index, StatusKind.Poison));
        Assert.Equal(TestContent.Statuses.Value[StatusKind.Poison]!.DurationTicks / 2, run.Enemies[index].StatusTicks);

        var abyss = new MoveInstance(TestContent.Move("fissure") with { OhkoChance = 1f });
        run.HitEnemy(index, abyss, Vector2.UnitX);
        Assert.True(run.Enemies[index].IsAlive);
        Assert.DoesNotContain(run.Events, e => e.Ohko);
    }

    [Fact]
    public void A_fallen_dominant_leaves_a_chest_that_opens_once_it_has_landed()
    {
        var run = NewRun();
        var index = Dominant(run, ChestTier.Great, at: new Vector2(4, 0));

        run.DamageEnemy(index, float.MaxValue);
        Assert.Contains(run.Events, e => e.Type == RunEventType.ChestDropped);
        Assert.Single(run.Pickups.Chests);

        // Right on top of the player, but it only opens after popping out.
        var opened = new List<int>();
        for (var i = 0; i < Chest.LandTicks + 2; i++)
        {
            // Its big gem brings level-ups on the way.
            while (run.IsPaused)
                run.ChooseLevelUp(0);
            run.Tick(default);
            if (run.Events.Any(e => e.Type == RunEventType.ChestOpened))
                opened.Add(i);
        }

        Assert.Single(opened);
        Assert.True(opened[0] >= Chest.LandTicks - 2, $"opened at tick {opened[0]}");
        Assert.Empty(run.Pickups.Chests);
        Assert.Equal(ChestTier.Great, run.LastChest!.Tier);
        Assert.Equal(3, run.LastChest.Rewards.Count);
        Assert.Equal(1, run.Stats.ChestsOpened);
    }

    [Fact]
    public void Chests_evolve_first_then_upgrade_what_the_player_has()
    {
        var run = NewRun();
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("thundershock"), level: 8));
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("ember"), level: 1));
        run.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));
        run.Player.RecalculateStats();

        var chest = run.OpenChest(ChestTier.Ultra, Vector2.Zero);

        Assert.Equal(5, chest.Rewards.Count);
        Assert.Equal(LevelUpKind.Evolution, chest.Rewards[0].Kind);
        Assert.Equal("thunder", run.Player.Moves[0].Definition.Id);
        Assert.All(chest.Rewards.Skip(1), r => Assert.True(r.Kind is LevelUpKind.MoveUpgrade or LevelUpKind.ItemUpgrade, $"{r.Kind} {r.Id}"));
        var levels = run.Player.Moves[1].Level - 1 + run.Player.Items[0].Level - 1;
        Assert.Equal(4, levels);
    }

    [Fact]
    public void With_everything_maxed_a_chest_gives_coins()
    {
        var run = NewRun();
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("ember"), level: 8));

        var chest = run.OpenChest(ChestTier.Poke, Vector2.Zero);

        Assert.Equal(LevelUpKind.Coins, Assert.Single(chest.Rewards).Kind);
        Assert.Equal(LevelUp.CoinAmount, run.Player.Coins);
    }
}
