using System.Numerics;
using PokeRogue.Core.Geometry;

namespace PokeRogue.Core.Run.Moves;

/// <summary>
/// Uses the player's moves when their cooldown is ready and advances everything they leave on the field:
/// projectiles (straight, homing, boomerang, orbit), zones, rings, beams and delayed hits.
/// </summary>
public sealed class MoveSystem
{
    /// <summary>Largest enemy radius, used to widen grid queries.</summary>
    private const float MaxEnemyRadius = 20f;

    /// <summary>How far a homing shot looks for a new target.</summary>
    private const float HomingSearchRadius = 160f;

    private readonly List<(int Index, float DistanceSquared)> _candidates = [];
    private readonly List<int> _found = [];
    private readonly HashSet<int> _arcHits = [];
    private int _serial;

    public ProjectilePool Projectiles { get; } = new(1024);

    public List<MoveZone> Zones { get; } = [];

    public List<MoveRing> Rings { get; } = [];

    public List<MoveBeam> Beams { get; } = [];

    public List<PendingHit> Pending { get; } = [];

    public void Tick(RunState run)
    {
        var moves = run.Player.Moves;
        for (var slot = 0; slot < moves.Count; slot++)
        {
            var move = moves[slot];
            if (move.Cooldown > 0)
            {
                move.Cooldown--;
                continue;
            }

            // Targeted moves are not used without a target in range: they stay ready and fire as soon as one shows up.
            var stats = Effective.Of(move, run.Player);
            var aim = run.Player.Position;
            var used = move.Definition.Archetype switch
            {
                MoveArchetype.Projectile => UseProjectile(run, move, slot, stats, ProjectileMotion.Straight, ref aim),
                MoveArchetype.Homing => UseProjectile(run, move, slot, stats, ProjectileMotion.Homing, ref aim),
                MoveArchetype.Boomerang => UseProjectile(run, move, slot, stats, ProjectileMotion.Boomerang, ref aim),
                MoveArchetype.Orbit => UseOrbit(run, move, slot, stats),
                MoveArchetype.Aura => UseAura(run, move, stats),
                MoveArchetype.Slash => UseSlash(run, move, slot, stats, ref aim),
                MoveArchetype.Beam => UseBeam(run, move, slot, stats, ref aim),
                MoveArchetype.Strike => UseStrike(run, move, slot, stats, ref aim),
                MoveArchetype.Arc => UseArc(run, move, stats, ref aim),
                MoveArchetype.Zone => UseZone(run, move, slot, stats, ref aim),
                MoveArchetype.Ring => UseRing(run, move, slot, stats),
                _ => false,
            };

            if (used)
            {
                move.Cooldown = RunClock.Seconds(stats.Level.Cooldown * run.Player.CooldownMultiplier * run.Player.AbilityCooldownMultiplier);
                run.AddEvent(new RunEvent(RunEventType.MoveUsed, run.Player.Position, slot, Target: aim, MoveId: move.Definition.Id));
            }
        }

        UpdateProjectiles(run);
        UpdateZones(run);
        UpdateRings(run);
        UpdateBeams(run);
        UpdatePending(run);
    }

    /// <summary>A move level combined with the player's area, duration, amount and speed bonuses.</summary>
    private readonly record struct Effective(MoveLevel Level, int Amount, float Radius, float Range, int DurationTicks, float Speed, int HitIntervalTicks)
    {
        public static Effective Of(MoveInstance move, Player player)
        {
            var level = move.Stats;
            var archetype = move.Definition.Archetype;

            // Area grows sizes; for orbits, rings and slashes the reach is a size too.
            var rangeIsArea = archetype is MoveArchetype.Orbit or MoveArchetype.Ring or MoveArchetype.Slash;

            // Duration bonuses stretch lingering effects, not flying shots (that would be extra range).
            var lingers = archetype is MoveArchetype.Orbit or MoveArchetype.Zone or MoveArchetype.Beam;

            return new Effective(
                level,
                System.Math.Max(1, level.Amount + player.AmountBonus),
                level.Radius * player.AreaMultiplier,
                rangeIsArea ? level.Range * player.AreaMultiplier : level.Range,
                RunClock.Seconds(level.Duration * (lingers ? player.DurationMultiplier : 1f)),
                level.Speed * player.ProjectileSpeedMultiplier,
                System.Math.Max(1, RunClock.Seconds(level.HitInterval)));
        }
    }

