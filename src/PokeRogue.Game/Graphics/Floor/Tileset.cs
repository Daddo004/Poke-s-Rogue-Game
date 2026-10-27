using Microsoft.Xna.Framework;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>What stands on a level's map and blocks the way (phase 7.2), drawn by <see cref="ObstacleArt"/>.</summary>
public enum ObstacleKind
{
    Tree,
    Pine,
    Palm,
    Rock,
    Stalagmite,
    Crystal,
    Gravestone,
    Pillar,
    Generator,
    Beam,
    Cactus,
    LavaRock,
    Hedge,
}

/// <summary>What lies beyond the walls of a corridor or an arena (phase 7.2).</summary>
public enum EdgeKind
{
    /// <summary>A rock face over darkness (caves, mountain paths).</summary>
    Cliff,

    /// <summary>The sea, with foam along the shore.</summary>
    Water,

    /// <summary>A building's wall over a black void (towers, a power plant, ruins).</summary>
    Wall,

    /// <summary>The sky with clouds, below a stone rim (a tower above the clouds, a mountain top).</summary>
    Clouds,
}

/// <summary>
/// A level's look (phase 7.2): its floor, its obstacle and the colours to draw it, what lies past the walls, and the tint of
/// its darkness. Every level names one in its waves file ("map": { "tileset": … }). All generated, as the floor since 1.1:
/// the PMD dungeon tiles were weighed and left aside (another source to download, square rooms that do not suit an open
/// horde map).
/// </summary>
public sealed record Tileset
{
    public required string Id { get; init; }

    public required FloorStyle Floor { get; init; }

    public ObstacleKind Obstacle { get; init; } = ObstacleKind.Tree;

    /// <summary>The obstacle's tones: dark, mid, light, and an accent (leaves, embers, stripes, glow).</summary>
    public Color[] ObstacleTones { get; init; } = [new(40, 92, 44), new(64, 128, 58), new(96, 164, 80), new(110, 74, 44)];

    public EdgeKind Edge { get; init; } = EdgeKind.Cliff;

    /// <summary>Past the walls: the void, the sea or the sky.</summary>
    public Color Beyond { get; init; } = new(14, 10, 12);

    /// <summary>The wall's face: dark and light.</summary>
    public Color WallDark { get; init; } = new(46, 38, 36);

    public Color WallLight { get; init; } = new(78, 66, 60);

    /// <summary>The colour the dark takes (caves, towers, the night).</summary>
    public Color Shade { get; init; } = new(8, 8, 22);

    private static readonly Dictionary<string, Tileset> All = new(StringComparer.Ordinal);

    private static Tileset Add(Tileset tileset)
    {
        All[tileset.Id] = tileset;
        return tileset;
    }

    /// <summary>The tileset named in a level's file; the forest when unknown.</summary>
    public static Tileset ById(string? id) => id is not null && All.TryGetValue(id, out var tileset) ? tileset : Forest;

    public static IReadOnlyCollection<Tileset> List => All.Values;

    // ---- Kanto ----

    public static readonly Tileset Forest = Add(new() { Id = "bosco", Floor = FloorStyle.MeadowDecorated with { Id = "bosco" } });

