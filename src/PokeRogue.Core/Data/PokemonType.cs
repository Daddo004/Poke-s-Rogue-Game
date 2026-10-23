namespace PokeRogue.Core.Data;

/// <summary>The 17 types of Generations 1–4. Numeric values match PokeAPI type ids minus one.</summary>
public enum PokemonType
{
    Normal,
    Fighting,
    Flying,
    Poison,
    Ground,
    Rock,
    Bug,
    Ghost,
    Steel,
    Fire,
    Water,
    Grass,
    Electric,
    Psychic,
    Ice,
    Dragon,
    Dark,
}

public static class PokemonTypes
{
    public const int Count = 17;

    public static IEnumerable<PokemonType> All => Enum.GetValues<PokemonType>();

    /// <summary>Maps a PokeAPI type id (1-based) to <see cref="PokemonType"/>; null for Fairy and non-standard types.</summary>
    public static PokemonType? FromPokeApiId(int id) =>
        id is >= 1 and <= Count ? (PokemonType)(id - 1) : null;
}
