using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Core.Run.Waves;

/// <summary>
/// One species of a wave, by its PokeAPI key ("caterpie"), with its share of the spawns. In the file it is an object, or
/// since phase 7.4 just "caterpie" (weight 1) or "caterpie:3".
/// </summary>
[JsonConverter(typeof(WaveSpeciesConverter))]
public sealed record WaveSpeciesSpec
{
    public required string Pokemon { get; set; }

    public int Weight { get; set; } = 1;
}

/// <summary>Reads a <see cref="WaveSpeciesSpec"/> from "key", "key:weight" or { "pokemon": …, "weight": … }.</summary>
public sealed class WaveSpeciesConverter : JsonConverter<WaveSpeciesSpec>
{
    public override WaveSpeciesSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString() ?? "";
            var colon = text.IndexOf(':');
            if (colon < 0)
                return new WaveSpeciesSpec { Pokemon = text };
            if (!int.TryParse(text[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var weight))
                throw new JsonException($"'{text}' is not a species like \"caterpie:3\".");
            return new WaveSpeciesSpec { Pokemon = text[..colon], Weight = weight };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A wave species is \"key\", \"key:weight\" or an object.");

        string? pokemon = null;
        var objectWeight = 1;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, "pokemon", StringComparison.OrdinalIgnoreCase))
                pokemon = reader.GetString();
            else if (string.Equals(name, "weight", StringComparison.OrdinalIgnoreCase))
                objectWeight = reader.GetInt32();
            else
                reader.Skip();
        }

        return new WaveSpeciesSpec { Pokemon = pokemon ?? throw new JsonException("A wave species needs \"pokemon\"."), Weight = objectWeight };
    }

    public override void Write(Utf8JsonWriter writer, WaveSpeciesSpec value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Weight == 1 ? value.Pokemon : $"{value.Pokemon}:{value.Weight}");
}

/// <summary>A stretch of the timeline as written in content/data/waves/*.json; times are "m:ss".</summary>
public sealed record WaveSpec
{
    /// <summary>Where it starts; by default where the wave before ends (the first at 0:00).</summary>
    public string? From { get; set; }

    /// <summary>Where it ends; by default one minute after it starts.</summary>
    public string? To { get; set; }

    public IReadOnlyList<WaveSpeciesSpec> Species { get; set; } = [];

    /// <summary>Who comes instead while it is night on the level (phase 7.2); null keeps <see cref="Species"/>.</summary>
    public IReadOnlyList<WaveSpeciesSpec>? Night { get; set; }

    /// <summary>Below this many enemies alive the wave refills at once, as in Vampire Survivors. Null: worlds.json's pace for its minute.</summary>
    public int? MinAlive { get; set; }

    /// <summary>Steady trickle on top of the minimum.</summary>
    public float? SpawnsPerSecond { get; set; }

    /// <summary>The trickle stops at this many enemies alive (special events ignore it).</summary>
    public int? MaxAlive { get; set; }
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

    /// <summary>A line about the place, for the level select (phase 7.1).</summary>
    public string DescriptionIt { get; set; } = "";

    /// <summary>Length of the level; the boss comes when it runs out (phase 3.4).</summary>
    public float Minutes { get; set; } = 15f;

    /// <summary>Enemy HP grows by this share of its base every minute.</summary>
    public float HpPerMinute { get; set; }

    /// <summary>Gem experience grows by this share every minute.</summary>
    public float XpPerMinute { get; set; }

    /// <summary>Enemy contact damage (and their shots and blasts) grows by this share every minute (phase 3.6).</summary>
    public float DamagePerMinute { get; set; }

    /// <summary>Multiplies the waves' numbers taken from worlds.json's pace (a narrow corridor holds fewer).</summary>
    public float Crowd { get; set; } = 1f;

    /// <summary>Frailer enemies in the first minutes (phase 7.4); null takes worlds.json's.</summary>
    public WarmUpSpec? WarmUp { get; set; }

    public IReadOnlyList<WaveSpec> Waves { get; set; } = [];

