using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Characters;

/// <summary>Where a character comes from in the user's roster.</summary>
public enum CharacterGroup
{
    Starter,
    Fossil,
    Legendary,
    Mythical,
    Other,
}

/// <summary>How a character is unlocked (the shop, missions and levels that grant it come in Blocks 6 and 7).</summary>
public enum UnlockKind
{
    /// <summary>Playable from the first run.</summary>
    Start,

    /// <summary>Bought in the shop.</summary>
    Shop,

    /// <summary>Its fossil is found on a level, then revived in the shop.</summary>
    Fossil,

    /// <summary>Its egg is hidden on a level.</summary>
    Egg,

    /// <summary>A mission's reward.</summary>
    Mission,

    /// <summary>Beaten in its own encounter, at Ultra.</summary>
    Defeat,
}

/// <summary>A character's ability as written in characters.json; its effect is phase 4.3's.</summary>
public sealed record AbilitySpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public string DescriptionIt { get; set; } = "";

    /// <summary>Used with a key and a cooldown; otherwise always on.</summary>
    public bool Active { get; set; }
}

/// <summary>One species of a character's line.</summary>
public sealed record StageSpec
{
    /// <summary>PokeAPI species key.</summary>
    public required string Key { get; set; }

    /// <summary>The stage it evolves from; by default the species it evolves from in the games.</summary>
    public string? From { get; set; }

    /// <summary>Run level at which it evolves.</summary>
    public int? Level { get; set; }

    /// <summary>Item (a stone, Metalcoperta, …) that makes it evolve, dropped during the run (phase 4.2).</summary>
    public string? Item { get; set; }

    /// <summary>Replaces the line's ability from this stage on (Gyarados' Prepotenza).</summary>
    public AbilitySpec? Ability { get; set; }
}

public sealed record UnlockSpec
{
    public required UnlockKind Kind { get; set; }

    /// <summary>Coins: the shop price, or the cost of reviving a fossil.</summary>
    public int Price { get; set; }

    /// <summary>How to get it, for the shop and the collection.</summary>
    public string HintIt { get; set; } = "";
}

/// <summary>One character as written in content/data/characters.json.</summary>
public sealed record CharacterSpec
{
    public required string Id { get; set; }

    public CharacterGroup Group { get; set; } = CharacterGroup.Other;

    public required IReadOnlyList<StageSpec> Species { get; set; }

    public required AbilitySpec Ability { get; set; }

    public required string MainMove { get; set; }

    public required string SecondaryMove { get; set; }

    /// <summary>Type of its special move (phase 4.4).</summary>
    public required PokemonType Special { get; set; }

    /// <summary>Always-on bonuses, like one level of an item; negative values are drawbacks.</summary>
    public ItemBonus Modifiers { get; set; } = ItemBonus.None;

    public required UnlockSpec Unlock { get; set; }
}

/// <summary>An item that makes a stage evolve (a stone, Metalcoperta, …).</summary>
public sealed record EvolutionItemSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }
}

public sealed record CharacterFile(int Version, IReadOnlyList<CharacterSpec> Characters, IReadOnlyList<EvolutionItemSpec>? EvolutionItems = null);

/// <summary>
/// One species of a line, resolved against the Pokédex. <see cref="FromIndex"/> is null for the first stage;
/// <see cref="Depth"/> counts the evolutions from it (Charmeleon 1, Charizard 2).
/// </summary>
public sealed record CharacterStage(int Index, SpeciesData Species, int? FromIndex, int? Level, string? Item, AbilitySpec Ability)
{
    public int Depth { get; init; }

    /// <summary>Italian name of <see cref="Item"/>.</summary>
    public string? ItemNameIt { get; init; }
}

/// <summary>A playable evolution line (phase 4.1).</summary>
public sealed record CharacterDefinition(
    string Id,
    CharacterGroup Group,
    IReadOnlyList<CharacterStage> Stages,
    MoveDefinition MainMove,
    MoveDefinition SecondaryMove,
    PokemonType Special,
    ItemBonus Modifiers,
    UnlockSpec Unlock)
{
    /// <summary>The species a run starts as; the character is named after it.</summary>
    public SpeciesData First => Stages[0].Species;

    public string NameIt => First.NameIt;

    public AbilitySpec Ability => Stages[0].Ability;

    /// <summary>The stages that evolve straight from <paramref name="stage"/>: more than one is a choice.</summary>
    public IEnumerable<CharacterStage> EvolutionsOf(CharacterStage stage) => Stages.Where(s => s.FromIndex == stage.Index);
}

/// <summary>Every playable character, from content/data/characters.json (phase 4.1).</summary>
public sealed class CharacterLibrary
{
    public const string FileName = "characters.json";

    /// <summary>The character of a run when none is chosen.</summary>
    public const string DefaultId = "pikachu";

    private readonly Dictionary<string, CharacterDefinition> _byId;

