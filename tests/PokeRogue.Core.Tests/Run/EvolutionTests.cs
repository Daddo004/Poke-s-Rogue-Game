using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Run;

public class EvolutionTests
{
    /// <summary>With the gold level-up card of phase 2.6 switched on, unless <paramref name="levelUpCard"/> is false.</summary>
    private static RunState NewRun(int seed = 1, bool levelUpCard = true)
    {
        var moves = TestContent.Moves.Value;
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360), seed)
        {
            MovePool = moves.Enabled,
            ItemPool = TestContent.Items.Value.Enabled,
            Evolutions = moves.Evolutions,
            EvolutionsAtLevelUp = levelUpCard,
        };
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        return run;
    }

    private static MoveInstance Maxed(string id) => new(TestContent.Move(id), MoveLibrary.MaxLevels);

    [Fact]
    public void Since_the_chests_level_ups_never_offer_evolutions()
    {
        var run = NewRun(levelUpCard: false);
        run.Player.Moves.Add(Maxed("thundershock"));
        run.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));

        for (var i = 0; i < 20; i++)
            Assert.DoesNotContain(LevelUp.Generate(run, 3), o => o.Kind == LevelUpKind.Evolution);
        Assert.Equal(LevelUpKind.Evolution, run.OpenChest(ChestTier.Poke, Vector2.Zero).Rewards[0].Kind);
    }

    [Fact]
    public void A_maxed_move_with_its_item_offers_the_evolution_first()
    {
        var run = NewRun();
        run.Player.Moves.Add(Maxed("thundershock"));
        run.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));

        var options = LevelUp.Generate(run, 3);

        Assert.Equal(LevelUpKind.Evolution, options[0].Kind);
        Assert.Equal("thunder", options[0].Id);
        Assert.Equal("thundershock", options[0].EvolvesFrom);
        Assert.Equal("magnet", options[0].EvolveItem);
    }

    [Fact]
    public void An_evolved_move_never_comes_back_as_a_new_move()
    {
        var run = NewRun();
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("thunder")));

        for (var i = 0; i < 50; i++)
            Assert.DoesNotContain(LevelUp.Generate(run, 3), o => o.Id == "thundershock");
    }

    [Fact]
    public void A_new_item_card_for_an_item_picked_up_meanwhile_raises_it()
    {
        var run = NewRun();
        var card = new LevelUpOption(LevelUpKind.NewItem, "magnet", "Calamita", 0, 1, "", "magnet");
        run.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));

        LevelUp.Apply(run, card);

        Assert.Equal(2, Assert.Single(run.Player.Items).Level);
    }

    [Fact]
    public void No_evolution_without_the_item_or_below_the_maximum_level()
    {
        var withoutItem = NewRun();
        withoutItem.Player.Moves.Add(Maxed("thundershock"));

        var notMaxed = NewRun();
        notMaxed.Player.Moves.Add(new MoveInstance(TestContent.Move("thundershock"), MoveLibrary.MaxLevels - 1));
        notMaxed.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));

        for (var i = 0; i < 20; i++)
        {
            Assert.DoesNotContain(LevelUp.Generate(withoutItem, 3), o => o.Kind == LevelUpKind.Evolution);
            Assert.DoesNotContain(LevelUp.Generate(notMaxed, 3), o => o.Kind == LevelUpKind.Evolution);
        }
    }

    [Fact]
    public void Evolving_takes_the_base_move_slot_and_keeps_its_damage()
    {
        var run = NewRun();
        run.Player.Moves.Add(Maxed("ember"));
        run.Player.Moves.Add(Maxed("thundershock"));
        run.Player.Moves[1].DamageDealt = 1234f;
        run.Player.Items.Add(new ItemInstance(TestContent.Item("magnet")));

        var evolution = LevelUp.Generate(run, 3).First(o => o.Kind == LevelUpKind.Evolution);
        LevelUp.Apply(run, evolution);

        Assert.Equal("thunder", run.Player.Moves[1].Definition.Id);
        Assert.Equal(1234f, run.Player.Moves[1].DamageDealt);
        Assert.Equal("ember", run.Player.Moves[0].Definition.Id);
        Assert.DoesNotContain(LevelUp.Generate(run, 10), o => o.Id == "thunder");

        // Chosen while the game is frozen: the renderer hears about it once the game resumes.
        Assert.DoesNotContain(run.Events, e => e.Type == RunEventType.MoveEvolved);
        run.Tick(default);
        Assert.Contains(run.Events, e => e.Type == RunEventType.MoveEvolved && e.MoveId == "thunder" && (int)e.Value == 1);
    }

    [Fact]
    public void Verdebufera_leaves_never_stop_circling()
    {
        var run = NewRun();
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("leaf-storm")));

        for (var i = 0; i < RunClock.TicksPerSecond * 10; i++)
            run.Tick(default);

        Assert.Equal(TestContent.Move("leaf-storm").Levels[0].Amount, run.MoveSystem.Projectiles.ActiveCount);
    }

    [Theory]
    [InlineData("thunder,flamethrower,air-slash,bone-rush,leaf-storm,toxic")]
    [InlineData("power-whip,ice-beam,stone-edge,earth-power,flare-blitz,fissure")]
    public void Every_evolved_move_fights_a_horde(string ids)
    {
        var run = NewRun(seed: 5);
        foreach (var id in ids.Split(','))
            run.Player.Moves.Add(new MoveInstance(TestContent.Move(id)));

        // A minute against waves closing in from every side, some right next to Pikachu; level-ups take the first card.
        for (var i = 0; i < RunClock.TicksPerSecond * 60; i++)
        {
            if (i % RunClock.TicksPerSecond == 0)
            {
                for (var k = 0; k < 24; k++)
                {
                    var angle = k * MathF.Tau / 24f;
                    var at = run.Player.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (30f + k % 4 * 40f);
                    run.Enemies.Spawn(TestContent.Species(19), at, hpMultiplier: 3f);
                }
            }

            if (run.IsPaused)
                run.ChooseLevelUp(0);
            run.Tick(default);
        }

        Assert.All(run.Player.Moves, m => Assert.True(m.DamageDealt > 0, $"{m.Definition.Id} dealt no damage"));
        Assert.True(run.Stats.Kills > 300);
    }

    [Fact]
    public void The_autopilot_builds_towards_evolutions()
    {
        var run = NewRun();
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("ember"), 3));
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("thundershock"), 5));
        var upgrade = new LevelUpOption(LevelUpKind.MoveUpgrade, "thundershock", "Tuonoshock", 5, 6, "", "thundershock");
        var weakerUpgrade = new LevelUpOption(LevelUpKind.MoveUpgrade, "ember", "Braciere", 3, 4, "", "ember");
        var evolveItem = new LevelUpOption(LevelUpKind.NewItem, "magnet", "Calamita", 0, 1, "", "magnet");
        var otherItem = new LevelUpOption(LevelUpKind.NewItem, "iron", "Ferro", 0, 1, "", "iron");
        var evolution = new LevelUpOption(LevelUpKind.Evolution, "thunder", "Tuono", 0, 1, "", "thunder");

        run.OfferLevelUp([otherItem, weakerUpgrade, upgrade]);
        Assert.Equal(2, AutoPilot.ChooseLevelUp(run));
        run.OfferLevelUp([otherItem, evolveItem, weakerUpgrade]);
        Assert.Equal(2, AutoPilot.ChooseLevelUp(run));
        run.OfferLevelUp([otherItem, evolveItem]);
        Assert.Equal(1, AutoPilot.ChooseLevelUp(run));
        run.OfferLevelUp([upgrade, evolution]);
        Assert.Equal(1, AutoPilot.ChooseLevelUp(run));
    }

    [Fact]
    public void Abisso_can_swallow_an_enemy_whole()
    {
        var run = NewRun();
        var sure = new MoveInstance(TestContent.Move("fissure") with { OhkoChance = 1f });
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0), hpMultiplier: 1000f);

        run.HitEnemy(index, sure, Vector2.UnitX);

        Assert.False(run.Enemies[index].IsAlive);
        Assert.Contains(run.Events, e => e.Type == RunEventType.EnemyHit && e.Ohko);
    }
}
