using System.Numerics;
using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Items;

namespace PokeRogue.Core.Run.Plates;

/// <summary>What a Plate does on top of its type's boost (phase 9.1).</summary>
public enum PlateEffectKind
{
    /// <summary>Just its modifiers.</summary>
    None,

    /// <summary>Lastrarogo: an enemy knocked out while burned explodes (power = share of its PS, radius), burning those it hits.</summary>
    BurnBlast,

    /// <summary>Lastraidro: every interval a wave from the player (radius) pushes the horde back and takes power × their PS.</summary>
    Tide,

    /// <summary>Lastrasaetta: every interval lightning strikes an enemy on screen (power × its PS) and may paralyse it (chance).</summary>
    Bolt,

    /// <summary>Lastraprato: food and potions heal power times as much.</summary>
    Bloom,

    /// <summary>Lastragelo: enemies within radius walk at power times their speed.</summary>
    Chill,

    /// <summary>Lastrapugno: critical hits deal power times the damage.</summary>
    Crit,

    /// <summary>Lastrafiele: an enemy knocked out while poisoned passes the poison to count enemies within radius.</summary>
    Contagion,

    /// <summary>Lastrageo: hits on enemies within radius of the player deal power times the damage.</summary>
    Brawl,

    /// <summary>Lastramente: count more cards at every level-up, and two Rilancio more.</summary>
    Insight,

    /// <summary>Lastrabaco: every hit deals power times the damage (the extra shot comes from its modifiers).</summary>
    Swarm,

    /// <summary>Lastratetra: a hit passes through the player with this chance.</summary>
    Phantom,

    /// <summary>Lastradrakon: + power damage for every minute played, up to count minutes.</summary>
    Fury,

    /// <summary>Lastratrema: regular enemies drop coins and berries power times as often.</summary>
    Greed,

    /// <summary>Lastraferro: hits on Dominanti, bosses and legendaries deal power times the damage.</summary>
    Slayer,
}

/// <summary>A Plate's effect as written in plates.json.</summary>
public sealed record PlateEffectSpec
{
    public PlateEffectKind Kind { get; set; }

    public float Power { get; set; }

    public float Radius { get; set; }

    /// <summary>Seconds between two pulses (tide, bolt).</summary>
    public float Interval { get; set; }

    public float Chance { get; set; }

    public int Count { get; set; }
}

/// <summary>One Plate as written in content/data/plates.json.</summary>
public sealed record PlateSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public PokemonType Type { get; set; }

    /// <summary>Its picture: content/icons/items/&lt;icon&gt;.png.</summary>
    public string Icon { get; set; } = "";

    public string DescriptionIt { get; set; } = "";

    /// <summary>The level it lies on, at x, y from the start.</summary>
    public string Level { get; set; } = "";

    public float X { get; set; }

    public float Y { get; set; }

    public PlateEffectSpec Effect { get; set; } = new();

    public ItemBonus Modifiers { get; set; } = ItemBonus.None;
}

public sealed record PlateFile
{
    public int Version { get; set; } = 1;

    /// <summary>False keeps Plates out of real runs; previews show them anyway.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Damage bonus of the moves of a Plate's type (0.25 = +25%).</summary>
    public float TypePower { get; set; } = 0.25f;

    /// <summary>How much more often the level-up cards of moves of a Plate's type come up.</summary>
    public float CardWeight { get; set; } = 2f;

    /// <summary>Plates a run can hold: the one chosen at the start and the one offered by a chest.</summary>
    public int MaxPerRun { get; set; } = 2;

    /// <summary>The first chest opened from this time ("m:ss") offers a Plate.</summary>
    public string OfferFrom { get; set; } = "8:00";

    /// <summary>Plates offered by that chest.</summary>
    public int OfferCount { get; set; } = 3;

    public IReadOnlyList<PlateSpec> Plates { get; set; } = [];
}

