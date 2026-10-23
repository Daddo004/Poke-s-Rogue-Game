using System.Text.Json;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Moves;

/// <summary>One move as written in content/data/moves.json: a base level plus the changes of each later level.</summary>
public sealed record MoveSpec
{
    public required string Id { get; set; }

    /// <summary>PokeAPI move identifier for name, type and category; defaults to <see cref="Id"/>.</summary>
    public string? CatalogKey { get; set; }

    public string? NameIt { get; set; }

    public PokemonType? Type { get; set; }

    /// <summary>Overrides the catalogue (status moves turned into damaging ones, e.g. Smog stays special).</summary>
    public MoveCategory? Category { get; set; }

    public required MoveArchetype Archetype { get; set; }

    public string DescriptionIt { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public string? Vfx { get; set; }

    public StatusKind Status { get; set; }

    public float StatusChance { get; set; }

    /// <summary>Added to the chance of a critical hit (Colpokarate).</summary>
    public float CritBonus { get; set; }

    /// <summary>Share of the damage dealt that heals the player (Sanguisuga).</summary>
    public float Drain { get; set; }

    /// <summary>Evolutions (phase 2.6): the move this one evolves from, once that move reaches its maximum level.</summary>
    public string? EvolvesFrom { get; set; }

    /// <summary>Evolutions: the held item (any level) the player needs for the evolution to be offered.</summary>
    public string? EvolveItem { get; set; }

    /// <summary>Orbits: once cast they never expire (Verdebufera).</summary>
    public bool Permanent { get; set; }

    /// <summary>Chance per hit to knock a regular enemy out at once (Abisso).</summary>
    public float OhkoChance { get; set; }

    public bool SpawnAtPlayer { get; set; }

    public MoveLevel Base { get; set; } = new();

    /// <summary>Levels 2 and up, each relative to the previous one.</summary>
    public IReadOnlyList<MoveLevelDelta> Upgrades { get; set; } = [];
}

public sealed record MoveFile(int Version, IReadOnlyList<MoveSpec> Moves);

/// <summary>All moves of the game, built from content/data/moves.json and the generated PokeAPI move catalogue.</summary>
public sealed class MoveLibrary
{
    public const string FileName = "moves.json";
    public const int MaxLevels = 8;

    private readonly Dictionary<string, MoveDefinition> _byId;

    private MoveLibrary(IReadOnlyList<MoveDefinition> all)
    {
        All = all;
        _byId = all.ToDictionary(m => m.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<MoveDefinition> All { get; }

    /// <summary>Moves that level-ups may offer as new moves (their animation is approved). Evolutions are never offered this way.</summary>
    public IReadOnlyList<MoveDefinition> Enabled => All.Where(m => m.Enabled && !m.IsEvolution).ToList();

    /// <summary>Approved evolutions, offered when their base move is maxed and their item is held.</summary>
    public IReadOnlyList<MoveDefinition> Evolutions => All.Where(m => m.Enabled && m.IsEvolution).ToList();

    public MoveDefinition this[string id] =>
        _byId.TryGetValue(id, out var move) ? move : throw new KeyNotFoundException($"Move '{id}' is not in {FileName}.");

    public static MoveLibrary Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.MoveFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, GameDataFiles.LoadMoveCatalog(contentRoot));
    }

    public static MoveLibrary From(MoveFile file, MoveCatalogFile catalog)
    {
        var byKey = catalog.Moves.ToDictionary(m => m.Key, StringComparer.Ordinal);
        var moves = new List<MoveDefinition>();

        foreach (var spec in file.Moves)
        {
            var key = spec.CatalogKey ?? spec.Id;
            if (!byKey.TryGetValue(key, out var entry) && (spec.NameIt is null || spec.Type is null || spec.Category is null))
                throw new InvalidDataException($"Move '{spec.Id}': '{key}' is not a Gen 1–4 move and name/type/category are not all given.");
            if (spec.Upgrades.Count > MaxLevels - 1)
                throw new InvalidDataException($"Move '{spec.Id}' has {spec.Upgrades.Count} upgrades; at most {MaxLevels - 1}.");
            if (spec.Base.Power <= 0)
                throw new InvalidDataException($"Move '{spec.Id}' needs a positive base power.");

            var levels = new List<MoveLevel> { spec.Base };
            foreach (var upgrade in spec.Upgrades)
                levels.Add(levels[^1].Apply(upgrade));

            moves.Add(new MoveDefinition(
                spec.Id,
                spec.NameIt ?? entry!.NameIt,
                spec.Type ?? entry!.Type,
                spec.Category ?? entry!.Category,
                spec.Archetype,
                levels)
            {
                DescriptionIt = spec.DescriptionIt,
                Enabled = spec.Enabled,
                Vfx = spec.Vfx,
                Status = spec.Status,
                StatusChance = spec.StatusChance,
                CritBonus = spec.CritBonus,
                Drain = spec.Drain,
                EvolvesFrom = spec.EvolvesFrom,
                EvolveItem = spec.EvolveItem,
                Permanent = spec.Permanent,
                OhkoChance = spec.OhkoChance,
                SpawnAtPlayer = spec.SpawnAtPlayer,
            });
        }

        var duplicate = moves.GroupBy(m => m.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Move '{duplicate.Key}' appears twice in {FileName}.");

        ValidateEvolutions(moves);
        return new MoveLibrary(moves);
    }

    /// <summary>Each evolution names an existing base move and an item, has a single level, and is the only one of its base.</summary>
    private static void ValidateEvolutions(List<MoveDefinition> moves)
    {
        var byId = moves.ToDictionary(m => m.Id, StringComparer.Ordinal);
        foreach (var evolution in moves.Where(m => m.IsEvolution))
        {
            if (!byId.TryGetValue(evolution.EvolvesFrom!, out var from) || from.IsEvolution)
                throw new InvalidDataException($"Move '{evolution.Id}' evolves from '{evolution.EvolvesFrom}', which is not a base move.");
            if (string.IsNullOrEmpty(evolution.EvolveItem))
                throw new InvalidDataException($"Evolution '{evolution.Id}' needs an \"evolveItem\".");
            if (evolution.MaxLevel != 1)
                throw new InvalidDataException($"Evolution '{evolution.Id}' must have a single level (no upgrades).");
        }

        var twice = moves.Where(m => m.IsEvolution).GroupBy(m => m.EvolvesFrom).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null)
            throw new InvalidDataException($"'{twice.Key}' has more than one evolution.");
    }
}
