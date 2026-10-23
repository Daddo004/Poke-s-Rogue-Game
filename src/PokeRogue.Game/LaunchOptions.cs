namespace PokeRogue.Game;

/// <summary>
/// Command line: <c>--preview &lt;id&gt;</c> opens an isolated scene (see <see cref="Preview.PreviewRegistry"/>);
/// <c>--capture out.png</c> or <c>--gif out.gif [--frames 120] [--step 2]</c> export it and exit.
/// </summary>
public sealed record LaunchOptions
{
    public string? Preview { get; init; }

    public string? CapturePng { get; init; }

    public string? CaptureGif { get; init; }

    public int Frames { get; init; } = 120;

    public int Step { get; init; } = 2;

    public int Scale { get; init; } = 2;

    public int WaitFrames { get; init; } = 30;

    /// <summary>Part of the virtual screen to capture ("--crop x,y,w,h").</summary>
    public Microsoft.Xna.Framework.Rectangle? Crop { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    public int Int(string key, int fallback) => Values.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

    public string? Str(string key) => Values.GetValueOrDefault(key);

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--"))
                continue;

            var key = args[i][2..];
            values[key] = i + 1 < args.Count && !args[i + 1].StartsWith("--") ? args[++i] : "true";
        }

        var options = new LaunchOptions { Values = values };
        return options with
        {
            Preview = values.GetValueOrDefault("preview"),
            CapturePng = values.GetValueOrDefault("capture"),
            CaptureGif = values.GetValueOrDefault("gif"),
            Frames = options.Int("frames", options.Frames),
            Step = System.Math.Max(1, options.Int("step", options.Step)),
            Scale = System.Math.Max(1, options.Int("scale", options.Scale)),
            WaitFrames = System.Math.Max(1, options.Int("wait", options.WaitFrames)),
            Crop = ParseCrop(values.GetValueOrDefault("crop")),
        };
    }

    private static Microsoft.Xna.Framework.Rectangle? ParseCrop(string? text)
    {
        var parts = text?.Split(',').Select(p => int.TryParse(p, out var v) ? v : -1).ToArray();
        return parts is { Length: 4 } && parts.All(p => p >= 0) ? new Microsoft.Xna.Framework.Rectangle(parts[0], parts[1], parts[2], parts[3]) : null;
    }
}
