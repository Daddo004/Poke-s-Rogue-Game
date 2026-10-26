using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Items;

namespace PokeRogue.Core.Meta;

/// <summary>The six stats a character can be trained in, as in the games (the vitamins' icons).</summary>
public enum StatKind
{
    Hp,
    Attack,
    Defense,
    SpAttack,
    SpDefense,
    Speed,
}

/// <summary>What a shop upgrade (phase 6.2, VS-like PowerUps) does to every run.</summary>
public enum UpgradeKind
{
    /// <summary>A sixth move slot.</summary>
    MoveSlot,

    /// <summary>Fortuna: a fourth card at every level-up.</summary>
    Choices,

    /// <summary>One more Rilancio per run per grade.</summary>
    Rerolls,

    /// <summary>One more Salta per run per grade.</summary>
    Skips,

    /// <summary>One more Bandisci per run per grade.</summary>
    Banishes,

    /// <summary><c>perGrade</c> added to every run, like a held item.</summary>
    Bonus,
}

/// <summary>A price that grows with a character's own price: <c>base</c> + <c>share</c> × that price.</summary>
public sealed record ScaledPrice
{
    public int Base { get; set; }

    public float Share { get; set; }
}

/// <summary>One stat a character can be trained in, five grades of <see cref="PerGrade"/>.</summary>
public sealed record StatGradeSpec
{
    public required StatKind Stat { get; set; }

    public required string NameIt { get; set; }

    /// <summary>content/icons/items/&lt;icon&gt;.png: the vitamin of that stat.</summary>
    public required string Icon { get; set; }

    public ItemBonus PerGrade { get; set; } = ItemBonus.None;
}

/// <summary>What the shop sells for a character: its secondary move, then its special, then stat grades.</summary>
public sealed record PokemonShopSpec
{
    /// <summary>The price characters without a shop price (Pikachu, mission rewards, legendaries) count as.</summary>
    public int ReferencePrice { get; set; } = 600;

    public ScaledPrice Secondary { get; set; } = new();

    public ScaledPrice Special { get; set; } = new();

    public int MaxGrade { get; set; } = 5;

    /// <summary>Grade N of a stat costs N × this.</summary>
    public int GradePrice { get; set; } = 150;

    public IReadOnlyList<StatGradeSpec> Stats { get; set; } = [];
}

/// <summary>Trainer grades (phase 6.2), like the PowerUps of Vampire Survivors: dearer the more are owned, refunded on request.</summary>
public sealed record TrainerShopSpec
{
    public int MaxGrade { get; set; } = 5;

    /// <summary>Grade N costs N × this, before the markup.</summary>
    public int GradePrice { get; set; } = 200;

    /// <summary>Every grade owned on any trainer makes the next ones dearer by this share.</summary>
    public float MarkupPerGrade { get; set; } = 0.1f;

    /// <summary>Each grade makes the trainer's passive bonuses stronger by this share.</summary>
    public float PassivePerGrade { get; set; } = 0.2f;

    /// <summary>Each grade cuts the active ability's cooldown by this share.</summary>
    public float CooldownPerGrade { get; set; } = 0.06f;
}

/// <summary>A shop upgrade for every run (phase 6.2).</summary>
public sealed record UpgradeSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public string DescriptionIt { get; set; } = "";

    /// <summary>content/icons/items/&lt;icon&gt;.png; null draws the key's letter in a badge (Rilancio, Salta, Bandisci).</summary>
    public string? Icon { get; set; }

    public required UpgradeKind Kind { get; set; }

    /// <summary>The price of each grade, in order: as many grades as prices.</summary>
    public IReadOnlyList<int> Prices { get; set; } = [];

    /// <summary>Medals (gym leaders beaten) each grade needs, in order; empty when none.</summary>
    public IReadOnlyList<int> Medals { get; set; } = [];

    /// <summary>Bonus upgrades: what each grade adds.</summary>
    public ItemBonus PerGrade { get; set; } = ItemBonus.None;

    public int MaxGrade => Prices.Count;
}

public sealed record ShopFile(int Version, PokemonShopSpec Pokemon, TrainerShopSpec Trainers, IReadOnlyList<UpgradeSpec> Upgrades);

/// <summary>Prices and effects of the meta-progression's shop (content/data/shop.json, phase 6.2).</summary>
public sealed class ShopRules
{
    public const string FileName = "shop.json";

    private ShopRules(ShopFile file) => File = file;

    public ShopFile File { get; }

    public PokemonShopSpec Pokemon => File.Pokemon;

    public TrainerShopSpec Trainers => File.Trainers;

    public IReadOnlyList<UpgradeSpec> Upgrades => File.Upgrades;

    public UpgradeSpec? Upgrade(string id) => File.Upgrades.FirstOrDefault(u => u.Id == id);

    public StatGradeSpec Stat(StatKind stat) => File.Pokemon.Stats.First(s => s.Stat == stat);

    public static ShopRules Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = System.IO.File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.ShopFile) ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static ShopRules From(ShopFile file)
    {
        var stats = file.Pokemon.Stats.Select(s => s.Stat).ToList();
        if (stats.Distinct().Count() != stats.Count || stats.Count != Enum.GetValues<StatKind>().Length)
            throw new InvalidDataException($"{FileName}: \"stats\" must list each of the six stats once.");
        if (file.Pokemon.MaxGrade <= 0 || file.Trainers.MaxGrade <= 0)
            throw new InvalidDataException($"{FileName}: \"maxGrade\" must be positive.");

        foreach (var upgrade in file.Upgrades)
        {
            if (upgrade.Prices.Count == 0 || upgrade.Prices.Any(p => p <= 0))
                throw new InvalidDataException($"{FileName}, '{upgrade.Id}': every grade needs a positive price.");
            if (upgrade.Medals.Count != 0 && upgrade.Medals.Count != upgrade.Prices.Count)
                throw new InvalidDataException($"{FileName}, '{upgrade.Id}': \"medals\" needs one value per price.");
            if (upgrade.Kind is UpgradeKind.MoveSlot or UpgradeKind.Choices && upgrade.Prices.Count != 1)
                throw new InvalidDataException($"{FileName}, '{upgrade.Id}': a {upgrade.Kind} upgrade has a single grade.");
            if ((upgrade.Kind == UpgradeKind.Bonus) != (upgrade.PerGrade != ItemBonus.None))
                throw new InvalidDataException($"{FileName}, '{upgrade.Id}': \"perGrade\" goes with Bonus upgrades, and only with them.");
        }

        var duplicate = file.Upgrades.GroupBy(u => u.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.Key}' appears twice.");

        return new ShopRules(file);
    }
}
