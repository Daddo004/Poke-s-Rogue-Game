using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Moves;

/// <summary>How a move behaves on the field (plan, phase 2.1).</summary>
public enum MoveArchetype
{
    /// <summary>Straight shots towards the nearest enemies, hitting everything along the path up to their pierce.</summary>
    Projectile,

    /// <summary>Shots that steer towards the nearest enemy.</summary>
    Homing,

    /// <summary>Thrown out towards a target, slows down and comes back to the player, hitting on both legs.</summary>
    Boomerang,

    /// <summary>Objects circling the player for a while, hitting what they touch.</summary>
    Orbit,

    /// <summary>Periodic pulse around the player that hits everything inside (Vampire Survivors' garlic).</summary>
    Aura,

    /// <summary>Instant sector hit in the facing direction, alternating sides with more amount (VS whip).</summary>
    Slash,

    /// <summary>Line from the player towards the nearest enemy, staying for a while and hitting repeatedly.</summary>
    Beam,

    /// <summary>Hits from the sky on random enemies after a short warning, with an area of effect.</summary>
    Strike,

    /// <summary>Instant bolt to the nearest enemy, then jumping to further enemies.</summary>
    Arc,

    /// <summary>Damaging ground zones dropped on enemies, or left behind as a trail.</summary>
    Zone,

    /// <summary>Shock wave expanding from the player, hitting each enemy once as it passes.</summary>
    Ring,
}

/// <summary>Numbers for one level of a move. Distances in pixels, speeds in pixels per second, times in seconds, angles in degrees.</summary>
public sealed record MoveLevel
{
    public float Power { get; set; }

    public float Cooldown { get; set; } = 1f;

    public int Amount { get; set; } = 1;

    public float Speed { get; set; } = 220f;

    /// <summary>Targeting range; also orbit radius, beam length, ring size and slash reach.</summary>
    public float Range { get; set; } = 220f;

    /// <summary>Hit size: projectile radius, zone/aura/strike radius, beam half-width, ring thickness.</summary>
    public float Radius { get; set; } = 5f;

    /// <summary>Lifetime of projectiles, orbits, beams and zones.</summary>
    public float Duration { get; set; } = 1.6f;

    /// <summary>Enemies a projectile can hit before vanishing; 0 or less = unlimited.</summary>
    public int Pierce { get; set; } = 1;

    public float Knockback { get; set; } = 60f;

    public int Chains { get; set; }

    public float ChainRange { get; set; } = 70f;

    /// <summary>Minimum time before the same move hits the same enemy again (orbits, beams, zones).</summary>
    public float HitInterval { get; set; } = 0.5f;

    /// <summary>Slash width, or fan angle between several shots.</summary>
    public float Spread { get; set; } = 100f;

    /// <summary>Strike warning time; spacing between repeated slashes or rings.</summary>
    public float Delay { get; set; } = 0.3f;

    /// <summary>How fast homing shots turn.</summary>
    public float TurnRate { get; set; } = 270f;

    public MoveLevel Apply(MoveLevelDelta d) => this with
    {
        Power = Power + d.Power,
        Cooldown = MathF.Max(0.05f, Cooldown + d.Cooldown),
        Amount = Amount + d.Amount,
        Speed = Speed + d.Speed,
        Range = Range + d.Range,
        Radius = Radius + d.Radius,
        Duration = Duration + d.Duration,
        Pierce = Pierce + d.Pierce,
        Knockback = Knockback + d.Knockback,
        Chains = Chains + d.Chains,
        ChainRange = ChainRange + d.ChainRange,
        HitInterval = MathF.Max(0.05f, HitInterval + d.HitInterval),
        Spread = Spread + d.Spread,
        Delay = MathF.Max(0f, Delay + d.Delay),
        TurnRate = TurnRate + d.TurnRate,
    };
}

/// <summary>Change from one level to the next; every field defaults to 0 so upgrades list only what changes.</summary>
public sealed record MoveLevelDelta
{
    public float Power { get; set; }

    public float Cooldown { get; set; }

    public int Amount { get; set; }

    public float Speed { get; set; }

    public float Range { get; set; }

    public float Radius { get; set; }

    public float Duration { get; set; }

    public int Pierce { get; set; }

    public float Knockback { get; set; }

    public int Chains { get; set; }

    public float ChainRange { get; set; }

    public float HitInterval { get; set; }

    public float Spread { get; set; }

    public float Delay { get; set; }

    public float TurnRate { get; set; }
}

public sealed record MoveDefinition(
    string Id,
    string NameIt,
    PokemonType Type,
    MoveCategory Category,
    MoveArchetype Archetype,
    IReadOnlyList<MoveLevel> Levels)
{
    public int MaxLevel => Levels.Count;

    /// <summary>Shown when the move is offered for the first time.</summary>
    public string DescriptionIt { get; init; } = "";

    /// <summary>Offered at level-up. Moves stay disabled until their animation is approved.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Visual preset (phase 2.2); null uses the type's default look.</summary>
    public string? Vfx { get; init; }

    /// <summary>Status condition the move may inflict on hit (see <see cref="Statuses.StatusSystem"/>).</summary>
    public StatusKind Status { get; init; }

    /// <summary>Chance per hit to inflict <see cref="Status"/>, from 0 to 1.</summary>
    public float StatusChance { get; init; }

    /// <summary>Added to the player's chance of a critical hit with this move (Colpokarate's high critical ratio).</summary>
    public float CritBonus { get; init; }

    /// <summary>Share of the damage dealt that comes back to the player as HP (Sanguisuga).</summary>
    public float Drain { get; init; }

    /// <summary>Evolutions (phase 2.6): the base move this one replaces, once maxed with <see cref="EvolveItem"/> held.</summary>
    public string? EvolvesFrom { get; init; }

    public string? EvolveItem { get; init; }

    public bool IsEvolution => EvolvesFrom is not null;

    /// <summary>Orbits that never expire once cast (Verdebufera).</summary>
    public bool Permanent { get; init; }

    /// <summary>Chance per hit to knock a regular enemy out at once (Abisso).</summary>
    public float OhkoChance { get; init; }

    /// <summary>Zones only: drop them where the player stands (a trail) instead of on enemies.</summary>
    public bool SpawnAtPlayer { get; init; }
}

/// <summary>A move the player owns during a run, with its level and cooldown.</summary>
public sealed class MoveInstance(MoveDefinition definition, int level = 1)
{
    public MoveDefinition Definition { get; } = definition;

    public int Level { get; set; } = level;

    public MoveLevel Stats => Definition.Levels[Math.Clamp(Level, 1, Definition.MaxLevel) - 1];

    /// <summary>Ticks until the next use.</summary>
    public int Cooldown { get; set; }

    /// <summary>Total damage dealt this run (results screen, phase 1.6).</summary>
    public float DamageDealt { get; set; }

    /// <summary>Run tick when the move was obtained, for damage per second on the results screen.</summary>
    public int AcquiredTick { get; init; }
}