    public IReadOnlyList<WaveEventSpec> Events { get; set; } = [];

    /// <summary>
    /// Without events of its own (phase 7.4): who plays each part of worlds.json's events, by the name the template gives
    /// it ("swarm", "ring", "stampede", "dominant1" … "dominant5").
    /// </summary>
    public IReadOnlyDictionary<string, string>? Cast { get; set; }

    /// <summary>The level's boss, arriving when the time runs out (phase 3.4); null ends the level at the time limit.</summary>
    public Enemies.BossSpec? Boss { get; set; }

    /// <summary>The legendaries that come instead of the boss at Ultra, one after another (phase 7.3).</summary>
    public IReadOnlyList<LegendarySpec> Legendaries { get; set; } = [];

    /// <summary>Things lying on the map from the start (phase 3.6); null takes worlds.json's layout.</summary>
    public IReadOnlyList<MapItemSpec>? MapItems { get; set; }

    /// <summary>Fossils and eggs hidden on the map (phase 7.4), on top of the map items: "find" is the character they bring.</summary>
    public IReadOnlyList<MapItemSpec> Finds { get; set; } = [];

    /// <summary>The map: floor, outline, obstacles (phase 7.2).</summary>
    public MapSpec Map { get; set; } = new();

    /// <summary>The weather and its changes (phase 7.2); empty for clear skies.</summary>
    public IReadOnlyList<WeatherSpec> Weather { get; set; } = [];

    /// <summary>When night falls (phase 7.2); null for a level that stays as it starts.</summary>
    public NightSpec? Night { get; set; }

    /// <summary>The level's own gloom, 0–1 (a cave, a haunted tower): the light around the player is all there is.</summary>
    public float Darkness { get; set; }
}

/// <summary>
/// One thing on the level's map: a held item ("item": its id), an item on the ground ("loot": its kind), or a fossil or an
/// egg ("find": the character it brings, phase 7.4), at x, y from the start.
/// </summary>
public sealed record MapItemSpec
{
    public string? Item { get; set; }

    public Pickups.LootKind? Loot { get; set; }

    public string? Find { get; set; }

    public float X { get; set; }

    public float Y { get; set; }
}

/// <summary>Something lying on the map from the start of the level.</summary>
public sealed record MapItem(System.Numerics.Vector2 Position, Pickups.LootKind Kind, string? ItemId);

/// <summary>A stretch of the timeline, in ticks. <see cref="NightSpecies"/> replaces <see cref="Species"/> at night.</summary>
public sealed record Wave(int FromTick, int ToTick, IReadOnlyList<SpawnEntry> Species, int MinAlive, float SpawnsPerSecond, int MaxAlive,
    IReadOnlyList<SpawnEntry>? NightSpecies = null)
{
    public int TotalWeight { get; } = Species.Sum(s => s.Weight);

    public int NightWeight { get; } = NightSpecies?.Sum(s => s.Weight) ?? 0;
}

/// <summary>A special event, in ticks. <see cref="EveryTicks"/> is 0 for a single occurrence.</summary>
public sealed record WaveEvent(int AtTick, int EveryTicks, int UntilTick, WaveEventKind Kind, int SpeciesId, int Count, float Speed, float Hp, float Radius, int LifetimeTicks,
    ChestTier Chest = ChestTier.Poke);

/// <summary>A legendary of the level, checked: its fight, and the characters it waits for.</summary>
public sealed record LegendaryBoss(Enemies.BossDefinition Boss, IReadOnlyList<string> Requires);

/// <summary>
/// The waves of one level (phase 3.1): which species come minute by minute, how many and how fast, plus special events
/// such as a Zubat swarm crossing the screen. Played by <see cref="Spawner"/>. Since Block 7 it is the whole level: its
/// map, weather and night, the legendaries of Ultra, and the difficulty it was built for.
/// </summary>
public sealed class WaveTimeline
{
    public const string Folder = "waves";

    /// <summary>Dominanti that come back at a higher difficulty arrive this long after the one before.</summary>
    public static readonly int ExtraDominantGapTicks = RunClock.ClockSeconds(40f);

