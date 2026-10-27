using System.Numerics;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Run;

/// <summary>Weighted species entry for a spawn table.</summary>
public readonly record struct SpawnEntry(int SpeciesId, int Weight);

/// <summary>
/// The wave director (phase 3.1): plays the level's <see cref="WaveTimeline"/>. Each wave keeps a minimum of enemies alive
/// just outside the screen, plus a steady trickle up to a cap; special events (swarms, rings, stampedes) fire on schedule.
/// Chasers left far behind are moved back next to the screen; travellers leave once they are past it.
/// </summary>
public sealed class Spawner
{
    /// <summary>Distance beyond the screen edge where enemies appear.</summary>
    public const float SpawnMargin = 32f;

    /// <summary>Most enemies a wave refills in one tick, so a cleared screen fills up again over a few frames.</summary>
    private const int RefillPerTick = 4;

    /// <summary>A swarm is a stream this long and this wide (pixels).</summary>
    private const float SwarmLength = 160f;

    private const float SwarmWidth = 70f;

    /// <summary>Travellers cross the screen at most this far to either side of the player.</summary>
    private const float LaneSpread = 80f;

    /// <summary>Travellers that never got past the screen give up after this long.</summary>
    private static readonly int TravellerLifetimeTicks = RunClock.Seconds(30f);

    private readonly int[] _nextEventTick;
    private float _accumulator;

    public Spawner(WaveTimeline timeline, Vector2 viewSize)
    {
        Timeline = timeline;
        ViewSize = viewSize;
        _nextEventTick = timeline.Events.Select(e => e.AtTick).ToArray();
    }

    public WaveTimeline Timeline { get; }

    public bool Enabled { get; set; } = true;

    /// <summary>Stress tests: refill up to at least this many enemies alive, whatever the wave says.</summary>
    public int StressMinAlive { get; set; }

    public Vector2 ViewSize { get; }

    /// <summary>Enemies farther than this from the player are moved back next to the screen.</summary>
    public float RecycleDistance => ViewSize.Length() * 0.75f;

    public void Tick(RunState run)
    {
        if (!Enabled)
            return;

        SpawnWave(run);
        FireEvents(run);
        Recycle(run);
    }

    /// <summary>Times the endless mode's loop has started again (phase 9.3).</summary>
    private int _cycle;

    /// <summary>The level's time the waves and events play now: the run's time, or past the end in an endless run, its loop.</summary>
    public int TimeOf(RunState run) => run.EndlessRules?.Looped(run.Ticks, Timeline.DurationTicks) ?? run.Ticks;

    /// <summary>A new round of the endless loop: its events are due again from its start.</summary>
    private void Rearm(int from)
    {
        for (var i = 0; i < _nextEventTick.Length; i++)
        {
            var e = Timeline.Events[i];
            var next = e.AtTick;
            while (e.EveryTicks > 0 && next < from)
                next += e.EveryTicks;
            _nextEventTick[i] = next >= from && (e.EveryTicks == 0 || next <= e.UntilTick) && next < Timeline.DurationTicks ? next : int.MaxValue;
        }
    }

    /// <summary>Playtests that start the run later: the events before <paramref name="tick"/> never fire, their repeats still do.</summary>
    public void SkipEventsBefore(int tick)
    {
        for (var i = 0; i < _nextEventTick.Length; i++)
        {
            var e = Timeline.Events[i];
            var next = e.AtTick;
            while (e.EveryTicks > 0 && next < tick)
                next += e.EveryTicks;
            _nextEventTick[i] = next >= tick && (e.EveryTicks == 0 || next <= e.UntilTick) ? next : int.MaxValue;
        }
    }

    /// <summary>Cheats and stress tests: one enemy of the current wave (or the first one) just outside the screen.</summary>
    public int SpawnOne(RunState run)
    {
        var wave = Timeline.WaveAt(TimeOf(run)) ?? Timeline.Waves.FirstOrDefault();
        return wave is null ? -1 : SpawnOne(run, wave);
    }

    /// <summary>
    /// A point just outside the visible area, inside the map's walls and clear of its obstacles (phase 7.2). In a corridor
    /// the sides beyond the walls are skipped, so enemies come from its open ends.
    /// </summary>
    public Vector2 OffscreenPoint(Random random, Vector2 center)
    {
        var map = Timeline.Map;
        if (!map.IsConstrained)
            return RawOffscreenPoint(random, center);

        var half = ViewSize / 2f;
        var point = center;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            point = map.Clamp(RawOffscreenPoint(random, center), 16f);
            var offset = point - center;
            if (MathF.Abs(offset.X) > half.X || MathF.Abs(offset.Y) > half.Y)
                break;
        }

