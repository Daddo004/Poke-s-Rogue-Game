using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Items;

/// <summary>One item as written in content/data/items.json.</summary>
public sealed record ItemSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public string DescriptionIt { get; set; } = "";

    public bool Enabled { get; set; } = true;

    /// <summary>How often its cards come up compared to other items: 1 normal, below 1 rarer.</summary>
    public float Rarity { get; set; } = 1f;

    public int MaxLevel { get; set; } = 5;

    /// <summary>What each level adds; fields not written stay at zero.</summary>
    public ItemBonus PerLevel { get; set; } = ItemBonus.None;
}

public sealed record ItemFile(int Version, IReadOnlyList<ItemSpec> Items);

/// <summary>Every passive held item of the game, from content/data/items.json (phase 2.5).</summary>
public sealed class ItemLibrary
{
    public const string FileName = "items.json";
    public const int MaxLevels = 8;

    private readonly Dictionary<string, ItemDefinition> _byId;

    private ItemLibrary(IReadOnlyList<ItemDefinition> all)
    {
        All = all;
        _byId = all.ToDictionary(i => i.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ItemDefinition> All { get; }

    /// <summary>Items that level-ups may offer.</summary>
    public IReadOnlyList<ItemDefinition> Enabled => All.Where(i => i.Enabled).ToList();

    public ItemDefinition this[string id] =>
        _byId.TryGetValue(id, out var item) ? item : throw new KeyNotFoundException($"Item '{id}' is not in {FileName}.");

    public static ItemLibrary Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.ItemFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static ItemLibrary From(ItemFile file)
    {
        foreach (var spec in file.Items)
        {
            if (spec.MaxLevel is < 1 or > MaxLevels)
                throw new InvalidDataException($"Item '{spec.Id}' has {spec.MaxLevel} levels; it needs 1 to {MaxLevels}.");
            if (spec.PerLevel == ItemBonus.None)
                throw new InvalidDataException($"Item '{spec.Id}' does nothing: give it a \"perLevel\" bonus.");
            if (spec.Rarity <= 0f)
                throw new InvalidDataException($"Item '{spec.Id}' needs a positive \"rarity\".");
        }

        var duplicate = file.Items.GroupBy(i => i.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Item '{duplicate.Key}' appears twice in {FileName}.");

        return new ItemLibrary(file.Items
            .Select(s => new ItemDefinition(s.Id, s.NameIt, s.Id, s.DescriptionIt, s.MaxLevel, s.PerLevel) { Enabled = s.Enabled, Rarity = s.Rarity })
            .ToList());
    }
}
