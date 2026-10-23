using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;

namespace PokeRogue.Core.Run;

public enum EnemyState : byte
{
    Inactive,
    Alive,
    Fainting,
}

/// <summary>One horde member. Stored by value in <see cref="EnemyPool"/> to keep thousands of them cache-friendly.</summary>
public struct Enemy
{
    public const int FaintTicks = 36;
    public const int HitFlashTicks = 6;

    public EnemyState State;

    /// <summary>Increments every time the slot is reused, so renderers can reset per-slot animation state.</summary>
    public int Serial;

    public int SpeciesId;
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Knockback;
    public Direction8 Facing;
    public float Radius;
    public float Hp;
    public float MaxHp;
    public float Speed;
    public float ContactDamage;

    /// <summary>Its touch is a special attack (Zinco softens it) rather than a physical one (Ferro).</summary>
    public bool HitsSpecially;

    public float Defense;
    public float SpDefense;
    public PokemonType Type1;
    public PokemonType? Type2;

    public int FlashTicks;
    public int FaintTicksLeft;

    /// <summary>Experience in the gem dropped when defeated.</summary>
    public int XpValue;

    /// <summary>Tick of the last hit from each of the player's move slots, for moves that hit repeatedly.</summary>
    public MoveHitTicks LastHit;

    /// <summary>Current status condition: one at a time, as in the games.</summary>
    public StatusKind Status;

    /// <summary>Ticks until <see cref="Status"/> wears off.</summary>
    public int StatusTicks;

    /// <summary>Ticks until the status' next damage tick or roll.</summary>
    public int StatusTimer;

    /// <summary>Burn: damage of each tick, fixed by the hit that inflicted it.</summary>
    public float StatusDamage;

    /// <summary>Player move slot credited with the status' damage; -1 for none.</summary>
    public int StatusSource;

    /// <summary>Paralysis: ticks left locked up.</summary>
    public int StuckTicks;

    /// <summary>Confusion: the direction it stumbles along (unit vector).</summary>
    public Vector2 Wander;

    public readonly bool IsAlive => State == EnemyState.Alive;

    /// <summary>Frozen, asleep or locked up by paralysis: no walking and no animation.</summary>
    public readonly bool IsImmobilized => Status is StatusKind.Freeze or StatusKind.Sleep || StuckTicks > 0;

    /// <summary>
    /// Vampire Survivors-style per-weapon hit delay: true (and records the hit) when the move in <paramref name="slot"/>
    /// has not hit this enemy within <paramref name="intervalTicks"/>.
    /// </summary>
    public bool TryHit(int slot, int now, int intervalTicks)
    {
        if ((uint)slot >= MoveHitTicks.Slots || now - LastHit[slot] < intervalTicks)
            return false;

        LastHit[slot] = now;
        return true;
    }

    [System.Runtime.CompilerServices.InlineArray(Slots)]
    public struct MoveHitTicks
    {
        public const int Slots = 6;

        private int _first;

        /// <summary>"Never hit" for every slot.</summary>
        public static MoveHitTicks Never()
        {
            var ticks = new MoveHitTicks();
            for (var i = 0; i < Slots; i++)
                ticks[i] = int.MinValue / 2;
            return ticks;
        }
    }
}

/// <summary>Fixed-capacity pool with a free list; slot indices are stable while an enemy lives.</summary>
public sealed class EnemyPool
{
    private readonly Enemy[] _items;
    private readonly Stack<int> _free;
    private int _serial;

    public EnemyPool(int capacity)
    {
        _items = new Enemy[capacity];
        _free = new Stack<int>(Enumerable.Range(0, capacity).Reverse());
    }

    public int Capacity => _items.Length;

    public int AliveCount { get; private set; }

    /// <summary>Highest slot index ever used + 1; loops can stop here.</summary>
    public int HighWater { get; private set; }

    public Span<Enemy> Items => _items.AsSpan(0, HighWater);

    public ref Enemy this[int index] => ref _items[index];

    public int Spawn(SpeciesData species, Vector2 position, float hpMultiplier = 1f, float xpMultiplier = 1f)
    {
        if (_free.Count == 0)
            return -1;

        var index = _free.Pop();
        HighWater = Math.Max(HighWater, index + 1);
        AliveCount++;

        var s = species.BaseStats;
        var hp = StatFormulas.EnemyMaxHp(s) * hpMultiplier;
        _items[index] = new Enemy
        {
            State = EnemyState.Alive,
            Serial = ++_serial,
            SpeciesId = species.Id,
            Position = position,
            Radius = StatFormulas.Radius(species),
            Hp = hp,
            MaxHp = hp,
            Speed = StatFormulas.EnemyMoveSpeed(s),
            ContactDamage = StatFormulas.EnemyContactDamage(s),
            HitsSpecially = StatFormulas.EnemyHitsSpecially(s),
            Defense = s.Defense,
            SpDefense = s.SpDefense,
            Type1 = species.Types[0],
            Type2 = species.Types.Count > 1 ? species.Types[1] : null,
            XpValue = System.Math.Max(1, (int)MathF.Round(StatFormulas.EnemyXp(s) * xpMultiplier)),
            LastHit = Enemy.MoveHitTicks.Never(),
            StatusSource = -1,
        };
        return index;
    }

    /// <summary>Starts the faint animation; the slot is released once it finishes.</summary>
    public void Defeat(int index)
    {
        ref var e = ref _items[index];
        if (e.State != EnemyState.Alive)
            return;

        e.State = EnemyState.Fainting;
        e.FaintTicksLeft = Enemy.FaintTicks;
        e.Velocity = Vector2.Zero;
        e.Status = StatusKind.None;
        e.StuckTicks = 0;
        AliveCount--;
    }

    public void Release(int index)
    {
        ref var e = ref _items[index];
        if (e.State == EnemyState.Inactive)
            return;
        if (e.State == EnemyState.Alive)
            AliveCount--;

        e.State = EnemyState.Inactive;
        _free.Push(index);
    }
}
