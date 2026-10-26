using System.Globalization;
using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Waves;

/// <summary>One species of a wave, by its PokeAPI key ("caterpie"), with its share of the spawns.</summary>
public sealed record WaveSpeciesSpec
{
    public required string Pokemon { get; set; }

    public int Weight { get; set; } = 1;
}

/// <summary>A stretch of the timeline as written in content/data/waves/*.json; times are "m:ss".</summary>
public sealed record WaveSpec
{
    public required string From { get; set; }

    public required string To { get; set; }

    public IReadOnlyList<WaveSpeciesSpec> Species { get; set; } = [];

    /// <summary>Below this many enemies alive the wave refills at once, as in Vampire Survivors.</summary>
    public int MinAlive { get; set; }

    /// <summary>Steady trickle on top of the minimum.</summary>
    public float SpawnsPerSecond { get; set; }

    /// <summary>The trickle stops at this many enemies alive (special events ignore it).</summary>
    public int MaxAlive { get; set; } = 300;
}

public enum WaveEventKind
{
    /// <summary>A loose flock flies straight across the screen, through the player (Zubat).</summary>
    Swarm,

    /// <summary>A circle appears around the player and closes in (Diglett).</summary>
    Ring,

    /// <summary>A wall charges straight across the screen (Tauros).</summary>
    Stampede,

    /// <summary>A mini boss arrives from off screen (phase 3.3): one big Pokémon with an aura that drops a chest.</summary>
    Dominant,
}

/// <summary>A special event of the timeline; times are "m:ss".</summary>
public sealed record WaveEventSpec
{
    public required string At { get; set; }

    /// <summary>Repeats this often after <see cref="At"/>; null for once.</summary>
    public string? Every { get; set; }

    /// <summary>Last time a repeat may start; null for the end of the level.</summary>
    public string? Until { get; set; }

    public required WaveEventKind Kind { get; set; }

    public required string Pokemon { get; set; }

    public int Count { get; set; } = 10;

    /// <summary>Multiplies the species' walking speed.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>Multiplies HP on top of the timeline's growth.</summary>
    public float Hp { get; set; } = 1f;

    /// <summary>Ring: distance from the player in pixels.</summary>
    public float Radius { get; set; } = 150f;

    /// <summary>Seconds before the survivors leave (a ring burrows back underground); 0 = until they are off screen.</summary>
    public float Lifetime { get; set; }

    /// <summary>Dominanti: the chest they drop.</summary>
    public ChestTier Chest { get; set; }
}

public sealed record WaveFile
{
    public int Version { get; set; } = 1;

    public required string Id { get; set; }

    public string NameIt { get; set; } = "";

    /// <summary>Length of the level; the boss comes when it runs out (phase 3.4).</summary>
    public float Minutes { get; set; } = 15f;

    /// <summary>Enemy HP grows by this share of its base every minute.</summary>
    public float HpPerMinute { get; set; }

    /// <summary>Gem experience grows by this share every minute.</summary>
    public float XpPerMinute { get; set; }

    /// <summary>Enemy contact damage (and their shots and blasts) grows by this share every minute (phase 3.6).</summary>
    public float DamagePerMinute { get; set; }

    public IReadOnlyList<WaveSpec> Waves { get; set; } = [];

    public IReadOnlyList<WaveEventSpec> Events { get; set; } = [];

    /// <summary>The level's boss, arriving when the time runs out (phase 3.4); null ends the level at the time limit.</summary>
    public Enemies.BossSpec? Boss { get; set; }

    /// <summary>Things lying on the map from the start (phase 3.6): held items to find, and a few potions or candies.</summary>
    public IReadOnlyList<MapItemSpec> MapItems { get; set; } = [];
}

/// <summary>One thing on the level's map: a held item ("item": its id) or an item on the ground ("loot": its kind), at x, y from the start.</summary>
public sealed record MapItemSpec
{
    public string? Item { get; set; }

    public Pickups.LootKind? Loot { get; set; }

    public float X { get; set; }

    public float Y { get; set; }
}

/// <summary>Something lying on the map from the start of the level.</summary>
public sealed record MapItem(System.Numerics.Vector2 Position, Pickups.LootKind Kind, string? ItemId);

