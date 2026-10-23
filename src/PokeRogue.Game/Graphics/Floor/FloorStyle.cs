using Microsoft.Xna.Framework;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>Parameters of a procedural ground: colours, noise scales and which layers are enabled.</summary>
public sealed record FloorStyle
{
    public required string Id { get; init; }

    public int Seed { get; init; } = 7;

    /// <summary>Dark, mid, light base tones blended by world-space value noise.</summary>
    public Color[] BaseTones { get; init; } = [new(76, 138, 66), new(88, 152, 72), new(102, 164, 80)];

    public float PatchScale { get; init; } = 56f;

    public Color Tuft { get; init; } = new(62, 118, 56);

    public Color TuftLight { get; init; } = new(124, 184, 94);

    /// <summary>Tufts per 10,000 px².</summary>
    public float TuftDensity { get; init; } = 45f;

    /// <summary>Flowers per 10,000 px².</summary>
    public float FlowerDensity { get; init; } = 4f;

    /// <summary>Clusters of 16 px "tall grass" tiles where a second noise field is high; 0 disables.</summary>
    public float TallGrassThreshold { get; init; }

    /// <summary>Half-width in pixels of a meandering dirt path following a noise iso-line; 0 disables.</summary>
    public float PathWidth { get; init; }

    public Color[] PathTones { get; init; } = [new(176, 140, 88), new(196, 160, 104), new(212, 180, 124)];

    public Color PathEdge { get; init; } = new(140, 108, 64);

    /// <summary>Raised decorations (rocks, stumps, bushes) per 64 px cell, 0–1.</summary>
    public float PropChance { get; init; }

    public static readonly FloorStyle Meadow = new() { Id = "prato" };

    public static readonly FloorStyle MeadowDecorated = new()
    {
        Id = "decorato",
        TallGrassThreshold = 0.7f,
        FlowerDensity = 7f,
        PropChance = 0.12f,
    };

    public static readonly FloorStyle Route = new()
    {
        Id = "sentiero",
        TallGrassThreshold = 0.72f,
        PathWidth = 11f,
        PropChance = 0.1f,
    };

    public static FloorStyle ById(string? id) => id switch
    {
        "decorato" => MeadowDecorated,
        "sentiero" => Route,
        _ => Meadow,
    };
}
