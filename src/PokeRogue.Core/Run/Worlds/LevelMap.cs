using System.Numerics;

namespace PokeRogue.Core.Run.Worlds;

/// <summary>The outline of a level's map (phase 7.2).</summary>
public enum MapShape
{
    /// <summary>Endless in every direction, as in Vampire Survivors' Mad Forest.</summary>
    Open,

    /// <summary>Endless along one axis, walled across it (the Inlaid Library).</summary>
    Corridor,

    /// <summary>A walled rectangle around the start (a tower floor, a power plant).</summary>
    Arena,
}

public enum CorridorAxis
{
    Horizontal,
    Vertical,
}

/// <summary>A level's map as written in its waves file: the floor's look, the outline and the obstacles (phase 7.2).</summary>
public sealed record MapSpec
{
    /// <summary>The floor's look, drawn by the game (Game/Graphics/Floor/FloorStyle): "bosco", "grotta", "torre", …</summary>
    public string Tileset { get; set; } = "bosco";

    public MapShape Shape { get; set; } = MapShape.Open;

    /// <summary>Corridor: which way it runs.</summary>
    public CorridorAxis Axis { get; set; } = CorridorAxis.Horizontal;

    /// <summary>Corridor: its width across, in pixels. Arena: its width.</summary>
    public float Width { get; set; }

    /// <summary>Arena: its height.</summary>
    public float Height { get; set; }

    /// <summary>Chance that a cell of the obstacle grid holds an obstacle (a tree, a stalagmite, a gravestone…); 0 for none.</summary>
    public float Obstacles { get; set; }

    /// <summary>The obstacle grid's cell, in pixels: at most one obstacle each, so they never touch.</summary>
    public float ObstacleCell { get; set; } = 112f;

    /// <summary>An obstacle's body before its own variation (×0.85 to ×1.25).</summary>
    public float ObstacleRadius { get; set; } = 11f;

    /// <summary>Changes where the obstacles stand.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>Multiply loot.json's chances of tall grass and boulders on this map (0: none of them).</summary>
    public float Grass { get; set; } = 1f;

    public float Rock { get; set; } = 1f;
}

/// <summary>Something solid on the map: Pokémon walk around it (moves and shots fly over it).</summary>
public readonly record struct Obstacle(Vector2 Position, float Radius, int Variant);

/// <summary>
/// A level's map in the simulation (phase 7.2): walls for corridors and arenas, and round obstacles laid out on a grid by a
/// hash of each cell, so they are the same every time and need no storage, however far the player walks. The start and
/// the things lying on the map are kept clear. <see cref="Resolve"/> pushes a body out of both.
/// </summary>
public sealed class LevelMap
{
    /// <summary>No obstacle stands this close to where the player begins.</summary>
    public const float StartClearance = 140f;

    /// <summary>Nor this close to a thing lying on the map, nor this close to a wall.</summary>
    private const float ItemClearance = 36f;

    private const float WallLane = 28f;

    private readonly Vector2[] _keepClear;

    public LevelMap(MapSpec spec, IEnumerable<Vector2>? keepClear = null)
    {
        Spec = spec;
        _keepClear = keepClear?.ToArray() ?? [];
        var inf = float.PositiveInfinity;
        (Min, Max) = spec.Shape switch
        {
            MapShape.Corridor when spec.Axis == CorridorAxis.Horizontal => (new Vector2(-inf, -spec.Width / 2f), new Vector2(inf, spec.Width / 2f)),
            MapShape.Corridor => (new Vector2(-spec.Width / 2f, -inf), new Vector2(spec.Width / 2f, inf)),
            MapShape.Arena => (new Vector2(-spec.Width / 2f, -spec.Height / 2f), new Vector2(spec.Width / 2f, spec.Height / 2f)),
            _ => (new Vector2(-inf), new Vector2(inf)),
        };
    }

    /// <summary>Endless, without obstacles: the map of tests and previews.</summary>
    public static LevelMap Open { get; } = new(new MapSpec());

    public MapSpec Spec { get; }

    /// <summary>The walls' corners; infinite where there is no wall.</summary>
    public Vector2 Min { get; }

    public Vector2 Max { get; }

    public bool IsBounded => Spec.Shape != MapShape.Open;

    public bool HasObstacles => Spec.Obstacles > 0f;

    /// <summary>Anything to keep bodies out of.</summary>
    public bool IsConstrained => IsBounded || HasObstacles;

