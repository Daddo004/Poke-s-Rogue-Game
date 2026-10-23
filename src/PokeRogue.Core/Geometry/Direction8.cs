using System.Numerics;

namespace PokeRogue.Core.Geometry;

/// <summary>Eight facing directions in PMD sprite-sheet row order (screen space, +Y is down).</summary>
public enum Direction8
{
    Down,
    DownRight,
    Right,
    UpRight,
    Up,
    UpLeft,
    Left,
    DownLeft,
}

public static class Direction8Extensions
{
    /// <summary>Nearest of the 8 directions for a screen-space vector; <paramref name="fallback"/> when the vector is ~zero.</summary>
    public static Direction8 FromVector(Vector2 v, Direction8 fallback = Direction8.Down)
    {
        if (v.LengthSquared() < 1e-6f)
            return fallback;

        // Angle measured from "down" (0,1) turning towards "right" (1,0), matching the enum order.
        var angle = MathF.Atan2(v.X, v.Y);
        var sector = (int)MathF.Round(angle / (MathF.PI / 4f));
        return (Direction8)(((sector % 8) + 8) % 8);
    }

    public static Vector2 ToVector(this Direction8 direction)
    {
        var angle = (int)direction * (MathF.PI / 4f);
        return Vector2.Normalize(new Vector2(MathF.Sin(angle), MathF.Cos(angle)));
    }
}