/// <summary>A stretch of the timeline, in ticks.</summary>
public sealed record Wave(int FromTick, int ToTick, IReadOnlyList<SpawnEntry> Species, int MinAlive, float SpawnsPerSecond, int MaxAlive)
{
    public int TotalWeight { get; } = Species.Sum(s => s.Weight);
}

/// <summary>A special event, in ticks. <see cref="EveryTicks"/> is 0 for a single occurrence.</summary>
public sealed record WaveEvent(int AtTick, int EveryTicks, int UntilTick, WaveEventKind Kind, int SpeciesId, int Count, float Speed, float Hp, float Radius, int LifetimeTicks,
    ChestTier Chest = ChestTier.Poke);

/// <summary>
/// The waves of one level (phase 3.1): which species come minute by minute, how many and how fast, plus special events
/// such as a Zubat swarm crossing the screen. Played by <see cref="Spawner"/>.
/// </summary>
public sealed class WaveTimeline
{
    public const string Folder = "waves";

    private WaveTimeline(string id, string nameIt, int durationTicks, float hpPerMinute, float xpPerMinute, IReadOnlyList<Wave> waves, IReadOnlyList<WaveEvent> events,
        Enemies.BossDefinition? boss = null, float damagePerMinute = 0f)
    {
        Boss = boss;
        DamagePerMinute = damagePerMinute;
        Id = id;
        NameIt = nameIt;
        DurationTicks = durationTicks;
        HpPerMinute = hpPerMinute;
        XpPerMinute = xpPerMinute;
        Waves = waves;
        Events = events;
    }

    public string Id { get; }

    public string NameIt { get; }

    public int DurationTicks { get; }

    public float HpPerMinute { get; }

    public float XpPerMinute { get; }

    public IReadOnlyList<Wave> Waves { get; }

    public IReadOnlyList<WaveEvent> Events { get; }

    /// <summary>The boss that comes when the time runs out; null for a level that simply ends then.</summary>
    public Enemies.BossDefinition? Boss { get; }

    /// <summary>Things lying on the map from the start.</summary>
    public IReadOnlyList<MapItem> MapItems { get; private init; } = [];

    /// <summary>Every species the level may spawn, so their sprites can be loaded up front.</summary>
    public IEnumerable<int> SpeciesIds => Waves.SelectMany(w => w.Species.Select(s => s.SpeciesId)).Concat(Events.Select(e => e.SpeciesId))
        .Concat(Boss is null ? [] : [Boss.SpeciesId]).Distinct();

    /// <summary>The wave playing at <paramref name="tick"/>; past the end the last wave keeps going. Null in a gap.</summary>
    public Wave? WaveAt(int tick)
    {
        if (Waves.Count == 0)
            return null;
        if (tick >= Waves[^1].ToTick)
            return Waves[^1];

        foreach (var wave in Waves)
        {
            if (tick >= wave.FromTick && tick < wave.ToTick)
                return wave;
        }

        return null;
    }

    /// <summary>HP multiplier of the enemies spawned at <paramref name="tick"/>.</summary>
    public float HpMultiplier(int tick) => 1f + Minutes(tick) * HpPerMinute;

    public float XpMultiplier(int tick) => 1f + Minutes(tick) * XpPerMinute;

    public float DamagePerMinute { get; }

    /// <summary>Contact damage multiplier of the enemies spawned at <paramref name="tick"/>.</summary>
    public float DamageMultiplier(int tick) => 1f + Minutes(tick) * DamagePerMinute;

    /// <summary>Tests and previews: the same species forever, with no growth and no events.</summary>
    public static WaveTimeline Constant(IReadOnlyList<SpawnEntry> species, float spawnsPerSecond = 2f, int maxAlive = 300) =>
        new("costante", "", 0, 0f, 0f, [new Wave(0, int.MaxValue, species, 0, spawnsPerSecond, maxAlive)], []);

