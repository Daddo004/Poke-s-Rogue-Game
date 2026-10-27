using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Enemies;

public enum BossAttackKind
{
    /// <summary>Aims (its lane shows on the ground), rushes in a straight line, stands dazed.</summary>
    Charge,

    /// <summary>Aims (red sight lines show where), then fires a fan of shots.</summary>
    Volley,

    /// <summary>Raises its arms; red circles mark where rocks will fall, one right under the player.</summary>
    Rockfall,

    /// <summary>Fades away and comes back behind the player, <see cref="BossAttackSpec.Radius"/> away (Darkrai, phase 9.3).</summary>
    Teleport,

    /// <summary>Vuototetro (Darkrai): dark circles open like the rocks' ones; inside one when it closes, the player falls asleep.</summary>
    DarkVoid,
}

/// <summary>One attack of a boss phase, as written in the level's waves file. Times in seconds, distances in pixels.</summary>
public sealed record BossAttackSpec
{
    public required BossAttackKind Kind { get; set; }

    /// <summary>Warning before the attack.</summary>
    public float WindUp { get; set; } = 0.8f;

    /// <summary>Charge: speed as a multiple of its walking speed, how long it lasts, and the daze afterwards.</summary>
    public float DashSpeed { get; set; } = 4f;

    public float DashTime { get; set; } = 0.7f;

    public float Recover { get; set; } = 0.8f;

    /// <summary>Volley: the move its shots look like.</summary>
    public string? Move { get; set; }

    /// <summary>Volley: shots in the fan. Rockfall: rocks.</summary>
    public int Count { get; set; } = 5;

    /// <summary>Volley: width of the fan in degrees.</summary>
    public float Spread { get; set; } = 60f;

    public float ShotSpeed { get; set; } = 110f;

    /// <summary>Volley and rockfall: damage as a multiple of the boss' contact damage.</summary>
    public float Damage { get; set; } = 1f;

    /// <summary>Rockfall: size of each rock's circle, and the time from the warning to the impact.</summary>
    public float Radius { get; set; } = 28f;

    public float Delay { get; set; } = 1f;

    /// <summary>Vuototetro: seconds the player sleeps when caught.</summary>
    public float Sleep { get; set; }
}

/// <summary>A stretch of the fight: it starts when the boss' PS drop to <see cref="Below"/> of the maximum.</summary>
public sealed record BossPhaseSpec
{
    public float Below { get; set; } = 1f;

    /// <summary>Multiplies the walking speed in this phase.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Seconds of walking between two attacks.</summary>
    public float Rest { get; set; } = 1.5f;

    /// <summary>Used in turn, in this order.</summary>
    public IReadOnlyList<BossAttackSpec> Attacks { get; set; } = [];
}

public sealed record BossSpec
{
    public required string Pokemon { get; set; }

    /// <summary>Shown under its name in the introduction, e.g. "Signore del Bosco".</summary>
    public string TitleIt { get; set; } = "";

    /// <summary>Multiplies HP on top of the level's growth at the time it arrives.</summary>
    public float Hp { get; set; } = 100f;

    public float Speed { get; set; } = 1f;

    /// <summary>Without phases of its own (phase 7.4): the move its volleys look like; by default its type's (worlds.json "shots").</summary>
    public string? Shot { get; set; }

    public IReadOnlyList<BossPhaseSpec> Phases { get; set; } = [];
}

/// <summary>
/// A shared way of fighting given to a Pokémon (trainers' teams in phase 5.3, levels' bosses and legendaries in Block 7):
/// its volleys shoot <c>shot</c>, or the move of its first type; a type without one throws rocks instead.
/// </summary>
public static class BossPattern
{
    public static BossSpec Apply(BossSpec pattern, Data.SpeciesData species, IReadOnlyDictionary<Data.PokemonType, string> shots, string? shot = null)
    {
        var move = shot ?? shots.GetValueOrDefault(species.Types[0]);
        var phases = pattern.Phases.Select(phase => phase with
        {
            Attacks = phase.Attacks.Select(attack => attack.Kind != BossAttackKind.Volley ? attack
                : move is not null ? attack with { Move = move }
                : new BossAttackSpec { Kind = BossAttackKind.Rockfall, WindUp = attack.WindUp, Count = Math.Max(3, attack.Count / 2), Damage = 2f, Delay = 1f }).ToList(),
        }).ToList();
        return pattern with { Pokemon = species.Key, Phases = phases, Shot = null };
    }
}

