using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Statuses;

/// <summary>
/// Status conditions on enemies (phase 2.3), one at a time as in the games. Moves inflict them on hit (<see cref="OnHit"/>);
/// <see cref="Tick"/> runs their timers, damage over time, paralysis lock-ups and confused wandering, and <see cref="Steer"/>
/// bends each enemy's walk and contact damage. The rules come from <see cref="RunState.StatusRules"/>.
/// </summary>
public static class StatusSystem
{
    /// <summary>Inflicts <paramref name="kind"/> unless the enemy is immune or already has another status (the same one refreshes).</summary>
    /// <param name="hitDamage">Damage of the hit that inflicted it: burn ticks are a share of it.</param>
    /// <param name="sourceSlot">Player move slot credited with the status' damage; -1 for none.</param>
    public static bool Apply(RunState run, int index, StatusKind kind, float hitDamage = 0f, int sourceSlot = -1)
    {
        ref var e = ref run.Enemies[index];
        if (!e.IsAlive || e.Rank == EnemyRank.Prop || run.StatusRules?[kind] is not { } rule || rule.IsImmune(e.Type1, e.Type2))
            return false;
        if (e.Status != StatusKind.None && e.Status != kind)
            return false;

        // Dominanti and bosses are never frozen or put to sleep.
        if (e.ResistsStatus && kind is StatusKind.Freeze or StatusKind.Sleep)
            return false;

        if (e.Status != kind)
        {
            e.Status = kind;
            e.StatusTimer = rule.TickIntervalTicks;
            e.StatusDamage = 0f;
            e.StuckTicks = 0;
            e.Wander = kind == StatusKind.Confusion ? RandomDirection(run.Random) : Vector2.Zero;
            run.AddEvent(new RunEvent(RunEventType.StatusApplied, e.Position, 0f, e.SpeciesId, Status: kind));
        }

        e.StatusTicks = e.ResistsStatus ? Math.Max(1, rule.DurationTicks / 2) : rule.DurationTicks;
        e.StatusSource = sourceSlot;

        // A refreshed burn keeps the stronger of the two.
        if (rule.HitDamageShare > 0f && hitDamage > 0f)
            e.StatusDamage = MathF.Max(e.StatusDamage, MathF.Max(1f, hitDamage * rule.HitDamageShare));
        return true;
    }

    /// <summary>After a move hit an enemy that is still standing: a cure (Fire on ice) first, then the move may inflict its status.</summary>
    public static void OnHit(RunState run, int index, MoveInstance move, float damage)
    {
        ref var e = ref run.Enemies[index];
        if (!e.IsAlive || run.StatusRules is not { } rules)
            return;

        var definition = move.Definition;
        if (e.Status != StatusKind.None && rules[e.Status]?.CuredBy == definition.Type)
            End(run, index);

        if (definition.Status != StatusKind.None && rules[definition.Status] is { Enabled: true } && run.Random.NextSingle() < definition.StatusChance)
            Apply(run, index, definition.Status, damage, run.Player.Moves.IndexOf(move));
    }

    /// <summary>Timers, damage over time, paralysis lock-ups and confused wandering. Runs before enemies move.</summary>
    public static void Tick(RunState run)
    {
        if (run.StatusRules is not { } rules)
            return;

        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (!e.IsAlive || e.Status == StatusKind.None)
                continue;

            if (rules[e.Status] is not { } rule)
            {
                End(run, i);
                continue;
            }

            if (e.StuckTicks > 0)
                e.StuckTicks--;

            if (--e.StatusTimer <= 0)
            {
                e.StatusTimer = rule.TickIntervalTicks;
                Pulse(run, i, ref e, rule);
                if (!e.IsAlive)
                    continue;
            }

            if (e.Status == StatusKind.Confusion && e.StatusTicks % rule.WanderTicks == 0)
                e.Wander = RandomDirection(run.Random);

            if (--e.StatusTicks <= 0)
                End(run, i);
        }
    }

    /// <summary>Bends an enemy's chase (<paramref name="velocity"/>) and contact damage by its status.</summary>
    public static void Steer(RunState run, in Enemy e, ref Vector2 velocity, ref float contactDamage)
    {
        if (e.Status == StatusKind.None || run.StatusRules?[e.Status] is not { } rule)
            return;

        if (e.Status == StatusKind.Confusion)
            velocity = e.Wander * e.Speed;

        // A paralysis lock-up stops everything, attacks included.
        var stuck = e.StuckTicks > 0;
        velocity *= stuck ? 0f : rule.Speed;
        contactDamage *= stuck ? 0f : rule.ContactDamage;
    }

    /// <summary>Clears the enemy's status (worn off or cured).</summary>
    public static void End(RunState run, int index)
    {
        ref var e = ref run.Enemies[index];
        if (e.Status == StatusKind.None)
            return;

        run.AddEvent(new RunEvent(RunEventType.StatusEnded, e.Position, 0f, e.SpeciesId, Status: e.Status));
        e.Status = StatusKind.None;
        e.StatusTicks = 0;
        e.StuckTicks = 0;
        e.StatusDamage = 0f;
        e.StatusSource = -1;
    }

    private static void Pulse(RunState run, int index, ref Enemy e, StatusSpec rule)
    {
        switch (e.Status)
        {
            case StatusKind.Burn:
                run.DamageFromStatus(index, e.StatusDamage);
                break;
            case StatusKind.Poison:
                run.DamageFromStatus(index, MathF.Max(1f, e.MaxHp * rule.MaxHpShare));
                break;
            case StatusKind.Paralysis when run.Random.NextSingle() < rule.Chance:
                e.StuckTicks = rule.BlockTicks;
                break;
            case StatusKind.Confusion when run.Random.NextSingle() < rule.Chance:
                run.DamageFromStatus(index, MathF.Max(1f, e.MaxHp * rule.MaxHpShare));
                break;
        }
    }

    private static Vector2 RandomDirection(Random random)
    {
        var angle = random.NextSingle() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
