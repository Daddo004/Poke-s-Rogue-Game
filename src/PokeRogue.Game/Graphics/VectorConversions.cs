namespace PokeRogue.Game.Graphics;

/// <summary>Core simulates with System.Numerics; MonoGame draws with its own vector type.</summary>
public static class VectorConversions
{
    public static Microsoft.Xna.Framework.Vector2 ToXna(this System.Numerics.Vector2 v) => new(v.X, v.Y);

    public static System.Numerics.Vector2 ToNumerics(this Microsoft.Xna.Framework.Vector2 v) => new(v.X, v.Y);
}
