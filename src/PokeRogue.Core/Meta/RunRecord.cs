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

    public int Coins { get; init; }

    public int HealingPickups { get; init; }

    public int Level { get; init; }

    public int MovesEvolved { get; init; }

    /// <summary>Evolutions down the character's line reached (0 = still the first stage).</summary>
    public int StageDepth { get; init; }

    public int Specials { get; init; }

    public int TrainerUses { get; init; }

    public IReadOnlyList<string> Moves { get; init; } = [];

    public IReadOnlyList<string> Items { get; init; } = [];

    public static RunRecord From(RunState run, string levelId, Difficulty difficulty = Difficulty.Normal) => new()
    {
        LevelId = levelId,
        Difficulty = difficulty,
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
        Coins = run.Stats.CoinsCollected,
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
