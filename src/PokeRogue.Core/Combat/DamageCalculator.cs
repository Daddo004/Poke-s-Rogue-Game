using PokeRogue.Core.Data;

namespace PokeRogue.Core.Combat;

/// <summary>
/// Who is hitting: types for STAB plus the offensive stats. Abilities (phase 4.3) may raise the STAB (Adattabilità) or
/// the critical damage (Sguardofermo), or ignore resistances (Rompiforma).
/// </summary>
public readonly record struct AttackerProfile(
    PokemonType Type1,
    PokemonType? Type2,
    float Attack,
    float SpAttack,
    float CritChance,
    float Stab = DamageCalculator.StabBonus,
    float CritDamage = DamageCalculator.CritMultiplier,
    bool IgnoresResistances = false);

/// <summary>Who is being hit: types and defensive stats.</summary>
public readonly record struct DefenderProfile(PokemonType Type1, PokemonType? Type2, float Defense, float SpDefense);

public readonly record struct DamageResult(float Amount, float Effectiveness, bool Critical)
{
    public bool SuperEffective => Effectiveness > 1.01f;

    public bool NotVeryEffective => Effectiveness < 0.99f;
}

/// <summary>
/// Roguelite take on the main-series formula: power × attack scaling × defence factor × STAB × type × crit × small random spread.
/// Immunities are softened so no enemy is unkillable.
/// </summary>
public static class DamageCalculator
{
    public const float ImmunityFactor = 0.25f;
    public const float StabBonus = 1.5f;
    public const float CritMultiplier = 1.5f;
    public const float MinRandom = 0.9f;

    /// <summary>50 → 1.0, 100 → 1.5, 135 → 1.85.</summary>
    public static float AttackScaling(float attackStat) => (attackStat + 50f) / 100f;

    /// <summary>35 → 0.85, 100 → 0.67, 160 → 0.56.</summary>
    public static float DefenseScaling(float defenseStat) => 100f / (100f + defenseStat * 0.5f);

    public static float Effectiveness(TypeChart chart, PokemonType moveType, PokemonType type1, PokemonType? type2)
    {
        var factor = chart.Factor(moveType, type1) * (type2 is { } t2 ? chart.Factor(moveType, t2) : 1f);
        return factor == 0f ? ImmunityFactor : factor;
    }

    public static DamageResult Compute(
        float power,
        MoveCategory category,
        PokemonType moveType,
        in AttackerProfile attacker,
        in DefenderProfile defender,
        TypeChart chart,
        Random random)
    {
        var special = category == MoveCategory.Special;
        var attack = AttackScaling(special ? attacker.SpAttack : attacker.Attack);
        var defense = DefenseScaling(special ? defender.SpDefense : defender.Defense);
        var stab = moveType == attacker.Type1 || moveType == attacker.Type2 ? attacker.Stab : 1f;
        var effectiveness = Effectiveness(chart, moveType, defender.Type1, defender.Type2);
        if (attacker.IgnoresResistances)
            effectiveness = MathF.Max(1f, effectiveness);
        var critical = random.NextSingle() < attacker.CritChance;
        var spread = MinRandom + random.NextSingle() * (1f - MinRandom);

        var amount = power * attack * defense * stab * effectiveness * (critical ? attacker.CritDamage : 1f) * spread;
        return new DamageResult(MathF.Max(1f, MathF.Round(amount)), effectiveness, critical);
    }
}
