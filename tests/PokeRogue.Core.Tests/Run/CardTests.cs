using PokeRogue.Core.Meta;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;

namespace PokeRogue.Core.Tests.Run;

public class CardTests
{
    private static readonly Lazy<ShopRules> Shop = new(() => ShopRules.Load(TestContent.Root));

    private static readonly Lazy<MissionLibrary> Missions = new(() =>
        MissionLibrary.Load(TestContent.Root, TestContent.Characters.Value, TrainerTests.Trainers.Value));

    private static readonly Lazy<CardFile> Rules = new(() => CardCatalog.LoadRules(TestContent.Root));

    /// <summary>Pikachu (25) and Raichu (26) with one TCG card of each rarity, Charmander (4) with two common ones.</summary>
    private static readonly Lazy<CardCatalog> Cards = new(() => CardCatalog.From(Rules.Value, Characters,
    [
        .. Enum.GetValues<CardRarity>().SelectMany(r => new[] { Tcg(25, r), Tcg(26, r) }),
        Tcg(4, CardRarity.Base, "a"),
        Tcg(4, CardRarity.Base, "b"),
    ]));

    private static CharacterLibrary Characters => TestContent.Characters.Value;

    private static TcgCard Tcg(int species, CardRarity tier, string suffix = "") => new()
    {
        Id = $"test-{species}-{tier}{suffix}",
        Species = species,
        Name = $"Carta {species}",
        Rarity = tier.ToString(),
        Tier = tier,
        Image = "https://assets.tcgdex.net/it/test",
    };

    private static MetaProgress Meta(SaveData? data = null) =>
        new(data ?? new SaveData(), Shop.Value, Characters, TrainerTests.Trainers.Value, Missions.Value, TestContent.Pokedex.Value.Species, cards: Cards.Value);

    private static RunRecord Record(string character = "pikachu", string? stage = null, bool won = false, int minutes = 5, int kills = 500,
        Difficulty difficulty = Difficulty.Normal) => new()
    {
        LevelId = "bosco-smeraldo",
        Difficulty = difficulty,
        CharacterId = character,
        TrainerId = "ash",
        Over = true,
        Won = won,
        Ticks = minutes * RunClock.TicksPerClockMinute,
        Kills = kills,
        Level = 20,
        StageKey = stage,
    };

    private static string Id(int species, CardRarity tier) => $"test-{species}-{tier}";

    [Fact]
    public void A_line_set_holds_its_species_cards_by_rarity_then_stage()
    {
        var pikachu = Cards.Value.Of(Characters["pikachu"]);

        Assert.Equal(10, pikachu.Count);
        Assert.Equal([Id(25, CardRarity.Base), Id(26, CardRarity.Base), Id(25, CardRarity.Holo)], pikachu.Take(3).Select(c => c.Id));
        Assert.Equal(2, Cards.Value.Of(Characters["charmander"]).Count);
        Assert.Equal(Enumerable.Range(1, Cards.Value.All.Count), Cards.Value.All.Select(c => c.Number));
    }

    [Fact]
    public void The_api_rarities_fall_in_the_games_rarities()
    {
        Assert.Equal(CardRarity.Base, CardCatalog.TierOf(Rules.Value, "Comune"));
        Assert.Equal(CardRarity.Holo, CardCatalog.TierOf(Rules.Value, "Trois Diamant"));
        Assert.Equal(CardRarity.FullArt, CardCatalog.TierOf(Rules.Value, "Ultrarara"));
        Assert.Equal(CardRarity.Shiny, CardCatalog.TierOf(Rules.Value, "Rara illustrazione speciale"));
        Assert.Equal(CardRarity.Crown, CardCatalog.TierOf(Rules.Value, "Couronne"));
        Assert.Equal(CardRarity.Base, CardCatalog.TierOf(Rules.Value, "una rarità mai vista"));
    }

