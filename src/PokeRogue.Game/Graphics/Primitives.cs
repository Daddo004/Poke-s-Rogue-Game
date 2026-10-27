using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>Procedural textures and shape helpers drawn through SpriteBatch.</summary>
public sealed class Primitives
{
    private readonly Dictionary<(int W, int H), Texture2D> _ellipses = [];
    private readonly GraphicsDevice _device;

    public Primitives(GraphicsDevice device)
    {
        _device = device;
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
    }

    public Texture2D Pixel { get; }

    /// <summary>
    /// A shift under one game pixel added to every <see cref="Rect"/>, so bars and labels laid out in whole pixels can
    /// follow something that glides between them (phase 8.6). Set it with <see cref="Follow"/>, clear it after.
    /// </summary>
    public Vector2 Nudge { get; set; }

    /// <summary>Shifts the following rectangles from <paramref name="whole"/> (where they were laid out) to the monitor pixel nearest <paramref name="exact"/>.</summary>
    public void Follow(Vector2 exact, Vector2 whole) => Nudge = VirtualScreen.Snap(exact) - whole;

    public void Rect(SpriteBatch batch, Rectangle rect, Color color)
    {
        if (Nudge == Vector2.Zero)
            batch.Draw(Pixel, rect, color);
        else
            batch.Draw(Pixel, new Vector2(rect.X + Nudge.X, rect.Y + Nudge.Y), null, color, 0f, Vector2.Zero, new Vector2(rect.Width, rect.Height), SpriteEffects.None, 0f);
    }

    /// <summary>A rectangle at a position between game pixels, on the nearest monitor pixel.</summary>
    public void Rect(SpriteBatch batch, Vector2 topLeft, Vector2 size, Color color) =>
        batch.Draw(Pixel, VirtualScreen.Snap(topLeft), null, color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);

    public void Outline(SpriteBatch batch, Rectangle rect, Color color, int thickness = 1)
    {
        Rect(batch, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        Rect(batch, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        Rect(batch, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        Rect(batch, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    /// <summary>Filled pixel-art ellipse centred on <paramref name="center"/>.</summary>
    public void Ellipse(SpriteBatch batch, Vector2 center, int width, int height, Color color)
    {
        width = System.Math.Max(1, width);
        height = System.Math.Max(1, height);
        var texture = EllipseTexture(width, height);
        batch.Draw(texture, VirtualScreen.Snap(new Vector2(center.X - width / 2f, center.Y - height / 2f)), color);
    }

    /// <summary>Pixel ring (ellipse outline) made of small squares, e.g. the level-up shock wave.</summary>
    public void Ring(SpriteBatch batch, Vector2 center, float radiusX, float radiusY, Color color, int thickness = 1)
    {
        var steps = System.Math.Max(12, (int)(MathF.Tau * MathF.Max(radiusX, radiusY) / 2f));
        for (var i = 0; i < steps; i++)
        {
            var angle = i * MathF.Tau / steps;
            Rect(batch, new Vector2(center.X + MathF.Cos(angle) * radiusX, center.Y + MathF.Sin(angle) * radiusY), new Vector2(thickness), color);
        }
    }

    private Texture2D EllipseTexture(int width, int height)
    {
        if (_ellipses.TryGetValue((width, height), out var texture))
            return texture;

        var pixels = new Color[width * height];
        var rx = width / 2f;
        var ry = height / 2f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = (x + 0.5f - rx) / rx;
                var dy = (y + 0.5f - ry) / ry;
                pixels[y * width + x] = dx * dx + dy * dy <= 1f ? Color.White : Color.Transparent;
            }
        }

        texture = new Texture2D(_device, width, height);
        texture.SetData(pixels);
        _ellipses[(width, height)] = texture;
        return texture;
    }
}
