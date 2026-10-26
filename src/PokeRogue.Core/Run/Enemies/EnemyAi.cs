using System.Numerics;

namespace PokeRogue.Core.Run.Enemies;

/// <summary>
/// Enemy behaviours of phase 3.2: shooters keep their distance and fire, chargers take aim and rush, kamikazes light a
/// fuse and blow up, teleporters hop around the player. Plain chasers and tanks just walk at the player.
/// </summary>
public static class EnemyAi
{
    /// <summary>Teleporters shrink away, then grow back, over this many ticks each.</summary>
    public const int HopTicks = 12;

    /// <summary>A kamikaze creeps on at this share of its speed while the fuse burns.</summary>
    private const float FuseCreep = 0.25f;

    /// <summary>Teleporters hop away early when the player comes this close.</summary>
    private const float TooClose = 60f;

    /// <summary>Gives a freshly spawned enemy its species' profile and stats.</summary>
    public static void Apply(RunState run, ref Enemy e, EnemyProfile profile)
    {
        var spec = profile.Spec;
        e.Profile = profile;
        e.Behavior = profile.Behavior;
        e.MaxHp *= spec.Hp;
        e.Hp = e.MaxHp;
        e.Speed *= spec.Speed;
        e.ContactDamage *= spec.Contact;
        e.KnockbackScale = spec.Knockback;

        // Staggered, so a crowd of shooters does not fire in volleys. Chargers are ready almost at once, so the first
        // charge comes from range rather than after they have already walked up to the player.
        e.CooldownTicks = run.Random.Next(profile.Behavior == EnemyBehavior.Charger ? profile.CooldownTicks / 4 : profile.CooldownTicks);
    }

    /// <summary>
    /// Where a fighting enemy wants to go this tick (<paramref name="desired"/>, a velocity) and where it looks
    /// (<paramref name="aim"/>). May fire, charge, teleport or blow it up; check <see cref="Enemy.IsAlive"/> afterwards.
    /// </summary>
    public static void Think(RunState run, int index, ref Enemy e, Vector2 toPlayer, out Vector2 desired, out Vector2 aim, ref float contactDamage)
    {
        var profile = e.Profile!;
        var distance = toPlayer.Length();
        var towards = toPlayer.NormalizedOrZero();
        desired = towards * e.Speed;
        aim = toPlayer;

        // Frozen, asleep or locked up: every timer waits too.
        if (e.IsImmobilized)
            return;

        if (e.CooldownTicks > 0)
            e.CooldownTicks--;

        switch (profile.Behavior)
        {
            case EnemyBehavior.Ranged:
                Ranged(run, ref e, profile, towards, distance, ref desired);
                break;
            case EnemyBehavior.Charger:
                Charger(run, ref e, profile, towards, distance, ref desired, ref aim);
                break;
            case EnemyBehavior.Kamikaze:
                Kamikaze(run, index, ref e, profile, towards, distance, ref desired);
                break;
            case EnemyBehavior.Teleport:
                Teleport(run, ref e, profile, towards, distance, ref desired, ref contactDamage);
                break;
        }
    }

    private static void Ranged(RunState run, ref Enemy e, EnemyProfile profile, Vector2 towards, float distance, ref Vector2 desired)
    {
        if (e.Action == EnemyAction.Aim)
        {
            desired = Vector2.Zero;
            if (--e.ActionTicks <= 0)
            {
                e.Action = EnemyAction.None;
                e.CooldownTicks = Jittered(run, profile.CooldownTicks);
                Fire(run, e,profile, towards);
            }

            return;
        }

        desired = KeepDistance(e, profile.Spec.Range, towards, distance);
        if (e.CooldownTicks == 0 && distance <= profile.Spec.Range * 1.3f)
        {
            e.Action = EnemyAction.Aim;
            e.ActionTicks = profile.WindUpTicks;
        }
    }

    private static void Charger(RunState run, ref Enemy e, EnemyProfile profile, Vector2 towards, float distance, ref Vector2 desired, ref Vector2 aim)
    {
        switch (e.Action)
        {
            case EnemyAction.WindUp:
                desired = Vector2.Zero;
                aim = e.ActionDirection;

                // It follows the player with its eyes for the first half of the wind-up, then the line is set.
                if (e.ActionTicks > profile.WindUpTicks / 2 && towards != Vector2.Zero)
                    e.ActionDirection = towards;
                if (--e.ActionTicks <= 0)
                {
                    e.Action = EnemyAction.Dash;
                    e.ActionTicks = profile.DashTicks;
                    run.AddEvent(new RunEvent(RunEventType.EnemyCharged, e.Position, 0f, e.SpeciesId, Target: e.ActionDirection));
                }

                return;

            case EnemyAction.Dash:
                desired = e.ActionDirection * e.Speed * profile.Spec.DashSpeed;
                aim = e.ActionDirection;
                if (--e.ActionTicks <= 0)
                {
                    e.Action = profile.RecoverTicks > 0 ? EnemyAction.Recover : EnemyAction.None;
                    e.ActionTicks = profile.RecoverTicks;
                    e.CooldownTicks = Jittered(run, profile.CooldownTicks);
                }

                return;

            case EnemyAction.Recover:
                desired = Vector2.Zero;
                aim = e.ActionDirection;
                if (--e.ActionTicks <= 0)
                    e.Action = EnemyAction.None;
                return;
        }

        if (e.CooldownTicks == 0 && distance <= profile.Spec.Range && towards != Vector2.Zero)
        {
            e.Action = EnemyAction.WindUp;
            e.ActionTicks = profile.WindUpTicks;
            e.ActionDirection = towards;
            desired = Vector2.Zero;
        }
    }