        return map.FreePoint(point, 12f);
    }

    /// <summary>Uniform point on the rectangle just outside the visible area.</summary>
    private Vector2 RawOffscreenPoint(Random random, Vector2 center)
    {
        var half = ViewSize / 2f + new Vector2(SpawnMargin);
        var perimeter = 4f * (half.X + half.Y);
        var t = random.NextSingle() * perimeter;

        Vector2 offset;
        if (t < 2f * half.X)
            offset = new Vector2(t - half.X, -half.Y);
        else if ((t -= 2f * half.X) < 2f * half.Y)
            offset = new Vector2(half.X, t - half.Y);
        else if ((t -= 2f * half.Y) < 2f * half.X)
            offset = new Vector2(half.X - t, half.Y);
        else
            offset = new Vector2(-half.X, half.Y - (t - 2f * half.X));

        return center + offset;
    }

    private void SpawnWave(RunState run)
    {
        if (Timeline.WaveAt(TimeOf(run)) is not { } wave)
        {
            _accumulator = 0f;
            return;
        }

        var minAlive = Math.Max(wave.MinAlive, StressMinAlive);
        for (var i = 0; i < RefillPerTick && run.Enemies.AliveCount < minAlive; i++)
            SpawnOne(run, wave);

        // The rate counts seconds of the level's clock, so a wave sends the same crowd however fast the clock runs.
        _accumulator += wave.SpawnsPerSecond * RunClock.Dt * RunClock.ClockSpeed;
        while (_accumulator >= 1f)
        {
            _accumulator -= 1f;
            if (run.Enemies.AliveCount >= wave.MaxAlive)
            {
                _accumulator = 0f;
                break;
            }

            SpawnOne(run, wave);
        }
    }

    /// <summary>One enemy of the wave just off screen; now and then a shiny one (phase 3.5).</summary>
    private int SpawnOne(RunState run, Wave wave)
    {
        var index = Spawn(run, PickSpecies(run.Random, wave, run.IsNight), OffscreenPoint(run.Random, run.Player.Position));
        if (index >= 0 && run.LootRules is { } loot && run.Random.NextSingle() < loot.File.ShinyChance * Timeline.Difficulty.Shiny)
            MakeShiny(run, index);
        return index;
    }

    /// <summary>
    /// A shiny Pokémon: ten times the PS, harder hits, a bit faster, five times the experience, and when it falls a big
    /// nugget and a held item (phase 3.6: three times the PS made it a free prize).
    /// </summary>
    public static void MakeShiny(RunState run, int index)
    {
        ref var e = ref run.Enemies[index];
        e.Shiny = true;
        e.MaxHp *= 10f;
        e.Hp = e.MaxHp;
        e.ContactDamage *= 1.5f;
        e.Speed *= 1.15f;
        e.KnockbackScale *= 0.3f;
        e.XpValue *= 5;
        run.AddEventOnResume(new RunEvent(RunEventType.ShinyAppeared, e.Position, 0f, e.SpeciesId));
    }

    /// <summary>
    /// The horde's PS against the faster clock (<see cref="RunClock.ClockSpeed"/>): the moves still recharge in real
    /// seconds, so at full PS a minute on the clock would see two thirds of the KO it was balanced for. Dominanti and
    /// bosses are duels timed in real seconds and keep theirs.
    /// </summary>
    public const float HordeHp = 1f / RunClock.ClockSpeed;

    private int Spawn(RunState run, int speciesId, Vector2 position, float hpMultiplier = 1f)
    {
        var index = run.SpawnEnemy(speciesId, position, Timeline.HpMultiplier(run.Ticks) * hpMultiplier * HordeHp, Timeline.XpMultiplier(run.Ticks));
        if (index >= 0)
        {
            run.Enemies[index].ContactDamage *= Timeline.DamageMultiplier(run.Ticks);
            run.Enemies[index].Speed *= Timeline.Difficulty.Speed;
        }

        return index;
    }

    /// <summary>Fires each event whose time has come; a time jump (cheat) fires a skipped event once, not once per repeat.</summary>
    private void FireEvents(RunState run)
    {
        // The endless mode (phase 9.3): each round of the loop plays its stretch's events again.
        var now = TimeOf(run);
        if (run.EndlessRules is { } endless && endless.Cycle(run.Ticks, Timeline.DurationTicks) is var cycle && cycle != _cycle)
        {
            _cycle = cycle;
            // At the loop's start, so from its first event (after a time jump, from where it landed).
            if (cycle > 0)
                Rearm(now);
        }

        for (var i = 0; i < _nextEventTick.Length; i++)
        {
            if (now < _nextEventTick[i])
                continue;

            var e = Timeline.Events[i];
            var origin = e.Kind switch
            {
                WaveEventKind.Ring => SpawnRing(run, e),
                WaveEventKind.Dominant => SpawnDominant(run, e),
                _ => SpawnTravellers(run, e),
            };
            run.AddEvent(new RunEvent(RunEventType.WaveEventStarted, origin, (int)e.Kind, e.SpeciesId));

            var next = _nextEventTick[i];
            while (e.EveryTicks > 0 && next <= now)
                next += e.EveryTicks;
            _nextEventTick[i] = e.EveryTicks > 0 && next <= e.UntilTick ? next : int.MaxValue;
        }
    }

    /// <summary>A Dominante walking in from just off screen. Returns where it appears.</summary>
    private Vector2 SpawnDominant(RunState run, WaveEvent e)
    {
        var at = OffscreenPoint(run.Random, run.Player.Position);
        var index = Spawn(run, e.SpeciesId, at, e.Hp / HordeHp);
        if (index >= 0)
            MakeDominant(run, index, e.Chest, e.Speed);
        return at;
    }

    /// <summary>
    /// Turns a freshly spawned enemy into a Dominante: twice the size, hits a quarter harder, barely pushed, ten times the
    /// experience, and a chest when it falls. Its HP comes from the caller. Previews use it too.
    /// </summary>
    public static void MakeDominant(RunState run, int index, ChestTier chest, float speed = 1f)
    {
        ref var enemy = ref run.Enemies[index];
        enemy.Rank = EnemyRank.Dominant;
        enemy.Chest = chest;
        enemy.Radius *= DominantHitboxScale;
        enemy.Speed *= speed;
        enemy.ContactDamage *= 1.25f;
        enemy.KnockbackScale *= 0.1f;
        enemy.XpValue *= 10;
        // Announced with the next tick, so a Dominante spawned between ticks (cheats, previews) is announced too.
        run.AddEventOnResume(new RunEvent(RunEventType.DominantAppeared, enemy.Position, (int)chest, enemy.SpeciesId));
    }

    /// <summary>Dominanti are drawn at this size.</summary>
    public const float DominantScale = 2f;

    /// <summary>Their body is smaller than the picture: PMD frames have room around the Pokémon, and a hitbox twice as big hit from too far.</summary>
    public const float DominantHitboxScale = 1.5f;

    /// <summary>A circle of enemies around the player that closes in. Returns the centre.</summary>
    private Vector2 SpawnRing(RunState run, WaveEvent e)
    {
        var center = run.Player.Position;
        var start = run.Random.NextSingle() * MathF.Tau;
        var map = Timeline.Map;
        for (var k = 0; k < e.Count; k++)
        {
            var angle = start + k * MathF.Tau / e.Count;
            var at = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * e.Radius;
            if (map.IsConstrained)
                at = map.FreePoint(at, 10f);
            var index = Spawn(run, e.SpeciesId, at, e.Hp);
            if (index < 0)
                break;

            ref var enemy = ref run.Enemies[index];
            enemy.Speed *= e.Speed;
            if (e.LifetimeTicks > 0)
                enemy.ExpiresAtTick = run.Ticks + e.LifetimeTicks;
        }

        return center;
    }

    /// <summary>
    /// A swarm (a loose stream) or a stampede (a wall abreast) entering from a random side and travelling straight across,
    /// on a lane that passes close to the player. Returns where it enters.
    /// </summary>
    private Vector2 SpawnTravellers(RunState run, WaveEvent e)
    {
        var random = run.Random;
        var angle = random.NextSingle() * MathF.Tau;
        var heading = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        // In a corridor they run along it, from one open end to the other (phase 7.2).
        var map = Timeline.Map;
        if (map.Spec.Shape == Worlds.MapShape.Corridor)
        {
            var sign = random.Next(2) == 0 ? -1f : 1f;
            heading = map.Spec.Axis == Worlds.CorridorAxis.Horizontal ? new Vector2(sign, 0f) : new Vector2(0f, sign);
        }

        var across = new Vector2(-heading.Y, heading.X);
        var lane = run.Player.Position + across * ((random.NextSingle() - 0.5f) * 2f * LaneSpread);
        var entry = lane - heading * (EdgeDistance(heading) + SpawnMargin);

        var species = run.Pokedex[e.SpeciesId - 1];
        var spacing = StatFormulas.Radius(species) * 2f + 4f;
        var lifetime = e.LifetimeTicks > 0 ? e.LifetimeTicks : TravellerLifetimeTicks;
        for (var k = 0; k < e.Count; k++)
        {
            var offset = e.Kind == WaveEventKind.Swarm
                ? -heading * (random.NextSingle() * SwarmLength) + across * ((random.NextSingle() - 0.5f) * SwarmWidth)
                : across * ((k - (e.Count - 1) / 2f) * spacing) - heading * (random.NextSingle() * 8f);
            var at = entry + offset;
            if (map.IsBounded)
                at = map.Clamp(at, 12f);
            var index = Spawn(run, e.SpeciesId, at, e.Hp);
            if (index < 0)
                break;

            // Travellers keep their course: a Tauros in a stampede does not stop to aim like a lone charger.
            ref var enemy = ref run.Enemies[index];
            enemy.Profile = null;
            enemy.Behavior = EnemyBehavior.Straight;
            enemy.Heading = heading;
            enemy.Speed *= e.Speed;
            enemy.ExpiresAtTick = run.Ticks + lifetime;
        }

        return entry;
    }

    /// <summary>Distance from the centre of the screen to its edge along <paramref name="direction"/> (a unit vector).</summary>
    private float EdgeDistance(Vector2 direction)
    {
        var half = ViewSize / 2f;
        var x = MathF.Abs(direction.X) > 1e-4f ? half.X / MathF.Abs(direction.X) : float.MaxValue;
        var y = MathF.Abs(direction.Y) > 1e-4f ? half.Y / MathF.Abs(direction.Y) : float.MaxValue;
        return MathF.Min(x, y);
    }

    /// <summary>Expired enemies leave, travellers past the screen are gone, and chasers left far behind come back.</summary>
    private void Recycle(RunState run)
    {
        var limitSquared = RecycleDistance * RecycleDistance;
        var player = run.Player.Position;
        var visible = ViewSize / 2f + new Vector2(SpawnMargin);
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (!e.IsAlive || e.Rank == EnemyRank.Prop)
                continue;

            var offset = e.Position - player;
            if (e.ExpiresAtTick > 0 && run.Ticks >= e.ExpiresAtTick)
            {
                // On screen it faints (without a drop or a KO), off screen it just goes.
                if (MathF.Abs(offset.X) < visible.X && MathF.Abs(offset.Y) < visible.Y)
                    run.Enemies.Defeat(i);
                else
                    run.Enemies.Release(i);
                continue;
            }

            if (offset.LengthSquared() <= limitSquared)
                continue;

            if (e.Behavior == EnemyBehavior.Straight)
            {
                // Still on its way in, or already past: once past, it never comes back.
                if (Vector2.Dot(offset, e.Heading) > 0f)
                    run.Enemies.Release(i);
                continue;
            }

            // Reappear on the side the player is heading towards, like Vampire Survivors.
            var heading = run.Player.Velocity.NormalizedOrZero();
            var point = OffscreenPoint(run.Random, player);
            if (heading != Vector2.Zero && Vector2.Dot(point - player, heading) < 0f)
            {
                var ahead = player - (point - player);
                if (!Timeline.Map.IsConstrained || Timeline.Map.Contains(ahead, 16f) && !Timeline.Map.Blocked(ahead, 12f))
                    point = ahead;
            }

            e.Position = point;
            e.Knockback = Vector2.Zero;
        }
    }

    /// <summary>A species of the wave by weight; at night from its night list, when it has one (phase 7.2).</summary>
    private static int PickSpecies(Random random, Wave wave, bool night = false)
    {
        var (species, total) = night && wave.NightSpecies is { } nightSpecies ? (nightSpecies, wave.NightWeight) : (wave.Species, wave.TotalWeight);
        var roll = random.Next(total);
        foreach (var entry in species)
        {
            if ((roll -= entry.Weight) < 0)
                return entry.SpeciesId;
        }

        return wave.Species[0].SpeciesId;
    }
}
