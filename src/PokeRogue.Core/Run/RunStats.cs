using PokeRogue.Core.Run.Pickups;

namespace PokeRogue.Core.Run;

public enum RunOutcome
{
    None,

    /// <summary>Survived until the time limit (prototype) or beat the level boss (phase 3.4).</summary>
    Victory,

    /// <summary>The player's Pokémon fainted.</summary>
    Defeat,
}

/// <summary>Totals shown on the results screen and fed to the missions and the Pokédex (Block 6).</summary>
public sealed class RunStats
{
    private readonly Dictionary<int, int> _killsBySpecies = [];
    private readonly HashSet<int> _seen = [];
    private readonly Dictionary<LootKind, int> _loot = [];

    public int Kills { get; private set; }

    public IReadOnlyDictionary<int, int> KillsBySpecies => _killsBySpecies;

    /// <summary>Species met this run (every enemy that came on the field, bosses and trainers' Pokémon too), for the Pokédex.</summary>
    public IReadOnlyCollection<int> Seen => _seen;

    /// <summary>Things picked up from the ground, by kind.</summary>
    public IReadOnlyDictionary<LootKind, int> Loot => _loot;

    public float DamageTaken { get; private set; }

    public int CoinsCollected { get; private set; }

    public int ShiniesDefeated { get; private set; }

    public int DominantsDefeated { get; private set; }

    public void RecordKill(int speciesId)
    {
        Kills++;
        _killsBySpecies[speciesId] = _killsBySpecies.GetValueOrDefault(speciesId) + 1;
    }

    public void RecordSeen(int speciesId) => _seen.Add(speciesId);

    public void RecordShiny() => ShiniesDefeated++;

    public void RecordDominant() => DominantsDefeated++;

    public void RecordLoot(LootKind kind) => _loot[kind] = _loot.GetValueOrDefault(kind) + 1;

    /// <summary>Potions and berries picked up: what heals.</summary>
    public int HealingPickups => _loot.GetValueOrDefault(LootKind.Potion) + _loot.GetValueOrDefault(LootKind.OranBerry);

    public void RecordDamageTaken(float amount) => DamageTaken += amount;

    public int ChestsOpened { get; private set; }

    public void RecordCoins(int amount) => CoinsCollected += amount;

    public void RecordChest() => ChestsOpened++;

    /// <summary>Most defeated species first.</summary>
    public IReadOnlyList<(int SpeciesId, int Count)> TopKills(int count) =>
        _killsBySpecies.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(count).Select(kv => (kv.Key, kv.Value)).ToList();
}
