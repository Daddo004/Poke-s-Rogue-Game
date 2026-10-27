using PokeRogue.Core.Run;

namespace PokeRogue.Core.Meta;

/// <summary>
/// What a run did, for the missions, the Pokédex and the records (Block 6). Built from a run in progress (the missions'
/// notices during the run) or a finished one (what the save keeps).
/// </summary>
public sealed record RunRecord
{
    public required string LevelId { get; init; }

    public Difficulty Difficulty { get; init; }

    public string? CharacterId { get; init; }

    public string? TrainerId { get; init; }

    public bool Over { get; init; }

    public bool Won { get; init; }

    /// <summary>The trainer beaten in this run's challenge (phase 5.3).</summary>
    public string? ChallengeWon { get; init; }

    public int Ticks { get; init; }

    public int Minutes => Ticks / (RunClock.TicksPerSecond * 60);

    public int Kills { get; init; }

    public IReadOnlyDictionary<int, int> KillsBySpecies { get; init; } = new Dictionary<int, int>();

    public IReadOnlyCollection<int> Seen { get; init; } = [];

    public int Shinies { get; init; }

    public int Dominants { get; init; }

    public int Chests { get; init; }

    /// <summary>Coins that go to the bank: collected, minus those spent at Kecleon's (phase 9.2).</summary>
    public int Coins { get; init; }

    /// <summary>Coins collected, spent or not (the totals and the missions); records without it count <see cref="Coins"/>.</summary>
    public int CoinsEarned { get; init; }

    public int Earned => Math.Max(CoinsEarned, Coins);

    public int HealingPickups { get; init; }

    public int Level { get; init; }

    public int MovesEvolved { get; init; }

    /// <summary>Evolutions down the character's line reached (0 = still the first stage).</summary>
    public int StageDepth { get; init; }

    public int Specials { get; init; }

    public int TrainerUses { get; init; }

    public IReadOnlyList<string> Moves { get; init; } = [];

    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>Fossils and eggs picked up (phase 7.4), by the character they bring.</summary>
    public IReadOnlyList<string> Finds { get; init; } = [];

    /// <summary>Legendaries beaten at Ultra (phase 7.3), by species id.</summary>
    public IReadOnlyList<int> LegendariesDefeated { get; init; } = [];

    /// <summary>Plates picked up from the map (phase 9.1).</summary>
    public IReadOnlyList<string> Plates { get; init; } = [];

    /// <param name="difficulty">The run's own (<see cref="RunState.Difficulty"/>) when null.</param>
    public static RunRecord From(RunState run, string levelId, Difficulty? difficulty = null) => new()
    {
        LevelId = levelId,
        Difficulty = difficulty ?? run.Difficulty,
        Finds = run.Stats.Finds.ToList(),
        LegendariesDefeated = run.Stats.LegendariesDefeated.ToList(),
        CharacterId = run.Character?.Id,
        TrainerId = run.Trainer?.Id,
        Over = run.IsOver,
        Won = run.Outcome == RunOutcome.Victory,
        ChallengeWon = run.Challenge is { Won: true } challenge ? challenge.Trainer.Id : null,
        Ticks = run.Ticks,
        Kills = run.Stats.Kills,
        KillsBySpecies = new Dictionary<int, int>(run.Stats.KillsBySpecies),
        Seen = run.Stats.Seen.Append(run.Player.Species.Id).Distinct().ToList(),
        Shinies = run.Stats.ShiniesDefeated,
        Dominants = run.Stats.DominantsDefeated,
        Chests = run.Stats.ChestsOpened,
        Coins = run.Stats.CoinsBanked,
        CoinsEarned = run.Stats.CoinsCollected,
        Plates = run.Stats.PlatesFound.ToList(),
        HealingPickups = run.Stats.HealingPickups,
        Level = run.Player.Level,
        MovesEvolved = run.Player.Moves.Count(m => m.Definition.IsEvolution),
        StageDepth = run.Stage?.Depth ?? 0,
        Specials = run.Special.Uses,
        TrainerUses = run.TrainerState.Uses,
        Moves = run.Player.Moves.SelectMany(m => m.Definition.EvolvesFrom is { } from ? new[] { from, m.Definition.Id } : [m.Definition.Id]).Distinct().ToList(),
        Items = run.Player.Items.Select(i => i.Definition.Id).ToList(),
    };
}