    [Fact]
    public void Bond_levels_follow_the_table_then_grow_by_the_step()
    {
        var catalog = Cards.Value;
        Assert.Equal(0, catalog.BondLevel(9));
        Assert.Equal(1, catalog.BondLevel(10));
        Assert.Equal(3, catalog.BondLevel(60));
        Assert.Equal(10, catalog.BondLevel(550));
        Assert.Equal(11, catalog.BondLevel(550 + catalog.Rules.LevelStep));
        Assert.Equal(550 + 2 * catalog.Rules.LevelStep, catalog.BondPoints(12));
    }

    [Fact]
    public void The_first_run_gives_a_pack_with_the_first_species_card()
    {
        var meta = Meta();
        var pikachu = Characters["pikachu"];

        var report = meta.RecordRun(Record());

        // Reaching Pikachu is a pack, and 5 minutes + 500 KO = 10 points is bond level 1: another one.
        Assert.Equal(10, report.Bond.Points);
        Assert.Equal([(pikachu, 2)], report.Packs);
        Assert.True(report.HasNews);
        Assert.Equal([Id(25, CardRarity.Base)], meta.PackPool(pikachu).Select(c => c.Id));

        var cards = meta.OpenPack(pikachu, new Random(1));
        Assert.Equal([Id(25, CardRarity.Base)], cards.Select(c => c.Id));
        Assert.True(meta.IsOwned(cards[0]) && meta.IsNew(cards[0]));

        // Nothing left to draw: the second pack stays closed.
        Assert.Empty(meta.OpenPack(pikachu, new Random(1)));
        Assert.Equal(1, meta.CardLine(pikachu).Packs);
    }

    [Fact]
    public void Evolving_and_winning_open_more_rarities_and_packs_never_give_doubles()
    {
        var meta = Meta();
        var pikachu = Characters["pikachu"];

        var report = meta.RecordRun(Record(stage: "raichu", won: true, difficulty: Difficulty.Hard, minutes: 15, kills: 3000));

        // Pikachu and Raichu reached, a first win at Hard, and (15 + 30 + 10) × 1.5 = 82 points (rounded to even) = bond level 3.
        Assert.Equal(82, report.Bond.Points);
        Assert.Equal(3, report.Bond.Level);
        Assert.Equal(2 + 1 + 3, meta.CardLine(pikachu).Packs);
        var pool = meta.PackPool(pikachu).Select(c => c.Rarity).ToList();
        Assert.Equal(8, pool.Count);
        Assert.DoesNotContain(CardRarity.Crown, pool);

        var owned = new List<CardDefinition>();
        while (meta.OpenPack(pikachu, new Random(owned.Count)) is { Count: > 0 } cards)
        {
            Assert.True(cards.Count <= Cards.Value.Rules.PackSize);
            Assert.Equal(cards.OrderBy(c => c.Rarity), cards);
            owned.AddRange(cards);
        }

        Assert.Equal(8, owned.Select(c => c.Id).Distinct().Count());
        Assert.Equal(8, owned.Count);

        // Winning at Ultra opens the secret cards and gives a pack for them.
        meta.RecordRun(Record(stage: "raichu", won: true, difficulty: Difficulty.Ultra));
        Assert.Equal([Id(25, CardRarity.Crown), Id(26, CardRarity.Crown)], meta.OpenPack(pikachu, new Random(3)).Select(c => c.Id).Order());
    }

    [Fact]
    public void A_save_from_before_the_cards_starts_with_a_bond_for_its_wins()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pokerogue-cards-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "version": 2, "characterWins": { "pikachu": 3 } }""");
            var meta = Meta(new SaveStore(path).Load());
            var pikachu = Characters["pikachu"];

            // 3 wins = 60 points = bond level 3: three packs, plus one for Pikachu reached.
            Assert.Equal(60, meta.CardLine(pikachu).Bond);
            Assert.Equal(4, meta.PacksWaiting);
            Assert.Contains(meta.PackPool(pikachu), c => c.Rarity == CardRarity.FullArt);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