    private CharacterLibrary(IReadOnlyList<CharacterDefinition> all)
    {
        All = all;
        _byId = all.ToDictionary(c => c.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<CharacterDefinition> All { get; }

    public CharacterDefinition this[string id] =>
        _byId.TryGetValue(id, out var character) ? character : throw new KeyNotFoundException($"Character '{id}' is not in {FileName}.");

    public CharacterDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The character whose line holds <paramref name="speciesId"/>, if any.</summary>
    public CharacterDefinition? WithSpecies(int speciesId) => All.FirstOrDefault(c => c.Stages.Any(s => s.Species.Id == speciesId));

    public static CharacterLibrary Load(string contentRoot, IReadOnlyList<SpeciesData> pokedex, MoveLibrary moves)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.CharacterFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, pokedex, moves);
    }

    public static CharacterLibrary From(CharacterFile file, IReadOnlyList<SpeciesData> pokedex, MoveLibrary moves)
    {
        var byKey = pokedex.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var items = (file.EvolutionItems ?? []).ToDictionary(i => i.Id, i => i.NameIt, StringComparer.Ordinal);
        var characters = file.Characters.Select(spec => Resolve(spec, byKey, items, moves)).ToList();

        var duplicate = characters.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.Key}' appears twice.");

        var shared = characters.SelectMany(c => c.Stages, (c, s) => (c.Id, s.Species))
            .GroupBy(p => p.Species.Id).FirstOrDefault(g => g.Count() > 1);
        if (shared is not null)
            throw new InvalidDataException($"{FileName}: {shared.First().Species.Key} is in two lines ({string.Join(", ", shared.Select(p => p.Id))}).");

        if (!characters.Any(c => c.Unlock.Kind == UnlockKind.Start))
            throw new InvalidDataException($"{FileName}: at least one character must be unlocked from the start.");

        return new CharacterLibrary(characters);
    }

    private static CharacterDefinition Resolve(CharacterSpec spec, Dictionary<string, SpeciesData> byKey, Dictionary<string, string> items, MoveLibrary moves)
    {
        string Where() => $"{FileName}, '{spec.Id}'";

        if (spec.Species.Count == 0)
            throw new InvalidDataException($"{Where()}: the line has no species.");

        var stages = new List<CharacterStage>();
        foreach (var stageSpec in spec.Species)
        {
            if (!byKey.TryGetValue(stageSpec.Key, out var species))
                throw new InvalidDataException($"{Where()}: '{stageSpec.Key}' is not a Pokémon key.");

            var ability = stageSpec.Ability;
            if (stages.Count == 0)
            {
                if (stageSpec.From is not null || stageSpec.Level is not null || stageSpec.Item is not null)
                    throw new InvalidDataException($"{Where()}: the first species, {species.Key}, is where the run starts and cannot evolve from anything.");

                stages.Add(new CharacterStage(0, species, null, null, null, ability ?? spec.Ability));
                continue;
            }

            var fromKey = stageSpec.From ?? stages.FirstOrDefault(s => s.Species.Id == species.EvolvesFromSpeciesId)?.Species.Key;
            var from = stages.FirstOrDefault(s => s.Species.Key == fromKey)
                ?? throw new InvalidDataException($"{Where()}: {species.Key} must come after the species it evolves from.");
            if (species.EvolvesFromSpeciesId != from.Species.Id)
                throw new InvalidDataException($"{Where()}: {species.Key} does not evolve from {from.Species.Key}.");
            if ((stageSpec.Level is null) == string.IsNullOrEmpty(stageSpec.Item))
                throw new InvalidDataException($"{Where()}: {species.Key} evolves either at a \"level\" or with an \"item\", one of the two.");
            if (stageSpec.Level is { } level && (level < 2 || level <= (from.Level ?? 1)))
                throw new InvalidDataException($"{Where()}: {species.Key} evolves at level {level}, which must come after {from.Species.Key}'s.");
            string? itemName = null;
            if (stageSpec.Item is { } item && !items.TryGetValue(item, out itemName))
                throw new InvalidDataException($"{Where()}: {species.Key} evolves with '{item}', which is not in \"evolutionItems\".");

            stages.Add(new CharacterStage(stages.Count, species, from.Index, stageSpec.Level, stageSpec.Item, ability ?? from.Ability)
            {
                Depth = from.Depth + 1,
                ItemNameIt = itemName,
            });
        }

        var main = BaseMove(spec.MainMove, "mainMove");
        var secondary = BaseMove(spec.SecondaryMove, "secondaryMove");
        if (main.Id == secondary.Id)
            throw new InvalidDataException($"{Where()}: the main and the secondary move are both '{main.Id}'.");

        var needsPrice = spec.Unlock.Kind is UnlockKind.Shop or UnlockKind.Fossil;
        if (needsPrice != spec.Unlock.Price > 0)
            throw new InvalidDataException($"{Where()}: a {spec.Unlock.Kind} unlock {(needsPrice ? "needs" : "takes no")} \"price\".");

        return new CharacterDefinition(spec.Id, spec.Group, stages, main, secondary, spec.Special, spec.Modifiers, spec.Unlock);

        MoveDefinition BaseMove(string id, string field)
        {
            var move = moves.All.FirstOrDefault(m => m.Id == id)
                ?? throw new InvalidDataException($"{Where()}: \"{field}\" '{id}' is not in {MoveLibrary.FileName}.");
            if (move.IsEvolution)
                throw new InvalidDataException($"{Where()}: \"{field}\" '{id}' is an evolved move; characters start with base moves.");
            return move;
        }
    }
}
