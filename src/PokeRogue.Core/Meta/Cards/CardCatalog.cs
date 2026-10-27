using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Characters;

namespace PokeRogue.Core.Meta.Cards;

/// <summary>A card's rarity in the game, from the most common (phase 10); each gathers several TCG rarities.</summary>
public enum CardRarity
{
    Base,
    Holo,
    FullArt,
    Shiny,
    Crown,
}

/// <summary>What opens a rarity: the cards of an open rarity can come out of the line's packs.</summary>
public enum CardNeed
{
    /// <summary>The species was reached in a run.</summary>
    Reached,

    /// <summary>Reached, and the line's bond is at least <see cref="RaritySpec.BondLevel"/>.</summary>
    Bond,

    /// <summary>Reached, and a level was won with the line.</summary>
    Win,

    /// <summary>Reached, and a level was won with the line at Difficile or Ultra.</summary>
    HardWin,

    /// <summary>Reached, and a level was won with the line at Ultra.</summary>
    UltraWin,
}

public sealed record RaritySpec
{
    public required CardRarity Rarity { get; set; }

    public required string NameIt { get; set; }

    /// <summary>Cards of this rarity kept per species.</summary>
    public int Quota { get; set; } = 1;

    public CardNeed Needs { get; set; } = CardNeed.Reached;

    public int BondLevel { get; set; }

    public string HintIt { get; set; } = "";

    /// <summary>The TCGdex rarities (Italian names) that fall in this one.</summary>
    public IReadOnlyList<string> ApiRarities { get; set; } = [];
}

public sealed record BondSpec
{
    public float PerMinute { get; set; } = 1f;

    public int KillsPerPoint { get; set; } = 100;

    public float Win { get; set; } = 10f;

    public Dictionary<Difficulty, float> Difficulty { get; set; } = [];
}

public sealed record CardFile(int Version, BondSpec Bond, IReadOnlyList<int> Levels, int LevelStep, int PackSize, IReadOnlyList<RaritySpec> Rarities);

/// <summary>
/// A real TCG card as the TCGdex API describes it (content/generated/carte.json, written by the DataTool): its id, the
/// species it shows, its Italian name, rarity, expansion and illustrator, and where the API keeps its picture
/// (<see cref="Image"/> + "/high.jpg").
/// </summary>
public sealed record TcgCard
{
    public required string Id { get; set; }

    public required int Species { get; set; }

    public required string Name { get; set; }

    public required string Rarity { get; set; }

    public required CardRarity Tier { get; set; }

    public string Set { get; set; } = "";

    public string? Illustrator { get; set; }

    public required string Image { get; set; }

    /// <summary>The picture's address at the quality the game uses.</summary>
    public string ImageUrl => $"{Image}/high.jpg";
}

public sealed record TcgCardFile(int Version, string Source, IReadOnlyList<TcgCard> Cards);

/// <summary>One collectible card of the game: a TCG card of one species of a line, at one of the game's rarities.</summary>
public sealed record CardDefinition(string Id, int Number, CharacterDefinition Line, SpeciesData Species, RaritySpec Spec, TcgCard Tcg)
{
    public CardRarity Rarity => Spec.Rarity;
}

/// <summary>
/// Every collectible card (phase 10): the rules of content/data/cards.json over the TCG cards the API gave
/// (content/generated/carte.json). Per line, the rarities in order, and within each the line's species in order.
/// Numbers run through the whole collection in that order.
/// </summary>
public sealed class CardCatalog
{
    public const string FileName = "cards.json";

    /// <summary>The TCG cards chosen from the TCGdex API, in content/generated.</summary>
    public const string TcgFileName = "carte.json";

    private readonly Dictionary<string, CardDefinition> _byId;
    private readonly Dictionary<string, IReadOnlyList<CardDefinition>> _byLine;

    private CardCatalog(CardFile rules, IReadOnlyList<CardDefinition> all)
    {
        Rules = rules;
        All = all;
        _byId = all.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _byLine = all.GroupBy(c => c.Line.Id).ToDictionary(g => g.Key, g => (IReadOnlyList<CardDefinition>)g.ToList(), StringComparer.Ordinal);
    }

