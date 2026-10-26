using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run;

public sealed class Player
{
    /// <summary>Invulnerability after taking a hit, in ticks.</summary>
    public const int HitInvulnerabilityTicks = 30;

    public const float BaseCritChance = 0.05f;

    /// <summary>Cooldowns never drop below this fraction, however many Rapidartigli.</summary>
    public const float MinCooldownMultiplier = 0.4f;

    /// <summary>Ferro and Zinco never cut contact damage by more than this.</summary>
    public const float MaxResist = 0.75f;

    /// <summary>Invulnerability after Focalnastro brings the player back, in ticks.</summary>
    public const int ReviveInvulnerabilityTicks = 120;

    /// <summary>
    /// Healing from damage dealt (Sanguisuga, Conchinella) is capped at this share of the max PS per second (phase 3.6):
    /// against a big horde it used to outheal every hit and the player could stand still and win.
    /// </summary>
    public const float DamageHealCapPerSecond = 0.025f;

    /// <summary>PS that damage dealt may still heal; refills with time up to one second's worth.</summary>
    private float _damageHealBudget;

    private float _baseMaxHp;
    private float _baseMoveSpeed;
    private float _physicalPower = 1f;
    private float _specialPower = 1f;
    private float _physicalResist;
    private float _specialResist;
    private int _reviveCharges;

    public Player(SpeciesData species)
    {
        Species = species;
        Radius = StatFormulas.Radius(species);
        _baseMaxHp = StatFormulas.PlayerMaxHp(species.BaseStats);
        _baseMoveSpeed = StatFormulas.PlayerMoveSpeed(species.BaseStats);
        MaxHp = Hp = _baseMaxHp;
        _damageHealBudget = MaxHp * DamageHealCapPerSecond;
        MoveSpeed = _baseMoveSpeed;
        PickupRadius = PickupSystem.BasePickupRadius;
    }

    public SpeciesData Species { get; private set; }

    public List<MoveInstance> Moves { get; } = [];

    public List<ItemInstance> Items { get; } = [];

    /// <summary>The character's own modifiers (characters.json), added to the items' bonuses by <see cref="RecalculateStats"/>.</summary>
    public ItemBonus CharacterBonus { get; set; } = ItemBonus.None;

    /// <summary>Trasformazione (Ditto, phase 4.3): the species it is copying for a while; null when it is itself.</summary>
    public SpeciesData? Disguise { get; private set; }

    /// <summary>What the player looks and fights like: its species, or the one Ditto copied.</summary>
    public SpeciesData Form => Disguise ?? Species;

    /// <summary>Multitipo (Arceus): the type it took from its strongest move; null keeps the species' types.</summary>
    public PokemonType? TypeOverride { get; set; }

    /// <summary>Speed from abilities of the moment (Acceleratore, Agiltecnica, Lentoinizio…), on top of <see cref="MoveSpeed"/>.</summary>
    public float AbilitySpeedMultiplier { get; set; } = 1f;

    /// <summary>Cooldowns from abilities of the moment (Acceleratore), on top of <see cref="CooldownMultiplier"/>.</summary>
    public float AbilityCooldownMultiplier { get; set; } = 1f;

    public AttackerProfile Attacker => new(
        TypeOverride ?? Form.Types[0],
        TypeOverride is not null ? null : Form.Types.Count > 1 ? Form.Types[1] : null,
        Form.BaseStats.Attack,
        Form.BaseStats.SpAttack,
        CritChance);

