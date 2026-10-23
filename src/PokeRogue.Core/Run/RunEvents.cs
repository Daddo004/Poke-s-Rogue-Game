using System.Numerics;
using PokeRogue.Core.Combat;

namespace PokeRogue.Core.Run;

public enum RunEventType
{
    PlayerHurt,
    EnemyDefeated,

    /// <summary>Damage dealt to an enemy: Value = amount, plus Effectiveness and Critical.</summary>
    EnemyHit,

    /// <summary>A move was used (renderer plays the attack animation). Value = move slot, Target = first target.</summary>
    MoveUsed,

    /// <summary>One segment of an arc move, from Position to Target.</summary>
    ArcSegment,

    /// <summary>An experience gem reached the player: Position = where it was collected, Value = experience.</summary>
    GemCollected,

    /// <summary>The player levelled up: Value = new level. The run pauses until a choice is made.</summary>
    LevelUp,

    /// <summary>The run is over: Value = (int)<see cref="RunOutcome"/>.</summary>
    RunEnded,

    /// <summary>A slash: Position = origin, Target = reach vector (direction × length), Value = width in degrees.</summary>
    Slash,

    /// <summary>An aura pulse around the player: Value = radius.</summary>
    AuraPulse,

    /// <summary>A strike landed: Position = impact centre, Value = radius.</summary>
    StrikeImpact,

    /// <summary>An enemy caught a status condition (<see cref="RunEvent.Status"/>).</summary>
    StatusApplied,

    /// <summary>Damage from a status condition (burn, poison, a confused self-hit): Value = amount.</summary>
    StatusDamage,

    /// <summary>A status condition wore off or was cured (a frozen Pokémon breaks out of the ice).</summary>
    StatusEnded,

    /// <summary>Focalnastro brought the player back from a knock-out: Value = revivals left.</summary>
    PlayerRevived,

    /// <summary>A move evolved (phase 2.6): Value = its slot, MoveId = the evolution. Comes out when the game resumes after the choice.</summary>
    MoveEvolved,
}

/// <summary>Something the renderer or audio may react to; the list is cleared at the start of every tick.</summary>
public readonly record struct RunEvent(
    RunEventType Type,
    Vector2 Position,
    float Value = 0f,
    int SpeciesId = 0,
    Vector2 Target = default,
    float Effectiveness = 1f,
    bool Critical = false,
    string? MoveId = null,
    StatusKind Status = StatusKind.None,
    bool Ohko = false);

public readonly record struct PlayerInput(Vector2 Move);
