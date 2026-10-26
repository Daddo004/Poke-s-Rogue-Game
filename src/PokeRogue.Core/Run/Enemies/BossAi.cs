using System.Numerics;

namespace PokeRogue.Core.Run.Enemies;

/// <summary>A rock about to fall (a boss' rockfall, phase 3.4): a warning circle, then damage to the player if inside.</summary>
public readonly record struct Hazard(Vector2 Position, float Radius, float Damage, bool Special, int StartTick, int DueTick, int SpeciesId);

/// <summary>
/// The level boss (phase 3.4). It arrives when the time runs out, walks at the player and takes turns with the attacks
/// of its phase: charges, volleys of shots, rocks from the sky. At each new phase it roars, untouchable for a moment,
/// pushing the horde away. Beating it wins the level.
/// </summary>
public static class BossAi
{
    /// <summary>Drawn at three times the size of the species; its body counts twice.</summary>
    public const float DrawScale = 3f;

    public const float HitboxScale = 2f;

    /// <summary>The roar at the start of each new phase.</summary>
    public const int RoarTicks = 60;

    /// <summary>The level is won this long after the boss falls, so its faint can play.</summary>
    public const int VictoryDelayTicks = 120;

    /// <summary>After its entrance the boss walks this long before the first attack.</summary>
    private const int FirstAttackTicks = 90;

    /// <summary>The roar pushes the horde away within this distance.</summary>
    private const float RoarReach = 140f;

    /// <summary>Spawns the boss on screen beside the player (clear of the HUD at the top) and starts the fight.</summary>
    public static BossFight Spawn(RunState run, BossDefinition boss, float hpMultiplier)
    {
        var index = run.SpawnEnemy(boss.SpeciesId, run.Player.Position + new Vector2(150, 10), hpMultiplier * boss.Spec.Hp);
        ref var e = ref run.Enemies[index];
        e.Profile = null;
        e.Behavior = EnemyBehavior.Chase;
        e.Rank = EnemyRank.Boss;
        e.Radius *= HitboxScale;
        e.Speed *= boss.Spec.Speed;
        e.ContactDamage *= 1.5f * run.Spawner.Timeline.DamageMultiplier(run.Ticks);
        e.KnockbackScale = 0f;
        e.XpValue = 0;
        e.CooldownTicks = FirstAttackTicks;

        var fight = new BossFight(boss, index, e.Serial);
        // Announced with the next tick, so a boss summoned between ticks (cheats, previews) gets its entrance too.
        run.AddEventOnResume(new RunEvent(RunEventType.BossAppeared, e.Position, 0f, e.SpeciesId));
        return fight;
    }

    public static void Think(RunState run, BossFight fight, ref Enemy e, Vector2 toPlayer, out Vector2 desired, out Vector2 aim, ref float contactDamage)
    {
        var phases = fight.Definition.Phases;
        var phase = phases[fight.Phase];
        var towards = toPlayer.NormalizedOrZero();
        desired = towards * e.Speed * phase.Speed;
        aim = toPlayer;

        // A new phase: it roars, out of reach for a moment, and the horde is thrown back.
        if (fight.Phase + 1 < phases.Count && e.Hp / e.MaxHp <= phases[fight.Phase + 1].Below && e.Action != EnemyAction.Roar)
        {
            fight.Phase++;
            fight.AttackCount = 0;
            fight.Current = null;
            e.Action = EnemyAction.Roar;
            e.ActionTicks = RoarTicks;
            desired = Vector2.Zero;
            PushHorde(run, e.Position);
            run.AddEvent(new RunEvent(RunEventType.BossPhaseChanged, e.Position, fight.Phase, e.SpeciesId));
            return;
        }

        if (e.IsImmobilized)
            return;

        var attack = fight.Current;
        switch (e.Action)
        {
            case EnemyAction.Roar:
                desired = Vector2.Zero;
                contactDamage = 0f;
                if (--e.ActionTicks <= 0)
                    Rest(fight, ref e);
                return;

            case EnemyAction.WindUp when attack is not null:
                desired = Vector2.Zero;
                aim = e.ActionDirection;
                if (e.ActionTicks > attack.WindUpTicks / 2 && towards != Vector2.Zero)
                    e.ActionDirection = towards;
                if (--e.ActionTicks <= 0)
                {
                    e.Action = EnemyAction.Dash;
                    e.ActionTicks = attack.DashTicks;
                    run.AddEvent(new RunEvent(RunEventType.EnemyCharged, e.Position, 0f, e.SpeciesId, Target: e.ActionDirection));
                }

                return;

            case EnemyAction.Dash when attack is not null:
                desired = e.ActionDirection * e.Speed * attack.Spec.DashSpeed;
                aim = e.ActionDirection;
                if (--e.ActionTicks <= 0)
                {
                    e.Action = EnemyAction.Recover;
                    e.ActionTicks = Math.Max(1, attack.RecoverTicks);
                }

                return;

            case EnemyAction.Recover:
                desired = Vector2.Zero;
                aim = e.ActionDirection;
                if (--e.ActionTicks <= 0)
                    Rest(fight, ref e);
                return;

            case EnemyAction.Aim when attack is not null:
                desired = Vector2.Zero;
                aim = e.ActionDirection;
                if (--e.ActionTicks <= 0)
                {
                    Volley(run, e, attack);
                    Rest(fight, ref e);
                }

                return;

            case EnemyAction.Slam when attack is not null:
                desired = Vector2.Zero;
                if (--e.ActionTicks <= 0)
                {
                    Rockfall(run, e, attack);
                    Rest(fight, ref e);
                }

                return;
        }

        // Walking between attacks.
        if (e.CooldownTicks > 0)
        {
            e.CooldownTicks--;
            return;
        }

        attack = fight.Current = phase.Attacks[fight.AttackCount++ % phase.Attacks.Count];
        e.ActionTicks = attack.WindUpTicks;
        e.ActionDirection = towards == Vector2.Zero ? Vector2.UnitY : towards;
        e.Action = attack.Spec.Kind switch
        {
            BossAttackKind.Charge => EnemyAction.WindUp,
            BossAttackKind.Volley => EnemyAction.Aim,
            _ => EnemyAction.Slam,
        };
        desired = Vector2.Zero;
    }

