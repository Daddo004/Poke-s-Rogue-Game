namespace PokeRogue.Core.Data;

public enum MoveCategory
{
    Physical,
    Special,
    Status,
}

/// <summary>Reference entry for a Generation 1–4 move, used when authoring the game's own move definitions.</summary>
public sealed record MoveCatalogEntry(
    int Id,
    string Key,
    string NameIt,
    string NameEn,
    int Generation,
    PokemonType Type,
    MoveCategory Category,
    int? Power,
    int? Accuracy);

public sealed record MoveCatalogFile(int Version, string Source, IReadOnlyList<MoveCatalogEntry> Moves);
