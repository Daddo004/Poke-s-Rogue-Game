using FontStashSharp;

namespace PokeRogue.Game.Text;

/// <summary>TTF fonts from content/fonts rendered at runtime. Pixel fonts stay crisp at multiples of their design size.</summary>
public sealed class Fonts
{
    private readonly Dictionary<string, FontSystem> _systems = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _fontsDir;

    public Fonts(string contentRoot)
    {
        _fontsDir = Path.Combine(contentRoot, "fonts");
    }

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

    public SpriteFontBase Get(string name, float size)
    {
        if (!_systems.TryGetValue(name, out var system))
        {
            system = new FontSystem(new FontSystemSettings { FontResolutionFactor = 1f, KernelWidth = 0, KernelHeight = 0 });
            system.AddFont(File.ReadAllBytes(Path.Combine(_fontsDir, name + ".ttf")));
            _systems[name] = system;
        }

        return system.GetFont(size);
    }
}