    /// <summary>Ticks the falling rocks: each one lands on its time and hurts the player if inside its circle.</summary>
    public static void TickHazards(RunState run)
    {
        var hazards = run.Hazards;
        for (var i = hazards.Count - 1; i >= 0; i--)
        {
            var hazard = hazards[i];
            if (run.Ticks < hazard.DueTick)
                continue;

            hazards.RemoveAt(i);
            var player = run.Player;
            var reach = hazard.Radius + player.Radius * 0.5f;
            if (Vector2.DistanceSquared(hazard.Position, player.Position) <= reach * reach)
            {
                var taken = player.TakeContactDamage(hazard.Damage, hazard.Special);
                if (taken > 0f)
                {
                    run.Stats.RecordDamageTaken(taken);
                    run.AddEvent(new RunEvent(RunEventType.PlayerHurt, player.Position, taken, hazard.SpeciesId));
                }
            }

            run.AddEvent(new RunEvent(RunEventType.HazardLanded, hazard.Position, hazard.Radius, hazard.SpeciesId));
        }
    }

    private static void Rest(BossFight fight, ref Enemy e)
    {
        e.Action = EnemyAction.None;
        e.CooldownTicks = fight.Definition.Phases[fight.Phase].RestTicks;
    }

    /// <summary>A fan of shots centred on where it aimed.</summary>
    private static void Volley(RunState run, in Enemy e, BossAttack attack)
    {
        var spec = attack.Spec;
        var centre = MathF.Atan2(e.ActionDirection.Y, e.ActionDirection.X);
        var spread = spec.Spread * MathF.PI / 180f;
        for (var k = 0; k < spec.Count; k++)
        {
            var angle = spec.Count == 1 ? centre : centre - spread / 2f + spread * k / (spec.Count - 1);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            run.EnemyShots.Fire(run, e.Position + direction * e.Radius * 0.6f, direction * spec.ShotSpeed, e.ContactDamage * spec.Damage,
                attack.Move!, e.SpeciesId, RunClock.Seconds(4f));
        }
    }

    /// <summary>Rocks over the player and around it; the first lands right where the player stands now.</summary>
    private static void Rockfall(RunState run, in Enemy e, BossAttack attack)
    {
        var spec = attack.Spec;
        var player = run.Player.Position;
        for (var k = 0; k < spec.Count; k++)
        {
            var at = player;
            if (k > 0)
            {
                var angle = run.Random.NextSingle() * MathF.Tau;
                at += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (spec.Radius * 1.5f + run.Random.NextSingle() * 60f);
            }

            run.Hazards.Add(new Hazard(at, spec.Radius, e.ContactDamage * spec.Damage, e.HitsSpecially, run.Ticks, run.Ticks + attack.DelayTicks, e.SpeciesId));
        }
    }

    /// <summary>The roar throws regular enemies back from the boss.</summary>
    private static void PushHorde(RunState run, Vector2 from)
    {
        foreach (ref var other in run.Enemies.Items)
        {
            if (!other.IsAlive || other.Rank != EnemyRank.Normal)
                continue;

            var offset = other.Position - from;
            var distance = offset.Length();
            if (distance < RoarReach && distance > 0.01f)
                other.Knockback += offset / distance * 420f * (1f - distance / RoarReach) * other.KnockbackScale;
        }
    }
}