    /// <param name="moves">The moves the boss' shots look like; needed only when the level has a boss that shoots.</param>
    public static WaveTimeline Load(string contentRoot, string id, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary? moves = null)
    {
        var path = Path.Combine(contentRoot, "data", Folder, id + ".json");
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.WaveFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, pokedex, moves);
    }

    public static WaveTimeline From(WaveFile file, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary? moves = null)
    {
        var byKey = pokedex.ToDictionary(s => s.Key, StringComparer.Ordinal);
        int Species(string key) => byKey.TryGetValue(key, out var s)
            ? s.Id
            : throw new InvalidDataException($"Waves '{file.Id}': '{key}' is not a Pokémon key.");

        var waves = new List<Wave>();
        foreach (var spec in file.Waves)
        {
            var from = ParseTime(spec.From, file.Id);
            var to = ParseTime(spec.To, file.Id);
            if (to <= from)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {spec.From}–{spec.To} ends before it starts.");
            if (waves.Count > 0 && from < waves[^1].ToTick)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {spec.From}–{spec.To} overlaps the one before (waves go in order).");
            if (spec.Species.Count == 0 || spec.Species.Any(s => s.Weight <= 0))
                throw new InvalidDataException($"Waves '{file.Id}': the wave {spec.From}–{spec.To} needs species with positive weights.");
            if (spec.SpawnsPerSecond < 0f || spec.MinAlive < 0 || spec.MaxAlive < spec.MinAlive)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {spec.From}–{spec.To} needs 0 ≤ minAlive ≤ maxAlive and a rate of 0 or more.");

            waves.Add(new Wave(from, to, spec.Species.Select(s => new SpawnEntry(Species(s.Pokemon), s.Weight)).ToList(),
                spec.MinAlive, spec.SpawnsPerSecond, spec.MaxAlive));
        }

        var duration = RunClock.Seconds(file.Minutes * 60f);
        var events = new List<WaveEvent>();
        foreach (var spec in file.Events)
        {
            var at = ParseTime(spec.At, file.Id);
            var every = spec.Every is null ? 0 : ParseTime(spec.Every, file.Id);
            var until = spec.Until is null ? duration : ParseTime(spec.Until, file.Id);
            if (spec.Every is not null && every <= 0)
                throw new InvalidDataException($"Waves '{file.Id}': the event at {spec.At} repeats every {spec.Every}; it must be longer than zero.");
            if (spec.Count <= 0 || spec.Speed <= 0f || spec.Hp <= 0f || spec.Lifetime < 0f)
                throw new InvalidDataException($"Waves '{file.Id}': the event at {spec.At} needs a positive count, speed and HP.");

            events.Add(new WaveEvent(at, every, until, spec.Kind, Species(spec.Pokemon), spec.Kind == WaveEventKind.Dominant ? 1 : spec.Count,
                spec.Speed, spec.Hp, spec.Radius, RunClock.Seconds(spec.Lifetime), spec.Chest));
        }

        var boss = file.Boss is null ? null : Enemies.BossDefinition.From(file.Boss, Species(file.Boss.Pokemon), moves, file.Id);
        if (file.DamagePerMinute < 0f)
            throw new InvalidDataException($"Waves '{file.Id}': damagePerMinute must be zero or more.");

        var mapItems = new List<MapItem>();
        foreach (var spec in file.MapItems)
        {
            if ((spec.Item is null) == (spec.Loot is null) || spec.Loot == Pickups.LootKind.HeldItem)
                throw new InvalidDataException($"Waves '{file.Id}': a map item at {spec.X}, {spec.Y} needs either an \"item\" or a \"loot\".");

            mapItems.Add(new MapItem(new System.Numerics.Vector2(spec.X, spec.Y), spec.Loot ?? Pickups.LootKind.HeldItem, spec.Item));
        }

        return new WaveTimeline(file.Id, file.NameIt, duration, file.HpPerMinute, file.XpPerMinute, waves, events, boss, file.DamagePerMinute)
        {
            MapItems = mapItems,
        };
    }

    /// <summary>"m:ss" (or "mm:ss") to ticks.</summary>
    public static int ParseTime(string text, string fileId = "")
    {
        var parts = text.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && parts[1].Length == 2
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds < 60)
        {
            return (minutes * 60 + seconds) * RunClock.TicksPerSecond;
        }

        throw new InvalidDataException($"Waves '{fileId}': '{text}' is not a time like \"2:30\".");
    }

    private static float Minutes(int tick) => tick / (float)(RunClock.TicksPerSecond * 60);
}
