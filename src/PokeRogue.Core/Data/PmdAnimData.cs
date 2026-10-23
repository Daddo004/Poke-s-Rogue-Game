using System.Xml.Linq;

namespace PokeRogue.Core.Data;

/// <summary>One animation from a SpriteCollab AnimData.xml, with CopyOf already resolved to the sheet it draws from.</summary>
public sealed record PmdAnim(
    string Name,
    string SheetName,
    int FrameWidth,
    int FrameHeight,
    IReadOnlyList<int> Durations,
    int? RushFrame,
    int? HitFrame,
    int? ReturnFrame)
{
    /// <summary>Total length in 60 Hz ticks.</summary>
    public int TotalTicks => Durations.Sum();
}

public sealed record PmdAnimData(int ShadowSize, IReadOnlyDictionary<string, PmdAnim> Anims)
{
    public static PmdAnimData Load(string path) => Parse(XDocument.Load(path));

    public static PmdAnimData Parse(XDocument document)
    {
        var root = document.Root ?? throw new InvalidDataException("AnimData.xml has no root.");
        var shadowSize = (int?)root.Element("ShadowSize") ?? 1;

        var raw = root.Element("Anims")?.Elements("Anim")
            .ToDictionary(a => (string)a.Element("Name")!, StringComparer.Ordinal)
            ?? [];

        var anims = new Dictionary<string, PmdAnim>(StringComparer.Ordinal);
        foreach (var name in raw.Keys)
        {
            var source = raw[name];
            for (var hops = 0; (string?)source.Element("CopyOf") is { } copyOf && raw.TryGetValue(copyOf, out var target) && hops < 8; hops++)
                source = target;

            if (source.Element("FrameWidth") is null)
                continue;

            anims[name] = new PmdAnim(
                Name: name,
                SheetName: (string)source.Element("Name")!,
                FrameWidth: (int)source.Element("FrameWidth")!,
                FrameHeight: (int)source.Element("FrameHeight")!,
                Durations: source.Element("Durations")?.Elements("Duration").Select(d => (int)d).ToList() ?? [1],
                RushFrame: (int?)source.Element("RushFrame"),
                HitFrame: (int?)source.Element("HitFrame"),
                ReturnFrame: (int?)source.Element("ReturnFrame"));
        }

        return new PmdAnimData(shadowSize, anims);
    }
}
