using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Debugging;

/// <summary>Developer text panel (F3): frame timings plus lines supplied by the current scene.</summary>
public sealed class DebugOverlay(GameServices services)
{
    private static readonly Color Text = new(200, 255, 200);
    private static readonly Color Back = new Color(0, 0, 0) * 0.6f;

    public bool Visible { get; set; } = true;

    public void Draw(SpriteBatch batch, FrameStats stats, IReadOnlyList<string> lines)
    {
        if (!Visible)
            return;

        var font = services.Fonts.Small(services.Style);
        var pp = services.Device.PresentationParameters;
        var screen = services.Screen;
        var all = new List<string>
        {
            $"FPS {stats.Fps}   update {stats.UpdateMs:0.00} ms   draw {stats.DrawMs:0.00} ms   frame max {stats.WorstFrameMs:0} ms" +
            $"   buffer {pp.BackBufferWidth}x{pp.BackBufferHeight}   immagine {screen.Destination.Width}x{screen.Destination.Height} (x{screen.Scale}{(screen.Fill ? " piena" : "")})",
        };
        all.AddRange(lines);

        var width = (int)all.Max(l => UiDraw.Measure(font, l).X) + 8;
        var height = all.Count * 10 + 4;
        var top = services.Screen.Height - height - 2;

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        services.Primitives.Rect(batch, new Rectangle(2, top, width, height), Back);
        for (var i = 0; i < all.Count; i++)
            UiDraw.Text(batch, font, all[i], new Vector2(6, top + 3 + i * 10), Text);
        batch.End();
    }
}
