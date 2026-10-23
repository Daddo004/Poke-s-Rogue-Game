using System.Numerics;

namespace PokeRogue.Core.Run;

/// <summary>Fixed simulation timing shared by Core and the renderer.</summary>
public static class RunClock
{
    public const int TicksPerSecond = 60;
    public const float Dt = 1f / TicksPerSecond;

    public static int Seconds(float seconds) => (int)MathF.Round(seconds * TicksPerSecond);
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
