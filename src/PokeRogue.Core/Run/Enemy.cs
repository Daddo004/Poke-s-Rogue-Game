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

/// <summary>How an enemy moves and fights (phases 3.1 and 3.2).</summary>
public enum EnemyBehavior : byte
{
    /// <summary>Walks towards the player.</summary>
    Chase,

    /// <summary>Keeps going along <see cref="Enemy.Heading"/>, whatever the player does (swarms, stampedes).</summary>
    Straight,

    /// <summary>Keeps its distance and shoots at the player now and then.</summary>
    Ranged,

    /// <summary>Walks closer, stops to take aim, then charges in a straight line and stands dazed.</summary>
    Charger,

    /// <summary>Walks towards the player, slow and sturdy, and barely moves when hit.</summary>
    Tank,

    /// <summary>Rushes the player, lights its fuse when close and blows up (Voltorb's Self-Destruct).</summary>
    Kamikaze,

    /// <summary>Keeps its distance and teleports around the player (Abra).</summary>
    Teleport,
}

/// <summary>Regular horde member, mini boss (phase 3.3) or boss (phase 3.4).</summary>
public enum EnemyRank : byte
{
    Normal,

    /// <summary>A "Dominante": twice the size, an aura, lots of HP; drops a chest.</summary>
    Dominant,

    /// <summary>The level boss (phase 3.4): beating it wins the level.</summary>
    Boss,

    /// <summary>
    /// Not a Pokémon: tall grass or a boulder that the player's moves break (phase 3.5). It never moves or attacks,
    /// moves never aim at it, and it does not count as an enemy alive.
    /// </summary>
    Prop,
}

/// <summary>What a breakable prop is (phase 3.5).</summary>
public enum BreakableKind : byte
{
    None,
    Grass,
    Rock,
}

/// <summary>The chest a Dominante drops: a Poké, Mega or Ultra Ball with 1, 3 or 5 rewards.</summary>
public enum ChestTier : byte
{
    Poke,
    Great,
    Ultra,
}

/// <summary>What a fighting enemy is busy with; <see cref="None"/> means it follows its behaviour's walk.</summary>
public enum EnemyAction : byte
{
    None,

    /// <summary>Ranged: standing still, about to shoot.</summary>
    Aim,

    /// <summary>Charger: standing still, aiming the charge along <see cref="Enemy.ActionDirection"/>.</summary>
    WindUp,

    /// <summary>Charger: rushing along <see cref="Enemy.ActionDirection"/>.</summary>
    Dash,

    /// <summary>Charger: dazed after a charge.</summary>
    Recover,

    /// <summary>Kamikaze: the fuse is burning.</summary>
    Fuse,

    /// <summary>Teleport: shrinking away before the hop.</summary>
    Vanish,

    /// <summary>Teleport: growing back where it landed.</summary>
    Appear,

    /// <summary>Boss: roaring at the start of a new phase, out of reach.</summary>
    Roar,

    /// <summary>Boss: calling down rocks.</summary>
    Slam,
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

    public EnemyBehavior Behavior;

    /// <summary>Straight: the direction it travels (unit vector).</summary>
    public Vector2 Heading;

    /// <summary>Tick when it leaves the field without a drop (a ring burrows back); 0 = never.</summary>
    public int ExpiresAtTick;

    /// <summary>Its fighting profile in <see cref="RunState.EnemyRules"/> (phase 3.2); null for plain chasers and travellers.</summary>
    public Enemies.EnemyProfile? Profile;

    public EnemyAction Action;

    /// <summary>Ticks left in <see cref="Action"/>.</summary>
    public int ActionTicks;

    /// <summary>Ticks before it may shoot, charge or teleport again.</summary>
    public int CooldownTicks;

    /// <summary>Charge direction (unit vector).</summary>
    public Vector2 ActionDirection;

    /// <summary>Multiplies the knockback of hits (tanks take less).</summary>
    public float KnockbackScale;

    public EnemyRank Rank;

    /// <summary>Dominanti: the chest left when defeated.</summary>
    public ChestTier Chest;

    /// <summary>Props: grass or rock, and the map cell it belongs to (a broken prop does not come back).</summary>
    public BreakableKind Breakable;

    public long PropCell;

    /// <summary>A shiny Pokémon (phase 3.5): rare, sturdier, and it leaves a big nugget.</summary>
    public bool Shiny;

    /// <summary>Statuses never freeze or put it to sleep, and others last half as long (Dominanti and bosses).</summary>
    public readonly bool ResistsStatus => Rank != EnemyRank.Normal;

    /// <summary>A roaring boss cannot be hurt.</summary>
    public readonly bool IsShielded => Rank == EnemyRank.Boss && Action == EnemyAction.Roar;

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

    /// <summary>Ventose (phase 4.3): ticks left walking at half speed after touching the player.</summary>
    public int SlowTicks;

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
            KnockbackScale = 1f,
        };
        return index;
    }

    /// <summary>A breakable prop (phase 3.5): a still object the player's moves can hit. It does not count in <see cref="AliveCount"/>.</summary>
    public int SpawnProp(BreakableKind kind, Vector2 position, float hp, float defense, PokemonType type, long cell)
    {
        if (_free.Count == 0)
            return -1;

        var index = _free.Pop();
        HighWater = Math.Max(HighWater, index + 1);
        _items[index] = new Enemy
        {
            State = EnemyState.Alive,
            Serial = ++_serial,
            Rank = EnemyRank.Prop,
            Breakable = kind,
            PropCell = cell,
            Position = position,
            Radius = kind == BreakableKind.Rock ? 9f : 7f,
            Hp = hp,
            MaxHp = hp,
            Defense = defense,
            SpDefense = defense,
            Type1 = type,
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
        e.Action = EnemyAction.None;
        if (e.Rank != EnemyRank.Prop)
            AliveCount--;
    }

    public void Release(int index)
    {
        ref var e = ref _items[index];
        if (e.State == EnemyState.Inactive)
            return;
        if (e.State == EnemyState.Alive && e.Rank != EnemyRank.Prop)
            AliveCount--;

        e.State = EnemyState.Inactive;
        _free.Push(index);
    }
}
