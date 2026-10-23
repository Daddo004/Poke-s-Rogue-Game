namespace PokeRogue.Core.Run.Progression;

/// <summary>
/// Experience needed per level, shaped like Vampire Survivors: 5 for the first level, then +10 per level up to 20,
/// +13 per level up to 40 and +16 afterwards (without VS's big jumps at 20 and 40, since runs are shorter).
/// </summary>
public static class XpCurve
{
    public static int ToNextLevel(int level) => level switch
    {
        < 1 => throw new ArgumentOutOfRangeException(nameof(level)),
        < 20 => 5 + (level - 1) * 10,
        < 40 => 185 + (level - 19) * 13,
        _ => 445 + (level - 39) * 16,
    };
}
