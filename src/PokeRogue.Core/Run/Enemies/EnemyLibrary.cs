using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Enemies;

/// <summary>
/// How one species fights, as written in content/data/enemies.json (phase 3.2). Every field has a neutral default, so
/// an entry lists only what its behaviour uses. Multipliers apply to the stats the species already has.
/// </summary>
public sealed record EnemySpec
{
    public required string Pokemon { get; set; }

    public required EnemyBehavior Behavior { get; set; }

    /// <summary>False keeps the behaviour out of real runs until its look is approved; previews still show it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Multiplies HP (tanks are sturdy).</summary>
    public float Hp { get; set; } = 1f;

    /// <summary>Multiplies walking speed.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Multiplies the push of the player's hits (tanks barely move).</summary>
    public float Knockback { get; set; } = 1f;

    /// <summary>Multiplies contact damage.</summary>
    public float Contact { get; set; } = 1f;

    /// <summary>Ranged and teleporters: the move whose look and type its shots borrow; null for none.</summary>
    public string? Move { get; set; }

    /// <summary>Ranged: preferred distance from the player. Chargers: distance from which they charge. Pixels.</summary>
    public float Range { get; set; } = 140f;

    /// <summary>Seconds between shots, charges or teleports.</summary>
    public float Cooldown { get; set; } = 3f;

    /// <summary>Shots: pixels per second.</summary>
    public float ShotSpeed { get; set; } = 100f;

    /// <summary>Shots: damage as a share of the species' contact damage.</summary>
    public float ShotDamage { get; set; } = 0.8f;

    /// <summary>Seconds of warning before a shot, a charge or a blast.</summary>
    public float WindUp { get; set; } = 0.6f;

    /// <summary>Chargers: speed of the charge as a multiple of the walking speed, and how long it lasts (seconds).</summary>
    public float DashSpeed { get; set; } = 4f;

    public float DashTime { get; set; } = 0.5f;

    /// <summary>Chargers: seconds standing dazed after a charge.</summary>
    public float Recover { get; set; } = 0.8f;

    /// <summary>Kamikaze: the fuse starts this close to the player; the blast reaches this far (pixels).</summary>
    public float Trigger { get; set; } = 36f;

    public float BlastRadius { get; set; } = 48f;

    /// <summary>Kamikaze: blast damage as a multiple of the species' contact damage.</summary>
    public float BlastDamage { get; set; } = 3f;

    /// <summary>Teleporters: they reappear between these distances from the player (pixels).</summary>
    public float HopMin { get; set; } = 110f;

    public float HopMax { get; set; } = 170f;
}

public sealed record EnemyFile(int Version, IReadOnlyList<EnemySpec> Enemies);

/// <summary>A species' fighting profile, in ticks and pixels, ready for the simulation.</summary>
public sealed record EnemyProfile(int SpeciesId, EnemySpec Spec, MoveDefinition? Move)
{
    public EnemyBehavior Behavior => Spec.Behavior;

    public int CooldownTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.Cooldown));

    public int WindUpTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.WindUp));

    public int DashTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.DashTime));

    public int RecoverTicks { get; } = RunClock.Seconds(Spec.Recover);
}

/// <summary>Fighting profiles of the enemy species (phase 3.2); species without one simply chase the player.</summary>
public sealed class EnemyLibrary
{
    public const string FileName = "enemies.json";

    private readonly Dictionary<int, EnemyProfile> _bySpecies;

    private EnemyLibrary(IReadOnlyList<EnemyProfile> all, bool includeDisabled)
    {
        All = all;
        _bySpecies = all.Where(p => includeDisabled || p.Spec.Enabled).ToDictionary(p => p.SpeciesId);
    }

    public IReadOnlyList<EnemyProfile> All { get; }

    /// <summary>The profile real runs use for <paramref name="speciesId"/>: null when it has none or it is not approved yet.</summary>
    public EnemyProfile? For(int speciesId) => _bySpecies.GetValueOrDefault(speciesId);

    /// <summary>Previews and playtests: every profile, approved or not.</summary>
    public EnemyLibrary WithDisabled() => new(All, includeDisabled: true);

    public static EnemyLibrary Load(string contentRoot, IReadOnlyList<SpeciesData> pokedex, MoveLibrary moves)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.EnemyFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, pokedex, moves);
    }

    public static EnemyLibrary From(EnemyFile file, IReadOnlyList<SpeciesData> pokedex, MoveLibrary moves)
    {
        var byKey = pokedex.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var profiles = new List<EnemyProfile>();
        foreach (var spec in file.Enemies)
        {
            if (!byKey.TryGetValue(spec.Pokemon, out var species))
                throw new InvalidDataException($"{FileName}: '{spec.Pokemon}' is not a Pokémon key.");
            if (spec.Behavior == EnemyBehavior.Straight)
                throw new InvalidDataException($"{FileName}: '{spec.Pokemon}' cannot be a straight traveller; swarms and stampedes are wave events.");
            if (spec.Hp <= 0f || spec.Speed <= 0f || spec.Knockback < 0f || spec.Contact < 0f || spec.Cooldown <= 0f)
                throw new InvalidDataException($"{FileName}: '{spec.Pokemon}' needs positive HP, speed and cooldown.");
            if (spec.Behavior == EnemyBehavior.Ranged && spec.Move is null)
                throw new InvalidDataException($"{FileName}: '{spec.Pokemon}' shoots, so it needs a \"move\" for the look of its shots.");
            if (spec.HopMax < spec.HopMin)
                throw new InvalidDataException($"{FileName}: '{spec.Pokemon}' needs hopMin ≤ hopMax.");

            profiles.Add(new EnemyProfile(species.Id, spec, spec.Move is null ? null : moves[spec.Move]));
        }

        var duplicate = profiles.GroupBy(p => p.SpeciesId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.First().Spec.Pokemon}' appears twice.");

        return new EnemyLibrary(profiles, includeDisabled: false);
    }
}
