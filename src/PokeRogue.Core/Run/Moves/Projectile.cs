using System.Numerics;

namespace PokeRogue.Core.Run.Moves;

public enum ProjectileMotion : byte
{
    Straight,
    Homing,
    Boomerang,
    Orbit,
}

public struct Projectile
{
    /// <summary>Enemies already hit, by pool slot serial, so a pierce never hits the same enemy twice.</summary>
    public const int MaxRemembered = 8;

    public bool Active;
    public int Serial;
    public MoveInstance? Move;

    /// <summary>Index of the move in the player's list (per-enemy hit delays are tracked per slot).</summary>
    public int Slot;

    public ProjectileMotion Motion;
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public int TicksLeft;

    /// <summary>Hits left before vanishing; <see cref="int.MaxValue"/> for unlimited.</summary>
    public int PierceLeft;

    public int HitCount;
    public HitBuffer Hits;

    /// <summary>Homing: radians per tick.</summary>
    public float TurnPerTick;

    /// <summary>Boomerang: launch direction, braking per tick and whether it is on the way back.</summary>
    public Vector2 LaunchDirection;
    public float BrakePerTick;
    public bool Returning;

    /// <summary>Orbit: angle, angular speed (radians per tick), distance from the player, re-hit delay.</summary>
    public float Angle;
    public float AngularSpeed;
    public float OrbitRadius;
    public int HitIntervalTicks;

    [System.Runtime.CompilerServices.InlineArray(MaxRemembered)]
    public struct HitBuffer
    {
        private int _first;
    }

    public readonly bool AlreadyHit(int enemySerial)
    {
        for (var i = 0; i < Math.Min(HitCount, MaxRemembered); i++)
        {
            if (Hits[i] == enemySerial)
                return true;
        }

        return false;
    }

    public void Remember(int enemySerial)
    {
        Hits[HitCount % MaxRemembered] = enemySerial;
        HitCount++;
    }

    public void ForgetHits() => HitCount = 0;
}

public sealed class ProjectilePool(int capacity)
{
    private readonly Projectile[] _items = new Projectile[capacity];
    private int _serial;

    public int HighWater { get; private set; }

    public int ActiveCount { get; private set; }

    public Span<Projectile> Items => _items.AsSpan(0, HighWater);

    public ref Projectile Spawn()
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i].Active)
                continue;

            HighWater = Math.Max(HighWater, i + 1);
            ActiveCount++;
            _items[i] = new Projectile { Active = true, Serial = ++_serial };
            return ref _items[i];
        }

        // Pool exhausted: recycle slot 0 rather than failing mid-fight.
        _items[0] = new Projectile { Active = true, Serial = ++_serial };
        return ref _items[0];
    }

    public void Release(ref Projectile projectile)
    {
        if (!projectile.Active)
            return;

        projectile.Active = false;
        ActiveCount--;
    }
}

/// <summary>A damaging circle on the ground (Zone) that hits enemies inside at the move's hit interval.</summary>
public sealed class MoveZone
{
    public required MoveInstance Move { get; init; }

    public required int Slot { get; init; }

    public required int Serial { get; init; }

    public Vector2 Position { get; set; }

    public float Radius { get; set; }

    public int TicksLeft { get; set; }

    public int Lifetime { get; init; }

    public int HitIntervalTicks { get; init; }
}

/// <summary>An expanding shock wave (Ring) that hits each enemy once as its edge passes.</summary>
public sealed class MoveRing
{
    public required MoveInstance Move { get; init; }

    public required int Slot { get; init; }

    public required int Serial { get; init; }

    public Vector2 Center { get; init; }

    public float Radius { get; set; }

    public float MaxRadius { get; init; }

    /// <summary>Pixels per tick.</summary>
    public float Growth { get; init; }

    /// <summary>Half the width of the band that hits.</summary>
    public float HalfThickness { get; init; }

    public HashSet<int> HitSerials { get; } = [];
}

/// <summary>A beam (Beam) from the player along a fixed direction, hitting at the move's hit interval.</summary>
public sealed class MoveBeam
{
    public required MoveInstance Move { get; init; }

    public required int Slot { get; init; }

    public required int Serial { get; init; }

    public Vector2 Origin { get; set; }

    public Vector2 Direction { get; init; }

    public float Length { get; init; }

    public float HalfWidth { get; init; }

    public int TicksLeft { get; set; }

    public int Lifetime { get; init; }

    public int HitIntervalTicks { get; init; }
}

public enum PendingKind : byte
{
    /// <summary>A strike from the sky lands.</summary>
    Strike,

    /// <summary>A follow-up slash of a multi-slash move.</summary>
    Slash,

    /// <summary>A follow-up ring of a multi-ring move.</summary>
    Ring,
}

/// <summary>Something scheduled a few ticks later: strike warnings, repeated slashes and rings.</summary>
public sealed class PendingHit
{
    public required PendingKind Kind { get; init; }

    public required MoveInstance Move { get; init; }

    public required int Slot { get; init; }

    public required int DueTick { get; init; }

    /// <summary>Tick it was scheduled, so renderers can show the warning growing.</summary>
    public required int StartTick { get; init; }

    public Vector2 Position { get; init; }

    public Vector2 Direction { get; init; }

    public float Radius { get; init; }
}
