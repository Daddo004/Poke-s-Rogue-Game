namespace PokeRogue.Core.Run.Items;

/// <summary>Stat bonuses granted by one level of a passive item. Percentages are fractions (0.1 = +10%).</summary>
public sealed record ItemBonus(
    float MaxHpPercent = 0f,
    float MoveSpeedPercent = 0f,
    float RegenPerSecond = 0f,
    float PickupRadiusPercent = 0f,
    float PhysicalPowerPercent = 0f,
    float SpecialPowerPercent = 0f,
    float CooldownReductionPercent = 0f,
    float GrowthPercent = 0f,
    float AreaPercent = 0f,
    float DurationPercent = 0f,
    int AmountBonus = 0,
    float ProjectileSpeedPercent = 0f,
    float PhysicalResistPercent = 0f,
    float SpecialResistPercent = 0f,
    float CoinPercent = 0f,
    float CritChance = 0f,
    int Revives = 0,
    float LifestealPercent = 0f)
{
    public static readonly ItemBonus None = new();

    public static ItemBonus operator +(ItemBonus a, ItemBonus b) => new(
        a.MaxHpPercent + b.MaxHpPercent,
        a.MoveSpeedPercent + b.MoveSpeedPercent,
        a.RegenPerSecond + b.RegenPerSecond,
        a.PickupRadiusPercent + b.PickupRadiusPercent,
        a.PhysicalPowerPercent + b.PhysicalPowerPercent,
        a.SpecialPowerPercent + b.SpecialPowerPercent,
        a.CooldownReductionPercent + b.CooldownReductionPercent,
        a.GrowthPercent + b.GrowthPercent,
        a.AreaPercent + b.AreaPercent,
        a.DurationPercent + b.DurationPercent,
        a.AmountBonus + b.AmountBonus,
        a.ProjectileSpeedPercent + b.ProjectileSpeedPercent,
        a.PhysicalResistPercent + b.PhysicalResistPercent,
        a.SpecialResistPercent + b.SpecialResistPercent,
        a.CoinPercent + b.CoinPercent,
        a.CritChance + b.CritChance,
        a.Revives + b.Revives,
        a.LifestealPercent + b.LifestealPercent);

    /// <summary>Every fraction scaled by <paramref name="factor"/> (a trainer's grades, Block 6); whole numbers (extra hits, revivals) stay as they are.</summary>
    public ItemBonus Scaled(float factor) => this with
    {
        MaxHpPercent = MaxHpPercent * factor,
        MoveSpeedPercent = MoveSpeedPercent * factor,
        RegenPerSecond = RegenPerSecond * factor,
        PickupRadiusPercent = PickupRadiusPercent * factor,
        PhysicalPowerPercent = PhysicalPowerPercent * factor,
        SpecialPowerPercent = SpecialPowerPercent * factor,
        CooldownReductionPercent = CooldownReductionPercent * factor,
        GrowthPercent = GrowthPercent * factor,
        AreaPercent = AreaPercent * factor,
        DurationPercent = DurationPercent * factor,
        ProjectileSpeedPercent = ProjectileSpeedPercent * factor,
        PhysicalResistPercent = PhysicalResistPercent * factor,
        SpecialResistPercent = SpecialResistPercent * factor,
        CoinPercent = CoinPercent * factor,
        CritChance = CritChance * factor,
        LifestealPercent = LifestealPercent * factor,
    };

    public static ItemBonus operator *(ItemBonus a, int levels) => new(
        a.MaxHpPercent * levels,
        a.MoveSpeedPercent * levels,
        a.RegenPerSecond * levels,
        a.PickupRadiusPercent * levels,
        a.PhysicalPowerPercent * levels,
        a.SpecialPowerPercent * levels,
        a.CooldownReductionPercent * levels,
        a.GrowthPercent * levels,
        a.AreaPercent * levels,
        a.DurationPercent * levels,
        a.AmountBonus * levels,
        a.ProjectileSpeedPercent * levels,
        a.PhysicalResistPercent * levels,
        a.SpecialResistPercent * levels,
        a.CoinPercent * levels,
        a.CritChance * levels,
        a.Revives * levels,
        a.LifestealPercent * levels);
}

/// <summary>A passive held item (Vampire Survivors "passive"), linear per level. Its icon is content/icons/items/&lt;IconKey&gt;.png.</summary>
public sealed record ItemDefinition(string Id, string NameIt, string IconKey, string DescriptionIt, int MaxLevel, ItemBonus PerLevel)
{
    /// <summary>Offered at level-up. Items stay out until their effect works and their look is approved.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How often its cards come up compared to other items (phase 3.6): 1 is normal, 0.35 rare (Metronomo).</summary>
    public float Rarity { get; init; } = 1f;
}

public sealed class ItemInstance(ItemDefinition definition, int level = 1)
{
    public ItemDefinition Definition { get; } = definition;

    public int Level { get; set; } = level;

    public ItemBonus Bonus => Definition.PerLevel * Level;

    /// <summary>Run tick when the item was obtained.</summary>
    public int AcquiredTick { get; init; }
}
