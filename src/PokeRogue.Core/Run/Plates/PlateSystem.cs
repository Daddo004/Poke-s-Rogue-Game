using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run.Plates;

/// <summary>Timers and pending effects of the run's Plates.</summary>
public sealed class PlateState
{
    /// <summary>Ticks until the next wave of Lastraidro and the next bolt of Lastrasaetta.</summary>
    public int TideTicks;
    public int BoltTicks;

    /// <summary>The chest's offer of a second Plate was made (once per run).</summary>
    public bool Offered;

    /// <summary>Blasts and contagions from last tick's knock-outs, set off on the next tick (so chains spread over time).</summary>
    internal readonly List<(Vector2 At, float Damage)> Blasts = [];
    internal readonly List<Vector2> Contagions = [];
}

/// <summary>
/// Arceus' Plates during a run (phase 9.1): every Plate held boosts the moves of its type and brings their cards up more
/// often, and adds its own effect. Hooks are called by <see cref="RunState"/>, like the ability's and the trainer's.
/// </summary>
public static class PlateSystem
{
    /// <summary>Share of their PS that Dominanti and bosses lose to a bolt, a wave or a blast (regular enemies lose the Plate's power).</summary>
    public const float EliteShare = 0.03f;

    /// <summary>A wave's push, in pixels per second.</summary>
    public const float TideKnockback = 240f;

    /// <summary>Rilancio gained with Lastramente.</summary>
    public const int InsightRerolls = 2;

    /// <summary>The run's Plate with this effect, if it holds one.</summary>
    public static PlateDefinition? With(RunState run, PlateEffectKind kind)
    {
        foreach (var plate in run.Plates)
        {
            if (plate.Effect.Kind == kind)
                return plate;
        }

        return null;
    }

    /// <summary>A Plate was gained: its modifiers join the player's stats, Lastramente gives its Rilancio, the timers start.</summary>
    internal static void OnGained(RunState run, PlateDefinition plate)
    {
        var bonus = run.Plates.Aggregate(Items.ItemBonus.None, (sum, p) => sum + p.Modifiers);
        run.Player.PlateBonus = bonus;
        run.Player.RecalculateStats();
        if (plate.Effect.Kind == PlateEffectKind.Insight)
            run.RerollsLeft += InsightRerolls;
        if (plate.Effect.Kind == PlateEffectKind.Tide)
            run.PlateState.TideTicks = RunClock.Seconds(plate.Effect.Interval);
        if (plate.Effect.Kind == PlateEffectKind.Bolt)
            run.PlateState.BoltTicks = RunClock.Seconds(plate.Effect.Interval);
    }

    /// <summary>Every tick: the blasts and contagions due, Lastraidro's wave and Lastrasaetta's bolt.</summary>
    public static void Tick(RunState run)
    {
        if (run.Plates.Count == 0)
            return;

        var state = run.PlateState;
        if (state.Blasts.Count > 0 && With(run, PlateEffectKind.BurnBlast) is { } fire)
        {
            var blasts = state.Blasts.ToArray();
            state.Blasts.Clear();
            foreach (var (at, damage) in blasts)
                Blast(run, fire, at, damage);
        }

        if (state.Contagions.Count > 0 && With(run, PlateEffectKind.Contagion) is { } poison)
        {
            var sources = state.Contagions.ToArray();
            state.Contagions.Clear();
            foreach (var at in sources)
                Spread(run, poison, at);
        }

        if (With(run, PlateEffectKind.Tide) is { } water && --state.TideTicks <= 0)
        {
            state.TideTicks = RunClock.Seconds(water.Effect.Interval);
            Tide(run, water);
        }

        if (With(run, PlateEffectKind.Bolt) is { } electric && --state.BoltTicks <= 0)
        {
            state.BoltTicks = RunClock.Seconds(electric.Effect.Interval);
            Bolt(run, electric);
        }
    }