    public CardFile Rules { get; }

    public IReadOnlyList<CardDefinition> All { get; }

    public CardDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The set of one line, in album order.</summary>
    public IReadOnlyList<CardDefinition> Of(CharacterDefinition line) => _byLine.GetValueOrDefault(line.Id) ?? [];

    public RaritySpec Spec(CardRarity rarity) => Rules.Rarities.First(r => r.Rarity == rarity);

    /// <summary>The game's rarity of a TCGdex rarity; one in no list is common.</summary>
    public static CardRarity TierOf(CardFile rules, string apiRarity) =>
        rules.Rarities.FirstOrDefault(r => r.ApiRarities.Contains(apiRarity, StringComparer.OrdinalIgnoreCase))?.Rarity ?? CardRarity.Base;

    /// <summary>The bond level reached with <paramref name="points"/>.</summary>
    public int BondLevel(int points)
    {
        var level = Rules.Levels.Count(threshold => points >= threshold);
        if (level == Rules.Levels.Count && Rules.LevelStep > 0)
            level += (points - Rules.Levels[^1]) / Rules.LevelStep;
        return level;
    }

    /// <summary>The points of bond level <paramref name="level"/> (0 for level 0).</summary>
    public int BondPoints(int level)
    {
        if (level <= 0)
            return 0;
        if (level <= Rules.Levels.Count)
            return Rules.Levels[level - 1];
        return Rules.Levels[^1] + (level - Rules.Levels.Count) * Rules.LevelStep;
    }

    /// <summary>The bond a run leaves: minutes, KO and a win, times the difficulty; at least one point.</summary>
    public int BondOf(RunRecord run)
    {
        var bond = Rules.Bond;
        var points = run.Minutes * bond.PerMinute + run.Kills / (float)Math.Max(1, bond.KillsPerPoint) + (run.Won ? bond.Win : 0f);
        return Math.Max(1, (int)MathF.Round(points * bond.Difficulty.GetValueOrDefault(run.Difficulty, 1f)));
    }

    public static CardFile LoadRules(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, DataJsonContext.Default.CardFile) ?? throw new InvalidDataException($"{path} is empty.");
    }

    /// <summary>The rules and the API's cards; without carte.json (the DataTool's "carte" not run yet) the collection is empty.</summary>
    public static CardCatalog Load(string contentRoot, CharacterLibrary characters)
    {
        var path = ContentPaths.Generated(contentRoot, TcgFileName);
        TcgCardFile? tcg = null;
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            tcg = JsonSerializer.Deserialize(stream, DataJsonContext.Default.TcgCardFile);
        }

        return From(LoadRules(contentRoot), characters, tcg?.Cards ?? []);
    }

    public static CardCatalog From(CardFile file, CharacterLibrary characters, IReadOnlyList<TcgCard> tcg)
    {
        if (file.Rarities.Select(r => r.Rarity).Distinct().Count() != file.Rarities.Count)
            throw new InvalidDataException($"{FileName}: a rarity is listed twice.");
        if (file.PackSize <= 0)
            throw new InvalidDataException($"{FileName}: \"packSize\" must be positive.");
        if (file.Levels.Count == 0 || file.Levels.Zip(file.Levels.Skip(1)).Any(p => p.Second <= p.First))
            throw new InvalidDataException($"{FileName}: \"levels\" must grow.");

        var bySpecies = tcg.GroupBy(c => c.Species).ToDictionary(g => g.Key, g => g.ToList());
        var cards = new List<CardDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in characters.All)
        {
            foreach (var spec in file.Rarities)
            {
                foreach (var stage in line.Stages)
                {
                    foreach (var card in bySpecies.GetValueOrDefault(stage.Species.Id) ?? [])
                    {
                        if (card.Tier == spec.Rarity && seen.Add(card.Id))
                            cards.Add(new CardDefinition(card.Id, cards.Count + 1, line, stage.Species, spec, card));
                    }
                }
            }
        }

        return new CardCatalog(file, cards);
    }
}
