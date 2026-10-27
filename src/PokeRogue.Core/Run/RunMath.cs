using System.Numerics;

namespace PokeRogue.Core.Run;

/// <summary>Fixed simulation timing shared by Core and the renderer.</summary>
public static class RunClock
{
    public const int TicksPerSecond = 60;
    public const float Dt = 1f / TicksPerSecond;

    public static int Seconds(float seconds) => (int)MathF.Round(seconds * TicksPerSecond);

    /// <summary>
    /// The level's clock runs faster than real time, as in Vampire Survivors-likes where "15 minutes" pass sooner
    /// (2026-10-27 playtest): a second on the clock is this many ticks, so 15:00 comes after 10 real minutes. The waves'
    /// times, the per-minute growth, the timer on screen, records and missions all count clock time; cooldowns, statuses
    /// and every other duration stay real seconds.
    /// </summary>
    public const int TicksPerClockSecond = 40;

    public const int TicksPerClockMinute = TicksPerClockSecond * 60;

    /// <summary>How many clock seconds pass in a real one.</summary>
    public const float ClockSpeed = TicksPerSecond / (float)TicksPerClockSecond;

    /// <summary>Ticks in <paramref name="seconds"/> of the level's clock.</summary>
    public static int ClockSeconds(float seconds) => (int)MathF.Round(seconds * TicksPerClockSecond);

    /// <summary>Minutes on the level's clock after <paramref name="ticks"/>.</summary>
    public static float ClockMinutes(int ticks) => ticks / (float)TicksPerClockMinute;
}

public static class VectorExtensions
{
    public static Vector2 NormalizedOrZero(this Vector2 v)
    {
        var lengthSquared = v.LengthSquared();
        return lengthSquared > 1e-8f ? v / MathF.Sqrt(lengthSquared) : Vector2.Zero;
    }

    public static Vector2 ClampLength(this Vector2 v, float max)
    {
        var lengthSquared = v.LengthSquared();
        return lengthSquared > max * max ? v / MathF.Sqrt(lengthSquared) * max : v;
    }
}