    /// <summary>Damage multiplier of a move hit on <paramref name="target"/>: the Plates' types, Lastrageo, Lastrabaco, Lastradrakon, Lastraferro.</summary>
    public static float DamageMultiplier(RunState run, MoveInstance move, in Enemy target)
    {
        if (run.Plates.Count == 0)
            return 1f;

        var multiplier = 1f;
        var typePower = run.PlateRules?.File.TypePower ?? 0f;
        foreach (var plate in run.Plates)
        {
            var effect = plate.Effect;
            if (move.Definition.Type == plate.Type)
                multiplier *= 1f + typePower;

            switch (effect.Kind)
            {
                case PlateEffectKind.Brawl when Vector2.DistanceSquared(target.Position, run.Player.Position) <= (effect.Radius + target.Radius) * (effect.Radius + target.Radius):
                case PlateEffectKind.Slayer when target.Rank is EnemyRank.Dominant or EnemyRank.Boss:
                case PlateEffectKind.Swarm:
                    multiplier *= effect.Power;
                    break;
                case PlateEffectKind.Fury:
                    multiplier *= 1f + effect.Power * Math.Min(effect.Count, RunClock.ClockMinutes(run.Ticks));
                    break;
            }
        }

        return multiplier;
    }

    /// <summary>The player's attack as a hit sees it: Lastrapugno's heavier critical hits.</summary>
    public static AttackerProfile Attacker(RunState run, AttackerProfile attacker) =>
        With(run, PlateEffectKind.Crit) is { } fist ? attacker with { CritDamage = MathF.Max(attacker.CritDamage, fist.Effect.Power) } : attacker;

    /// <summary>An enemy was knocked out: if burned or poisoned, Lastrarogo and Lastrafiele set off on the next tick.</summary>
    public static void OnDefeated(RunState run, in Enemy e)
    {
        if (run.Plates.Count == 0 || e.Rank == EnemyRank.Prop)
            return;

        if (e.Status == StatusKind.Burn && With(run, PlateEffectKind.BurnBlast) is { } fire)
            run.PlateState.Blasts.Add((e.Position, e.MaxHp * fire.Effect.Power));
        else if (e.Status == StatusKind.Poison && With(run, PlateEffectKind.Contagion) is not null)
            run.PlateState.Contagions.Add(e.Position);
    }

    /// <summary>Lastragelo: enemies close to the player walk slower.</summary>
    public static void Steer(RunState run, in Enemy e, ref Vector2 velocity)
    {
        if (With(run, PlateEffectKind.Chill) is not { } ice || e.Rank == EnemyRank.Boss)
            return;

        var reach = ice.Effect.Radius + e.Radius;
        if (Vector2.DistanceSquared(e.Position, run.Player.Position) <= reach * reach)
            velocity *= ice.Effect.Power;
    }

    /// <summary>Lastratetra: now and then a hit passes through the player (0), otherwise all of it lands (1).</summary>
    public static float HurtMultiplier(RunState run)
    {
        if (With(run, PlateEffectKind.Phantom) is not { } ghost || run.Random.NextSingle() >= ghost.Effect.Chance)
            return 1f;

        run.Player.Shrug();
        Event(run, ghost, run.Player.Position);
        return 0f;
    }

    /// <summary>Lastraprato: food and potions heal more.</summary>
    public static float HealMultiplier(RunState run) => With(run, PlateEffectKind.Bloom)?.Effect.Power ?? 1f;

    /// <summary>Lastratrema: regular enemies drop coins and berries more often.</summary>
    public static float DropMultiplier(RunState run) => With(run, PlateEffectKind.Greed)?.Effect.Power ?? 1f;

    /// <summary>Lastramente: more cards at every level-up.</summary>
    public static int ExtraChoices(RunState run) => With(run, PlateEffectKind.Insight)?.Effect.Count ?? 0;

    /// <summary>How much more often a level-up card comes up: the moves of a Plate's type, as new moves and as upgrades.</summary>
    public static float CardWeight(RunState run, LevelUpOption option)
    {
        if (run.Plates.Count == 0 || option.MoveType is not { } type || option.Kind is not (LevelUpKind.NewMove or LevelUpKind.MoveUpgrade))
            return 1f;
        return run.Plates.Any(p => p.Type == type) ? run.PlateRules?.File.CardWeight ?? 1f : 1f;
    }

