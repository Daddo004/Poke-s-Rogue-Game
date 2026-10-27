using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// The game's 640×360 screen. Everything is laid out in its pixels, but since phase 8.2 it is drawn into a target
/// <see cref="Zoom"/> times larger, the whole number the window is scaled by: pixel art comes out exactly as before, while
/// smooth text is drawn at the monitor's own resolution. Scenes begin their batches with
/// <see cref="SpriteBatchExtensions.BeginVirtual"/>, which adds the zoom.
/// The target is shown scaled by a whole number (letterboxed), or with <see cref="Fill"/> smoothly stretched to fill the window.
/// </summary>
public sealed class VirtualScreen(GraphicsDevice device, int width, int height) : IDisposable
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 360;

    /// <summary>Zoom above this costs memory for nothing visible (an 8K screen is ×12, still fine at ×8 plus filtering).</summary>
    public const int MaxZoom = 8;

    /// <summary>The zoom of the screen being drawn, for the batches that begin on it (one screen per game).</summary>
    public static int CurrentZoom { get; private set; } = 1;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public RenderTarget2D Target { get; private set; } = Create(device, width, height);

    public Rectangle Destination { get; private set; } = new(0, 0, width, height);

    public int Scale { get; private set; } = 1;

    /// <summary>How many target pixels a virtual pixel takes.</summary>
    public int Zoom { get; private set; } = 1;

    /// <summary>Captures choose their zoom (--scale); null follows the window.</summary>
    public int? FixedZoom { get; set; }

    /// <summary>Fill the window, at a scale that need not be whole (the player's "Scala: piena").</summary>
    public bool Fill { get; set; }

    private static RenderTarget2D Create(GraphicsDevice device, int width, int height) =>
        new(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

    /// <summary>Starts a frame: the target follows the window's scale, then everything is drawn into it.</summary>
    public void Begin()
    {
        var viewport = device.PresentationParameters;
        var fit = MathF.Min(viewport.BackBufferWidth / (float)Width, viewport.BackBufferHeight / (float)Height);
        var zoom = Math.Clamp(FixedZoom ?? (Fill ? (int)MathF.Ceiling(fit - 0.01f) : (int)fit), 1, MaxZoom);
        if (zoom != Zoom)
        {
            Zoom = zoom;
            Target.Dispose();
            Target = Create(device, Width * zoom, Height * zoom);
        }

        CurrentZoom = Zoom;
        device.SetRenderTarget(Target);
    }

    public void End() => device.SetRenderTarget(null);

    public void Present(SpriteBatch batch, Color letterbox)
    {
        var viewport = device.PresentationParameters;
        var fit = MathF.Min(viewport.BackBufferWidth / (float)Width, viewport.BackBufferHeight / (float)Height);
        Scale = Math.Max(1, (int)fit);
        var fill = Fill && fit - Scale >= 0.01f;
        var w = fill ? (int)MathF.Round(Width * fit) : Width * Scale;
        var h = fill ? (int)MathF.Round(Height * fit) : Height * Scale;
        Destination = new Rectangle((viewport.BackBufferWidth - w) / 2, (viewport.BackBufferHeight - h) / 2, w, h);

        // At the window's whole scale the target is copied pixel for pixel; filling, it is smoothly stretched a little.
        device.Clear(letterbox);
        batch.Begin(samplerState: w == Target.Width ? SamplerState.PointClamp : fill ? SamplerState.LinearClamp : SamplerState.PointClamp);
        batch.Draw(Target, Destination, Color.White);
        batch.End();
    }

    /// <summary>A point of the window in virtual-screen pixels (the mouse).</summary>
    public Vector2 ToVirtual(Point window) => new(
        (window.X - Destination.X) * Width / (float)Math.Max(1, Destination.Width),
        (window.Y - Destination.Y) * Height / (float)Math.Max(1, Destination.Height));

    /// <summary>The point of the window lies on the picture, not on the bars around it.</summary>
    public bool Shows(Point window) => Destination.Contains(window);

    public void Dispose() => Target.Dispose();
}

/// <summary>Batches drawn on the virtual screen, in its pixels whatever its zoom (phase 8.2).</summary>
public static class SpriteBatchExtensions
{
    public static void BeginVirtual(this SpriteBatch batch, BlendState? blendState = null, SamplerState? samplerState = null, Matrix? transformMatrix = null) =>
        batch.Begin(blendState: blendState, samplerState: samplerState,
            transformMatrix: (transformMatrix ?? Matrix.Identity) * Matrix.CreateScale(VirtualScreen.CurrentZoom, VirtualScreen.CurrentZoom, 1f));
}
