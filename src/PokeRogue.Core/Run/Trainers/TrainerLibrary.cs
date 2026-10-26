using System.Text.Json;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Trainers;

/// <summary>Where a trainer comes from: the region of the games where they first lead a gym, the League or the story.</summary>
public enum TrainerRegion
{
    Kanto,
    Johto,
    Hoenn,
    Sinnoh,
}

/// <summary>A trainer's place in the games, for the collection and the challenges of phase 5.3.</summary>
public enum TrainerRole
{
    /// <summary>The trainer every run starts with (Ash).</summary>
    Base,
    GymLeader,
    EliteFour,
    Champion,

    /// <summary>Red: the legend on top of Mt. Silver.</summary>
    Legend,
}

/// <summary>The archetypes of the trainers' active abilities (phase 5.2): what the key does.</summary>
public enum TrainerActiveKind
{
    /// <summary>Blocks the next <c>charges</c> hits within <c>duration</c> seconds.</summary>
    Shield,

    /// <summary>Heals <c>power</c> of the max PS at once.</summary>
    Heal,

    /// <summary>A ring from the player within <c>radius</c>: regular enemies lose <c>power</c> of their max PS and are pushed away.</summary>
    Wave,

    /// <summary>For <c>duration</c> seconds enemies and their shots move at <c>power</c> of their speed.</summary>
    SlowTime,

    /// <summary>No hit lands for <c>duration</c> seconds.</summary>
    Intangible,

    /// <summary>For <c>duration</c> seconds moves of the trainer's type deal <c>power</c> more, the others a third of it (all moves <c>power</c> without a type).</summary>
    TypeBoost,

    /// <summary>Every gem on the field flies in; for <c>duration</c> seconds experience and coins grow by <c>power</c>.</summary>
    Fortune,

    /// <summary>For <c>duration</c> seconds move cooldowns shrink by <c>power</c>.</summary>
    Frenzy,

    /// <summary>Every enemy on screen catches <c>status</c> (with chance <c>power</c>), immunities and resistances apply.</summary>
    StatusStorm,

    /// <summary>For <c>duration</c> seconds the player runs <c>power</c> faster.</summary>
    Haste,
}

/// <summary>A trainer's active ability as written in trainers.json.</summary>
public sealed record TrainerActiveSpec
{
    public required TrainerActiveKind Kind { get; set; }

    public required string NameIt { get; set; }

    public string DescriptionIt { get; set; } = "";

    /// <summary>Seconds between uses.</summary>
    public float Cooldown { get; set; } = 40f;

    /// <summary>Seconds the effect lasts (shield, slow time, boosts); 0 for instant ones.</summary>
    public float Duration { get; set; }

    /// <summary>The archetype's strength (see <see cref="TrainerActiveKind"/>).</summary>
    public float Power { get; set; }

    /// <summary>Wave: its reach in pixels.</summary>
    public float Radius { get; set; }

    /// <summary>Shield: hits it blocks.</summary>
    public int Charges { get; set; }

    /// <summary>StatusStorm: the status it spreads.</summary>
    public StatusKind Status { get; set; }
}

/// <summary>A trainer's passive as written in trainers.json: always-on bonuses and a boost to the moves of their type.</summary>
public sealed record TrainerPassiveSpec
{
    public required string NameIt { get; set; }

    public string DescriptionIt { get; set; } = "";

    /// <summary>The same fields as an item's "perLevel" (fractions: 0.1 = +10%).</summary>
    public ItemBonus Modifiers { get; set; } = ItemBonus.None;

    /// <summary>Extra damage of the moves of the trainer's type.</summary>
    public float TypePower { get; set; }
}

/// <summary>One trainer as written in content/data/trainers.json.</summary>
public sealed record TrainerSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public required TrainerRegion Region { get; set; }

    public required TrainerRole Role { get; set; }

    /// <summary>Their specialty; null for trainers without one (Ash, Red, Blu).</summary>
    public PokemonType? Type { get; set; }

    /// <summary>content/trainers/&lt;sprite&gt;.png; the id when omitted.</summary>
    public string? Sprite { get; set; }

    public required TrainerPassiveSpec Passive { get; set; }

    public required TrainerActiveSpec Active { get; set; }

    public required UnlockSpec Unlock { get; set; }

    /// <summary>Their title in the challenge's introduction, when the role's word does not fit.</summary>
    public string? TitleIt { get; set; }

    /// <summary>The team of their challenge (phase 5.3), PokeAPI species keys in the order they are sent out; the last is the ace.</summary>
    public IReadOnlyList<string> Team { get; set; } = [];
}

