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

    /// <summary>Which decorations: "rock", "bush", "stump" (repeat one to make it commoner).</summary>
    public string[] Props { get; init; } = ["rock", "bush", "stump"];

    // ---- Phase 7.2: the other levels' floors ----

    /// <summary>Single pixels scattered on the floor (pebbles, sand grains, sparkles of snow), per 10,000 px².</summary>
    public Color[] Speckles { get; init; } = [];

    public float SpeckleDensity { get; init; }

    /// <summary>Short crooked cracks, per 10,000 px²; with <see cref="CrackGlow"/> they glow (lava).</summary>
    public Color? Crack { get; init; }

    public Color? CrackGlow { get; init; }

    public float CrackDensity { get; init; }

    /// <summary>A floor of tiles or planks this big, with <see cref="Grout"/> between them; 0 for open ground.</summary>
    public int TileWidth { get; init; }

    public int TileHeight { get; init; }

    /// <summary>Every other row shifted by half a tile, like planks and bricks.</summary>
    public bool TileStagger { get; init; }

    public Color Grout { get; init; } = new(40, 40, 48);

    /// <summary>Metal plates: a rivet in each corner.</summary>
    public Color? Rivets { get; init; }

    /// <summary>Pools of water where a noise field is high (lakes, wet caves); 0 disables.</summary>
    public float PoolThreshold { get; init; }

    public Color[] PoolTones { get; init; } = [new(60, 120, 170), new(100, 160, 206)];

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

    /// <summary>The floors of phase 1.1, and since phase 7.2 every level's (<see cref="Tileset"/>).</summary>
    public static FloorStyle ById(string? id) => id switch
    {
        "decorato" => MeadowDecorated,
        "sentiero" => Route,
        "prato" or null => Meadow,
        _ => Tileset.ById(id).Floor,
    };
}
