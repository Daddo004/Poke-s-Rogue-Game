using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run;

/// <summary>
/// What the meta-progression (Block 6) brings into a run: the moves bought for the character, its stat grades, the
/// shop's upgrades (sixth move slot, Fortuna's fourth card, more Rilancio, Salta and Bandisci). Built by
/// <c>Meta.MetaProgress.Loadout</c>; tests, previews and simulations use <see cref="Default"/>.
/// </summary>
public sealed record RunLoadout
{
    /// <summary>As before Block 6: the special on, no secondary move, five move slots, three cards.</summary>
    public static readonly RunLoadout Default = new();

    /// <summary>The character's secondary move is there from the start, beside the main one.</summary>
    public bool SecondaryMove { get; init; }

    /// <summary>The special gauge and key (phase 4.4).</summary>
    public bool SpecialMove { get; init; } = true;

    /// <summary>Stat grades and the shop's upgrades, added like one more held item.</summary>
    public ItemBonus Bonus { get; init; } = ItemBonus.None;

    /// <summary>Move slots: five, six with the shop's Sesto slot.</summary>
    public int MaxMoves { get; init; } = LevelUp.MaxMoves;

    /// <summary>Cards per level-up: three, four with Fortuna.</summary>
    public int Choices { get; init; } = RunState.LevelUpChoiceCount;

    public int ExtraRerolls { get; init; }

    public int ExtraSkips { get; init; }

    public int ExtraBanishes { get; init; }
}