/// <summary>
/// How a trainer's team fights in a challenge (phase 5.3), the same for every trainer: each Pokémon is a boss built from
/// <see cref="Member"/> (the ace from <see cref="Ace"/>). Volleys shoot the move <see cref="Shots"/> gives for the
/// Pokémon's first type; a type without one throws rocks instead.
/// </summary>
public sealed record ChallengeRules
{
    /// <summary>Seconds between a Pokémon fainting and the next one coming out.</summary>
    public float NextDelay { get; set; } = 1.5f;

    public BossSpec Member { get; set; } = new() { Pokemon = "" };

    public BossSpec Ace { get; set; } = new() { Pokemon = "" };

    /// <summary>Type → the move whose look a volley borrows.</summary>
    public IReadOnlyDictionary<PokemonType, string> Shots { get; set; } = new Dictionary<PokemonType, string>();
}

public sealed record TrainerFile(int Version, IReadOnlyList<TrainerSpec> Trainers, ChallengeRules? Challenge = null);

/// <summary>A trainer (phase 5.1): portrait, passive bonuses and an active ability with a cooldown.</summary>
public sealed record TrainerDefinition(
    string Id,
    string NameIt,
    TrainerRegion Region,
    TrainerRole Role,
    PokemonType? Type,
    string Sprite,
    TrainerPassiveSpec Passive,
    TrainerActiveSpec Active,
    UnlockSpec Unlock)
{
    /// <summary>Path of the portrait under content/.</summary>
    public string SpritePath => Path.Combine("trainers", Sprite + ".png");

    /// <summary>The team of their challenge, as bosses in the order they come out (phase 5.3); empty for Ash.</summary>
    public IReadOnlyList<BossDefinition> Team { get; init; } = [];

    /// <summary>"titleIt" of trainers.json, where the role's word does not fit (Camilla is the Campionessa).</summary>
    public string? CustomTitleIt { get; init; }

    /// <summary>Their role, as the games call it, for the challenge's introduction.</summary>
    public string TitleIt => CustomTitleIt ?? Role switch
    {
        TrainerRole.GymLeader => "Capopalestra",
        TrainerRole.EliteFour => "Superquattro",
        TrainerRole.Champion => "Campione",
        TrainerRole.Legend => "Leggenda",
        _ => "Allenatore",
    };
}

/// <summary>Every trainer, from content/data/trainers.json (phase 5.1).</summary>
public sealed class TrainerLibrary
{
    public const string FileName = "trainers.json";

    /// <summary>The trainer of a run when none is chosen.</summary>
    public const string DefaultId = "ash";

    private readonly Dictionary<string, TrainerDefinition> _byId;

    private TrainerLibrary(IReadOnlyList<TrainerDefinition> all)
    {
        All = all;
        _byId = all.ToDictionary(t => t.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<TrainerDefinition> All { get; }

    public TrainerDefinition this[string id] =>
        _byId.TryGetValue(id, out var trainer) ? trainer : throw new KeyNotFoundException($"Trainer '{id}' is not in {FileName}.");

    public TrainerDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>How teams fight in a challenge; null when the file has no "challenge" block (no team can be fought).</summary>
    public ChallengeRules? Challenge { get; private init; }

    public static TrainerLibrary Load(string contentRoot, IReadOnlyList<SpeciesData> pokedex, MoveLibrary moves)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.TrainerFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, pokedex, moves);
    }

    /// <summary>Without the Pokédex no team is resolved (tests of the file's other rules).</summary>
    public static TrainerLibrary From(TrainerFile file, IReadOnlyList<SpeciesData>? pokedex = null, MoveLibrary? moves = null)
    {
        var byKey = pokedex?.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var trainers = file.Trainers.Select(spec => Resolve(spec) with { Team = Team(spec, file.Challenge, byKey, moves), CustomTitleIt = spec.TitleIt }).ToList();

        var duplicate = trainers.GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.Key}' appears twice.");
        if (trainers.All(t => t.Id != DefaultId))
            throw new InvalidDataException($"{FileName}: the default trainer '{DefaultId}' is missing.");

