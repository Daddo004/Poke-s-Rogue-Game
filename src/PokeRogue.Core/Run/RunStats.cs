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

    /// <summary>Coins spent at Kecleon's (phase 9.2): they do not go to the bank.</summary>
    public int CoinsSpent { get; private set; }

    public void RecordSpent(int amount) => CoinsSpent += amount;

    /// <summary>The coins that go to the bank at the end: collected, minus spent.</summary>
    public int CoinsBanked => Math.Max(0, CoinsCollected - CoinsSpent);

    private readonly List<string> _plates = [];

    /// <summary>Plates picked up from the map (phase 9.1): the meta-progression unlocks them.</summary>
    public IReadOnlyList<string> PlatesFound => _plates;

    public void RecordPlate(string plateId) => _plates.Add(plateId);

    public void RecordChest() => ChestsOpened++;

    private readonly List<string> _finds = [];
    private readonly List<int> _legendaries = [];

    /// <summary>Fossils and eggs picked up (phase 7.4), by the character they bring.</summary>
    public IReadOnlyList<string> Finds => _finds;

    /// <summary>Legendaries beaten at Ultra (phase 7.3), by species id: the meta-progression unlocks them.</summary>
    public IReadOnlyList<int> LegendariesDefeated => _legendaries;

    public void RecordFind(string characterId) => _finds.Add(characterId);

    public void RecordLegendary(int speciesId) => _legendaries.Add(speciesId);

    /// <summary>Most defeated species first.</summary>
    public IReadOnlyList<(int SpeciesId, int Count)> TopKills(int count) =>
        _killsBySpecies.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(count).Select(kv => (kv.Key, kv.Value)).ToList();
}
