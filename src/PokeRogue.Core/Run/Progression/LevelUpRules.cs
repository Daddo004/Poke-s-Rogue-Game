using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Progression;

/// <summary>Share of the level-up draw for each kind of card, as written in content/data/levelup.json.</summary>
public sealed record LevelUpShares
{
    public float MoveUpgrade { get; set; } = 1f;

    public float ItemUpgrade { get; set; } = 1f;

    public float NewMove { get; set; } = 1f;

    public float NewItem { get; set; } = 1f;
}

public sealed record LevelUpFile(int Version, LevelUpShares Shares)
{
    /// <summary>A share is split among at least this many cards, so a lone owned move cannot take a whole share.</summary>
    public int SplitAtLeast { get; set; } = 1;
}

/// <summary>
/// How level-ups draw their cards (phase 2.7). Each kind of card takes its share of the draw, split evenly among its
/// cards (but at least <see cref="SplitAtLeast"/> ways): with 0.35 for upgrades of owned moves, about one card in three
/// upgrades a move the player has once it has a few. A kind with nothing to offer leaves its share to the others.
/// Without rules every card is equally likely.
/// </summary>
public sealed class LevelUpRules
{
    public const string FileName = "levelup.json";

    private LevelUpRules(LevelUpShares shares, int splitAtLeast)
    {
        Shares = shares;
        SplitAtLeast = splitAtLeast;
    }

    public LevelUpShares Shares { get; }

    public int SplitAtLeast { get; }

    public float ShareOf(LevelUpKind kind) => kind switch
    {
        LevelUpKind.MoveUpgrade => Shares.MoveUpgrade,
        LevelUpKind.ItemUpgrade => Shares.ItemUpgrade,
        LevelUpKind.NewMove => Shares.NewMove,
        LevelUpKind.NewItem => Shares.NewItem,
        _ => 0f,
    };

    /// <summary>Weight of one card when <paramref name="cardsOfKind"/> cards of its kind are on the table.</summary>
    public float WeightOf(LevelUpKind kind, int cardsOfKind) => ShareOf(kind) / Math.Max(cardsOfKind, SplitAtLeast);

    public static LevelUpRules Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.LevelUpFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static LevelUpRules From(LevelUpFile file)
    {
        var s = file.Shares;
        float[] shares = [s.MoveUpgrade, s.ItemUpgrade, s.NewMove, s.NewItem];
        if (shares.Any(share => share < 0f) || shares.Sum() <= 0f)
            throw new InvalidDataException($"{FileName}: shares must be zero or more, and not all zero.");
        if (file.SplitAtLeast < 1)
            throw new InvalidDataException($"{FileName}: splitAtLeast must be 1 or more.");

        return new LevelUpRules(s, file.SplitAtLeast);
    }
}