    private bool UseProjectile(RunState run, MoveInstance move, int slot, Effective stats, ProjectileMotion motion, ref Vector2 aim)
    {
        var origin = run.Player.Position;
        FindNearest(run, origin, stats.Level.Range, stats.Amount, exclude: null);
        if (_candidates.Count == 0)
            return false;

        aim = run.Enemies[_candidates[0].Index].Position;
        var first = (aim - origin).NormalizedOrZero();
        var step = stats.Level.Spread / stats.Amount * MathF.PI / 180f;
        for (var i = 0; i < stats.Amount; i++)
        {
            // One shot per target; extra shots fan out around the nearest one.
            var direction = i < _candidates.Count
                ? (run.Enemies[_candidates[i].Index].Position - origin).NormalizedOrZero()
                : Rotate(first, (i - _candidates.Count + 2) / 2 * step * (i % 2 == 0 ? 1f : -1f));

            ref var p = ref Projectiles.Spawn();
            p.Move = move;
            p.Slot = slot;
            p.Motion = motion;
            p.Position = origin;
            p.Velocity = direction * stats.Speed;
            p.Radius = stats.Radius;
            p.TicksLeft = stats.DurationTicks;
            p.PierceLeft = stats.Level.Pierce <= 0 || motion == ProjectileMotion.Boomerang ? int.MaxValue : stats.Level.Pierce;
            p.TurnPerTick = stats.Level.TurnRate * MathF.PI / 180f / RunClock.TicksPerSecond;
            p.LaunchDirection = direction;

            // Brake so the boomerang turns around after travelling its range: v² = 2·a·d.
            var range = MathF.Max(20f, stats.Level.Range);
            p.BrakePerTick = stats.Speed * stats.Speed / (2f * range) / RunClock.TicksPerSecond;
        }

        return true;
    }

    private bool UseOrbit(RunState run, MoveInstance move, int slot, Effective stats)
    {
        // A permanent orbit (Verdebufera) is cast once; it is only rebuilt when the number of objects changes (Metronomo).
        if (move.Definition.Permanent)
        {
            var circling = 0;
            foreach (ref readonly var existing in Projectiles.Items)
            {
                if (existing.Active && existing.Move == move)
                    circling++;
            }

            if (circling == stats.Amount)
                return false;

            foreach (ref var existing in Projectiles.Items)
            {
                if (existing.Active && existing.Move == move)
                    Projectiles.Release(ref existing);
            }
        }

        var radius = MathF.Max(8f, stats.Range);
        for (var i = 0; i < stats.Amount; i++)
        {
            ref var p = ref Projectiles.Spawn();
            p.Move = move;
            p.Slot = slot;
            p.Motion = ProjectileMotion.Orbit;
            p.OrbitRadius = radius;
            p.AngularSpeed = stats.Speed / radius / RunClock.TicksPerSecond;
            p.Angle = i * MathF.Tau / stats.Amount;
            p.Position = run.Player.Position + new Vector2(MathF.Cos(p.Angle), MathF.Sin(p.Angle)) * radius;
            p.Radius = stats.Radius;
            p.TicksLeft = move.Definition.Permanent ? int.MaxValue : stats.DurationTicks;
            p.PierceLeft = int.MaxValue;
            p.HitIntervalTicks = stats.HitIntervalTicks;
        }

        return true;
    }

    private bool UseAura(RunState run, MoveInstance move, Effective stats)
    {
        var center = run.Player.Position;
        Collect(run, center, stats.Radius);
        foreach (var index in _found)
            run.HitEnemy(index, move, (run.Enemies[index].Position - center).NormalizedOrZero());

        run.AddEvent(new RunEvent(RunEventType.AuraPulse, center, stats.Radius, MoveId: move.Definition.Id));
        return true;
    }

