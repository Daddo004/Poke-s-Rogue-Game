using System.Text.Json;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Statuses;

/// <summary>One status condition as written in content/data/statuses.json; fields a status does not use keep their neutral default.</summary>
public sealed record StatusSpec
{
    public required StatusKind Kind { get; set; }

    public string NameIt { get; set; } = "";

    /// <summary>False keeps the status out of real runs (moves do not inflict it) until its look is approved; previews still show it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds.</summary>
    public float Duration { get; set; } = 3f;

    /// <summary>Seconds between damage ticks or rolls.</summary>
    public float TickInterval { get; set; } = 1f;

    /// <summary>Burn: damage per tick as a share of the hit that inflicted the status.</summary>
    public float HitDamageShare { get; set; }

    /// <summary>Poison and confusion: damage per tick (or per self-hit) as a share of the enemy's max HP.</summary>
    public float MaxHpShare { get; set; }

    /// <summary>Movement speed multiplier.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Contact damage multiplier.</summary>
    public float ContactDamage { get; set; } = 1f;

    /// <summary>Chance per tick: paralysis locks up, confusion hurts itself.</summary>
    public float Chance { get; set; }

    /// <summary>Paralysis: seconds a lock-up lasts.</summary>
    public float BlockDuration { get; set; }

    /// <summary>Confusion: seconds between changes of direction.</summary>
    public float WanderTurn { get; set; } = 1f;

    /// <summary>A hit of this type ends the status at once (Fire thaws a frozen Pokémon).</summary>
    public PokemonType? CuredBy { get; set; }

    public IReadOnlyList<PokemonType> ImmuneTypes { get; set; } = [];

    public int DurationTicks => RunClock.Seconds(Duration);

    public int TickIntervalTicks => Math.Max(1, RunClock.Seconds(TickInterval));

    public int BlockTicks => RunClock.Seconds(BlockDuration);

    public int WanderTicks => Math.Max(1, RunClock.Seconds(WanderTurn));

    public bool IsImmune(PokemonType type1, PokemonType? type2) =>
        ImmuneTypes.Contains(type1) || (type2 is { } second && ImmuneTypes.Contains(second));
}

public sealed record StatusFile(int Version, IReadOnlyList<StatusSpec> Statuses);

/// <summary>The rules of every status condition, from content/data/statuses.json.</summary>
public sealed class StatusLibrary
{
    public const string FileName = "statuses.json";

    private readonly StatusSpec?[] _byKind = new StatusSpec?[Enum.GetValues<StatusKind>().Length];

    private StatusLibrary(IReadOnlyList<StatusSpec> all)
    {
        All = all;
        foreach (var status in all)
            _byKind[(int)status.Kind] = status;
    }

    public IReadOnlyList<StatusSpec> All { get; }

    /// <summary>The rules of <paramref name="kind"/>; null for <see cref="StatusKind.None"/> or a status missing from the file.</summary>
    public StatusSpec? this[StatusKind kind] => _byKind[(int)kind];

    public static StatusLibrary Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.StatusFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static StatusLibrary From(StatusFile file)
    {
        foreach (var status in file.Statuses)
        {
            if (status.Kind == StatusKind.None)
                throw new InvalidDataException($"{FileName}: \"None\" is not a status.");
            if (status.Duration <= 0f)
                throw new InvalidDataException($"{FileName}: {status.Kind} needs a positive duration.");
        }

        var duplicate = file.Statuses.GroupBy(s => s.Kind).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: {duplicate.Key} appears twice.");

        return new StatusLibrary(file.Statuses);
    }

    /// <summary>Previews: the same rules with every status enabled.</summary>
    public StatusLibrary WithAllEnabled() => new(All.Select(s => s with { Enabled = true }).ToList());
}
