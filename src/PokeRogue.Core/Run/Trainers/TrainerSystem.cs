using System.Numerics;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Trainers;

/// <summary>The trainer's active ability during a run.</summary>
public sealed class TrainerState
{
    /// <summary>Ticks before the key works again.</summary>
    public int CooldownTicks;

    /// <summary>Ticks left of a lasting effect (shield, slow time, boosts).</summary>
    public int EffectTicks;

    /// <summary>Shield: hits it will still block.</summary>
    public int ShieldCharges;

    public int Uses;

    public bool IsActive => EffectTicks > 0;
}

/// <summary>What a <see cref="RunEventType.TrainerAbility"/> event shows.</summary>
public enum TrainerEffect
{
    /// <summary>The key was pressed.</summary>
    Used,

    /// <summary>A lasting effect wore off.</summary>
    Ended,

    /// <summary>The shield stopped a hit.</summary>
    Blocked,

    /// <summary>The key works again.</summary>
    Ready,
}

/// <summary>
/// The trainer of the run (phase 5.2): passive bonuses always on (<see cref="RunState.UseTrainer"/>), and an active ability
/// on its key (Q) with a cooldown, one of the ten <see cref="TrainerActiveKind"/> archetypes. Hooks are called by
/// <see cref="RunState"/>, the moves and the enemies' walk, like the Pokémon's own ability.
/// </summary>
public static class TrainerSystem
{
    /// <summary>Wave: pixels per second of push away from the player.</summary>
    public const float WaveKnockback = 260f;

    /// <summary>Wave: share of the damage Dominanti and bosses take.</summary>
    public const float WaveEliteShare = 0.1f;

    /// <summary>The key would work now (scripted players press it then).</summary>
    public static bool CanUse(RunState run) => run.Trainer is not null && run.TrainerState.CooldownTicks == 0;

    /// <summary>0 to 1: how far the recharge is (1 = ready). Null without a trainer.</summary>
    public static float? Gauge(RunState run) => run.Trainer is { } trainer
        ? 1f - run.TrainerState.CooldownTicks / (float)RunClock.Seconds(trainer.Active.Cooldown)
        : null;

    /// <summary>0 to 1: what is left of a lasting effect; 0 when none is running.</summary>
    public static float EffectLeft(RunState run) => run.Trainer is { } trainer && run.TrainerState.EffectTicks > 0
        ? run.TrainerState.EffectTicks / (float)RunClock.Seconds(trainer.Active.Duration)
        : 0f;

    /// <summary>Whether <paramref name="kind"/> is the trainer's ability and is running now.</summary>
    public static bool Running(RunState run, TrainerActiveKind kind) => run.TrainerState.EffectTicks > 0 && run.Trainer?.Active.Kind == kind;

    /// <summary>Every tick, after the player has moved: timers, the key, and the multipliers the player reads.</summary>
    public static void Tick(RunState run, bool usePressed)
    {
        if (run.Trainer is not { } trainer)
            return;

        var state = run.TrainerState;
        var active = trainer.Active;
        if (state.CooldownTicks > 0 && --state.CooldownTicks == 0)
            Event(run, TrainerEffect.Ready);

        if (state.EffectTicks > 0 && --state.EffectTicks == 0)
        {
            state.ShieldCharges = 0;
            Event(run, TrainerEffect.Ended);
        }

        if (usePressed && state.CooldownTicks == 0)
            Use(run, trainer);

        var player = run.Player;
        player.TrainerSpeedMultiplier = Running(run, TrainerActiveKind.Haste) ? 1f + active.Power : 1f;
        player.TrainerCooldownMultiplier = Running(run, TrainerActiveKind.Frenzy) ? 1f - active.Power : 1f;
    }

    private static void Use(RunState run, TrainerDefinition trainer)
    {
        var state = run.TrainerState;
        var active = trainer.Active;
        var player = run.Player;
        state.CooldownTicks = RunClock.Seconds(active.Cooldown);
        state.EffectTicks = active.Duration > 0f ? RunClock.Seconds(active.Duration) : 0;
        state.Uses++;

        switch (active.Kind)
        {
            case TrainerActiveKind.Shield:
                state.ShieldCharges = active.Charges;
                break;
            case TrainerActiveKind.Heal:
                player.Heal(player.MaxHp * active.Power);
                break;
            case TrainerActiveKind.Wave:
                Wave(run, active);
                break;
            case TrainerActiveKind.Fortune:
                run.Pickups.Gems.MagnetizeAll();
                break;
            case TrainerActiveKind.StatusStorm:
                StatusStorm(run, active);
                break;
        }

        run.AddEvent(new RunEvent(RunEventType.TrainerAbility, player.Position, (int)TrainerEffect.Used,
            Target: new Vector2(active.Radius, (int)active.Kind), MoveId: trainer.Id, Status: active.Status));
    }

