using FontStashSharp;

namespace PokeRogue.Game.Text;

/// <summary>How a font is drawn on the virtual screen: its glyphs' scale (below 1 for a font rasterised at the screen's resolution).</summary>
public readonly record struct FontLook(float Scale)
{
    public static readonly FontLook Pixel = new(1f);

    /// <summary>Rasterised larger than its layout size and drawn scaled back down, a monitor pixel per glyph pixel.</summary>
    public bool HighRes => Scale < 1f;
}

/// <summary>
/// TTF pixel fonts from content/fonts rendered at runtime. Since the playtest of 2026-10-27 (the smooth M PLUS font of
/// phase 8.2 is gone: "lascia solo quello a pixel ma alzane la qualità") each font is rasterised at its size times the
/// screen's <see cref="Zoom"/> and drawn scaled back down: the letters keep their pixel shapes, but their edges are cut at
/// the monitor's pixels instead of being a small antialiased glyph blown up four times, and text can sit between game
/// pixels. Layouts keep working in virtual pixels.
/// </summary>
public sealed class Fonts
{
    private static readonly Dictionary<SpriteFontBase, FontLook> Looks = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<string, FontSystem> _systems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Name, float Size, int Zoom), SpriteFontBase> _highRes = [];
    private readonly string _fontsDir;

    public Fonts(string contentRoot)
    {
        _fontsDir = Path.Combine(contentRoot, "fonts");
    }

    /// <summary>
    /// Monitor pixels per layout pixel of the batch being drawn: the virtual screen's zoom, set each frame, or less while
    /// the HUD is drawn at a smaller size (Opzioni "Dimensione HUD").
    /// </summary>
    public int Zoom { get; set; } = 1;

    /// <summary>How <paramref name="font"/> is drawn; fonts at their own size as they are.</summary>
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

    /// <summary>A font at <paramref name="size"/> virtual pixels, rasterised at the current zoom.</summary>
    public SpriteFontBase Get(string name, float size)
    {
        if (Zoom <= 1)
            return System(name).GetFont(size);

        var key = (name, size, Zoom);
        if (_highRes.TryGetValue(key, out var font))
            return font;

        font = System(name).GetFont(size * Zoom);
        Looks[font] = new FontLook(1f / Zoom);
        _highRes[key] = font;
        return font;
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
