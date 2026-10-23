using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>Fixed low-resolution render target, scaled to the window by the largest integer factor (letterboxed).</summary>
public sealed class VirtualScreen(GraphicsDevice device, int width, int height) : IDisposable
{
    public const int DefaultWidth = 640;
    public const int DefaultHeight = 360;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public RenderTarget2D Target { get; } = new(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

    public Rectangle Destination { get; private set; }

    public int Scale { get; private set; } = 1;

    public void Begin() => device.SetRenderTarget(Target);

    public void End() => device.SetRenderTarget(null);

    public void Present(SpriteBatch batch, Color letterbox)
    {
        var viewport = device.PresentationParameters;
        Scale = System.Math.Max(1, System.Math.Min(viewport.BackBufferWidth / Width, viewport.BackBufferHeight / Height));
        var w = Width * Scale;
        var h = Height * Scale;
        Destination = new Rectangle((viewport.BackBufferWidth - w) / 2, (viewport.BackBufferHeight - h) / 2, w, h);

        device.Clear(letterbox);
        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(Target, Destination, Color.White);
        batch.End();
    }

    public void Dispose() => Target.Dispose();
}