    /// <summary>The player's types right now (Multitipo and Trasformazione change them).</summary>
    public IReadOnlyList<PokemonType> Types => TypeOverride is { } type ? [type] : Form.Types;

    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; private set; }

    public Direction8 Facing { get; private set; } = Direction8.Down;

    public bool IsMoving => Velocity.LengthSquared() > 1e-4f;

    public float Radius { get; private set; }

    public float MaxHp { get; private set; }

    public float Hp { get; set; }

    public float MoveSpeed { get; private set; }

    public float Defense => Form.BaseStats.Defense;

    public float SpDefense => Form.BaseStats.SpDefense;

    /// <summary>Chance of a critical hit (Mirino adds to it).</summary>
    public float CritChance { get; private set; } = BaseCritChance;

    /// <summary>Multiplier on coins gained (Monetamuleto).</summary>
    public float CoinMultiplier { get; private set; } = 1f;

    /// <summary>Share of the damage dealt by moves that heals the player (Conchinella).</summary>
    public float Lifesteal { get; private set; }

    /// <summary>Times Focalnastro has already brought the player back this run.</summary>
    public int RevivesUsed { get; private set; }

    /// <summary>Revivals still available: one per Focalnastro level, minus those used.</summary>
    public int RevivesLeft => System.Math.Max(0, _reviveCharges - RevivesUsed);

    /// <summary>PS recovered per second (Avanzi).</summary>
    public float Regen { get; private set; }

    /// <summary>Distance at which gems start flying to the player (Calamita).</summary>
    public float PickupRadius { get; private set; }

    /// <summary>Experience multiplier (Fortunuovo).</summary>
    public float Growth { get; private set; } = 1f;

    /// <summary>Multiplier on move cooldowns (Rapidartigli).</summary>
    public float CooldownMultiplier { get; private set; } = 1f;

    /// <summary>Multiplier on move sizes: hit radius, orbit radius, ring size, slash reach (Grandelente).</summary>
    public float AreaMultiplier { get; private set; } = 1f;

    /// <summary>Multiplier on how long orbits, beams and zones last (Creta Luce).</summary>
    public float DurationMultiplier { get; private set; } = 1f;

    /// <summary>Extra projectiles, orbs, strikes, zones or slashes per use (Metronomo).</summary>
    public int AmountBonus { get; private set; }

    public float ProjectileSpeedMultiplier { get; private set; } = 1f;

    public int Level { get; private set; } = 1;

    /// <summary>Experience gathered towards the next level.</summary>
    public float Xp { get; private set; }

    public int XpToNext => XpCurve.ToNextLevel(Level);

    public int Coins { get; set; }

    /// <summary>PS recovered this run (Avanzi, Baccarancia), for the results screen.</summary>
    public float HpRecovered { get; private set; }

    public int InvulnerableTicks { get; private set; }

    public bool Invincible { get; set; }

    public bool IsDown => Hp <= 0f;

    /// <summary>A hit would land now: not invincible, not blinking after a hit, not knocked out.</summary>
    public bool CanBeHurt => !Invincible && InvulnerableTicks <= 0 && !IsDown;

    /// <summary>A hit was dodged or blocked (Mantelneve, Muta): the usual blink after a hit, without the damage.</summary>
    public void Shrug() => InvulnerableTicks = HitInvulnerabilityTicks;

    /// <summary>Teletrasporto: a moment of invulnerability on arrival.</summary>
    public void Blink(int ticks) => InvulnerableTicks = Math.Max(InvulnerableTicks, ticks);

    public float PowerMultiplier(MoveCategory category) => category switch
    {
        MoveCategory.Physical => _physicalPower,
        MoveCategory.Special => _specialPower,
        _ => 1f,
    };

    /// <summary>Moves with the given input (length ≤ 1, screen space).</summary>
    public void Move(Vector2 input)
    {
        Velocity = input.ClampLength(1f) * MoveSpeed * AbilitySpeedMultiplier;
        Position += Velocity * RunClock.Dt;
        Facing = Direction8Extensions.FromVector(input, Facing);
    }

    public void Tick()
    {
        if (InvulnerableTicks > 0)
            InvulnerableTicks--;
        if (Regen > 0f)
            Heal(Regen * RunClock.Dt);

        var cap = MaxHp * DamageHealCapPerSecond;
        _damageHealBudget = MathF.Min(cap, _damageHealBudget + cap * RunClock.Dt);
    }

    /// <summary>Healing from damage dealt (drain, lifesteal), within <see cref="DamageHealCapPerSecond"/>.</summary>
    public float HealFromDamage(float amount)
    {
        var healed = Heal(MathF.Min(amount, _damageHealBudget));
        _damageHealBudget -= healed;
        return healed;
    }

    /// <summary>Restores PS up to the maximum (never revives) and returns how much was actually recovered.</summary>
    public float Heal(float amount)
    {
        if (IsDown || amount <= 0f)
            return 0f;

        var healed = MathF.Min(amount, MaxHp - Hp);
        Hp += healed;
        HpRecovered += healed;
        return healed;
    }

    /// <summary>
    /// Applies contact damage unless invulnerable: a <paramref name="special"/> touch meets Special Defense and Zinco,
    /// a physical one Defense and Ferro. Returns the damage actually taken.
    /// </summary>
    public float TakeContactDamage(float rawDamage, bool special = false)
    {
        if (Invincible || InvulnerableTicks > 0 || IsDown)
            return 0f;

        var defense = special ? SpDefense : Defense;
        var resist = special ? _specialResist : _physicalResist;
        var damage = MathF.Max(1f, MathF.Round(rawDamage * StatFormulas.DefenseFactor(defense) * (1f - resist)));
        Hp = MathF.Max(0f, Hp - damage);
        InvulnerableTicks = HitInvulnerabilityTicks;
        return damage;
    }

    /// <summary>Focalnastro: once knocked out, stand back up with half the PS, if a revival is left. Returns whether it happened.</summary>
    public bool TryRevive()
    {
        if (!IsDown || RevivesLeft == 0)
            return false;

        RevivesUsed++;
        Hp = MathF.Round(MaxHp * 0.5f);
        InvulnerableTicks = ReviveInvulnerabilityTicks;
        return true;
    }

    /// <summary>Adds experience (scaled by <see cref="Growth"/>) and returns how many levels were gained.</summary>
    public int AddXp(float amount)
    {
        Xp += amount * Growth;
        var levels = 0;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            levels++;
        }

        return levels;
    }

    /// <summary>
    /// Becomes <paramref name="species"/> (phase 4.2): size, types and stats follow it; items and moves stay. The extra
    /// max PS come already healed, as in the games.
    /// </summary>
    public void Evolve(SpeciesData species)
    {
        Species = species;
        Radius = StatFormulas.Radius(species);
        _baseMaxHp = StatFormulas.PlayerMaxHp(species.BaseStats);
        _baseMoveSpeed = StatFormulas.PlayerMoveSpeed(Form.BaseStats);
        RecalculateStats();
    }

    /// <summary>
    /// Trasformazione: takes <paramref name="species"/>' look, types, attack, defence and speed (null turns back). PS stay
    /// the player's own, as in the games.
    /// </summary>
    public void SetDisguise(SpeciesData? species)
    {
        Disguise = species;
        _baseMoveSpeed = StatFormulas.PlayerMoveSpeed(Form.BaseStats);
        RecalculateStats();
    }

    /// <summary>Re-derives every item-dependent stat. Gaining max PS also heals by the same amount.</summary>
    public void RecalculateStats()
    {
        var bonus = Items.Aggregate(CharacterBonus, (sum, item) => sum + item.Bonus);

        var newMaxHp = MathF.Round(_baseMaxHp * (1f + bonus.MaxHpPercent));
        if (newMaxHp > MaxHp)
            Hp += newMaxHp - MaxHp;
        MaxHp = newMaxHp;
        Hp = MathF.Min(Hp, MaxHp);

        MoveSpeed = _baseMoveSpeed * (1f + bonus.MoveSpeedPercent);
        Regen = bonus.RegenPerSecond;
        PickupRadius = PickupSystem.BasePickupRadius * (1f + bonus.PickupRadiusPercent);
        _physicalPower = 1f + bonus.PhysicalPowerPercent;
        _specialPower = 1f + bonus.SpecialPowerPercent;
        CooldownMultiplier = MathF.Max(MinCooldownMultiplier, 1f - bonus.CooldownReductionPercent);
        Growth = 1f + bonus.GrowthPercent;
        AreaMultiplier = 1f + bonus.AreaPercent;
        DurationMultiplier = 1f + bonus.DurationPercent;
        AmountBonus = bonus.AmountBonus;
        ProjectileSpeedMultiplier = 1f + bonus.ProjectileSpeedPercent;
        _physicalResist = MathF.Min(MaxResist, bonus.PhysicalResistPercent);
        _specialResist = MathF.Min(MaxResist, bonus.SpecialResistPercent);
        CoinMultiplier = 1f + bonus.CoinPercent;
        CritChance = BaseCritChance + bonus.CritChance;
        _reviveCharges = bonus.Revives;
        Lifesteal = bonus.LifestealPercent;
    }
}
