using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Run;

public class ProgressionTests
{
    private static RunState NewRun(int seed = 1, LevelUpRules? rules = null)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360), seed)
        {
            MovePool = TestContent.Moves.Value.Enabled,
            ItemPool = TestContent.Items.Value.Enabled,
            LevelUpRules = rules,
        };
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("thundershock")));
        return run;
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(19, 185)]
    [InlineData(20, 198)]
    [InlineData(39, 445)]
    [InlineData(40, 461)]
    public void Xp_curve_matches_the_vampire_survivors_shape(int level, int expected)
    {
        Assert.Equal(expected, XpCurve.ToNextLevel(level));
    }

    [Fact]
    public void Defeated_enemies_drop_a_gem_worth_their_experience()
    {
        var run = NewRun();
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0));

        run.DamageEnemy(index, 9999f);

        var gem = Assert.Single(run.Pickups.Gems.Items.ToArray(), g => g.Active);
        Assert.Equal(run.Enemies[index].XpValue, gem.Value);
        Assert.Equal(new Vector2(300, 0), gem.Position);
    }

    [Fact]
    public void Gems_inside_the_pickup_radius_fly_to_the_player_and_grant_experience()
    {
        var run = NewRun();
        run.Pickups.Gems.Spawn(new Vector2(20, 0), 2);
        run.Pickups.Gems.Spawn(new Vector2(200, 0), 2);

        var collected = 0f;
        for (var i = 0; i < 60; i++)
        {
            run.Tick(default);
            collected += run.Events.Where(e => e.Type == RunEventType.GemCollected).Sum(e => e.Value);
        }

        Assert.Equal(2f, collected);
        Assert.Equal(2f, run.Player.Xp);
        Assert.Equal(1, run.Pickups.Gems.ActiveCount);
    }

    [Fact]
    public void Reaching_the_threshold_levels_up_and_pauses_the_run_on_a_choice()
    {
        var run = NewRun();

        run.CollectExperience(5, Vector2.Zero);

        Assert.Equal(2, run.Player.Level);
        Assert.Equal(1, run.PendingLevelUps);
        Assert.True(run.IsPaused);
        Assert.Equal(RunState.LevelUpChoiceCount, run.LevelUpChoices!.Count);
        Assert.Contains(run.Events, e => e.Type == RunEventType.LevelUp);

        var ticks = run.Ticks;
        run.Tick(default);
        Assert.Equal(ticks, run.Ticks);

        run.ChooseLevelUp(0);
        Assert.False(run.IsPaused);
        Assert.Equal(0, run.PendingLevelUps);
    }

    [Fact]
    public void Several_levels_at_once_offer_one_choice_per_level()
    {
        var run = NewRun();

        run.CollectExperience(5 + 15 + 25, Vector2.Zero);

        Assert.Equal(4, run.Player.Level);
        Assert.Equal(3, run.PendingLevelUps);
        run.ChooseLevelUp(0);
        run.ChooseLevelUp(0);
        Assert.True(run.IsPaused);
        run.ChooseLevelUp(0);
        Assert.False(run.IsPaused);
    }

    [Fact]
    public void Options_are_distinct_and_never_offer_maxed_or_impossible_upgrades()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var run = NewRun(seed);
            var hpUp = TestContent.Item("hp-up");
            run.Player.Moves[0].Level = TestContent.Move("thundershock").MaxLevel;
            run.Player.Items.Add(new ItemInstance(hpUp, hpUp.MaxLevel));

            var options = LevelUp.Generate(run, 3);

            Assert.Equal(3, options.Count);
            Assert.Equal(options.Count, options.Select(o => (o.Kind, o.Id)).Distinct().Count());
            Assert.DoesNotContain(options, o => o.Id == "thundershock");
            Assert.DoesNotContain(options, o => o.Id == hpUp.Id);
        }
    }

    [Fact]
    public void Full_item_slots_offer_only_upgrades_of_held_items()
    {
        var run = NewRun();
        foreach (var item in TestContent.Items.Value.Enabled.Take(LevelUp.MaxItems))
            run.Player.Items.Add(new ItemInstance(item));

        var options = LevelUp.Generate(run, 20);

        Assert.DoesNotContain(options, o => o.Kind == LevelUpKind.NewItem);
        Assert.Contains(options, o => o.Kind == LevelUpKind.ItemUpgrade);
    }

    [Fact]
    public void Everything_maxed_falls_back_to_heal_and_coins()
    {
        var run = NewRun();
        run.Player.Moves[0].Level = TestContent.Move("thundershock").MaxLevel;
        foreach (var move in TestContent.Moves.Value.Enabled.Where(m => m.Id != "thundershock").Take(LevelUp.MaxMoves - 1))
            run.Player.Moves.Add(new MoveInstance(move, move.MaxLevel));
        foreach (var item in TestContent.Items.Value.Enabled.Take(LevelUp.MaxItems))
            run.Player.Items.Add(new ItemInstance(item, item.MaxLevel));

        var options = LevelUp.Generate(run, 3);

        Assert.Equal([LevelUpKind.Heal, LevelUpKind.Coins], options.Select(o => o.Kind));
        LevelUp.Apply(run, options[1]);
        Assert.Equal(LevelUp.CoinAmount, run.Player.Coins);
    }

    [Fact]
    public void Rilancio_draws_new_options_and_spends_a_charge()
    {
        var run = NewRun(seed: 5);
        run.CollectExperience(5, Vector2.Zero);
        var first = run.LevelUpChoices!.Select(o => o.Id).ToList();

        Assert.True(run.RerollLevelUp());
        Assert.True(run.RerollLevelUp());
        Assert.False(run.RerollLevelUp());

        Assert.Equal(0, run.RerollsLeft);
        Assert.Equal(RunState.LevelUpChoiceCount, run.LevelUpChoices!.Count);
        Assert.True(run.IsPaused);
        Assert.NotEqual(first, run.LevelUpChoices.Select(o => o.Id).ToList());
    }

    [Fact]
    public void Salta_gives_up_the_choice_and_moves_to_the_next_level()
    {
        var run = NewRun();
        run.CollectExperience(5 + 15, Vector2.Zero);

        Assert.True(run.SkipLevelUp());
        Assert.True(run.IsPaused);
        Assert.Equal(1, run.PendingLevelUps);

        Assert.True(run.SkipLevelUp());
        Assert.False(run.IsPaused);
        Assert.Single(run.Player.Moves);
        Assert.Empty(run.Player.Items);
    }

    [Fact]
    public void Bandisci_removes_an_option_for_the_rest_of_the_run()
    {
        var run = NewRun(seed: 2);
        run.CollectExperience(5, Vector2.Zero);
        var banished = run.LevelUpChoices![1];

        Assert.True(run.BanishLevelUp(1));

        Assert.Equal(RunState.LevelUpChoiceCount, run.LevelUpChoices!.Count);
        Assert.DoesNotContain(run.LevelUpChoices, o => o.Id == banished.Id);
        for (var i = 0; i < 30; i++)
            Assert.DoesNotContain(LevelUp.Generate(run, 50), o => o.Id == banished.Id);
        Assert.Equal(RunState.StartingLevelUpCharges - 1, run.BanishesLeft);
    }

    [Fact]
    public void Items_change_player_stats()
    {
        var run = NewRun();
        var player = run.Player;
        var baseMaxHp = player.MaxHp;
        var baseRadius = player.PickupRadius;

        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.NewItem, "hp-up", "", 0, 1, "", ""));
        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.NewItem, "magnet", "", 0, 1, "", ""));
        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.NewItem, "quick-claw", "", 0, 1, "", ""));
        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.ItemUpgrade, "quick-claw", "", 1, 2, "", ""));

        Assert.Equal(MathF.Round(baseMaxHp * 1.2f), player.MaxHp);
        Assert.Equal(player.MaxHp, player.Hp);
        Assert.Equal(baseRadius * 1.3f, player.PickupRadius, 3);
        Assert.Equal(0.84f, player.CooldownMultiplier, 3);
    }

    [Fact]
    public void A_full_gem_pool_merges_extra_experience_instead_of_losing_it()
    {
        var pool = new GemPool(capacity: 4);
        for (var i = 0; i < 4; i++)
            pool.Spawn(new Vector2(i * 50, 0), 1);

        pool.Spawn(Vector2.Zero, 7);
        pool.Spawn(Vector2.Zero, 3);

        var gems = pool.Items.ToArray();
        Assert.Equal(4, pool.ActiveCount);
        Assert.Equal(14, gems.Sum(g => g.Value));
        Assert.Contains(gems, g => g.Tier == GemTier.Large);
    }

    [Fact]
    public void Weighted_cards_favour_upgrading_what_the_player_owns()
    {
        // Only Tuonoshock owned: alone it takes its part of the move-upgrade share, against 34 new moves and 15 new items.
        var rules = TestContent.LevelUpRules.Value;
        var upgrade = rules.WeightOf(LevelUpKind.MoveUpgrade, 1);
        var expected = upgrade / (upgrade + rules.Shares.NewMove + rules.Shares.NewItem);

        int weighted = 0, uniform = 0;
        const int Draws = 1000;
        for (var seed = 0; seed < Draws; seed++)
        {
            if (LevelUp.Generate(NewRun(seed, TestContent.LevelUpRules.Value), 3)[0].Id == "thundershock")
                weighted++;
            if (LevelUp.Generate(NewRun(seed), 3)[0].Id == "thundershock")
                uniform++;
        }

        Assert.InRange(weighted / (float)Draws, expected - 0.05f, expected + 0.05f);
        Assert.True(uniform < Draws / 10, $"Without rules the upgrade is one card among ~50: {uniform}/{Draws}");
        Assert.True(weighted > uniform * 3);
    }

    [Fact]
    public void A_share_is_split_at_least_three_ways()
    {
        var rules = TestContent.LevelUpRules.Value;

        Assert.Equal(3, rules.SplitAtLeast);
        Assert.Equal(rules.Shares.MoveUpgrade / 3f, rules.WeightOf(LevelUpKind.MoveUpgrade, 1));
        Assert.Equal(rules.Shares.MoveUpgrade / 5f, rules.WeightOf(LevelUpKind.MoveUpgrade, 5));
    }

    [Fact]
    public void A_kind_without_share_never_shows_unless_nothing_else_is_left()
    {
        var noNewItems = LevelUpRules.From(new LevelUpFile(1, new LevelUpShares { NewItem = 0f }));
        for (var seed = 0; seed < 50; seed++)
            Assert.DoesNotContain(LevelUp.Generate(NewRun(seed, noNewItems), 3), o => o.Kind == LevelUpKind.NewItem);

        // Everything else maxed or full: the new items still fill the cards rather than leaving the window empty.
        var run = NewRun(rules: noNewItems);
        run.Player.Moves[0].Level = TestContent.Move("thundershock").MaxLevel;
        foreach (var move in TestContent.Moves.Value.Enabled.Where(m => m.Id != "thundershock").Take(LevelUp.MaxMoves - 1))
            run.Player.Moves.Add(new MoveInstance(move, move.MaxLevel));
        Assert.Equal(3, LevelUp.Generate(run, 3).Count(o => o.Kind == LevelUpKind.NewItem));
    }

    [Fact]
    public void Shares_cannot_be_negative_or_all_zero()
    {
        Assert.Throws<InvalidDataException>(() => LevelUpRules.From(new LevelUpFile(1, new LevelUpShares { NewMove = -1f })));
        Assert.Throws<InvalidDataException>(() => LevelUpRules.From(new LevelUpFile(1, new LevelUpShares { MoveUpgrade = 0f, ItemUpgrade = 0f, NewMove = 0f, NewItem = 0f })));
    }

    [Fact]
    public void Move_upgrade_text_lists_what_changes()
    {
        var levels = TestContent.Move("thundershock").Levels;

        Assert.Equal("+1 rimbalzo, potenza +2, ricarica -0,05 s, portata +10.", LevelUp.DescribeUpgrade(levels[0], levels[1]));
        Assert.Equal("PS massimi +20%.", LevelUp.Describe(TestContent.Item("hp-up").PerLevel));
    }
}
