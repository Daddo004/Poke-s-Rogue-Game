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

    public IReadOnlyList<BossPhaseSpec> Phases { get; set; } = [];
}

public sealed record BossAttack(BossAttackSpec Spec, MoveDefinition? Move)
{
    public int WindUpTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.WindUp));

    public int DashTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.DashTime));

    public int RecoverTicks { get; } = RunClock.Seconds(Spec.Recover);

    public int DelayTicks { get; } = Math.Max(1, RunClock.Seconds(Spec.Delay));
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
}