public sealed record BossAttack(BossAttackSpec Spec, MoveDefinition? Move)
{
    public int WindUpTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.WindUp));

    public int DashTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.DashTime));

    public int RecoverTicks { get; } = RunClock.Seconds(Spec.Recover);

    public int DelayTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.Delay));

    public int SleepTicks { get; } = RunClock.Seconds(Spec.Sleep);
}

public sealed record BossPhase(float Below, float Speed, int RestTicks, IReadOnlyList<BossAttack> Attacks);

/// <summary>A level's boss, checked and ready for the simulation.</summary>
public sealed record BossDefinition(int SpeciesId, BossSpec Spec, IReadOnlyList<BossPhase> Phases)
{
    public static BossDefinition From(BossSpec spec, int speciesId, MoveLibrary? moves, string fileId)
    {
        if (spec.Phases.Count == 0 || spec.Phases[0].Below < 1f)
            throw new InvalidDataException($"Waves '{fileId}': the boss needs phases, the first starting at \"below\": 1.");
        for (var i = 1; i < spec.Phases.Count; i++)
        {
            if (spec.Phases[i].Below >= spec.Phases[i - 1].Below || spec.Phases[i].Below <= 0f)
                throw new InvalidDataException($"Waves '{fileId}': boss phases go from full PS down, each \"below\" smaller than the one before.");
        }

        if (spec.Hp <= 0f || spec.Speed <= 0f)
            throw new InvalidDataException($"Waves '{fileId}': the boss needs positive HP and speed.");

        var phases = new List<BossPhase>();
        foreach (var phase in spec.Phases)
        {
            if (phase.Attacks.Count == 0)
                throw new InvalidDataException($"Waves '{fileId}': every boss phase needs at least one attack.");

            var attacks = new List<BossAttack>();
            foreach (var attack in phase.Attacks)
            {
                if (attack.Kind == BossAttackKind.Volley && (attack.Move is null || moves is null))
                    throw new InvalidDataException($"Waves '{fileId}': a boss volley needs a \"move\" (and the move library).");
                if (attack.Count <= 0)
                    throw new InvalidDataException($"Waves '{fileId}': a boss attack needs a positive count.");

                attacks.Add(new BossAttack(attack, attack.Move is null || moves is null ? null : moves[attack.Move]));
            }

            phases.Add(new BossPhase(phase.Below, phase.Speed, RunClock.Seconds(phase.Rest), attacks));
        }

        return new BossDefinition(speciesId, spec, phases);
    }
}

/// <summary>The boss fight in progress: which enemy slot is the boss, its phase and its next attack.</summary>
public sealed class BossFight(BossDefinition definition, int index, int serial)
{
    public BossDefinition Definition { get; } = definition;

    /// <summary>The boss' slot in the enemy pool.</summary>
    public int Index { get; } = index;

    public int Serial { get; } = serial;

    public int Phase { get; set; }

    /// <summary>How many attacks it has started in this phase (the next one is this, modulo the phase's attacks).</summary>
    public int AttackCount { get; set; }

    /// <summary>The attack under way, for renderers drawing its warning.</summary>
    public BossAttack? Current { get; set; }

    /// <summary>Tick it was defeated; the level is won a moment later.</summary>
    public int? DefeatedTick { get; set; }

    /// <summary>A legendary of Ultra (phase 7.3): beating it unlocks it.</summary>
    public bool IsLegendary { get; set; }

    /// <summary>Darkrai the Reaper of the endless mode (phase 9.3): it grows angrier every minute, and beating it wins the run.</summary>
    public bool IsReaper { get; set; }

    /// <summary>The tick it arrived.</summary>
    public int StartTick { get; set; }
}