/// <summary>A Plate (phase 9.1), checked.</summary>
public sealed record PlateDefinition(string Id, string NameIt, PokemonType Type, string Icon, string DescriptionIt, string Level, Vector2 Position,
    PlateEffectSpec Effect, ItemBonus Modifiers);

/// <summary>Arceus' Plates, from content/data/plates.json (phase 9.1): Vampire Survivors' Arcana.</summary>
public sealed class PlateLibrary
{
    public const string FileName = "plates.json";

    private readonly Dictionary<string, PlateDefinition> _byId;

    private PlateLibrary(PlateFile file, IReadOnlyList<PlateDefinition> all, int offerFromTicks)
    {
        File = file;
        All = all;
        OfferFromTicks = offerFromTicks;
        _byId = all.ToDictionary(p => p.Id, StringComparer.Ordinal);
    }

    public PlateFile File { get; }

    public IReadOnlyList<PlateDefinition> All { get; }

    /// <summary>The first chest opened from this tick offers a Plate.</summary>
    public int OfferFromTicks { get; }

    public PlateDefinition? Find(string? id) => id is not null && _byId.TryGetValue(id, out var plate) ? plate : null;

    public PlateDefinition this[string id] => Find(id) ?? throw new KeyNotFoundException($"Plate '{id}' is not in {FileName}.");

    /// <summary>The Plate lying on a level, if any.</summary>
    public IEnumerable<PlateDefinition> OnLevel(string levelId) => All.Where(p => p.Level == levelId);

    public static PlateLibrary Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = System.IO.File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.PlateFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static PlateLibrary From(PlateFile file)
    {
        if (file.TypePower < 0f || file.CardWeight <= 0f || file.MaxPerRun < 1 || file.OfferCount < 1)
            throw new InvalidDataException($"{FileName}: typePower ≥ 0, cardWeight > 0, maxPerRun and offerCount at least 1.");

        var duplicate = file.Plates.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.Key}' appears twice.");

        foreach (var spec in file.Plates)
        {
            var effect = spec.Effect;
            var where = $"{FileName}, '{spec.Id}'";
            if (string.IsNullOrEmpty(spec.Icon) || string.IsNullOrEmpty(spec.Level))
                throw new InvalidDataException($"{where}: it needs an \"icon\" and a \"level\".");
            var bad = effect.Kind switch
            {
                PlateEffectKind.BurnBlast => effect.Power <= 0f || effect.Radius <= 0f,
                PlateEffectKind.Tide => effect.Power <= 0f || effect.Radius <= 0f || effect.Interval <= 0f,
                PlateEffectKind.Bolt => effect.Power <= 0f || effect.Interval <= 0f || effect.Chance is < 0f or > 1f,
                PlateEffectKind.Bloom or PlateEffectKind.Crit or PlateEffectKind.Greed or PlateEffectKind.Slayer => effect.Power < 1f,
                PlateEffectKind.Chill => effect.Power is <= 0f or >= 1f || effect.Radius <= 0f,
                PlateEffectKind.Contagion => effect.Radius <= 0f || effect.Count < 1,
                PlateEffectKind.Brawl => effect.Power < 1f || effect.Radius <= 0f,
                PlateEffectKind.Insight => effect.Count < 1,
                PlateEffectKind.Swarm => effect.Power is <= 0f or > 1f,
                PlateEffectKind.Phantom => effect.Chance is <= 0f or >= 1f,
                PlateEffectKind.Fury => effect.Power <= 0f || effect.Count < 1,
                _ => spec.Modifiers == ItemBonus.None,
            };
            if (bad)
                throw new InvalidDataException($"{where}: the numbers of its effect \"{effect.Kind}\" are out of range (or it does nothing).");
        }

        var plates = file.Plates
            .Select(s => new PlateDefinition(s.Id, s.NameIt, s.Type, s.Icon, s.DescriptionIt, s.Level, new Vector2(s.X, s.Y), s.Effect, s.Modifiers))
            .ToList();
        return new PlateLibrary(file, plates, Waves.WaveTimeline.ParseTime(file.OfferFrom, FileName));
    }
}
