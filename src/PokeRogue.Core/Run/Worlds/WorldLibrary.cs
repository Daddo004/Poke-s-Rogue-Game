using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Run.Worlds;

/// <summary>A level's place in its world, as written in worlds.json: its file, and the trainers challenged there at Ultra.</summary>
public sealed record LevelEntrySpec
{
    /// <summary>Its waves file, content/data/waves/&lt;id&gt;.json.</summary>
    public required string Id { get; set; }

    /// <summary>Trainers whose challenge (phase 5.3) is fought here, at Ultra.</summary>
    public IReadOnlyList<string> Trainers { get; set; } = [];
}

/// <summary>A world as written in worlds.json: a region of the games and its levels in order.</summary>
public sealed record WorldSpec
{
    public required string Id { get; set; }

    public required TrainerRegion Region { get; set; }

    public required string NameIt { get; set; }

    public IReadOnlyList<LevelEntrySpec> Levels { get; set; } = [];
}

/// <summary>content/data/worlds.json (Block 7).</summary>
public sealed record WorldsFile
{
    public int Version { get; set; } = 1;

    public IReadOnlyDictionary<Difficulty, DifficultySpec> Difficulties { get; set; } = new Dictionary<Difficulty, DifficultySpec>();

    /// <summary>An ordinary level's pace, one entry per minute.</summary>
    public IReadOnlyList<PacingSpec> Pacing { get; set; } = [];

    /// <summary>Frailer enemies in an ordinary level's first minutes.</summary>
    public WarmUpSpec? WarmUp { get; set; }

    /// <summary>How a level's boss fights when its file gives no phases.</summary>
    public BossSpec? Boss { get; set; }

    /// <summary>How the legendaries of Ultra fight.</summary>
    public BossSpec? Legendary { get; set; }

    /// <summary>Type → the move whose look a boss' volley borrows.</summary>
    public IReadOnlyDictionary<PokemonType, string> Shots { get; set; } = new Dictionary<PokemonType, string>();

    /// <summary>The things lying on a level's map when its file lists none.</summary>
    public IReadOnlyList<MapItemSpec> MapItems { get; set; } = [];

    /// <summary>An ordinary level's special events; "pokemon" names a part the level's "cast" gives a species.</summary>
    public IReadOnlyList<WaveEventSpec> Events { get; set; } = [];

    /// <summary>The endless mode and its Reaper (phase 9.3).</summary>
    public EndlessSpec? Endless { get; set; }

    public IReadOnlyList<WorldSpec> Worlds { get; set; } = [];
}

/// <summary>What worlds.json gives every level file that leaves it out.</summary>
public sealed record LevelDefaults(
    IReadOnlyList<PacingSpec> Pacing,
    BossSpec? Boss,
    BossSpec? Legendary,
    IReadOnlyDictionary<PokemonType, string> Shots,
    IReadOnlyList<MapItemSpec> MapItems,
    IReadOnlyList<WaveEventSpec>? Events = null,
    WarmUpSpec? WarmUp = null);

public sealed record WorldInfo(string Id, TrainerRegion Region, string NameIt, int Index)
{
    public IReadOnlyList<LevelInfo> Levels { get; internal set; } = [];
}

/// <summary>
/// A level for the menus (phase 7.1): its place, its file as written (name, description, map, weather, boss, legendaries)
/// and the trainers challenged there.
/// </summary>
public sealed record LevelInfo(string Id, WorldInfo World, int Index, int Order, IReadOnlyList<string> Trainers, WaveFile File)
{
    public string NameIt => File.NameIt;

    public string DescriptionIt => File.DescriptionIt;
}

/// <summary>
/// The worlds of Block 7: the four regions, their levels in the order they unlock, the three difficulties, and the
/// defaults level files share (the pace of the waves, the bosses' and legendaries' way of fighting, the map items).
/// </summary>
public sealed class WorldLibrary
{
    public const string FileName = "worlds.json";

    private readonly Dictionary<string, LevelInfo> _byId;
    private readonly IReadOnlyDictionary<Difficulty, DifficultySpec> _difficulties;