        return new TrainerLibrary(trainers) { Challenge = file.Challenge };
    }

    /// <summary>
    /// A trainer's team as bosses: every Pokémon fights by the "member" pattern, the last by the "ace" one, its volleys
    /// shooting its type's move; a type with no move in "shots" (Rock, Ground) throws rocks instead.
    /// </summary>
    private static IReadOnlyList<BossDefinition> Team(TrainerSpec spec, ChallengeRules? rules, Dictionary<string, SpeciesData>? byKey, MoveLibrary? moves)
    {
        if (spec.Team.Count == 0 || byKey is null)
            return [];
        if (rules is null)
            throw new InvalidDataException($"{FileName}: '{spec.Id}' has a team but the file has no \"challenge\" block.");

        var team = new List<BossDefinition>();
        for (var i = 0; i < spec.Team.Count; i++)
        {
            if (!byKey.TryGetValue(spec.Team[i], out var species))
                throw new InvalidDataException($"{FileName}, '{spec.Id}': '{spec.Team[i]}' is not a Pokémon key.");

            var pattern = i == spec.Team.Count - 1 ? rules.Ace : rules.Member;
            var shot = rules.Shots.GetValueOrDefault(species.Types[0]);
            var phases = pattern.Phases.Select(phase => phase with
            {
                Attacks = phase.Attacks.Select(attack => attack.Kind != BossAttackKind.Volley ? attack
                    : shot is not null ? attack with { Move = shot }
                    : new BossAttackSpec { Kind = BossAttackKind.Rockfall, WindUp = attack.WindUp, Count = Math.Max(3, attack.Count / 2), Damage = 2f, Delay = 1f }).ToList(),
            }).ToList();
            var boss = pattern with { Pokemon = species.Key, TitleIt = spec.NameIt, Phases = phases };
            team.Add(BossDefinition.From(boss, species.Id, moves, FileName));
        }

        return team;
    }

    private static TrainerDefinition Resolve(TrainerSpec spec)
    {
        string Where() => $"{FileName}, '{spec.Id}'";

        var active = spec.Active;
        if (active.Cooldown <= 0f)
            throw new InvalidDataException($"{Where()}: the active ability needs a positive \"cooldown\".");

        var lasting = active.Kind is TrainerActiveKind.Shield or TrainerActiveKind.SlowTime or TrainerActiveKind.Intangible
            or TrainerActiveKind.TypeBoost or TrainerActiveKind.Fortune or TrainerActiveKind.Frenzy or TrainerActiveKind.Haste;
        if (lasting != active.Duration > 0f)
            throw new InvalidDataException($"{Where()}: a {active.Kind} ability {(lasting ? "needs" : "takes no")} \"duration\".");
        if (lasting && active.Duration >= active.Cooldown)
            throw new InvalidDataException($"{Where()}: the effect must end before the cooldown does.");

        var problem = active.Kind switch
        {
            TrainerActiveKind.Shield when active.Charges <= 0 => "a Shield needs \"charges\"",
            TrainerActiveKind.Wave when active.Radius <= 0f => "a Wave needs \"radius\"",
            TrainerActiveKind.StatusStorm when active.Status == StatusKind.None => "a StatusStorm needs \"status\"",
            TrainerActiveKind.SlowTime when active.Power is <= 0f or >= 1f => "SlowTime's \"power\" is the speed left, between 0 and 1",
            TrainerActiveKind.Frenzy when active.Power is <= 0f or >= 1f => "Frenzy's \"power\" is the cooldown cut, between 0 and 1",
            not (TrainerActiveKind.Shield or TrainerActiveKind.Intangible) when active.Power <= 0f => $"a {active.Kind} needs \"power\"",
            _ => null,
        };
        if (problem is not null)
            throw new InvalidDataException($"{Where()}: {problem}.");

        if (spec.Passive.TypePower > 0f && spec.Type is null)
            throw new InvalidDataException($"{Where()}: \"typePower\" needs the trainer's \"type\".");

        var unlock = spec.Unlock.Kind;
        if (unlock is not (UnlockKind.Start or UnlockKind.Defeat or UnlockKind.Mission or UnlockKind.Shop))
            throw new InvalidDataException($"{Where()}: trainers are unlocked from the start, by beating them, by a mission or in the shop.");
        if ((unlock == UnlockKind.Shop) != spec.Unlock.Price > 0)
            throw new InvalidDataException($"{Where()}: a {unlock} unlock {(unlock == UnlockKind.Shop ? "needs" : "takes no")} \"price\".");

        return new TrainerDefinition(spec.Id, spec.NameIt, spec.Region, spec.Role, spec.Type, spec.Sprite ?? spec.Id, spec.Passive, active, spec.Unlock);
    }
}
