using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run;

/// <summary>How main-series base stats become run stats. Kept in one place so balancing touches a single file.</summary>
public static class StatFormulas
{
    public static float PlayerMaxHp(BaseStats s) => MathF.Round(40f + s.Hp * 1.2f);

    /// <summary>Pixels per second.</summary>
    public static float PlayerMoveSpeed(BaseStats s) => 60f + s.Speed * 0.45f;

    /// <summary>Collision radius in pixels, from the Pokédex height (decimetres).</summary>
    public static float Radius(SpeciesData species) => Math.Clamp(4f + species.HeightDm * 1.2f, 5f, 20f);

    /// <summary>Early enemies (Rattata 8.5, Caterpie 10.8) fall to one level-1 Thundershock, as in Vampire Survivors' first minute.</summary>
    public static float EnemyMaxHp(BaseStats s) => 4f + s.Hp * 0.15f;

    /// <summary>Pixels per second; always well below the player's so hordes are escapable.</summary>
    public static float EnemyMoveSpeed(BaseStats s) => 30f + s.Speed * 0.3f;

    /// <summary>Contact damage from the stronger of the two attacking stats: Gastly's touch hits with its Special Attack.</summary>
    public static float EnemyContactDamage(BaseStats s) => 3f + Math.Max(s.Attack, s.SpAttack) * 0.06f;

    /// <summary>Whether an enemy's touch is a special attack (softened by Zinco) rather than a physical one (Ferro).</summary>
    public static bool EnemyHitsSpecially(BaseStats s) => s.SpAttack > s.Attack;

    /// <summary>Experience dropped by a regular enemy: weak species give 1, sturdier ones a bit more.</summary>
    public static int EnemyXp(BaseStats s) => s.Total switch
    {
        < 300 => 1,
        < 400 => 2,
        < 500 => 3,
        _ => 5,
    };

    /// <summary>Damage multiplier from a defence stat: 50 → 0.67, 100 → 0.5, 150 → 0.4.</summary>
    public static float DefenseFactor(float defense) => 100f / (100f + defense);
}
