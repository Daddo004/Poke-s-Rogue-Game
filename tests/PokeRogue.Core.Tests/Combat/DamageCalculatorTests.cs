using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Tests.Combat;

public class DamageCalculatorTests
{
    private static readonly AttackerProfile Pikachu = new(PokemonType.Electric, null, Attack: 55, SpAttack: 50, CritChance: 0f);

    private static DamageResult Hit(DefenderProfile defender, PokemonType moveType = PokemonType.Electric, AttackerProfile? attacker = null, int seed = 1) =>
        DamageCalculator.Compute(10f, MoveCategory.Special, moveType, attacker ?? Pikachu, defender, TestContent.TypeChart.Value, new Random(seed));

    private static DefenderProfile Defender(int speciesId)
    {
        var s = TestContent.Species(speciesId);
        return new DefenderProfile(s.Types[0], s.Types.Count > 1 ? s.Types[1] : null, s.BaseStats.Defense, s.BaseStats.SpDefense);
    }

    [Fact]
    public void Electric_is_super_effective_on_flying()
    {
        var result = Hit(Defender(16)); // Pidgey

        Assert.Equal(2f, result.Effectiveness);
        Assert.True(result.SuperEffective);
    }

    [Fact]
    public void Immunities_are_softened_not_zero()
    {
        var result = Hit(Defender(50)); // Diglett, Ground

        Assert.Equal(DamageCalculator.ImmunityFactor, result.Effectiveness);
        Assert.True(result.Amount >= 1f);
    }

    [Fact]
    public void Stab_and_effectiveness_scale_damage()
    {
        var rattata = Defender(19);
        var stab = Hit(rattata, PokemonType.Electric);
        var noStab = Hit(rattata, PokemonType.Water);

        // Same random roll (same seed): only the 1.5× STAB differs, before rounding.
        Assert.InRange(stab.Amount / noStab.Amount, 1.3f, 1.7f);
    }

    [Fact]
    public void Expected_magnitude_for_pikachu_thundershock_on_rattata()
    {
        // 10 power × 1.0 (SpA 50) × 100/(100+35·0.5) × 1.5 STAB × 0.9–1.0 → 11.5–12.8
        Assert.InRange(Hit(Defender(19)).Amount, 11f, 13f);
    }

    [Fact]
    public void Guaranteed_crit_multiplies_damage()
    {
        var crit = Hit(Defender(19), attacker: Pikachu with { CritChance = 1f });

        Assert.True(crit.Critical);
        Assert.InRange(crit.Amount, 17f, 20f);
    }
}