    /// <summary>Lastrarogo: the burned enemy blows up, hurting and burning those around.</summary>
    private static void Blast(RunState run, PlateDefinition plate, Vector2 at, float damage)
    {
        var radius = plate.Effect.Radius;
        Event(run, plate, at, radius);
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            var offset = e.Position - at;
            if (!e.IsAlive || e.Rank == EnemyRank.Prop || offset.LengthSquared() > (radius + e.Radius) * (radius + e.Radius))
                continue;

            var dealt = e.Rank == EnemyRank.Normal ? damage : MathF.Min(damage, e.MaxHp * EliteShare);
            Statuses.StatusSystem.Apply(run, i, StatusKind.Burn, dealt);
            run.DamageEnemy(i, MathF.Max(1f, dealt), offset.NormalizedOrZero() * 120f);
        }
    }

    /// <summary>Lastrafiele: the poison passes to the nearest enemies that have no status yet.</summary>
    private static void Spread(RunState run, PlateDefinition plate, Vector2 at)
    {
        var radius = plate.Effect.Radius;
        var targets = new List<(int Index, float Distance)>();
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var e = ref items[i];
            if (!e.IsAlive || e.Rank == EnemyRank.Prop || e.Status != StatusKind.None)
                continue;

            var d = Vector2.DistanceSquared(e.Position, at);
            if (d <= (radius + e.Radius) * (radius + e.Radius))
                targets.Add((i, d));
        }

        foreach (var (index, _) in targets.OrderBy(t => t.Distance).Take(plate.Effect.Count))
        {
            if (Statuses.StatusSystem.Apply(run, index, StatusKind.Poison))
                run.AddEvent(new RunEvent(RunEventType.PlateEffect, items[index].Position, (int)plate.Effect.Kind, Target: at, MoveId: plate.Id));
        }
    }

    /// <summary>Lastraidro: a ring from the player pushes the horde back; grass and rocks break.</summary>
    private static void Tide(RunState run, PlateDefinition plate)
    {
        var center = run.Player.Position;
        var radius = plate.Effect.Radius;
        Event(run, plate, center, radius);
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            var offset = e.Position - center;
            if (!e.IsAlive || e.IsShielded || offset.LengthSquared() > (radius + e.Radius) * (radius + e.Radius))
                continue;

            if (e.Rank == EnemyRank.Prop)
            {
                run.DamageEnemy(i, e.Hp, flash: false);
                continue;
            }

            var share = e.Rank == EnemyRank.Normal ? plate.Effect.Power : EliteShare;
            var damage = MathF.Max(1f, MathF.Round(e.MaxHp * share));
            run.AddEvent(new RunEvent(RunEventType.EnemyHit, e.Position, damage, e.SpeciesId, MoveId: plate.Id));
            run.DamageEnemy(i, damage, offset.NormalizedOrZero() * TideKnockback);
        }
    }

    /// <summary>Lastrasaetta: lightning from the sky on one enemy on screen, at random.</summary>
    private static void Bolt(RunState run, PlateDefinition plate)
    {
        var half = run.ViewSize / 2f;
        var center = run.Player.Position;
        var items = run.Enemies.Items;
        var target = -1;
        var seen = 0;
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var e = ref items[i];
            var offset = e.Position - center;
            if (!e.IsAlive || e.IsShielded || e.Rank == EnemyRank.Prop || MathF.Abs(offset.X) > half.X || MathF.Abs(offset.Y) > half.Y)
                continue;

            // One at random among those on screen, in a single pass.
            if (run.Random.Next(++seen) == 0)
                target = i;
        }

        if (target < 0)
            return;

        ref var hit = ref items[target];
        var share = hit.Rank == EnemyRank.Normal ? plate.Effect.Power : EliteShare;
        var damage = MathF.Max(1f, MathF.Round(hit.MaxHp * share));
        run.AddEvent(new RunEvent(RunEventType.PlateEffect, hit.Position, (int)plate.Effect.Kind, Target: hit.Position - new Vector2(0, 120), MoveId: plate.Id));
        run.AddEvent(new RunEvent(RunEventType.EnemyHit, hit.Position, damage, hit.SpeciesId, MoveId: plate.Id));
        if (run.Random.NextSingle() < plate.Effect.Chance)
            Statuses.StatusSystem.Apply(run, target, StatusKind.Paralysis, damage);
        run.DamageEnemy(target, damage);
    }

    private static void Event(RunState run, PlateDefinition plate, Vector2 at, float radius = 0f) =>
        run.AddEvent(new RunEvent(RunEventType.PlateEffect, at, (int)plate.Effect.Kind, Target: new Vector2(radius, 0f), MoveId: plate.Id));
}
