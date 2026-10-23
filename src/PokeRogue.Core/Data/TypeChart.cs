namespace PokeRogue.Core.Data;

public sealed record TypeInfo(PokemonType Type, string NameIt, string NameEn);

/// <summary>Serialized form: only the matchups that differ from 1x.</summary>
public sealed record TypeMatchup(PokemonType Attack, PokemonType Defense, float Factor);

public sealed record TypeChartFile(int Version, string Source, IReadOnlyList<TypeInfo> Types, IReadOnlyList<TypeMatchup> Matchups);

/// <summary>Raw Generation 4 type effectiveness (0, 0.5, 1, 2). Gameplay softening of immunities lives in the combat layer.</summary>
public sealed class TypeChart
{
    private readonly float[,] _factors = new float[PokemonTypes.Count, PokemonTypes.Count];

    public TypeChart(IEnumerable<TypeMatchup> matchups)
    {
        for (var a = 0; a < PokemonTypes.Count; a++)
            for (var d = 0; d < PokemonTypes.Count; d++)
                _factors[a, d] = 1f;

        foreach (var m in matchups)
            _factors[(int)m.Attack, (int)m.Defense] = m.Factor;
    }

    public float Factor(PokemonType attack, PokemonType defense) => _factors[(int)attack, (int)defense];

    public float Factor(PokemonType attack, IReadOnlyList<PokemonType> defenderTypes)
    {
        var result = 1f;
        foreach (var t in defenderTypes)
            result *= Factor(attack, t);
        return result;
    }
}