    /// <summary>A ring from the player: regular enemies within reach lose a share of their max PS and fly back; grass and rocks break.</summary>
    private static void Wave(RunState run, TrainerActiveSpec active)
    {
        var center = run.Player.Position;
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            var offset = e.Position - center;
            if (!e.IsAlive || offset.LengthSquared() > (active.Radius + e.Radius) * (active.Radius + e.Radius))
                continue;

            if (e.Rank == EnemyRank.Prop)
            {
                run.DamageEnemy(i, e.Hp, flash: false);
                continue;
            }

            if (e.IsShielded)
                continue;

            var share = e.Rank == EnemyRank.Normal ? active.Power : active.Power * WaveEliteShare;
            var damage = MathF.Max(1f, MathF.Round(e.MaxHp * share));
            run.AddEvent(new RunEvent(RunEventType.EnemyHit, e.Position, damage, e.SpeciesId, MoveId: run.Trainer!.Id));
            run.DamageEnemy(i, damage, offset.NormalizedOrZero() * WaveKnockback);
        }
    }

    /// <summary>Every enemy on screen may catch the status; burns take a fifth of the enemy's PS as their hit.</summary>
    private static void StatusStorm(RunState run, TrainerActiveSpec active)
    {
        var half = run.ViewSize / 2f + new Vector2(24f);
        var center = run.Player.Position;
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            var offset = e.Position - center;
            if (!e.IsAlive || e.Rank == EnemyRank.Prop || MathF.Abs(offset.X) > half.X || MathF.Abs(offset.Y) > half.Y)
                continue;
            if (run.Random.NextSingle() < active.Power)
                Statuses.StatusSystem.Apply(run, i, active.Status, e.MaxHp * 0.2f);
        }
    }

    /// <summary>Damage multiplier of a move hit: the passive boost to the trainer's type, and the active one while it runs.</summary>
    public static float DamageMultiplier(RunState run, MoveInstance move)
    {
        if (run.Trainer is not { } trainer)
            return 1f;

        var ofType = trainer.Type is { } type && move.Definition.Type == type;
        var multiplier = ofType ? 1f + trainer.Passive.TypePower : 1f;
        if (Running(run, TrainerActiveKind.TypeBoost))
            multiplier *= 1f + (trainer.Type is null || ofType ? trainer.Active.Power : trainer.Active.Power / 3f);
        return multiplier;
    }

    /// <summary>
    /// Whether a hit is stopped before it lands: while intangible, or by a shield charge (which it uses up). The player
    /// blinks as after a hit, so a crowd does not drain the shield in one tick.
    /// </summary>
    public static bool Blocks(RunState run, in Hurt hurt)
    {
        if (Running(run, TrainerActiveKind.Intangible))
            return true;
        if (!Running(run, TrainerActiveKind.Shield) || run.TrainerState.ShieldCharges <= 0)
            return false;

        run.Player.Shrug();
        if (--run.TrainerState.ShieldCharges == 0)
            run.TrainerState.EffectTicks = 1;
        Event(run, TrainerEffect.Blocked);
        return true;
    }

    /// <summary>Speed left to enemies and their shots: slowed while time is bent.</summary>
    public static float EnemySpeed(RunState run) => Running(run, TrainerActiveKind.SlowTime) ? run.Trainer!.Active.Power : 1f;

    /// <summary>Experience and coins while Fortune runs.</summary>
    public static float GainMultiplier(RunState run) => Running(run, TrainerActiveKind.Fortune) ? 1f + run.Trainer!.Active.Power : 1f;

    private static void Event(RunState run, TrainerEffect effect) =>
        run.AddEvent(new RunEvent(RunEventType.TrainerAbility, run.Player.Position, (int)effect,
            Target: new Vector2(0f, (int)run.Trainer!.Active.Kind), MoveId: run.Trainer.Id));
}
