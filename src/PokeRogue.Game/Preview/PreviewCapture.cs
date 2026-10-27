using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Captures the virtual screen to a PNG (one frame) or an animated GIF, then asks the game to exit.
/// <c>--crop x,y,w,h</c> keeps only part of the screen (combine with <c>--scale</c> to zoom pixel art).
/// </summary>
public sealed class PreviewCapture(LaunchOptions options)
{
    private readonly List<uint[]> _frames = [];
    private int _frame;

    public bool Active => options.CapturePng is not null || options.CaptureGif is not null;

    /// <summary>
    /// Call after the virtual screen is drawn. Returns true once the export is written. Since phase 8.2 the screen is
    /// drawn at <paramref name="zoom"/> (the capture's --scale), so smooth text shows as it does on a monitor; only what
    /// --scale asks beyond the zoom is upscaled.
    /// </summary>
    public bool AfterDraw(GraphicsDevice device, RenderTarget2D target, int zoom)
    {
        if (!Active)
            return false;

        _frame++;
        if (_frame < options.WaitFrames)
            return false;

        var area = options.Crop is { } crop ? new Rectangle(crop.X * zoom, crop.Y * zoom, crop.Width * zoom, crop.Height * zoom) : new Rectangle(0, 0, target.Width, target.Height);
        var scale = Math.Max(1, options.Scale / zoom);
        if (options.CapturePng is { } png)
        {
            SavePng(device, Crop(ReadRgba(target), target.Width, area), area, png, scale);
            Console.WriteLine($"Saved {png}");
            return true;
        }

        if ((_frame - options.WaitFrames) % options.Step == 0)
            _frames.Add(Crop(ReadRgba(target), target.Width, area));

        if (_frames.Count < options.Frames)
            return false;

        var gif = options.CaptureGif!;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(gif))!);
        var scaled = _frames.Select(f => Upscale(f, area.Width, area.Height, scale)).ToList();
        using (var file = File.Create(gif))
            GifEncoder.Write(file, area.Width * scale, area.Height * scale, scaled, (int)MathF.Round(options.Step * 100f / PokeRogueGame.TargetFps));
        Console.WriteLine($"Saved {gif} ({_frames.Count} frames)");
        return true;
    }

    private static uint[] ReadRgba(RenderTarget2D target)
    {
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels.Select(c => c.PackedValue).ToArray();
    }

    private static uint[] Crop(uint[] pixels, int width, Rectangle area)
    {
        if (area.X == 0 && area.Y == 0 && area.Width == width && pixels.Length == area.Width * area.Height)
            return pixels;

        var result = new uint[area.Width * area.Height];
        for (var y = 0; y < area.Height; y++)
            Array.Copy(pixels, (area.Y + y) * width + area.X, result, y * area.Width, area.Width);
        return result;
    }

    private static uint[] Upscale(uint[] pixels, int width, int height, int scale)
    {
        if (scale == 1)
            return pixels;

        var result = new uint[width * scale * height * scale];
        for (var y = 0; y < height * scale; y++)
        {
            var row = (y / scale) * width;
            for (var x = 0; x < width * scale; x++)
                result[y * width * scale + x] = pixels[row + x / scale];
        }

        return result;
    }

    private static void SavePng(GraphicsDevice device, uint[] pixels, Rectangle area, string path, int scale)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var scaled = Upscale(pixels, area.Width, area.Height, scale);
        using var texture = new Texture2D(device, area.Width * scale, area.Height * scale);
        texture.SetData(scaled.Select(p => new Color(p)).ToArray());
        using var file = File.Create(path);
        texture.SaveAsPng(file, texture.Width, texture.Height);
    }
}