    private WorldLibrary(IReadOnlyList<WorldInfo> worlds, LevelDefaults defaults, IReadOnlyDictionary<Difficulty, DifficultySpec> difficulties, EndlessSpec? endless)
    {
        EndlessSpec = endless;
        Worlds = worlds;
        Levels = worlds.SelectMany(w => w.Levels).ToList();
        _byId = Levels.ToDictionary(l => l.Id, StringComparer.Ordinal);
        Defaults = defaults;
        _difficulties = difficulties;
    }

    public IReadOnlyList<WorldInfo> Worlds { get; }

    /// <summary>Every level, world after world, in the order they unlock.</summary>
    public IReadOnlyList<LevelInfo> Levels { get; }

    public LevelDefaults Defaults { get; }

    /// <summary>The first level: open from the start.</summary>
    public LevelInfo First => Levels[0];

    public LevelInfo? Find(string id) => _byId.GetValueOrDefault(id);

    public LevelInfo this[string id] => _byId.TryGetValue(id, out var level) ? level : throw new KeyNotFoundException($"Level '{id}' is not in {FileName}.");

    /// <summary>The level before <paramref name="level"/> in the unlock order; null for the first.</summary>
    public LevelInfo? Previous(LevelInfo level) => level.Order == 0 ? null : Levels[level.Order - 1];

    /// <summary>Where a trainer's challenge is fought; null for a trainer without one.</summary>
    public LevelInfo? LevelOfTrainer(string trainerId) => Levels.FirstOrDefault(l => l.Trainers.Contains(trainerId));

    /// <summary>The endless mode as written; null when the file has none.</summary>
    public EndlessSpec? EndlessSpec { get; }

    /// <summary>The endless mode with its Reaper ready to fight (phase 9.3); null when the file has none.</summary>
    public EndlessRules? Endless(IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary moves) =>
        EndlessSpec is { } spec ? EndlessRules.From(spec, pokedex, moves) : null;

    public DifficultySpec Difficulty(Difficulty difficulty) => _difficulties.GetValueOrDefault(difficulty) ?? DifficultySpec.Normal;

    /// <summary>A level built for a difficulty, ready to be played.</summary>
    public WaveTimeline Timeline(string levelId, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary? moves, Difficulty difficulty = Meta.Difficulty.Normal) =>
        WaveTimeline.From(this[levelId].File, pokedex, moves, Defaults, Difficulty(difficulty));

    /// <summary>Reads worlds.json and the waves file of each level.</summary>
    public static WorldLibrary Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.WorldsFile) ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, id => WaveTimeline.ReadFile(contentRoot, id));
    }

    public static WorldLibrary From(WorldsFile file, Func<string, WaveFile> readLevel)
    {
        if (Enum.GetValues<Difficulty>().Any(d => !file.Difficulties.ContainsKey(d)))
            throw new InvalidDataException($"{FileName}: \"difficulties\" needs Normal, Hard and Ultra.");
        if (file.Worlds.Count == 0 || file.Worlds.Any(w => w.Levels.Count == 0))
            throw new InvalidDataException($"{FileName}: every world needs at least one level.");

        var worlds = new List<WorldInfo>();
        var order = 0;
        foreach (var spec in file.Worlds)
        {
            var world = new WorldInfo(spec.Id, spec.Region, spec.NameIt, worlds.Count);
            var levels = new List<LevelInfo>();
            foreach (var entry in spec.Levels)
            {
                var level = readLevel(entry.Id);
                if (level.Id != entry.Id)
                    throw new InvalidDataException($"{FileName}: the file of '{entry.Id}' calls itself '{level.Id}'.");
                levels.Add(new LevelInfo(entry.Id, world, levels.Count, order++, entry.Trainers, level));
            }

            world.Levels = levels;
            worlds.Add(world);
        }

        var all = worlds.SelectMany(w => w.Levels).ToList();
        if (all.GroupBy(l => l.Id).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new InvalidDataException($"{FileName}: the level '{twice.Key}' appears twice.");
        if (all.SelectMany(l => l.Trainers).GroupBy(t => t).FirstOrDefault(g => g.Count() > 1) is { } trainer)
            throw new InvalidDataException($"{FileName}: '{trainer.Key}' is challenged on more than one level.");

        var defaults = new LevelDefaults(file.Pacing, file.Boss, file.Legendary, file.Shots, file.MapItems, file.Events, file.WarmUp);
        return new WorldLibrary(worlds, defaults, file.Difficulties, file.Endless);
    }
}
