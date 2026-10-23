using System.Text.Json.Serialization;

namespace PokeRogue.Core.Data;

public sealed record BaseStats(int Hp, int Attack, int Defense, int SpAttack, int SpDefense, int Speed)
{
    [JsonIgnore]
    public int Total => Hp + Attack + Defense + SpAttack + SpDefense + Speed;
}

public enum EvolutionTrigger
{
    LevelUp,
    UseItem,
    Trade,
    Other,
}

/// <summary>One way a species can evolve, as recorded by PokeAPI. Game rules decide how each condition maps to a run.</summary>
public sealed record EvolutionData(
    int ToSpeciesId,
    EvolutionTrigger Trigger,
    int? MinLevel = null,
    string? ItemKey = null,
    string? ItemNameIt = null,
    string? HeldItemKey = null,
    string? TimeOfDay = null,
    int? MinHappiness = null,
    string? KnownMoveKey = null,
    PokemonType? KnownMoveType = null,
    string? LocationKey = null,
    int? GenderId = null,
    int? RelativePhysicalStats = null);

public sealed record AbilityRef(string Key, string NameIt, bool IsHidden);

public sealed record SpeciesData(
    int Id,
    string Key,
    string NameIt,
    string NameEn,
    string GenusIt,
    int Generation,
    IReadOnlyList<PokemonType> Types,
    BaseStats BaseStats,
    int HeightDm,
    int WeightHg,
    bool IsBaby,
    bool IsLegendary,
    bool IsMythical,
    int CaptureRate,
    int? EvolvesFromSpeciesId,
    int EvolutionChainId,
    IReadOnlyList<EvolutionData> Evolutions,
    IReadOnlyList<AbilityRef> Abilities);

public sealed record PokedexFile(int Version, string Source, IReadOnlyList<SpeciesData> Species);