    public static readonly Tileset Cave = Add(new()
    {
        Id = "grotta",
        Floor = new FloorStyle
        {
            Id = "grotta", Seed = 11, BaseTones = [new(74, 62, 56), new(86, 72, 64), new(98, 84, 74)], PatchScale = 40f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(120, 104, 92), new(58, 48, 44)], SpeckleDensity = 30f, Crack = new(52, 42, 38), CrackDensity = 3f, PropChance = 0.06f, Props = ["rock"],
        },
        Obstacle = ObstacleKind.Stalagmite,
        ObstacleTones = [new(56, 46, 42), new(96, 82, 72), new(140, 122, 106), new(70, 58, 52)],
        Beyond = new(12, 9, 9), WallDark = new(44, 36, 32), WallLight = new(84, 70, 62), Shade = new(10, 6, 12),
    });

    public static readonly Tileset Tower = Add(new()
    {
        Id = "torre",
        Floor = new FloorStyle
        {
            Id = "torre", Seed = 12, BaseTones = [new(78, 66, 96), new(88, 74, 108), new(98, 84, 118)], PatchScale = 70f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 24, TileHeight = 24, Grout = new(52, 42, 66), Crack = new(64, 54, 80), CrackDensity = 2f,
        },
        Obstacle = ObstacleKind.Gravestone,
        ObstacleTones = [new(70, 70, 84), new(132, 132, 148), new(184, 184, 198), new(96, 84, 112)],
        Edge = EdgeKind.Wall, Beyond = new(8, 6, 12), WallDark = new(46, 36, 60), WallLight = new(82, 68, 104), Shade = new(10, 4, 20),
    });

    public static readonly Tileset PowerPlant = Add(new()
    {
        Id = "centrale",
        Floor = new FloorStyle
        {
            Id = "centrale", Seed = 13, BaseTones = [new(96, 100, 108), new(106, 110, 118), new(116, 120, 128)], PatchScale = 90f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 32, TileHeight = 32, Grout = new(64, 66, 74), Rivets = new(150, 154, 162), Speckles = [new(70, 72, 60)], SpeckleDensity = 4f,
        },
        Obstacle = ObstacleKind.Generator,
        ObstacleTones = [new(52, 56, 64), new(96, 102, 112), new(150, 156, 166), new(240, 200, 48)],
        Edge = EdgeKind.Wall, Beyond = new(10, 10, 14), WallDark = new(42, 44, 52), WallLight = new(78, 82, 92), Shade = new(6, 8, 16),
    });

    public static readonly Tileset IceCave = Add(new()
    {
        Id = "ghiaccio",
        Floor = new FloorStyle
        {
            Id = "ghiaccio", Seed = 14, BaseTones = [new(160, 196, 220), new(176, 210, 232), new(192, 222, 240)], PatchScale = 60f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(240, 250, 255)], SpeckleDensity = 12f, Crack = new(130, 170, 200), CrackDensity = 4f,
        },
        Obstacle = ObstacleKind.Crystal,
        ObstacleTones = [new(90, 140, 190), new(150, 200, 236), new(220, 244, 255), new(255, 255, 255)],
        Edge = EdgeKind.Water, Beyond = new(30, 62, 112), WallDark = new(120, 160, 196), WallLight = new(236, 246, 255), Shade = new(10, 20, 44),
    });

    public static readonly Tileset Mountain = Add(new()
    {
        Id = "montagna",
        Floor = new FloorStyle
        {
            Id = "montagna", Seed = 15, BaseTones = [new(132, 112, 88), new(146, 124, 98), new(158, 136, 108)], PatchScale = 50f, TuftDensity = 6f, FlowerDensity = 0f,
            Tuft = new(110, 120, 72), TuftLight = new(150, 160, 98), Speckles = [new(110, 104, 100), new(176, 156, 126)], SpeckleDensity = 22f, PropChance = 0.08f, Props = ["rock"],
        },
        Obstacle = ObstacleKind.Rock,
        ObstacleTones = [new(78, 72, 70), new(124, 116, 110), new(170, 162, 152), new(96, 110, 72)],
        Beyond = new(36, 28, 24), WallDark = new(82, 66, 52), WallLight = new(126, 104, 82), Shade = new(14, 10, 16),
    });

    public static readonly Tileset Crystals = Add(new()
    {
        Id = "cristalli",
        Floor = new FloorStyle
        {
            Id = "cristalli", Seed = 16, BaseTones = [new(46, 44, 70), new(54, 52, 82), new(62, 60, 94)], PatchScale = 46f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(130, 120, 210), new(90, 200, 220)], SpeckleDensity = 10f, Crack = new(38, 36, 58), CrackDensity = 3f,
        },
        Obstacle = ObstacleKind.Crystal,
        ObstacleTones = [new(70, 50, 130), new(130, 100, 210), new(200, 180, 255), new(120, 230, 240)],
        Beyond = new(8, 6, 16), WallDark = new(34, 30, 56), WallLight = new(70, 64, 104), Shade = new(6, 4, 18),
    });

    // ---- Johto ----

    public static readonly Tileset DeepForest = Add(new()
    {
        Id = "lecci",
        Floor = FloorStyle.MeadowDecorated with
        {
            Id = "lecci", Seed = 17, BaseTones = [new(56, 104, 56), new(66, 118, 62), new(76, 130, 68)], Tuft = new(46, 88, 46), TuftLight = new(96, 150, 80),
            FlowerDensity = 3f, TallGrassThreshold = 0.72f,
        },
        ObstacleTones = [new(28, 66, 36), new(44, 96, 48), new(70, 128, 64), new(90, 60, 38)],
    });

    public static readonly Tileset Ruins = Add(new()
    {
        Id = "rovine",
        Floor = new FloorStyle
        {
            Id = "rovine", Seed = 18, BaseTones = [new(176, 152, 112), new(188, 164, 122), new(198, 174, 132)], PatchScale = 80f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 28, TileHeight = 20, TileStagger = true, Grout = new(130, 108, 76), Crack = new(140, 118, 84), CrackDensity = 3f,
        },
        Obstacle = ObstacleKind.Pillar,
        ObstacleTones = [new(120, 98, 68), new(176, 152, 112), new(222, 202, 160), new(150, 126, 90)],
        Edge = EdgeKind.Wall, Beyond = new(20, 16, 10), WallDark = new(110, 88, 60), WallLight = new(160, 136, 98), Shade = new(14, 10, 8),
    });

    public static readonly Tileset Burnt = Add(new()
    {
        Id = "bruciata",
        Floor = new FloorStyle
        {
            Id = "bruciata", Seed = 19, BaseTones = [new(74, 54, 42), new(84, 62, 48), new(94, 70, 54)], PatchScale = 60f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 72, TileHeight = 12, TileStagger = true, Grout = new(40, 28, 22), Speckles = [new(120, 110, 104), new(200, 96, 40)], SpeckleDensity = 14f,
        },
        Obstacle = ObstacleKind.Beam,
        ObstacleTones = [new(40, 28, 22), new(112, 84, 62), new(150, 116, 86), new(240, 120, 40)],
        Edge = EdgeKind.Wall, Beyond = new(10, 6, 4), WallDark = new(38, 26, 20), WallLight = new(70, 50, 38), Shade = new(12, 6, 6),
    });

    public static readonly Tileset Reef = Add(new()
    {
        Id = "scogliera",
        Floor = new FloorStyle
        {
            Id = "scogliera", Seed = 20, BaseTones = [new(62, 74, 82), new(72, 86, 94), new(82, 98, 106)], PatchScale = 44f, TuftDensity = 0f, FlowerDensity = 0f,
            PoolThreshold = 0.7f, PoolTones = [new(52, 96, 128), new(70, 120, 150)], Speckles = [new(110, 130, 140)], SpeckleDensity = 14f,
        },
        Obstacle = ObstacleKind.Rock,
        ObstacleTones = [new(40, 50, 58), new(70, 86, 96), new(110, 128, 138), new(70, 120, 90)],
        Edge = EdgeKind.Water, Beyond = new(24, 50, 92), WallDark = new(60, 90, 120), WallLight = new(220, 236, 244), Shade = new(6, 14, 30),
    });

    public static readonly Tileset Pagoda = Add(new()
    {
        Id = "campana",
        Floor = new FloorStyle
        {
            Id = "campana", Seed = 21, BaseTones = [new(176, 120, 64), new(188, 132, 72), new(198, 142, 80)], PatchScale = 70f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 72, TileHeight = 12, TileStagger = true, Grout = new(120, 76, 40),
        },
        Obstacle = ObstacleKind.Pillar,
        ObstacleTones = [new(110, 30, 30), new(176, 52, 44), new(224, 96, 72), new(232, 190, 80)],
        Edge = EdgeKind.Wall, Beyond = new(16, 8, 6), WallDark = new(120, 40, 34), WallLight = new(190, 70, 56), Shade = new(16, 8, 10),
    });

    public static readonly Tileset Snow = Add(new()
    {
        Id = "neve",
        Floor = new FloorStyle
        {
            Id = "neve", Seed = 22, BaseTones = [new(214, 224, 236), new(226, 234, 244), new(238, 244, 250)], PatchScale = 64f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(190, 206, 228), new(255, 255, 255)], SpeckleDensity = 16f, PropChance = 0.05f, Props = ["rock"],
        },
        Obstacle = ObstacleKind.Pine,
        ObstacleTones = [new(30, 70, 60), new(48, 104, 84), new(240, 246, 252), new(96, 70, 50)],
        Shade = new(10, 16, 36),
    });

    // ---- Hoenn ----

    public static readonly Tileset Jungle = Add(new()
    {
        Id = "giungla",
        Floor = FloorStyle.MeadowDecorated with
        {
            Id = "giungla", Seed = 23, BaseTones = [new(64, 132, 60), new(76, 148, 66), new(90, 162, 74)], TallGrassThreshold = 0.66f, FlowerDensity = 6f, PropChance = 0.14f,
            Props = ["bush", "bush", "stump"],
        },
        ObstacleTones = [new(30, 92, 40), new(52, 136, 56), new(98, 184, 82), new(120, 84, 52)],
    });

    public static readonly Tileset Volcano = Add(new()
    {
        Id = "vulcano",
        Floor = new FloorStyle
        {
            Id = "vulcano", Seed = 24, BaseTones = [new(70, 62, 62), new(80, 70, 70), new(90, 80, 78)], PatchScale = 46f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(110, 100, 96), new(150, 140, 134)], SpeckleDensity = 22f, Crack = new(150, 70, 40), CrackGlow = new(240, 140, 60), CrackDensity = 1.4f,
        },
        Obstacle = ObstacleKind.LavaRock,
        ObstacleTones = [new(34, 28, 28), new(62, 54, 52), new(96, 86, 82), new(248, 132, 48)],
        Shade = new(20, 8, 6),
    });

    public static readonly Tileset Desert = Add(new()
    {
        Id = "deserto",
        Floor = new FloorStyle
        {
            Id = "deserto", Seed = 25, BaseTones = [new(222, 192, 128), new(230, 202, 138), new(238, 212, 150)], PatchScale = 90f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(200, 170, 110), new(246, 226, 172)], SpeckleDensity = 18f, PropChance = 0.04f, Props = ["rock"],
        },
        Obstacle = ObstacleKind.Cactus,
        ObstacleTones = [new(40, 96, 56), new(70, 140, 76), new(120, 186, 104), new(246, 214, 96)],
        Shade = new(26, 16, 10),
    });

    public static readonly Tileset Beach = Add(new()
    {
        Id = "spiaggia",
        Floor = new FloorStyle
        {
            Id = "spiaggia", Seed = 26, BaseTones = [new(236, 214, 160), new(242, 222, 170), new(248, 230, 182)], PatchScale = 80f, TuftDensity = 0f, FlowerDensity = 0f,
            Speckles = [new(252, 244, 232), new(232, 172, 152)], SpeckleDensity = 8f, PoolThreshold = 0.76f, PoolTones = [new(96, 176, 206), new(140, 206, 226)],
        },
        Obstacle = ObstacleKind.Palm,
        ObstacleTones = [new(40, 108, 52), new(70, 150, 70), new(120, 196, 100), new(150, 104, 62)],
        Shade = new(8, 12, 36),
    });

    public static readonly Tileset Abyss = Add(new()
    {
        Id = "abisso",
        Floor = new FloorStyle
        {
            Id = "abisso", Seed = 27, BaseTones = [new(36, 60, 70), new(42, 70, 80), new(50, 80, 90)], PatchScale = 44f, TuftDensity = 0f, FlowerDensity = 0f,
            PoolThreshold = 0.7f, PoolTones = [new(30, 80, 120), new(50, 110, 150)], Speckles = [new(80, 110, 120)], SpeckleDensity = 10f,
        },
        Obstacle = ObstacleKind.Rock,
        ObstacleTones = [new(22, 40, 48), new(44, 70, 80), new(76, 108, 118), new(60, 140, 160)],
        Beyond = new(6, 12, 16), WallDark = new(26, 44, 52), WallLight = new(52, 82, 92), Shade = new(4, 10, 20),
    });

    public static readonly Tileset Sky = Add(new()
    {
        Id = "cielo",
        Floor = new FloorStyle
        {
            Id = "cielo", Seed = 28, BaseTones = [new(120, 128, 112), new(132, 140, 124), new(144, 152, 136)], PatchScale = 60f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 26, TileHeight = 26, Grout = new(90, 98, 84), Crack = new(90, 120, 70), CrackDensity = 4f,
        },
        Obstacle = ObstacleKind.Pillar,
        ObstacleTones = [new(84, 92, 80), new(132, 140, 124), new(184, 190, 172), new(96, 130, 76)],
        Edge = EdgeKind.Clouds, Beyond = new(132, 186, 228), WallDark = new(86, 94, 80), WallLight = new(150, 158, 140), Shade = new(10, 14, 30),
    });

    // ---- Sinnoh ----

    public static readonly Tileset OldForest = Add(new()
    {
        Id = "evopoli",
        Floor = FloorStyle.MeadowDecorated with
        {
            Id = "evopoli", Seed = 29, BaseTones = [new(52, 96, 70), new(60, 110, 80), new(70, 122, 90)], Tuft = new(42, 80, 58), TuftLight = new(88, 140, 104),
            FlowerDensity = 2f, TallGrassThreshold = 0.72f,
        },
        ObstacleTones = [new(24, 60, 50), new(40, 90, 70), new(64, 124, 96), new(84, 60, 42)],
    });

    public static readonly Tileset Coronet = Add(Cave with
    {
        Id = "corona",
        Floor = Cave.Floor with { Id = "corona", Seed = 30, BaseTones = [new(70, 72, 84), new(82, 84, 96), new(94, 96, 108)], Speckles = [new(120, 124, 140), new(56, 58, 70)], Crack = new(54, 56, 66) },
        Obstacle = ObstacleKind.Rock,
        ObstacleTones = [new(52, 54, 66), new(92, 96, 110), new(140, 146, 160), new(80, 96, 70)],
        WallDark = new(46, 48, 58), WallLight = new(86, 90, 104), Shade = new(6, 6, 16),
    });

    public static readonly Tileset Lake = Add(new()
    {
        Id = "lago",
        Floor = FloorStyle.Meadow with
        {
            Id = "lago", Seed = 31, BaseTones = [new(84, 150, 84), new(96, 162, 92), new(108, 174, 100)], FlowerDensity = 3f, PoolThreshold = 0.74f,
            PoolTones = [new(70, 132, 186), new(110, 170, 214)], PropChance = 0.08f, Props = ["rock", "bush"],
        },
        Obstacle = ObstacleKind.Rock,
        ObstacleTones = [new(70, 76, 72), new(116, 122, 112), new(164, 170, 156), new(84, 130, 70)],
    });

    public static readonly Tileset Flowers = Add(new()
    {
        Id = "fiori",
        Floor = FloorStyle.Meadow with
        {
            Id = "fiori", Seed = 32, BaseTones = [new(102, 170, 82), new(114, 182, 90), new(126, 192, 98)], FlowerDensity = 34f, PropChance = 0.06f, Props = ["bush"],
        },
        Obstacle = ObstacleKind.Hedge,
        ObstacleTones = [new(40, 104, 48), new(70, 148, 66), new(116, 190, 96), new(240, 120, 160)],
    });

    public static readonly Tileset IceTemple = Add(new()
    {
        Id = "tempio",
        Floor = new FloorStyle
        {
            Id = "tempio", Seed = 33, BaseTones = [new(170, 200, 224), new(182, 212, 234), new(194, 222, 242)], PatchScale = 80f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 32, TileHeight = 32, Grout = new(130, 160, 190), Speckles = [new(240, 250, 255)], SpeckleDensity = 6f,
        },
        Obstacle = ObstacleKind.Pillar,
        ObstacleTones = [new(96, 140, 186), new(156, 200, 232), new(226, 244, 255), new(255, 255, 255)],
        Edge = EdgeKind.Wall, Beyond = new(10, 16, 30), WallDark = new(90, 126, 166), WallLight = new(160, 196, 228), Shade = new(8, 14, 34),
    });

    public static readonly Tileset Summit = Add(new()
    {
        Id = "vetta",
        Floor = new FloorStyle
        {
            Id = "vetta", Seed = 34, BaseTones = [new(206, 206, 214), new(216, 216, 224), new(226, 226, 232)], PatchScale = 80f, TuftDensity = 0f, FlowerDensity = 0f,
            TileWidth = 32, TileHeight = 32, Grout = new(160, 160, 172), Crack = new(170, 170, 180), CrackDensity = 3f,
        },
        Obstacle = ObstacleKind.Pillar,
        ObstacleTones = [new(140, 140, 156), new(200, 200, 212), new(240, 240, 246), new(150, 120, 200)],
        Edge = EdgeKind.Clouds, Beyond = new(112, 120, 168), WallDark = new(150, 150, 164), WallLight = new(210, 210, 220), Shade = new(10, 8, 30),
    });
}
