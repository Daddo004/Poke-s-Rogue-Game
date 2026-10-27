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

    /// <summary>
    /// Top-left world point shown on screen, rounded to the monitor's pixels so sprites never shimmer and still scroll
    /// smoothly on a big screen (<see cref="VirtualScreen.Snap(Vector2)"/>, phase 8.6).
    /// </summary>
    public Vector2 Corner => VirtualScreen.Snap(Position + Shake) - new Vector2(ViewWidth / 2, ViewHeight / 2);

    /// <summary>Top-left world pixel shown on screen, whole (culling).</summary>
    public Point Origin => new((int)MathF.Floor(Corner.X), (int)MathF.Floor(Corner.Y));

    public Rectangle VisibleWorld => new(Origin.X, Origin.Y, ViewWidth + 1, ViewHeight + 1);

    public Matrix Transform => Matrix.CreateTranslation(-Corner.X, -Corner.Y, 0f);

    public Vector2 WorldToScreen(Vector2 world) => world - Corner;

    public Vector2 ScreenToWorld(Vector2 screen) => screen + Corner;

    /// <summary>Frame-rate independent smoothing towards a target (1 = snap).</summary>
    public void Follow(Vector2 target, float stiffness = 1f) => Position = Vector2.Lerp(Position, target, MathHelper.Clamp(stiffness, 0f, 1f));
}
