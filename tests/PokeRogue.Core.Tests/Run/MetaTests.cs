using System.Numerics;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Run;

public class MetaTests
{
    private static readonly Lazy<ShopRules> Shop = new(() => ShopRules.Load(TestContent.Root));

    private static readonly Lazy<MissionLibrary> Missions = new(() =>
        MissionLibrary.Load(TestContent.Root, TestContent.Characters.Value, TrainerTests.Trainers.Value));

    private static CharacterLibrary Characters => TestContent.Characters.Value;

    private static MetaProgress Meta(SaveData? data = null, SaveStore? store = null) =>
        new(data ?? new SaveData(), Shop.Value, Characters, TrainerTests.Trainers.Value, Missions.Value, TestContent.Pokedex.Value.Species, store);

    private static RunState Run(CharacterDefinition character, RunLoadout? loadout)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            MovePool = TestContent.Moves.Value.Enabled,
            SpecialRules = Specials.Value,
        };
        run.Spawner.Enabled = false;
        run.UseCharacter(character, loadout);
        return run;
    }

    private static readonly Lazy<Core.Run.Specials.SpecialMoves> Specials = new(() => Core.Run.Specials.SpecialMoves.Load(TestContent.Root));

    private static RunRecord Record(string? character = "pikachu", bool won = false, int minutes = 6, int kills = 500, int coins = 300,
        string? challengeWon = null, Difficulty difficulty = Difficulty.Normal) => new()
    {
        LevelId = "bosco-smeraldo",
        Difficulty = difficulty,
        CharacterId = character,
        TrainerId = "ash",
        Over = true,
        Won = won,
        ChallengeWon = challengeWon,
        Ticks = minutes * 60 * RunClock.TicksPerSecond,
        Kills = kills,
        KillsBySpecies = new Dictionary<int, int> { [10] = kills },
        Seen = [10, 16, 25],
        Coins = coins,
        Level = 20,
    };

    [Fact]
    public void A_fresh_save_has_only_the_starting_character_and_ash()
    {
        var meta = Meta();

        Assert.Equal(["pikachu"], Characters.All.Where(meta.IsUnlocked).Select(c => c.Id));
        Assert.Equal(["ash"], TrainerTests.Trainers.Value.All.Where(meta.IsUnlocked).Select(t => t.Id));
        Assert.Equal(0, meta.Coins);
    }

    [Fact]
    public void Characters_are_bought_with_the_bank_coins_once()
    {
        var meta = Meta(new SaveData { Coins = 500 });
        var bulbasaur = Characters["bulbasaur"];

        Assert.Equal(Purchase.Done, meta.Buy(bulbasaur));
        Assert.Equal(100, meta.Coins);
        Assert.True(meta.IsUnlocked(bulbasaur));
        Assert.Equal(Purchase.Owned, meta.Buy(bulbasaur));
        Assert.Equal(Purchase.NotEnoughCoins, meta.Buy(Characters["chikorita"]));
        Assert.Equal(Purchase.Unavailable, meta.Buy(Characters["ditto"]));
        Assert.Null(meta.Price(Characters["omanyte"]));
    }

    [Fact]
    public void The_special_needs_the_secondary_move_and_both_reach_the_run()
    {
        var pikachu = Characters["pikachu"];
        var meta = Meta(new SaveData { Coins = 10_000 });

        var bare = Run(pikachu, meta.Loadout(pikachu));
        Assert.Single(bare.Player.Moves);
        Assert.Null(bare.SpecialMove);

        Assert.Equal(Purchase.NeedsSecondary, meta.BuySpecial(pikachu));
        Assert.Equal(Purchase.Done, meta.BuySecondary(pikachu));
        Assert.Equal(Purchase.Done, meta.BuySpecial(pikachu));
        Assert.Equal(10_000 - meta.SecondaryPrice(pikachu) - meta.SpecialPrice(pikachu), meta.Coins);

        var full = Run(pikachu, meta.Loadout(pikachu));
        Assert.Equal([pikachu.MainMove.Id, pikachu.SecondaryMove.Id], full.Player.Moves.Select(m => m.Definition.Id));
        Assert.NotNull(full.SpecialMove);
    }

    [Fact]
    public void Stat_grades_cost_more_each_time_and_are_refunded_in_full()
    {
        var pikachu = Characters["pikachu"];
        var meta = Meta(new SaveData { Coins = 10_000 });

        var first = meta.GradePrice(pikachu, StatKind.Hp)!.Value;
        meta.BuyGrade(pikachu, StatKind.Hp);
        var second = meta.GradePrice(pikachu, StatKind.Hp)!.Value;
        meta.BuyGrade(pikachu, StatKind.Hp);
        Assert.True(second > first);

        var plain = Run(pikachu, RunLoadout.Default).Player.MaxHp;
        var trained = Run(pikachu, meta.Loadout(pikachu)).Player.MaxHp;
        Assert.Equal(MathF.Round(plain * (1f + 2 * Shop.Value.Stat(StatKind.Hp).PerGrade.MaxHpPercent)), trained);

        Assert.Equal(first + second, meta.RefundGrades(pikachu));
        Assert.Equal(10_000, meta.Coins);
        Assert.Equal(0, meta.Grade(pikachu, StatKind.Hp));
    }

    [Fact]
    public void Trainer_grades_get_dearer_with_every_grade_owned_and_shorten_the_cooldown()
    {
        var trainers = TrainerTests.Trainers.Value;
        var ash = trainers["ash"];
        var meta = Meta(new SaveData { Coins = 10_000, Trainers = ["brock"] });

        var price = meta.TrainerGradePrice(trainers["brock"]);
        meta.BuyTrainerGrade(ash);
        Assert.True(meta.TrainerGradePrice(trainers["brock"]) > price);
        Assert.True(meta.Upgraded(ash).Active.Cooldown < ash.Active.Cooldown);
        Assert.Equal(Purchase.Unavailable, meta.BuyTrainerGrade(trainers["misty"]));

        var spent = 10_000 - meta.Coins;
        Assert.Equal(spent, meta.RefundTrainer(ash));
        Assert.Equal(ash.Active.Cooldown, meta.Upgraded(ash).Active.Cooldown);
    }

    [Fact]
    public void Upgrades_open_the_sixth_slot_and_the_fourth_card_and_some_need_medals()
    {
        var pikachu = Characters["pikachu"];
        var meta = Meta(new SaveData { Coins = 20_000 });
        var shop = Shop.Value;

        Assert.Equal(Purchase.Done, meta.BuyUpgrade(shop.Upgrade("sesto-slot")!));
        Assert.Equal(Purchase.Done, meta.BuyUpgrade(shop.Upgrade("fortuna")!));
        Assert.Equal(Purchase.NeedsMedals, meta.BuyUpgrade(shop.Upgrade("rilancio")!));

        var run = Run(pikachu, meta.Loadout(pikachu));
        Assert.Equal(6, run.MaxMoves);
        run.GrantLevel();
        Assert.Equal(4, run.LevelUpChoices!.Count);

        meta.Data.TrainersDefeated.Add("brock");
        Assert.Equal(Purchase.Done, meta.BuyUpgrade(shop.Upgrade("rilancio")!));
        Assert.Equal(RunState.StartingLevelUpCharges + 1, Run(pikachu, meta.Loadout(pikachu)).RerollsLeft);

        var spent = 20_000 - meta.Coins;
        Assert.Equal(spent, meta.RefundUpgrades());
        Assert.Equal(LevelUp.MaxMoves, Run(pikachu, meta.Loadout(pikachu)).MaxMoves);
    }

    [Fact]
    public void A_run_banks_its_coins_fills_the_pokedex_and_completes_missions()
    {
        var meta = Meta();
        var report = meta.RecordRun(Record(minutes: 6, kills: 1200, coins: 300));

        Assert.Equal(300, report.CoinsBanked);
        Assert.Contains(report.Missions, m => m.Id == "prima-partita");
        Assert.Contains(report.Missions, m => m.Id == "cinque-minuti");
        Assert.Contains(report.Missions, m => m.Id == "mille-ko");
        Assert.Equal(300 + report.MissionCoins, meta.Coins);
        Assert.Equal(3, report.NewSpecies);
        Assert.Equal(1200, meta.Data.Pokedex[10].Defeated);
        Assert.Equal(1, meta.Data.Totals.Runs);

        // A mission is completed once.
        var again = meta.RecordRun(Record(minutes: 6, kills: 1200, coins: 0));
        Assert.DoesNotContain(again.Missions, m => m.Id == "mille-ko");
    }

    [Fact]
    public void Winning_with_magikarp_unlocks_feebas_and_winning_a_challenge_unlocks_the_trainer()
    {
        var meta = Meta(new SaveData { Characters = ["magikarp"] });
        var report = meta.RecordRun(Record("magikarp", won: true, challengeWon: "brock"));

        Assert.Contains(report.NewCharacters, c => c.Id == "feebas");
        Assert.True(meta.IsUnlocked(Characters["feebas"]));
        Assert.Contains(report.NewTrainers, t => t.Id == "brock");
        Assert.Equal(1, meta.Medals);
        Assert.Contains(report.Missions, m => m.Id == "prima-medaglia");
        Assert.Contains(report.Missions, m => m.Id == "bosco-normale");
        Assert.DoesNotContain(report.Missions, m => m.Id == "bosco-ultra");
    }

    [Fact]
    public void Missions_reached_during_a_run_are_announced_but_given_at_its_end()
    {
        var meta = Meta();
        var live = Record(minutes: 5, kills: 1000) with { Over = false };

        var reached = meta.ReachedDuring(live).Select(m => m.Id).ToList();
        Assert.Contains("cinque-minuti", reached);
        Assert.Contains("mille-ko", reached);
        Assert.DoesNotContain("prima-partita", reached);
        Assert.Empty(meta.Data.Missions);

        // A level's mission at another difficulty never counts.
        var ultra = meta.ReachedDuring(Record(won: true, difficulty: Difficulty.Ultra) with { Over = false });
        Assert.Contains(ultra, m => m.Id == "bosco-ultra");
        Assert.DoesNotContain(ultra, m => m.Id == "bosco-normale");
    }

    [Fact]
    public void Every_mission_character_has_its_mission_and_progress_counts_across_runs()
    {
        foreach (var character in Characters.All.Where(c => c.Unlock.Kind == UnlockKind.Mission))
            Assert.NotNull(Missions.Value.Unlocking(character.Id));

        var meta = Meta();
        meta.RecordRun(Record(kills: 4000));
        var tenThousand = Missions.Value.Find("diecimila-ko")!;
        Assert.Equal((4000, 10_000), meta.Progress(tenThousand));
        Assert.Equal((7000, 10_000), meta.Progress(tenThousand, Record(kills: 3000) with { Over = false }));
    }

    [Fact]
    public void The_save_is_written_with_a_backup_and_read_back_from_it_when_broken()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pokerogue-tests", Guid.NewGuid().ToString("N"));
        var store = new SaveStore(Path.Combine(folder, "save.json"));
        try
        {
            var meta = Meta(new SaveData { Coins = 1000 }, store);
            meta.Buy(Characters["bulbasaur"]);
            meta.RecordRun(Record(coins: 50));

            var loaded = store.Load();
            Assert.Equal(SaveSource.Save, store.LastSource);
            Assert.Equal(meta.Coins, loaded.Coins);
            Assert.Contains("bulbasaur", loaded.Characters);
            Assert.True(File.Exists(store.BackupPath));

            File.WriteAllText(store.Path, "{ not json");
            var recovered = store.Load();
            Assert.Equal(SaveSource.Backup, store.LastSource);
            Assert.Contains("bulbasaur", recovered.Characters);
            Assert.Single(Directory.GetFiles(folder, "save.json.broken-*"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Without_a_save_the_game_starts_fresh()
    {
        var store = new SaveStore(Path.Combine(Path.GetTempPath(), "pokerogue-tests", Guid.NewGuid().ToString("N"), "save.json"));

        var data = store.Load();
        Assert.Equal(SaveSource.None, store.LastSource);
        Assert.Equal(SaveData.CurrentVersion, data.Version);
        Assert.Equal(0, data.Coins);
    }
}