    private bool UseSlash(RunState run, MoveInstance move, int slot, Effective stats, ref Vector2 aim)
    {
        var facing = run.Player.Facing.ToVector();
        aim = run.Player.Position + facing * stats.Range;
        for (var i = 0; i < stats.Amount; i++)
        {
            // Facing side first, then behind, then the two flanks (Vampire Survivors' whip pattern).
            var direction = (i % 4) switch
            {
                0 => facing,
                1 => -facing,
                2 => new Vector2(-facing.Y, facing.X),
                _ => new Vector2(facing.Y, -facing.X),
            };

            if (i == 0)
            {
                DoSlash(run, move, run.Player.Position, direction, stats.Range, stats.Level.Spread);
            }
            else
            {
                Pending.Add(new PendingHit
                {
                    Kind = PendingKind.Slash, Move = move, Slot = slot, StartTick = run.Ticks,
                    DueTick = run.Ticks + i * System.Math.Max(1, RunClock.Seconds(stats.Level.Delay)),
                    Direction = direction, Radius = stats.Range,
                });
            }
        }

        return true;
    }

    private void DoSlash(RunState run, MoveInstance move, Vector2 origin, Vector2 direction, float reach, float spreadDegrees)
    {
        var halfAngleCos = MathF.Cos(spreadDegrees * 0.5f * MathF.PI / 180f);
        Collect(run, origin, reach);
        foreach (var index in _found)
        {
            var offset = run.Enemies[index].Position - origin;
            var length = offset.Length();
            if (length < 4f || Vector2.Dot(offset / length, direction) >= halfAngleCos)
                run.HitEnemy(index, move, direction);
        }

        run.AddEvent(new RunEvent(RunEventType.Slash, origin, spreadDegrees, Target: direction * reach, MoveId: move.Definition.Id));
    }

    private bool UseBeam(RunState run, MoveInstance move, int slot, Effective stats, ref Vector2 aim)
    {
        var origin = run.Player.Position;
        FindNearest(run, origin, stats.Range, 1, exclude: null);
        if (_candidates.Count == 0)
            return false;

        aim = run.Enemies[_candidates[0].Index].Position;
        var direction = (aim - origin).NormalizedOrZero();
        var step = stats.Level.Spread / stats.Amount * MathF.PI / 180f;
        for (var i = 0; i < stats.Amount; i++)
        {
            Beams.Add(new MoveBeam
            {
                Move = move, Slot = slot, Serial = ++_serial,
                Origin = origin,
                Direction = Rotate(direction, (i - (stats.Amount - 1) / 2f) * step),
                Length = stats.Range,
                HalfWidth = stats.Radius,
                TicksLeft = stats.DurationTicks,
                Lifetime = stats.DurationTicks,
                HitIntervalTicks = stats.HitIntervalTicks,
            });
        }

        return true;
    }

    private bool UseStrike(RunState run, MoveInstance move, int slot, Effective stats, ref Vector2 aim)
    {
        PickRandom(run, run.Player.Position, stats.Level.Range, stats.Amount);
        if (_candidates.Count == 0)
            return false;

        aim = run.Enemies[_candidates[0].Index].Position;
        var warning = RunClock.Seconds(stats.Level.Delay);
        for (var i = 0; i < _candidates.Count; i++)
        {
            Pending.Add(new PendingHit
            {
                Kind = PendingKind.Strike, Move = move, Slot = slot, StartTick = run.Ticks,
                DueTick = run.Ticks + warning + i * 4,
                Position = run.Enemies[_candidates[i].Index].Position,
                Radius = stats.Radius,
            });
        }

        return true;
    }

    private bool UseArc(RunState run, MoveInstance move, Effective stats, ref Vector2 aim)
    {
        var origin = run.Player.Position;
        FindNearest(run, origin, stats.Level.Range, stats.Amount, exclude: null);
        if (_candidates.Count == 0)
            return false;

        aim = run.Enemies[_candidates[0].Index].Position;
        var starts = _candidates.Select(c => c.Index).ToList();

        // Several arcs share their hit list, so they spread over different enemies.
        _arcHits.Clear();
        foreach (var start in starts)
        {
            if (_arcHits.Contains(start) || !run.Enemies[start].IsAlive)
                continue;

            var from = origin;
            var target = start;
            for (var jump = 0; jump <= stats.Level.Chains; jump++)
            {
                var to = run.Enemies[target].Position;
                run.AddEvent(new RunEvent(RunEventType.ArcSegment, from, Target: to, MoveId: move.Definition.Id));
                run.HitEnemy(target, move, (to - from).NormalizedOrZero());
                _arcHits.Add(target);

                FindNearest(run, to, stats.Level.ChainRange, 1, _arcHits);
                if (_candidates.Count == 0)
                    break;

                from = to;
                target = _candidates[0].Index;
            }
        }

        return true;
    }

