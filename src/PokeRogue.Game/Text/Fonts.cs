using FontStashSharp;
using Microsoft.Xna.Framework;

namespace PokeRogue.Game.Text;

/// <summary>How a font is drawn on the virtual screen: its glyphs' scale and a nudge that puts its letters where the pixel font's were.</summary>
public readonly record struct FontLook(float Scale, float OffsetY)
{
    public static readonly FontLook Pixel = new(1f, 0f);

    public bool Smooth => Scale < 1f;
}

/// <summary>
/// TTF fonts from content/fonts rendered at runtime. Pixel fonts stay crisp at multiples of their design size.
/// Since phase 8.2 text can instead be smooth ("Scritte: nitide"): each pixel font is swapped for M PLUS Rounded 1c,
/// rasterised at the screen's own resolution (the virtual screen's zoom) and drawn scaled back down, sized so a line takes
/// about the room the pixel font took and its capitals sit where they sat. Layouts keep working in virtual pixels.
/// </summary>
public sealed class Fonts
{
    public const string SmoothBold = "MPLUSRounded1c-Bold";
    public const string SmoothHeavy = "MPLUSRounded1c-ExtraBold";

    private const string Sample = "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789 abcdefghijklmnopqrstuvwxyz";

    private static readonly Dictionary<SpriteFontBase, FontLook> Looks = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<string, FontSystem> _systems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Name, float Size, int Zoom), SpriteFontBase> _smooth = [];
    private readonly string _fontsDir;

    public Fonts(string contentRoot)
    {
        _fontsDir = Path.Combine(contentRoot, "fonts");
    }

    /// <summary>The player's choice (Opzioni): smooth text when the screen is zoomed at least twice and the fonts are there.</summary>
    public bool Smooth { get; set; } = true;

    /// <summary>The zoom text is drawn for (the virtual screen's, set each frame).</summary>
    public int Zoom { get; set; } = 1;

    private bool SmoothNow => Smooth && Zoom >= 2 && File.Exists(Path.Combine(_fontsDir, SmoothBold + ".ttf"));

    /// <summary>How <paramref name="font"/> is drawn; pixel fonts as they are.</summary>
    public static FontLook LookOf(SpriteFontBase font) => Looks.GetValueOrDefault(font, FontLook.Pixel);

    /// <summary>Font file names (without extension) available in content/fonts.</summary>
    public IReadOnlyList<string> Available =>
        Directory.Exists(_fontsDir)
            ? Directory.EnumerateFiles(_fontsDir, "*.ttf").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order().ToList()
            : [];

    /// <summary>Approved body font (titles, names, damage numbers).</summary>
    public SpriteFontBase Body(Graphics.UiStyle style) => Get(style.BodyFont, style.BodyFontSize);

    /// <summary>Approved label font for tiny text.</summary>
    public SpriteFontBase Small(Graphics.UiStyle style) => Get(style.SmallFont, style.SmallFontSize);

    /// <summary>Approved font for numbers: damage, levels, timer, counters.</summary>
    public SpriteFontBase Numbers(Graphics.UiStyle style) => Get(style.NumberFont, style.NumberFontSize);

    /// <summary>Big titles and banners: the body font at twice its size, so pixels stay square.</summary>
    public SpriteFontBase Title(Graphics.UiStyle style) => Get(style.BodyFont, style.BodyFontSize * 2);

    /// <summary>A font at <paramref name="size"/> virtual pixels: the pixel font itself, or its smooth stand-in.</summary>
    public SpriteFontBase Get(string name, float size)
    {
        var pixel = System(name).GetFont(size);
        if (!SmoothNow || name.StartsWith("MPLUS", StringComparison.OrdinalIgnoreCase))
            return pixel;

        var key = (name, size, Zoom);
        if (_smooth.TryGetValue(key, out var smooth))
            return smooth;

        smooth = Fit(pixel, System(SmoothFile(name)), Zoom);
        _smooth[key] = smooth;
        return smooth;
    }

    /// <summary>Labels and numbers take the heavier weight: at small sizes thin strokes fade.</summary>
    private static string SmoothFile(string name) =>
        name.StartsWith("Silkscreen", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Jersey", StringComparison.OrdinalIgnoreCase) ? SmoothHeavy : SmoothBold;

    /// <summary>
    /// The smooth font for <paramref name="pixel"/>: as wide over a sample line, but never with capitals much taller (the
    /// layouts' line pitches were made for the pixel fonts), lifted so the capitals' middle stays where it was.
    /// </summary>
    private static SpriteFontBase Fit(SpriteFontBase pixel, FontSystem smooth, int zoom)
    {
        var pixelWidth = pixel.MeasureString(Sample).X;
        var pixelCaps = Caps(pixel);

        var probe = smooth.GetFont(32f);
        var probeWidth = probe.MeasureString(Sample).X;
        var probeCaps = Caps(probe);

        var byWidth = pixelWidth / probeWidth;
        var byHeight = pixelCaps.Height / Math.Max(1f, probeCaps.Height) * 1.12f;
        var size = 32f * MathF.Min(byWidth, byHeight);

        var font = smooth.GetFont(MathF.Max(4f, size * zoom));
        var caps = Caps(font);
        var offset = pixelCaps.Middle - caps.Middle / zoom;
        Looks[font] = new FontLook(1f / zoom, offset);
        return font;
    }

    /// <summary>Where the capitals stand in a line: their height and the middle of them below the line's top.</summary>
    private static (float Height, float Middle) Caps(SpriteFontBase font)
    {
        var bounds = font.TextBounds("HEMBIX", Vector2.Zero);
        return (bounds.Y2 - bounds.Y, (bounds.Y + bounds.Y2) / 2f);
    }

    private FontSystem System(string name)
    {
        if (!_systems.TryGetValue(name, out var system))
        {
            system = new FontSystem(new FontSystemSettings { FontResolutionFactor = 1f, KernelWidth = 0, KernelHeight = 0 });
            system.AddFont(File.ReadAllBytes(Path.Combine(_fontsDir, name + ".ttf")));
            _systems[name] = system;
        }

        return system;
    }
}