    /// <summary>Dusk and dawn last this long on the clock.</summary>
    public static readonly int TwilightTicks = RunClock.ClockSeconds(60f);

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

    public string DescriptionIt { get; private init; } = "";

    public int DurationTicks { get; }

    public float HpPerMinute { get; }

    public float XpPerMinute { get; }

    public IReadOnlyList<Wave> Waves { get; }

    public IReadOnlyList<WaveEvent> Events { get; }

    /// <summary>The boss that comes when the time runs out; null for a level that simply ends then.</summary>
    public Enemies.BossDefinition? Boss { get; }

    /// <summary>The legendaries of Ultra (phase 7.3), in the order they come.</summary>
    public IReadOnlyList<LegendaryBoss> Legendaries { get; private init; } = [];

    /// <summary>Things lying on the map from the start.</summary>
    public IReadOnlyList<MapItem> MapItems { get; private init; } = [];

    /// <summary>The map's walls and obstacles (phase 7.2).</summary>
    public LevelMap Map { get; private init; } = LevelMap.Open;

    /// <summary>The weather from each tick on, in order; empty for clear skies.</summary>
    public IReadOnlyList<(int Tick, WeatherKind Kind)> Weather { get; private init; } = [];

    /// <summary>Night, in ticks; null when it never falls.</summary>
    public (int From, int To)? Night { get; private init; }

    /// <summary>The level's own gloom, 0–1.</summary>
    public float Darkness { get; private init; }

    /// <summary>The difficulty the timeline was built for (phase 7.3).</summary>
    public DifficultySpec Difficulty { get; private init; } = DifficultySpec.Normal;

    /// <summary>Every species the level may spawn, so their sprites can be loaded up front.</summary>
    public IEnumerable<int> SpeciesIds => Waves.SelectMany(w => w.Species.Concat(w.NightSpecies ?? []).Select(s => s.SpeciesId)).Concat(Events.Select(e => e.SpeciesId))
        .Concat(Boss is null ? [] : [Boss.SpeciesId]).Concat(Legendaries.Select(l => l.Boss.SpeciesId)).Distinct();

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

    /// <summary>Frailer enemies in the first minutes (phase 7.4); none by default.</summary>
    public WarmUpSpec WarmUp { get; private init; } = new();

    /// <summary>HP multiplier of the enemies spawned at <paramref name="tick"/>.</summary>
    public float HpMultiplier(int tick) => (1f + Minutes(tick) * HpPerMinute) * Difficulty.Hp * WarmUp.At(Minutes(tick));

    public float XpMultiplier(int tick) => 1f + Minutes(tick) * XpPerMinute;

    public float DamagePerMinute { get; }

    /// <summary>Contact damage multiplier of the enemies spawned at <paramref name="tick"/>.</summary>
    public float DamageMultiplier(int tick) => (1f + Minutes(tick) * DamagePerMinute) * Difficulty.Damage;

    /// <summary>The weather of the level at <paramref name="tick"/> (abilities may change it: <see cref="WeatherSystem"/>).</summary>
    public WeatherKind WeatherAt(int tick)
    {
        var kind = WeatherKind.Clear;
        foreach (var (from, k) in Weather)
        {
            if (tick < from)
                break;
            kind = k;
        }

        return kind;
    }

    /// <summary>How deep into the night it is, 0 (day) to 1, with a minute of dusk and of dawn.</summary>
    public float NightAmount(int tick)
    {
        if (Night is not var (from, to))
            return 0f;
        if (tick < from || tick >= to + TwilightTicks)
            return 0f;
        if (tick < from + TwilightTicks)
            return (tick - from) / (float)TwilightTicks;
        return tick < to ? 1f : 1f - (tick - to) / (float)TwilightTicks;
    }

    /// <summary>Night has fallen: the waves send their night species.</summary>
    public bool IsNight(int tick) => NightAmount(tick) >= 0.5f;