    private bool UseZone(RunState run, MoveInstance move, int slot, Effective stats, ref Vector2 aim)
    {
        var positions = new List<Vector2>();
        if (move.Definition.SpawnAtPlayer)
        {
            // Trail: drop where the player stands.
            for (var i = 0; i < stats.Amount; i++)
                positions.Add(run.Player.Position + new Vector2(run.Random.NextSingle() - 0.5f, run.Random.NextSingle() - 0.5f) * stats.Radius);
        }
        else
        {
            PickRandom(run, run.Player.Position, stats.Level.Range, stats.Amount);
            if (_candidates.Count == 0)
                return false;
            positions.AddRange(_candidates.Select(c => run.Enemies[c.Index].Position));
        }

        aim = positions[0];
        foreach (var position in positions)
        {
            Zones.Add(new MoveZone
            {
                Move = move, Slot = slot, Serial = ++_serial,
                Position = position,
                Radius = stats.Radius,
                TicksLeft = stats.DurationTicks,
                Lifetime = stats.DurationTicks,
                HitIntervalTicks = stats.HitIntervalTicks,
            });
        }

        return true;
    }

    private bool UseRing(RunState run, MoveInstance move, int slot, Effective stats)
    {
        SpawnRing(run, move, slot, stats.Range, stats.Speed, stats.Radius);
        for (var i = 1; i < stats.Amount; i++)
        {
            Pending.Add(new PendingHit
            {
                Kind = PendingKind.Ring, Move = move, Slot = slot, StartTick = run.Ticks,
                DueTick = run.Ticks + i * System.Math.Max(1, RunClock.Seconds(stats.Level.Delay)),
                Radius = stats.Range,
            });
        }

        return true;
    }

    private void SpawnRing(RunState run, MoveInstance move, int slot, float maxRadius, float speed, float halfThickness) =>
        Rings.Add(new MoveRing
        {
            Move = move, Slot = slot, Serial = ++_serial,
            Center = run.Player.Position,
            MaxRadius = maxRadius,
            Growth = speed / RunClock.TicksPerSecond,
            HalfThickness = halfThickness,
        });

