using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run.Characters;

namespace PokeRogue.Core.Meta;

/// <summary>
/// The collectible cards (phase 10): every run with a line adds to its bond; a new bond level, a new species reached and
/// the first win at each difficulty each give a pack of that line. A pack draws, without doubles, from the line's cards
/// whose rarity is open (<see cref="IsOpen"/>), the rarest revealed last.
/// </summary>
public sealed partial class MetaProgress
{
    private bool _cardsSynced;

    public bool IsOwned(CardDefinition card) => Data.Cards.Contains(card.Id);

    public bool IsNew(CardDefinition card) => Data.NewCards.Contains(card.Id);

    /// <summary>The line's bond and packs (empty for a line never played).</summary>
    public CardLineProgress CardLine(CharacterDefinition line)
    {
        SyncCards();
        return Data.CardLines.GetValueOrDefault(line.Id) ?? new CardLineProgress();
    }

    /// <summary>Packs waiting to be opened, across every line.</summary>
    public int PacksWaiting
    {
        get
        {
            SyncCards();
            return Data.CardLines.Values.Sum(l => l.Packs);
        }
    }

    /// <summary>The card can come out of its line's packs: its rarity is open for the line.</summary>
    public bool IsOpen(CardDefinition card)
    {
        var line = Data.CardLines.GetValueOrDefault(card.Line.Id);
        if (line is null)
            return false;

        if (!line.Reached.Contains(card.Species.Key))
            return false;
        return card.Spec.Needs switch
        {
            CardNeed.Reached => true,
            CardNeed.Bond => CardCatalog!.BondLevel(line.Bond) >= card.Spec.BondLevel,
            CardNeed.Win => line.Won.Count > 0,
            CardNeed.HardWin => line.Won.Contains(Difficulty.Hard) || line.Won.Contains(Difficulty.Ultra),
            _ => line.Won.Contains(Difficulty.Ultra),
        };
    }

    /// <summary>The line's cards a pack could give now: open and not owned.</summary>
    public IReadOnlyList<CardDefinition> PackPool(CharacterDefinition line) =>
        CardCatalog?.Of(line).Where(c => !IsOwned(c) && IsOpen(c)).ToList() ?? [];

    /// <summary>
    /// Opens one of the line's packs: up to the pack size of cards from <see cref="PackPool"/>, in order of rarity. An
    /// empty pool keeps the pack closed and returns nothing.
    /// </summary>
    public IReadOnlyList<CardDefinition> OpenPack(CharacterDefinition line, Random random)
    {
        SyncCards();
        var progress = Data.CardLines.GetValueOrDefault(line.Id);
        var pool = PackPool(line).ToList();
        if (CardCatalog is null || progress is not { Packs: > 0 } || pool.Count == 0)
            return [];

        var drawn = new List<CardDefinition>();
        while (drawn.Count < CardCatalog.Rules.PackSize && pool.Count > 0)
        {
            var card = pool[random.Next(pool.Count)];
            pool.Remove(card);
            drawn.Add(card);
        }

        progress.Packs--;
        foreach (var card in drawn)
        {
            Data.Cards.Add(card.Id);
            Data.NewCards.Add(card.Id);
        }

        Persist();
        return drawn.OrderBy(c => c.Rarity).ThenBy(c => c.Number).ToList();
    }

    /// <summary>The album looked at the card: it is no longer new.</summary>
    public void MarkSeen(CardDefinition card)
    {
        if (Data.NewCards.Remove(card.Id))
            Persist();
    }

    /// <summary>The bond, records and packs a finished run leaves to its line; the packs it gave and the bond added.</summary>
    private (IReadOnlyList<(CharacterDefinition, int)> Packs, (int Points, int Level) Bond) RecordCards(RunRecord run)
    {
        if (CardCatalog is null || run.CharacterId is not { } id || Characters.Find(id) is not { } character)
            return ([], default);

        SyncCards();
        var line = Data.CardLineOf(id);
        var packsBefore = line.Packs;
        var points = CardCatalog.BondOf(run);
        line.Bond += points;
        line.Runs++;
        line.Kills += run.Kills;
        line.BestLevel = Math.Max(line.BestLevel, run.Level);
        line.BestTicks = Math.Max(line.BestTicks, run.Ticks);

        // The stage it ended as, and every one it went through.
        foreach (var key in StagesUpTo(character, run.StageKey))
        {
            if (line.Reached.Contains(key))
                continue;
            line.Reached.Add(key);
            line.Packs++;
        }

        if (run.Won && !run.Endless && !line.Won.Contains(run.Difficulty))
        {
            line.Won.Add(run.Difficulty);
            line.Packs++;
        }

        GrantBondPacks(line);
        var gained = line.Packs - packsBefore;
        return (gained > 0 ? [(character, gained)] : [], (points, CardCatalog.BondLevel(line.Bond)));
    }

    /// <summary>The species keys from the line's first one to <paramref name="stageKey"/> (only the first without one).</summary>
    private static IEnumerable<string> StagesUpTo(CharacterDefinition character, string? stageKey)
    {
        var stage = character.Stages.FirstOrDefault(s => s.Species.Key == stageKey) ?? character.Stages[0];
        var chain = new List<string>();
        for (CharacterStage? s = stage; s is not null; s = s.FromIndex is { } from ? character.Stages[from] : null)
            chain.Add(s.Species.Key);
        chain.Reverse();
        return chain;
    }

    private void GrantBondPacks(CardLineProgress line)
    {
        var level = CardCatalog!.BondLevel(line.Bond);
        if (level <= line.Level)
            return;
        line.Packs += level - line.Level;
        line.Level = level;
    }

    /// <summary>
    /// Once per progress: lines with a bond but no packs given for it yet (a save from before the cards) get them, and
    /// their first species counts as reached.
    /// </summary>
    private void SyncCards()
    {
        if (_cardsSynced || CardCatalog is null)
            return;
        _cardsSynced = true;

        var changed = false;
        foreach (var (id, line) in Data.CardLines)
        {
            if (Characters.Find(id) is not { } character)
                continue;
            if (line.Bond > 0 && line.Reached.Count == 0)
            {
                line.Reached.Add(character.First.Key);
                line.Packs++;
                changed = true;
            }

            if (CardCatalog.BondLevel(line.Bond) > line.Level)
            {
                GrantBondPacks(line);
                changed = true;
            }
        }

        if (changed)
            Persist();
    }
}
