using System.Numerics;

namespace PokeRogue.Core.Run.Pickups;

/// <summary>Visual size of an experience gem, from its value.</summary>
public enum GemTier : byte
{
    Small,
    Medium,
    Large,
}

public struct Gem
{
    public bool Active;
    public int Serial;
    public Vector2 Position;
    public int Value;
    public bool Magnetized;
    public float Speed;
    public int Age;

    public readonly GemTier Tier => TierOf(Value);

    public static GemTier TierOf(int value) => value >= 10 ? GemTier.Large : value >= 3 ? GemTier.Medium : GemTier.Small;
}

/// <summary>
/// Experience gems on the ground. When the pool is full, new experience is added to a single overflow gem
/// (Vampire Survivors merges excess gems the same way), so nothing is ever lost.
/// </summary>
public sealed class GemPool(int capacity = 400)
{
    private readonly Gem[] _items = new Gem[capacity];
    private int _serial;
    private int _overflow = -1;

    public int Capacity => _items.Length;

    public int ActiveCount { get; private set; }

    public int HighWater { get; private set; }

    public Span<Gem> Items => _items.AsSpan(0, HighWater);

    public ref Gem this[int index] => ref _items[index];

    public void Spawn(Vector2 position, int value)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i].Active)
                continue;

            HighWater = Math.Max(HighWater, i + 1);
            ActiveCount++;
            _items[i] = new Gem { Active = true, Serial = ++_serial, Position = position, Value = value };
            return;
        }

        if (_overflow >= 0 && _items[_overflow].Active)
            _items[_overflow].Value += value;
        else
            _overflow = MergeIntoOldest(value);
    }

    public void Release(int index)
    {
        if (!_items[index].Active)
            return;

        _items[index].Active = false;
        ActiveCount--;
        if (index == _overflow)
            _overflow = -1;
    }

    /// <summary>Pulls every gem towards the player (Condividi Esp. pickup, phase 3.5).</summary>
    public void MagnetizeAll()
    {
        foreach (ref var gem in Items)
        {
            if (gem.Active)
                gem.Magnetized = true;
        }
    }

    private int MergeIntoOldest(int value)
    {
        var oldest = 0;
        for (var i = 1; i < _items.Length; i++)
        {
            if (_items[i].Serial < _items[oldest].Serial)
                oldest = i;
        }

        _items[oldest].Value += value;
        return oldest;
    }
}

/// <summary>A chest on the ground (a Poké, Mega or Ultra Ball). Age counts ticks since it dropped, for its pop-out arc.</summary>
public readonly record struct Chest(int Serial, Vector2 Position, ChestTier Tier, int Age = 0)
{
    /// <summary>The player opens it from this close (plus its own radius).</summary>
    public const float Radius = 8f;

    /// <summary>It cannot be opened while it is still popping out of the fallen Dominante.</summary>
    public const int LandTicks = 40;
}

/// <summary>Magnetises gems inside the pickup radius, flies them to the player and grants experience.</summary>
public sealed class PickupSystem
{
    /// <summary>Base pickup radius in pixels, before Calamita.</summary>
    public const float BasePickupRadius = 30f;

    private const float InitialSpeed = 60f;
    private const float Acceleration = 900f;
    private const float MaxSpeed = 700f;

    public GemPool Gems { get; } = new();

    /// <summary>Chests left by Dominanti (phase 3.3): they wait on the ground until the player walks over one.</summary>
    public List<Chest> Chests { get; } = [];

    public void Tick(RunState run)
    {
        var player = run.Player;
        for (var i = Chests.Count - 1; i >= 0; i--)
        {
            var chest = Chests[i] = Chests[i] with { Age = Chests[i].Age + 1 };
            if (chest.Age < Chest.LandTicks)
                continue;

            var reach = player.Radius + Chest.Radius;
            if (Vector2.DistanceSquared(chest.Position, player.Position) <= reach * reach && !player.IsDown)
            {
                Chests.RemoveAt(i);
                run.OpenChest(chest.Tier, chest.Position);
            }
        }

        var radiusSquared = player.PickupRadius * player.PickupRadius;
        var items = Gems.Items;

        for (var i = 0; i < items.Length; i++)
        {
            ref var gem = ref items[i];
            if (!gem.Active)
                continue;

            gem.Age++;
            var toPlayer = player.Position - gem.Position;
            var distanceSquared = toPlayer.LengthSquared();

            if (!gem.Magnetized)
            {
                if (distanceSquared > radiusSquared)
                    continue;

                gem.Magnetized = true;
                gem.Speed = InitialSpeed;
            }

            var reach = player.Radius + 4f;
            if (distanceSquared <= reach * reach)
            {
                var value = gem.Value;
                var position = gem.Position;
                Gems.Release(i);
                run.CollectExperience(value, position);
                continue;
            }

            gem.Speed = MathF.Min(gem.Speed + Acceleration * RunClock.Dt, MaxSpeed);
            var step = gem.Speed * RunClock.Dt;
            var distance = MathF.Sqrt(distanceSquared);
            gem.Position += toPlayer / distance * MathF.Min(step, distance);
        }
    }
}
