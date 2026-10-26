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

    /// <summary>A special wave event began (phase 3.1): Value = (int)WaveEventKind, Position = where it comes from, SpeciesId = who.</summary>
    WaveEventStarted,

    /// <summary>An enemy fired a shot (phase 3.2): Position = muzzle, Target = velocity, MoveId = the move it looks like.</summary>
    EnemyShotFired,

    /// <summary>An enemy shot popped: Value = 1 on the player, 0 when it ran out of range.</summary>
    EnemyShotEnded,

    /// <summary>A charger set off: Target = direction.</summary>
    EnemyCharged,

    /// <summary>A kamikaze blew up: Value = blast radius.</summary>
    EnemyExploded,

    /// <summary>A teleporter hopped from Position to Target.</summary>
    EnemyTeleported,

    /// <summary>A Dominante entered the level (phase 3.3): SpeciesId = who, Position = where.</summary>
    DominantAppeared,

    /// <summary>A defeated Dominante left its chest: Value = (int)ChestTier.</summary>
    ChestDropped,

    /// <summary>The player opened a chest: Value = (int)ChestTier; the rewards are in <see cref="RunState.LastChest"/>.</summary>
    ChestOpened,

    /// <summary>The level boss arrived (phase 3.4): the game plays its introduction.</summary>
    BossAppeared,

    /// <summary>The boss entered a new phase and roars: Value = the phase (1 = second).</summary>
    BossPhaseChanged,

    /// <summary>The boss fell; the level is won a moment later.</summary>
    BossDefeated,

    /// <summary>A boss' rock landed: Value = radius.</summary>
    HazardLanded,

    /// <summary>Tall grass or a boulder was broken (phase 3.5): Value = (int)BreakableKind.</summary>
    PropBroken,

    /// <summary>The player picked something up: Value = (int)LootKind.</summary>
    LootCollected,

    /// <summary>A shiny Pokémon appeared: SpeciesId = who.</summary>
    ShinyAppeared,
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
