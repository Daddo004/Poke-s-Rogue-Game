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

    /// <summary>The player evolved (phase 4.2): Value = the species it was, SpeciesId = the new one. Comes out when the game resumes.</summary>
    PlayerEvolved,

    /// <summary>
    /// The player's ability did something worth showing (phase 4.3): MoveId = the ability id, Value = (int)AbilityEffect.
    /// Teletrasporto: Position = from, Target = to. Trasformazione: SpeciesId = the copied species (0 when it turns back).
    /// </summary>
    AbilityTriggered,

    /// <summary>The special key was pressed with a full gauge (phase 4.4): the run waits while the game plays the special. Value = (int)type.</summary>
    SpecialStarted,

    /// <summary>The special hit everything on screen. Value = (int)type, MoveId = its id. Comes out when the game resumes.</summary>
    SpecialUnleashed,

    /// <summary>
    /// The trainer's active ability (phase 5.2): Value = (int)TrainerEffect, MoveId = the trainer's id, Target.Y = (int)its
    /// archetype, Target.X = a wave's radius, Status = what a status storm spreads.
    /// </summary>
    TrainerAbility,

    /// <summary>The time ran out on a challenge run (phase 5.3): the trainer steps in. Value = team size, MoveId = their id.</summary>
    TrainerChallenged,

    /// <summary>The trainer sent out a Pokémon: SpeciesId = it, Value = its place in the team (0 = first), Position = where.</summary>
    TrainerSentOut,

    /// <summary>The trainer's last Pokémon fainted: the trainer is beaten. MoveId = their id.</summary>
    TrainerDefeated,
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

/// <summary>What the player does this tick: where it walks, and whether it pressed the keys of its ability, its special and its trainer.</summary>
public readonly record struct PlayerInput(Vector2 Move, bool UseAbility = false, bool UseSpecial = false, bool UseTrainer = false);