    /// <summary>Tests and previews: the same species forever, with no growth and no events.</summary>
    public static WaveTimeline Constant(IReadOnlyList<SpawnEntry> species, float spawnsPerSecond = 2f, int maxAlive = 300) =>
        new("costante", "", 0, 0f, 0f, [new Wave(0, int.MaxValue, species, 0, spawnsPerSecond, maxAlive)], []);

    /// <param name="moves">The moves the boss' shots look like; needed only when the level has a boss that shoots.</param>
    /// <param name="defaults">worlds.json's pace, boss patterns and map items, for files that leave them out.</param>
    /// <param name="difficulty">The difficulty to build the level for; Normal when null.</param>
    public static WaveTimeline Load(string contentRoot, string id, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary? moves = null,
        LevelDefaults? defaults = null, DifficultySpec? difficulty = null) =>
        From(ReadFile(contentRoot, id), pokedex, moves, defaults, difficulty);

    /// <summary>The level's file as written, for the level select.</summary>
    public static WaveFile ReadFile(string contentRoot, string id)
    {
        var path = Path.Combine(contentRoot, "data", Folder, id + ".json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, DataJsonContext.Default.WaveFile) ?? throw new InvalidDataException($"{path} is empty.");
    }

    public static WaveTimeline From(WaveFile file, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary? moves = null,
        LevelDefaults? defaults = null, DifficultySpec? difficulty = null)
    {
        var level = difficulty ?? DifficultySpec.Normal;
        var byKey = pokedex.ToDictionary(s => s.Key, StringComparer.Ordinal);
        SpeciesData Species(string key) => byKey.TryGetValue(key, out var s)
            ? s
            : throw new InvalidDataException($"Waves '{file.Id}': '{key}' is not a Pokémon key.");
        List<SpawnEntry> Entries(IReadOnlyList<WaveSpeciesSpec> list, string what)
        {
            if (list.Count == 0 || list.Any(s => s.Weight <= 0))
                throw new InvalidDataException($"Waves '{file.Id}': the wave {what} needs species with positive weights.");
            return list.Select(s => new SpawnEntry(Species(s.Pokemon).Id, s.Weight)).ToList();
        }

        var minute = RunClock.TicksPerClockMinute;
        var waves = new List<Wave>();
        foreach (var spec in file.Waves)
        {
            var from = spec.From is null ? (waves.Count == 0 ? 0 : waves[^1].ToTick) : ParseTime(spec.From, file.Id);
            var to = spec.To is null ? from + minute : ParseTime(spec.To, file.Id);
            var what = $"{FormatTime(from)}–{FormatTime(to)}";
            if (to <= from)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {what} ends before it starts.");
            if (waves.Count > 0 && from < waves[^1].ToTick)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {what} overlaps the one before (waves go in order).");

            // The numbers a wave leaves out come from worlds.json's pace for its minute, scaled by the level's crowd.
            var pace = defaults?.Pacing is { Count: > 0 } pacing ? pacing[Math.Min(pacing.Count - 1, from / minute)] : null;
            if ((spec.MinAlive is null || spec.SpawnsPerSecond is null || spec.MaxAlive is null) && pace is null)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {what} needs minAlive, spawnsPerSecond and maxAlive (or worlds.json's pace).");
            var minAlive = (int)MathF.Round((spec.MinAlive ?? pace!.MinAlive * file.Crowd) * level.Crowd);
            var rate = (spec.SpawnsPerSecond ?? pace!.SpawnsPerSecond * file.Crowd) * level.Crowd;
            var maxAlive = (int)MathF.Round((spec.MaxAlive ?? pace!.MaxAlive * file.Crowd) * level.Crowd);
            if (rate < 0f || minAlive < 0 || maxAlive < minAlive)
                throw new InvalidDataException($"Waves '{file.Id}': the wave {what} needs 0 ≤ minAlive ≤ maxAlive and a rate of 0 or more.");

            waves.Add(new Wave(from, to, Entries(spec.Species, what), minAlive, rate, Math.Min(maxAlive, 1500),
                spec.Night is null ? null : Entries(spec.Night, what + " (notte)")));
        }

        var duration = RunClock.ClockSeconds(file.Minutes * 60f);
        var events = new List<WaveEvent>();
        var eventSpecs = file.Events;
        if (eventSpecs.Count == 0 && file.Cast is { } cast)
        {
            // worlds.json's events, each played by the species the level casts in its part.
            eventSpecs = (defaults?.Events ?? throw new InvalidDataException($"Waves '{file.Id}': \"cast\" needs worlds.json's \"events\"."))
                .Select(e => e with
                {
                    Pokemon = cast.TryGetValue(e.Pokemon, out var actor) ? actor
                        : throw new InvalidDataException($"Waves '{file.Id}': \"cast\" has nobody for \"{e.Pokemon}\"."),
                }).ToList();
        }

        foreach (var spec in eventSpecs)
        {
            var at = ParseTime(spec.At, file.Id);
            var every = spec.Every is null ? 0 : ParseTime(spec.Every, file.Id);
            var until = spec.Until is null ? duration : ParseTime(spec.Until, file.Id);
            if (spec.Every is not null && every <= 0)
                throw new InvalidDataException($"Waves '{file.Id}': the event at {spec.At} repeats every {spec.Every}; it must be longer than zero.");
            if (spec.Count <= 0 || spec.Speed <= 0f || spec.Hp <= 0f || spec.Lifetime < 0f)
                throw new InvalidDataException($"Waves '{file.Id}': the event at {spec.At} needs a positive count, speed and HP.");

            var species = Species(spec.Pokemon).Id;
            if (spec.Kind == WaveEventKind.Dominant)
            {
                // A harder difficulty brings each Dominante back, a little later each time (phase 7.3), with a Poké Ball:
                // more fights, not five times the rewards.
                for (var k = 0; k <= (spec.Every is null ? level.ExtraDominants : 0); k++)
                {
                    var when = at + k * ExtraDominantGapTicks;
                    if (k == 0 || when < duration)
                        events.Add(new WaveEvent(when, every, until, spec.Kind, species, 1, spec.Speed, spec.Hp * level.DominantHp, spec.Radius,
                            RunClock.Seconds(spec.Lifetime), k == 0 ? spec.Chest : ChestTier.Poke));
                }

                continue;
            }

            var count = Math.Max(1, (int)MathF.Round(spec.Count * level.Crowd));
            events.Add(new WaveEvent(at, every, until, spec.Kind, species, count, spec.Speed, spec.Hp, spec.Radius, RunClock.Seconds(spec.Lifetime), spec.Chest));
        }

        var shots = defaults?.Shots ?? new Dictionary<PokemonType, string>();
        Enemies.BossDefinition? boss = null;
        if (file.Boss is { } bossSpec)
        {
            var species = Species(bossSpec.Pokemon);
            var spec = bossSpec.Phases.Count > 0 ? bossSpec
                : defaults?.Boss is { } pattern ? Enemies.BossPattern.Apply(pattern, species, shots, bossSpec.Shot) with
                {
                    TitleIt = bossSpec.TitleIt, Hp = bossSpec.Hp, Speed = bossSpec.Speed,
                }
                : throw new InvalidDataException($"Waves '{file.Id}': the boss needs \"phases\" (or worlds.json's boss pattern).");
            boss = Enemies.BossDefinition.From(Harder(spec, level), species.Id, moves, file.Id);
        }

        var legendaries = new List<LegendaryBoss>();
        foreach (var spec in file.Legendaries)
        {
            var species = Species(spec.Pokemon);
            var pattern = defaults?.Legendary ?? throw new InvalidDataException($"Waves '{file.Id}': legendaries need worlds.json's \"legendary\" pattern.");
            var fight = Enemies.BossPattern.Apply(pattern, species, shots, spec.Shot) with
            {
                TitleIt = spec.TitleIt, Hp = pattern.Hp * spec.Hp, Speed = pattern.Speed * spec.Speed,
            };
            legendaries.Add(new LegendaryBoss(Enemies.BossDefinition.From(Harder(fight, level), species.Id, moves, file.Id), spec.Requires));
        }

        if (file.DamagePerMinute < 0f || file.Crowd <= 0f)
            throw new InvalidDataException($"Waves '{file.Id}': damagePerMinute must be zero or more, crowd more than zero.");

        // The map's walls first: what lies on the map is brought inside them, and kept clear of obstacles.
        var bounds = new LevelMap(file.Map);
        var mapItems = new List<MapItem>();
        foreach (var spec in (file.MapItems ?? defaults?.MapItems ?? []).Concat(file.Finds))
        {
            var given = (spec.Item is null ? 0 : 1) + (spec.Loot is null ? 0 : 1) + (spec.Find is null ? 0 : 1);
            if (given != 1 || spec.Loot is Pickups.LootKind.HeldItem or Pickups.LootKind.Find)
                throw new InvalidDataException($"Waves '{file.Id}': a map item at {spec.X}, {spec.Y} needs one of \"item\", \"loot\" or \"find\".");

            var kind = spec.Find is not null ? Pickups.LootKind.Find : spec.Loot ?? Pickups.LootKind.HeldItem;
            mapItems.Add(new MapItem(bounds.Clamp(new System.Numerics.Vector2(spec.X, spec.Y), 40f), kind, spec.Item ?? spec.Find));
        }

        if (file.Darkness is < 0f or > 1f)
            throw new InvalidDataException($"Waves '{file.Id}': darkness goes from 0 to 1.");
        if (file.Map.Shape != MapShape.Open && (file.Map.Width < 360f || file.Map.Shape == MapShape.Arena && file.Map.Height < 360f))
            throw new InvalidDataException($"Waves '{file.Id}': a corridor or an arena needs to be at least 360 px wide (and an arena as tall).");

        var weather = file.Weather.Select(w => (ParseTime(w.At, file.Id), w.Kind)).ToList();
        if (weather.Zip(weather.Skip(1)).Any(pair => pair.Second.Item1 <= pair.First.Item1))
            throw new InvalidDataException($"Waves '{file.Id}': weather changes go in order.");

        (int, int)? night = null;
        if (file.Night is { } nightSpec)
        {
            night = (ParseTime(nightSpec.From, file.Id), ParseTime(nightSpec.To, file.Id));
            if (night.Value.Item2 <= night.Value.Item1 + TwilightTicks)
                throw new InvalidDataException($"Waves '{file.Id}': the night must last longer than its dusk.");
        }

        return new WaveTimeline(file.Id, file.NameIt, duration, file.HpPerMinute, file.XpPerMinute, waves, events, boss, file.DamagePerMinute)
        {
            DescriptionIt = file.DescriptionIt,
            MapItems = mapItems,
            Map = new LevelMap(file.Map, mapItems.Select(i => i.Position)),
            Legendaries = legendaries,
            Weather = weather,
            Night = night,
            Darkness = file.Darkness,
            Difficulty = level,
            WarmUp = file.WarmUp ?? defaults?.WarmUp ?? new WarmUpSpec(),
        };
    }

    /// <summary>A boss as the difficulty makes it: more PS, shorter rests.</summary>
    private static Enemies.BossSpec Harder(Enemies.BossSpec spec, DifficultySpec level) =>
        level.BossHp == 1f && level.BossRest == 1f ? spec : spec with
        {
            Hp = spec.Hp * level.BossHp,
            Phases = spec.Phases.Select(p => p with { Rest = p.Rest * level.BossRest }).ToList(),
        };

    /// <summary>"m:ss" (or "mm:ss") on the level's clock to ticks.</summary>
    public static int ParseTime(string text, string fileId = "")
    {
        var parts = text.Split(':');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && parts[1].Length == 2
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds < 60)
        {
            return (minutes * 60 + seconds) * RunClock.TicksPerClockSecond;
        }

        throw new InvalidDataException($"Waves '{fileId}': '{text}' is not a time like \"2:30\".");
    }

    private static string FormatTime(int ticks)
    {
        var seconds = ticks / RunClock.TicksPerClockSecond;
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static float Minutes(int tick) => RunClock.ClockMinutes(tick);
}