    private void UpdateProjectiles(RunState run)
    {
        var player = run.Player;
        var items = Projectiles.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var p = ref items[i];
            if (!p.Active)
                continue;

            if (--p.TicksLeft <= 0)
            {
                Projectiles.Release(ref p);
                continue;
            }

            switch (p.Motion)
            {
                case ProjectileMotion.Homing:
                    Steer(run, ref p);
                    p.Position += p.Velocity * RunClock.Dt;
                    break;

                case ProjectileMotion.Boomerang:
                    if (!p.Returning)
                    {
                        p.Velocity -= p.LaunchDirection * p.BrakePerTick;
                        if (Vector2.Dot(p.Velocity, p.LaunchDirection) <= 0f)
                        {
                            // Turnaround: the way back may hit the same enemies again.
                            p.Returning = true;
                            p.ForgetHits();
                        }
                    }
                    else
                    {
                        var home = player.Position - p.Position;
                        var speed = MathF.Min(p.Velocity.Length() + p.BrakePerTick, p.BrakePerTick * RunClock.TicksPerSecond * 4f + 1f);
                        p.Velocity = home.NormalizedOrZero() * speed;
                        if (home.LengthSquared() <= (player.Radius + 6f) * (player.Radius + 6f))
                        {
                            Projectiles.Release(ref p);
                            continue;
                        }
                    }

                    p.Position += p.Velocity * RunClock.Dt;
                    break;

                case ProjectileMotion.Orbit:
                    p.Angle += p.AngularSpeed;
                    p.Position = player.Position + new Vector2(MathF.Cos(p.Angle), MathF.Sin(p.Angle)) * p.OrbitRadius;
                    break;

                default:
                    p.Position += p.Velocity * RunClock.Dt;
                    break;
            }

            Collect(run, p.Position, p.Radius);
            foreach (var index in _found)
            {
                ref var e = ref run.Enemies[index];
                if (!e.IsAlive)
                    continue;

                if (p.Motion == ProjectileMotion.Orbit)
                {
                    if (!e.TryHit(p.Slot, run.Ticks, p.HitIntervalTicks))
                        continue;

                    var tangent = new Vector2(-MathF.Sin(p.Angle), MathF.Cos(p.Angle));
                    run.HitEnemy(index, p.Move!, tangent);
                    continue;
                }

                if (p.AlreadyHit(e.Serial))
                    continue;

                p.Remember(e.Serial);
                run.HitEnemy(index, p.Move!, p.Velocity.NormalizedOrZero());
                if (p.PierceLeft != int.MaxValue && --p.PierceLeft <= 0)
                {
                    Projectiles.Release(ref p);
                    break;
                }
            }
        }
    }

    /// <summary>Turns a homing shot towards the nearest enemy it has not hit yet, limited by its turn rate.</summary>
    private void Steer(RunState run, ref Projectile p)
    {
        Collect(run, p.Position, HomingSearchRadius);
        var best = -1;
        var bestDistance = float.MaxValue;
        foreach (var index in _found)
        {
            ref var e = ref run.Enemies[index];
            if (p.AlreadyHit(e.Serial))
                continue;

            var d = Vector2.DistanceSquared(e.Position, p.Position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = index;
            }
        }

        if (best < 0)
            return;

        var speed = p.Velocity.Length();
        var current = MathF.Atan2(p.Velocity.Y, p.Velocity.X);
        var wanted = run.Enemies[best].Position - p.Position;
        var target = MathF.Atan2(wanted.Y, wanted.X);
        var delta = MathF.IEEERemainder(target - current, MathF.Tau);
        var turned = current + Math.Clamp(delta, -p.TurnPerTick, p.TurnPerTick);
        p.Velocity = new Vector2(MathF.Cos(turned), MathF.Sin(turned)) * speed;
    }

    private void UpdateZones(RunState run)
    {
        for (var z = Zones.Count - 1; z >= 0; z--)
        {
            var zone = Zones[z];
            if (--zone.TicksLeft <= 0)
            {
                Zones.RemoveAt(z);
                continue;
            }

            Collect(run, zone.Position, zone.Radius);
            foreach (var index in _found)
            {
                ref var e = ref run.Enemies[index];
                if (e.TryHit(zone.Slot, run.Ticks, zone.HitIntervalTicks))
                    run.HitEnemy(index, zone.Move, (e.Position - zone.Position).NormalizedOrZero() * 0.3f);
            }
        }
    }

    private void UpdateRings(RunState run)
    {
        for (var r = Rings.Count - 1; r >= 0; r--)
        {
            var ring = Rings[r];
            ring.Radius += ring.Growth;

            Collect(run, ring.Center, ring.Radius + ring.HalfThickness);
            foreach (var index in _found)
            {
                ref var e = ref run.Enemies[index];
                var distance = Vector2.Distance(e.Position, ring.Center);
                if (MathF.Abs(distance - ring.Radius) <= ring.HalfThickness + e.Radius && ring.HitSerials.Add(e.Serial))
                    run.HitEnemy(index, ring.Move, (e.Position - ring.Center).NormalizedOrZero());
            }

            if (ring.Radius >= ring.MaxRadius)
                Rings.RemoveAt(r);
        }
    }

    private void UpdateBeams(RunState run)
    {
        for (var b = Beams.Count - 1; b >= 0; b--)
        {
            var beam = Beams[b];
            if (--beam.TicksLeft <= 0)
            {
                Beams.RemoveAt(b);
                continue;
            }

            beam.Origin = run.Player.Position;
            var middle = beam.Origin + beam.Direction * (beam.Length / 2f);
            Collect(run, middle, beam.Length / 2f + beam.HalfWidth);
            foreach (var index in _found)
            {
                ref var e = ref run.Enemies[index];
                var offset = e.Position - beam.Origin;
                var along = Vector2.Dot(offset, beam.Direction);
                if (along < -e.Radius || along > beam.Length + e.Radius)
                    continue;

                var across = (offset - beam.Direction * along).Length();
                if (across <= beam.HalfWidth + e.Radius && e.TryHit(beam.Slot, run.Ticks, beam.HitIntervalTicks))
                    run.HitEnemy(index, beam.Move, beam.Direction);
            }
        }
    }

    private void UpdatePending(RunState run)
    {
        for (var i = Pending.Count - 1; i >= 0; i--)
        {
            var pending = Pending[i];
            if (pending.DueTick > run.Ticks)
                continue;

            Pending.RemoveAt(i);
            var stats = Effective.Of(pending.Move, run.Player);
            switch (pending.Kind)
            {
                case PendingKind.Strike:
                    Collect(run, pending.Position, pending.Radius);
                    foreach (var index in _found)
                        run.HitEnemy(index, pending.Move, (run.Enemies[index].Position - pending.Position).NormalizedOrZero());
                    run.AddEvent(new RunEvent(RunEventType.StrikeImpact, pending.Position, pending.Radius, MoveId: pending.Move.Definition.Id));
                    break;

                case PendingKind.Slash:
                    DoSlash(run, pending.Move, run.Player.Position, pending.Direction, pending.Radius, stats.Level.Spread);
                    break;

                case PendingKind.Ring:
                    SpawnRing(run, pending.Move, pending.Slot, stats.Range, stats.Speed, stats.Radius);
                    break;
            }
        }
    }

    /// <summary>Fills <see cref="_found"/> with alive enemies whose body overlaps the circle.</summary>
    private void Collect(RunState run, Vector2 center, float radius)
    {
        _found.Clear();
        var state = new CollectState(run, center, radius, _found);
        run.Grid.Query(center, radius + MaxEnemyRadius, ref state, CollectVisitor);
    }

    private readonly struct CollectState(RunState run, Vector2 center, float radius, List<int> found)
    {
        public readonly RunState Run = run;
        public readonly Vector2 Center = center;
        public readonly float Radius = radius;
        public readonly List<int> Found = found;
    }

    private static void CollectVisitor(int index, ref CollectState state)
    {
        ref var e = ref state.Run.Enemies[index];
        var reach = state.Radius + e.Radius;
        if (e.IsAlive && Vector2.DistanceSquared(e.Position, state.Center) <= reach * reach)
            state.Found.Add(index);
    }

    /// <summary>Fills <see cref="_candidates"/> with up to <paramref name="count"/> alive enemies within range, nearest first.</summary>
    private void FindNearest(RunState run, Vector2 from, float range, int count, HashSet<int>? exclude)
    {
        _candidates.Clear();
        var rangeSquared = range * range;
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            // Moves aim at Pokémon, never at grass or boulders (they still break what they pass through).
            if (!items[i].IsAlive || items[i].Rank == EnemyRank.Prop || (exclude?.Contains(i) ?? false))
                continue;

            var d = Vector2.DistanceSquared(items[i].Position, from);
            if (d <= rangeSquared)
                _candidates.Add((i, d));
        }

        _candidates.Sort(static (a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
        if (_candidates.Count > count)
            _candidates.RemoveRange(count, _candidates.Count - count);
    }

    /// <summary>Fills <see cref="_candidates"/> with up to <paramref name="count"/> distinct random alive enemies within range.</summary>
    private void PickRandom(RunState run, Vector2 from, float range, int count)
    {
        FindNearest(run, from, range, int.MaxValue, exclude: null);
        for (var i = 0; i < System.Math.Min(count, _candidates.Count); i++)
        {
            var j = run.Random.Next(i, _candidates.Count);
            (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
        }

        if (_candidates.Count > count)
            _candidates.RemoveRange(count, _candidates.Count - count);
    }

    private static Vector2 Rotate(Vector2 v, float radians) => Vector2.Transform(v, Matrix3x2.CreateRotation(radians));
}
