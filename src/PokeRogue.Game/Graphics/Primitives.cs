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

    public void Rect(SpriteBatch batch, Rectangle rect, Color color) => batch.Draw(Pixel, rect, color);

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
        batch.Draw(texture, new Vector2(MathF.Round(center.X - width / 2f), MathF.Round(center.Y - height / 2f)), color);
    }

    /// <summary>Pixel ring (ellipse outline) made of small squares, e.g. the level-up shock wave.</summary>
    public void Ring(SpriteBatch batch, Vector2 center, float radiusX, float radiusY, Color color, int thickness = 1)
    {
        var steps = System.Math.Max(12, (int)(MathF.Tau * MathF.Max(radiusX, radiusY) / 2f));
        for (var i = 0; i < steps; i++)
        {
            var angle = i * MathF.Tau / steps;
            var x = (int)MathF.Round(center.X + MathF.Cos(angle) * radiusX);
            var y = (int)MathF.Round(center.Y + MathF.Sin(angle) * radiusY);
            Rect(batch, new Rectangle(x, y, thickness, thickness), color);
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
