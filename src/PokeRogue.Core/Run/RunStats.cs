namespace PokeRogue.Core.Run;

public enum RunOutcome
{
    None,

    /// <summary>Survived until the time limit (prototype) or beat the level boss (phase 3.4).</summary>
    Victory,

    /// <summary>The player's Pokémon fainted.</summary>
    Defeat,
}

/// <summary>Totals shown on the results screen and later fed to missions and the Pokédex.</summary>
public sealed class RunStats
{
    private readonly Dictionary<int, int> _killsBySpecies = [];

    public int Kills { get; private set; }

    public IReadOnlyDictionary<int, int> KillsBySpecies => _killsBySpecies;

    public float DamageTaken { get; private set; }

    public int CoinsCollected { get; private set; }

    public void RecordKill(int speciesId)
    {
        Kills++;
        _killsBySpecies[speciesId] = _killsBySpecies.GetValueOrDefault(speciesId) + 1;
    }

    public void RecordDamageTaken(float amount) => DamageTaken += amount;

    public void RecordCoins(int amount) => CoinsCollected += amount;

    /// <summary>Most defeated species first.</summary>
    public IReadOnlyList<(int SpeciesId, int Count)> TopKills(int count) =>
        _killsBySpecies.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(count).Select(kv => (kv.Key, kv.Value)).ToList();
}
