using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Merchant;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Plates;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Block 9: Arceus' Plates (9.1) and Kecleon the merchant (9.2).</summary>
public class PlateMerchantTests
{
    private static readonly Lazy<PlateLibrary> Plates = new(() => PlateLibrary.Load(TestContent.Root));

    private static readonly Lazy<MerchantRules> Merchant = new(() => MerchantRules.Load(TestContent.Root));

    private static readonly Lazy<MissionLibrary> Missions = new(() =>
        MissionLibrary.Load(TestContent.Root, TestContent.Characters.Value, TrainerTests.Trainers.Value, TestContent.Worlds.Value));

    private static RunState NewRun(IReadOnlyList<PlateDefinition>? pool = null) =>
        Quiet(new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            PlateRules = Plates.Value,
            PlatePool = pool ?? [],
            StatusRules = TestContent.Statuses.Value,
            ItemPool = TestContent.Items.Value.Enabled,
            MerchantRules = Merchant.Value,
        });

    private static RunState Quiet(RunState run)
    {
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        return run;
    }

    private static PlateDefinition Plate(string id) => Plates.Value[id];

    [Fact]
    public void Sixteen_plates_one_per_type_each_hidden_on_a_level()
    {
        var plates = Plates.Value.All;
        Assert.Equal(16, plates.Count);
        Assert.Equal(16, plates.Select(p => p.Type).Distinct().Count());
        Assert.DoesNotContain(plates, p => p.Type == PokemonType.Normal);
        Assert.All(plates, p => Assert.NotNull(TestContent.Worlds.Value.Find(p.Level)));
        Assert.Equal(16, plates.Select(p => p.Level).Distinct().Count());
        Assert.All(plates, p => Assert.True(File.Exists(Path.Combine(TestContent.Root, "icons", "items", p.Icon + ".png")), $"{p.Icon} has no icon."));
    }

    [Fact]
    public void A_plate_strengthens_the_moves_of_its_type_and_their_cards()
    {
        var run = NewRun();
        var index = run.SpawnEnemy(19, new Vector2(300, 0));
        var thunder = new MoveInstance(TestContent.Move("thundershock"));
        Assert.Equal(1f, PlateSystem.DamageMultiplier(run, thunder, run.Enemies[index]));

        Assert.True(run.UsePlate(Plate("lastrasaetta")));
        Assert.Equal(1f + Plates.Value.File.TypePower, PlateSystem.DamageMultiplier(run, thunder, run.Enemies[index]), 3);
        var card = new Core.Run.Progression.LevelUpOption(Core.Run.Progression.LevelUpKind.NewMove, "thundershock", "", 0, 1, "", "", PokemonType.Electric);
        Assert.Equal(Plates.Value.File.CardWeight, PlateSystem.CardWeight(run, card));
    }

    [Fact]
    public void A_run_holds_two_plates_at_most_and_their_modifiers_add_up()
    {
        var run = NewRun();
        var speed = run.Player.MoveSpeed;
        var choices = run.ChoiceCount;
        var rerolls = run.RerollsLeft;

        Assert.True(run.UsePlate(Plate("lastracielo")));
        Assert.True(run.UsePlate(Plate("lastramente")));
        Assert.False(run.UsePlate(Plate("lastrarogo")));
        Assert.False(run.UsePlate(Plate("lastracielo")));

        Assert.Equal(speed * 1.2f, run.Player.MoveSpeed, 2);
        Assert.Equal(choices + 1, run.ChoiceCount);
        Assert.Equal(rerolls + PlateSystem.InsightRerolls, run.RerollsLeft);
    }

    [Fact]
    public void A_burned_enemy_knocked_out_with_lastrarogo_explodes_on_the_next_tick()
    {
        var run = NewRun();
        run.UsePlate(Plate("lastrarogo"));
        var burned = run.SpawnEnemy(19, new Vector2(200, 0));
        var neighbour = run.SpawnEnemy(19, new Vector2(215, 0));
        var far = run.SpawnEnemy(19, new Vector2(400, 0));
        run.Enemies[neighbour].MaxHp = run.Enemies[neighbour].Hp = 1000f;
        StatusSystem.Apply(run, burned, StatusKind.Burn, 10f);

        run.DamageEnemy(burned, 9999f);
        run.Tick(default);

        Assert.Contains(run.Events, e => e.Type == RunEventType.PlateEffect && (PlateEffectKind)(int)e.Value == PlateEffectKind.BurnBlast);
        Assert.True(run.Enemies[neighbour].Hp < 1000f);
        Assert.Equal(StatusKind.Burn, run.Enemies[neighbour].Status);
        Assert.Equal(run.Enemies[far].MaxHp, run.Enemies[far].Hp);
    }

    [Fact]
    public void Lastraidro_sends_a_wave_at_its_interval_and_lastrasaetta_strikes_one_enemy()
    {
        var run = NewRun();
        run.UsePlate(Plate("lastraidro"));
        run.UsePlate(Plate("lastrasaetta"));
        // A Dominante, so it loses only a small share of its PS to each bolt and wave and lasts the whole test.
        var near = run.SpawnEnemy(19, new Vector2(60, 0));
        run.Enemies[near].MaxHp = run.Enemies[near].Hp = 1000f;
        run.Enemies[near].Rank = EnemyRank.Dominant;
        run.Enemies[near].Speed = 0f;

        var waves = 0;
        var bolts = 0;
        for (var i = 0; i < RunClock.Seconds(Plate("lastraidro").Effect.Interval); i++)
        {
            run.Tick(default);
            waves += run.Events.Count(e => e.Type == RunEventType.PlateEffect && (PlateEffectKind)(int)e.Value == PlateEffectKind.Tide);
            bolts += run.Events.Count(e => e.Type == RunEventType.PlateEffect && (PlateEffectKind)(int)e.Value == PlateEffectKind.Bolt);
        }

        Assert.Equal(1, waves);
        Assert.Equal((int)(Plate("lastraidro").Effect.Interval / Plate("lastrasaetta").Effect.Interval), bolts);
        Assert.True(run.Enemies[near].Hp < 1000f);
        Assert.True(run.Enemies[near].Knockback.X > 0f, "The wave pushes it away.");
    }

    [Fact]
    public void Lastratetra_lets_some_hits_through_and_lastraferro_hits_dominanti_harder()
    {
        var run = NewRun();
        run.Player.Invincible = false;
        run.UsePlate(Plate("lastratetra"));
        var dodged = 0;
        for (var i = 0; i < 400; i++)
        {
            if (PlateSystem.HurtMultiplier(run) == 0f)
                dodged++;
        }

        Assert.InRange(dodged, 40, 110);

        var steel = NewRun();
        steel.UsePlate(Plate("lastraferro"));
        var index = steel.SpawnEnemy(19, new Vector2(300, 0));
        var move = new MoveInstance(TestContent.Move("thundershock"));
        var regular = PlateSystem.DamageMultiplier(steel, move, steel.Enemies[index]);
        steel.Enemies[index].Rank = EnemyRank.Dominant;
        Assert.Equal(regular * 1.5f, PlateSystem.DamageMultiplier(steel, move, steel.Enemies[index]), 3);
    }

    [Fact]
    public void A_plate_lies_on_its_level_and_once_picked_up_is_kept_by_the_meta_progression()
    {
        var level = TestContent.Worlds.Value.Timeline("bosco-smeraldo", TestContent.Pokedex.Value.Species, TestContent.Moves.Value, Difficulty.Normal);
        var run = Quiet(new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, level, new Vector2(640, 360))
        {
            PlateRules = Plates.Value,
            LootRules = TestContent.Loot.Value,
        });

        run.Tick(default);
        var lying = Assert.Single(run.Loot.Items, l => l.Kind == LootKind.Plate);
        Assert.Equal("lastrabaco", lying.ItemId);

        run.Player.Position = lying.Position;
        run.Tick(default);
        Assert.Equal(["lastrabaco"], run.Stats.PlatesFound);
        Assert.Contains(run.Plates, p => p.Id == "lastrabaco");

        var meta = new MetaProgress(new SaveData(), ShopRules.Load(TestContent.Root), TestContent.Characters.Value, TrainerTests.Trainers.Value,
            Missions.Value, TestContent.Pokedex.Value.Species, worlds: TestContent.Worlds.Value, plates: Plates.Value);
        var report = meta.RecordRun(RunRecord.From(run, "bosco-smeraldo"));
        Assert.Equal("lastrabaco", Assert.Single(report.Plates).Id);
        Assert.Equal(["lastrabaco"], meta.UnlockedPlates().Select(p => p.Id));

        // Found once, it is not laid on the map again.
        var again = Quiet(new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, level, new Vector2(640, 360))
        {
            PlateRules = Plates.Value,
            LootRules = TestContent.Loot.Value,
            SkipPlates = new HashSet<string> { "lastrabaco" },
        });
        again.Tick(default);
        Assert.DoesNotContain(again.Loot.Items, l => l.Kind == LootKind.Plate);
    }

    [Fact]
    public void The_first_chest_from_minute_eight_offers_a_plate_once()
    {
        var run = NewRun([Plate("lastrarogo"), Plate("lastraidro"), Plate("lastrabaco")]);
        run.UsePlate(Plate("lastrabaco"));

        run.OpenChest(ChestTier.Poke, Vector2.Zero);
        Assert.Null(run.PlateChoices);

        run.AdvanceTime(Plates.Value.OfferFromTicks);
        run.OpenChest(ChestTier.Poke, Vector2.Zero);
        var offered = Assert.IsAssignableFrom<IReadOnlyList<PlateDefinition>>(run.PlateChoices);
        Assert.Equal(2, offered.Count);
        Assert.DoesNotContain(offered, p => p.Id == "lastrabaco");
        Assert.True(run.IsPaused);

        run.ChoosePlate(0);
        Assert.Null(run.PlateChoices);
        Assert.Equal(2, run.Plates.Count);

        run.OpenChest(ChestTier.Poke, Vector2.Zero);
        Assert.Null(run.PlateChoices);
    }

    [Fact]
    public void Kecleon_comes_at_its_time_opens_its_shop_when_touched_and_leaves()
    {
        var run = NewRun();
        run.AdvanceTime(Merchant.Value.AppearTicks[0] - 1);
        run.Tick(default);
        Assert.True(run.Merchant.Present);
        Assert.Contains(run.Events, e => e.Type == RunEventType.MerchantArrived);
        Assert.Equal(Merchant.Value.File.Stock, run.Merchant.Stock.Count);
        Assert.Equal(run.Merchant.Stock.Count, run.Merchant.Stock.Select(w => w.Kind).Distinct().Count());

        run.Player.Position = run.Merchant.Position;
        run.Tick(default);
        Assert.True(run.ShopOpen);
        Assert.True(run.IsPaused);

        // Closed, it does not open again until the player steps away and comes back.
        run.CloseShop();
        run.Tick(default);
        Assert.False(run.ShopOpen);
        run.Player.Position = run.Merchant.Position + new Vector2(200, 0);
        run.Tick(default);
        run.Player.Position = run.Merchant.Position;
        run.Tick(default);
        Assert.True(run.ShopOpen);
        run.CloseShop();

        run.AdvanceTime(Merchant.Value.StayTicks);
        run.Tick(default);
        Assert.False(run.Merchant.Present);
        Assert.Contains(run.Events, e => e.Type == RunEventType.MerchantLeft);
    }

    [Fact]
    public void What_is_bought_from_kecleon_costs_run_coins_that_never_reach_the_bank()
    {
        var run = NewRun();
        MerchantSystem.Arrive(run, Merchant.Value);
        run.Merchant.Stock.Clear();
        run.Merchant.Stock.Add(new Ware(WareKind.Potion, 40, "Superpozione", "", "super-potion", power: 0.5f));
        run.Merchant.Stock.Add(new Ware(WareKind.Reroll, 500, "Rilancio", "", "max-elixir"));
        run.Player.Hp = run.Player.MaxHp * 0.2f;
        run.AddCoins(100);
        run.OpenShop();

        Assert.True(run.BuyWare(0));
        Assert.Equal(60, run.Player.Coins);
        Assert.Equal(run.Player.MaxHp * 0.7f, run.Player.Hp, 1);
        Assert.False(run.BuyWare(0));
        Assert.False(run.BuyWare(1));

        var record = RunRecord.From(run, "bosco-smeraldo");
        Assert.Equal(60, record.Coins);
        Assert.Equal(100, record.Earned);
    }
}
