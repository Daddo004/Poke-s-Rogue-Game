using Microsoft.Xna.Framework;

namespace PokeRogue.Game.Graphics;

/// <summary>Pixel-perfect camera centred on <see cref="Position"/> over a virtual screen of fixed size.</summary>
public sealed class Camera2D(int viewWidth, int viewHeight)
{
    public Vector2 Position { get; set; }

    public int ViewWidth { get; } = viewWidth;

    public int ViewHeight { get; } = viewHeight;

    /// <summary>Extra screen offset (screen shake), in virtual pixels.</summary>
    public Vector2 Shake { get; set; }

    /// <summary>Top-left world pixel shown on screen, rounded so sprites never shimmer.</summary>
    public Point Origin => new(
        (int)MathF.Round(Position.X + Shake.X) - ViewWidth / 2,
        (int)MathF.Round(Position.Y + Shake.Y) - ViewHeight / 2);

    public Rectangle VisibleWorld => new(Origin.X, Origin.Y, ViewWidth, ViewHeight);

    public Matrix Transform => Matrix.CreateTranslation(-Origin.X, -Origin.Y, 0f);

    public Vector2 WorldToScreen(Vector2 world) => world - Origin.ToVector2();

    public Vector2 ScreenToWorld(Vector2 screen) => screen + Origin.ToVector2();

    /// <summary>Frame-rate independent smoothing towards a target (1 = snap).</summary>
    public void Follow(Vector2 target, float stiffness = 1f) => Position = Vector2.Lerp(Position, target, MathHelper.Clamp(stiffness, 0f, 1f));
}