    /// <summary>Inside the walls, at least <paramref name="margin"/> from them.</summary>
    public bool Contains(Vector2 p, float margin = 0f) =>
        p.X >= Min.X + margin && p.X <= Max.X - margin && p.Y >= Min.Y + margin && p.Y <= Max.Y - margin;

    /// <summary>The nearest point inside the walls, at least <paramref name="margin"/> from them.</summary>
    public Vector2 Clamp(Vector2 p, float margin = 0f) => new(
        Math.Clamp(p.X, Min.X + margin, MathF.Max(Min.X + margin, Max.X - margin)),
        Math.Clamp(p.Y, Min.Y + margin, MathF.Max(Min.Y + margin, Max.Y - margin)));

    /// <summary>The obstacle of grid cell (<paramref name="cx"/>, <paramref name="cy"/>), if it has one.</summary>
    public bool TryObstacle(int cx, int cy, out Obstacle obstacle)
    {
        obstacle = default;
        if (Spec.Obstacles <= 0f)
            return false;

        var seed = Spec.Seed * 7919;
        if (Hash(cx, cy, seed) % 10000 / 10000f >= Spec.Obstacles)
            return false;

        // Inside the middle of its cell, so neighbours never touch.
        var cell = Spec.ObstacleCell;
        var at = new Vector2(
            (cx + 0.25f + 0.5f * (Hash(cx, cy, seed + 1) % 1000 / 1000f)) * cell,
            (cy + 0.25f + 0.5f * (Hash(cx, cy, seed + 2) % 1000 / 1000f)) * cell);
        var radius = Spec.ObstacleRadius * (0.85f + 0.4f * (Hash(cx, cy, seed + 3) % 1000 / 1000f));
        if (at.LengthSquared() < StartClearance * StartClearance || !Contains(at, radius + WallLane))
            return false;

        foreach (var point in _keepClear)
        {
            var clear = ItemClearance + radius;
            if (Vector2.DistanceSquared(point, at) < clear * clear)
                return false;
        }

        obstacle = new Obstacle(at, radius, Hash(cx, cy, seed + 4) % 4);
        return true;
    }

    /// <summary>The obstacles whose centre lies in the area (renderers, the pause map).</summary>
    public IEnumerable<Obstacle> ObstaclesIn(float left, float top, float right, float bottom)
    {
        if (!HasObstacles)
            yield break;

        var cell = Spec.ObstacleCell;
        for (var cy = (int)MathF.Floor(top / cell); cy <= (int)MathF.Floor(bottom / cell); cy++)
        {
            for (var cx = (int)MathF.Floor(left / cell); cx <= (int)MathF.Floor(right / cell); cx++)
            {
                if (TryObstacle(cx, cy, out var obstacle))
                    yield return obstacle;
            }
        }
    }

    /// <summary>Whether a body of <paramref name="radius"/> at <paramref name="p"/> overlaps a wall or an obstacle.</summary>
    public bool Blocked(Vector2 p, float radius)
    {
        var moved = p;
        return Resolve(ref moved, radius) != Vector2.Zero;
    }

    /// <summary>
    /// Pushes a body of <paramref name="radius"/> out of the obstacles around it and back inside the walls. Returns how
    /// far it was pushed (zero when it touched nothing), so walkers can slide around what stopped them.
    /// </summary>
    public Vector2 Resolve(ref Vector2 position, float radius)
    {
        var before = position;
        if (HasObstacles)
        {
            var cell = Spec.ObstacleCell;
            var cx = (int)MathF.Floor(position.X / cell);
            var cy = (int)MathF.Floor(position.Y / cell);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!TryObstacle(cx + dx, cy + dy, out var o))
                        continue;

                    var reach = o.Radius + radius;
                    var offset = position - o.Position;
                    var distanceSquared = offset.LengthSquared();
                    if (distanceSquared >= reach * reach)
                        continue;

                    var distance = MathF.Sqrt(distanceSquared);
                    var normal = distance > 1e-4f ? offset / distance : Vector2.UnitY;
                    position = o.Position + normal * reach;
                }
            }
        }

        if (IsBounded)
            position = Clamp(position, radius);
        return position - before;
    }

    /// <summary>
    /// The nearest free spot to <paramref name="p"/> for a body of <paramref name="radius"/>: inside the walls and out of
    /// any obstacle (spawns, teleports, things dropped on the map).
    /// </summary>
    public Vector2 FreePoint(Vector2 p, float radius)
    {
        for (var i = 0; i < 3; i++)
        {
            if (Resolve(ref p, radius) == Vector2.Zero)
                break;
        }

        return p;
    }

    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }
}