    private static void Kamikaze(RunState run, int index, ref Enemy e, EnemyProfile profile, Vector2 towards, float distance, ref Vector2 desired)
    {
        if (e.Action != EnemyAction.Fuse)
        {
            if (distance <= profile.Spec.Trigger)
            {
                e.Action = EnemyAction.Fuse;
                e.ActionTicks = profile.WindUpTicks;
            }

            return;
        }

        desired = towards * e.Speed * FuseCreep;
        if (--e.ActionTicks > 0)
            return;

        // Self-Destruct: the blast hurts the player if it is inside, and the Pokémon is gone (no KO, no gem).
        var spec = profile.Spec;
        var player = run.Player;
        var reach = spec.BlastRadius + player.Radius;
        if (Vector2.DistanceSquared(e.Position, player.Position) <= reach * reach)
        {
            run.HurtPlayer(new Abilities.Hurt(Abilities.HurtKind.Blast, e.ContactDamage * spec.BlastDamage, e.HitsSpecially, e.SpeciesId, e.Type1));
        }

        run.AddEvent(new RunEvent(RunEventType.EnemyExploded, e.Position, spec.BlastRadius, e.SpeciesId));
        run.Enemies.Release(index);
    }

    private static void Teleport(RunState run, ref Enemy e, EnemyProfile profile, Vector2 towards, float distance, ref Vector2 desired, ref float contactDamage)
    {
        switch (e.Action)
        {
            case EnemyAction.Vanish:
                desired = Vector2.Zero;
                contactDamage = 0f;
                if (--e.ActionTicks <= 0)
                {
                    var from = e.Position;
                    var angle = run.Random.NextSingle() * MathF.Tau;
                    var hop = profile.Spec.HopMin + run.Random.NextSingle() * (profile.Spec.HopMax - profile.Spec.HopMin);
                    e.Position = run.Player.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * hop;
                    e.Knockback = Vector2.Zero;
                    e.Action = EnemyAction.Appear;
                    e.ActionTicks = HopTicks;
                    run.AddEvent(new RunEvent(RunEventType.EnemyTeleported, from, 0f, e.SpeciesId, Target: e.Position));
                }

                return;

            case EnemyAction.Appear:
                desired = Vector2.Zero;
                contactDamage = 0f;
                if (--e.ActionTicks <= 0)
                {
                    e.Action = EnemyAction.None;
                    e.CooldownTicks = Jittered(run, profile.CooldownTicks);
                    if (profile.Move is not null)
                        Fire(run, e,profile, (run.Player.Position - e.Position).NormalizedOrZero());
                }

                return;
        }

        desired = KeepDistance(e, profile.Spec.Range, towards, distance);
        if (e.CooldownTicks == 0 || (distance < TooClose && e.CooldownTicks < profile.CooldownTicks / 2))
        {
            e.Action = EnemyAction.Vanish;
            e.ActionTicks = HopTicks;
        }
    }

    /// <summary>Closes in when far, backs off when too close, otherwise sidesteps around the player.</summary>
    private static Vector2 KeepDistance(in Enemy e, float range, Vector2 towards, float distance)
    {
        if (distance > range * 1.15f)
            return towards * e.Speed;
        if (distance < range * 0.75f)
            return -towards * e.Speed * 0.7f;

        var side = e.Serial % 2 == 0 ? 1f : -1f;
        return new Vector2(-towards.Y, towards.X) * side * e.Speed * 0.35f;
    }

    private static void Fire(RunState run, in Enemy e, EnemyProfile profile, Vector2 direction)
    {
        if (profile.Move is null || direction == Vector2.Zero)
            return;

        var spec = profile.Spec;
        var lifetime = RunClock.Seconds(spec.Range * 2.5f / spec.ShotSpeed);
        run.EnemyShots.Fire(run, e.Position + direction * e.Radius, direction * spec.ShotSpeed, e.ContactDamage * spec.ShotDamage,
            profile.Move, e.SpeciesId, lifetime);
    }

    /// <summary>A cooldown ±20%, so enemies drift out of step.</summary>
    private static int Jittered(RunState run, int ticks) => Math.Max(1, (int)(ticks * (0.8f + run.Random.NextSingle() * 0.4f)));
}
