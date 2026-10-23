using System.Numerics;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run;

/// <summary>Weighted species entry for a spawn table.</summary>
public readonly record struct SpawnEntry(int SpeciesId, int Weight);

/// <summary>
/// Prototype spawner: keeps a steady trickle of enemies just outside the screen and recycles stragglers.
/// Replaced by the data-driven wave director in phase 3.1.
/// </summary>
public sealed class Spawner(IReadOnlyList<SpawnEntry> table, Vector2 viewSize)
{
    /// <summary>Distance beyond the screen edge where enemies appear.</summary>
    public const float SpawnMargin = 32f;

    private float _accumulator;

    public bool Enabled { get; set; } = true;

    public float SpawnsPerSecond { get; set; } = 2f;

    public int MaxAlive { get; set; } = 300;

    public Vector2 ViewSize { get; } = viewSize;

    /// <summary>Enemies farther than this from the player are moved back next to the screen.</summary>
    public float RecycleDistance => ViewSize.Length() * 0.75f;

    public void Tick(RunState run)
    {
        if (!Enabled)
            return;

        // Gradual ramp so the prototype gets busier over time.
        var minutes = run.Ticks / (float)(RunClock.TicksPerSecond * 60);
        _accumulator += SpawnsPerSecond * (1f + minutes * 0.5f) * RunClock.Dt;
        while (_accumulator >= 1f)
        {
            _accumulator -= 1f;
            if (run.Enemies.AliveCount >= MaxAlive)
                break;
            SpawnOne(run, 1f + minutes * 0.15f, 1f + minutes * 0.1f);
        }

        Recycle(run);
    }

    public int SpawnOne(RunState run, float hpMultiplier = 1f, float xpMultiplier = 1f)
    {
        var species = PickSpecies(run.Random, run.Pokedex);
        return run.Enemies.Spawn(species, OffscreenPoint(run.Random, run.Player.Position), hpMultiplier, xpMultiplier);
    }

    /// <summary>Uniform point on the rectangle just outside the visible area.</summary>
    public Vector2 OffscreenPoint(Random random, Vector2 center)
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

    private void Recycle(RunState run)
    {
        var limitSquared = RecycleDistance * RecycleDistance;
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (e.IsAlive && Vector2.DistanceSquared(e.Position, run.Player.Position) > limitSquared)
            {
                // Reappear on the side the player is heading towards, like Vampire Survivors.
                var heading = run.Player.Velocity.NormalizedOrZero();
                var point = OffscreenPoint(run.Random, run.Player.Position);
                if (heading != Vector2.Zero && Vector2.Dot(point - run.Player.Position, heading) < 0f)
                    point = run.Player.Position - (point - run.Player.Position);
                e.Position = point;
                e.Knockback = Vector2.Zero;
            }
        }
    }

    private SpeciesData PickSpecies(Random random, IReadOnlyList<SpeciesData> pokedex)
    {
        var total = table.Sum(e => e.Weight);
        var roll = random.Next(total);
        foreach (var entry in table)
        {
            if ((roll -= entry.Weight) < 0)
                return pokedex[entry.SpeciesId - 1];
        }

        return pokedex[table[0].SpeciesId - 1];
    }
}
