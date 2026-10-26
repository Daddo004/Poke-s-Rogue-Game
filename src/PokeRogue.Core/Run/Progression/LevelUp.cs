using System.Globalization;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Progression;

public enum LevelUpKind
{
    NewMove,
    MoveUpgrade,
    NewItem,
    ItemUpgrade,

    /// <summary>Fallback when everything is maxed: heal (Baccarancia).</summary>
    Heal,

    /// <summary>Fallback when everything is maxed: coins (Pepita).</summary>
    Coins,

    /// <summary>A maxed move plus its item: the move evolves (phase 2.6). Always offered first.</summary>
    Evolution,

    /// <summary>Chests only (phase 4.2): the stone or item the player's own evolution needs.</summary>
    EvolutionItem,
}

/// <summary>One card of the level-up window. Texts are ready to display (Italian).</summary>
public sealed record LevelUpOption(
    LevelUpKind Kind,
    string Id,
    string NameIt,
    int FromLevel,
    int ToLevel,
    string DescriptionIt,
    string IconKey,
    PokemonType? MoveType = null,
    string? EvolvesFrom = null,
    string? EvolveItem = null)
{
    public bool IsNew => Kind is LevelUpKind.NewMove or LevelUpKind.NewItem;
}

/// <summary>Builds and applies level-up choices, Vampire Survivors style: upgrade or add moves and held items.</summary>
public static class LevelUp
{
    /// <summary>Move slots in a run (phase 3.6: five; the sixth will be bought with the meta-progression of Block 6).</summary>
    public const int MaxMoves = 5;
    public const int MaxItems = 6;
    public const int HealAmount = 30;
    public const int CoinAmount = 25;

    private static readonly CultureInfo Italian = CultureInfo.InvariantCulture;

    public static IReadOnlyList<LevelUpOption> Generate(RunState run, int count)
    {
        var player = run.Player;
        // Evolutions come from chests; the gold level-up card only when switched back on.
        var evolutions = run.EvolutionsAtLevelUp ? ReadyEvolutions(run) : [];
        var candidates = new List<LevelUpOption>();

        foreach (var move in player.Moves.Where(m => m.Level < m.Definition.MaxLevel))
            candidates.Add(MoveUpgrade(move));

        if (player.Moves.Count < MaxMoves)
        {
            // A move already evolved is gone for good, as in Vampire Survivors: its base is not offered again.
            foreach (var definition in run.MovePool.Where(d => player.Moves.All(m => m.Definition.Id != d.Id && m.Definition.EvolvesFrom != d.Id)))
                candidates.Add(new LevelUpOption(LevelUpKind.NewMove, definition.Id, definition.NameIt, 0, 1, definition.DescriptionIt, definition.Id, definition.Type));
        }

        foreach (var item in player.Items.Where(i => i.Level < i.Definition.MaxLevel))
            candidates.Add(ItemUpgrade(item));

        if (player.Items.Count < MaxItems)
        {
            foreach (var definition in run.ItemPool.Where(d => player.Items.All(i => i.Definition.Id != d.Id)))
                candidates.Add(new LevelUpOption(LevelUpKind.NewItem, definition.Id, definition.NameIt, 0, 1, $"{definition.DescriptionIt} {Describe(definition.PerLevel)}", definition.IconKey));
        }

        // Bandisci: banished moves and items never come back, not even as upgrades.
        candidates.RemoveAll(c => run.Banished.Contains(c.Id));

        if (candidates.Count == 0 && evolutions.Count == 0)
        {
            return
            [
                new LevelUpOption(LevelUpKind.Heal, "oran-berry", "Baccarancia", 0, 0, $"Recupera {HealAmount} PS.", "oran-berry"),
                new LevelUpOption(LevelUpKind.Coins, "nugget", "Pepita", 0, 0, $"Ottieni {CoinAmount} monete.", "nugget"),
            ];
        }

        // The evolutions that are ready always come first, then distinct cards drawn by kind.
        return evolutions.Concat(Draw(run, candidates, Math.Max(0, count - evolutions.Count))).ToList();
    }

    /// <summary>
    /// Up to <paramref name="count"/> distinct cards at random: each kind takes its share of <see cref="RunState.LevelUpRules"/>,
    /// split evenly among its cards. Without rules, or when only kinds with no share are left, every card is alike.
    /// </summary>
    private static List<LevelUpOption> Draw(RunState run, List<LevelUpOption> candidates, int count)
    {
        var weights = new float[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            var kind = candidates[i].Kind;
            weights[i] = run.LevelUpRules is { } rules ? rules.WeightOf(kind, candidates.Count(c => c.Kind == kind)) : 1f;

            // Rare items (Metronomo) come up less often, as new items and as upgrades.
            if (kind is LevelUpKind.NewItem or LevelUpKind.ItemUpgrade && run.ItemPool.FirstOrDefault(d => d.Id == candidates[i].Id) is { } item)
                weights[i] *= item.Rarity;
        }

        var remaining = Enumerable.Range(0, candidates.Count).ToList();
        var picked = new List<LevelUpOption>();
        while (picked.Count < count && remaining.Count > 0)
        {
            if (remaining.All(i => weights[i] <= 0f))
            {
                foreach (var i in remaining)
                    weights[i] = 1f;
            }

            var roll = run.Random.NextSingle() * remaining.Sum(i => weights[i]);
            var chosen = remaining.Last(i => weights[i] > 0f);
            foreach (var i in remaining)
            {
                if (roll < weights[i])
                {
                    chosen = i;
                    break;
                }

                roll -= weights[i];
            }

            picked.Add(candidates[chosen]);
            remaining.Remove(chosen);
        }

        return picked;
    }

    /// <summary>Evolution cards for every maxed move whose evolution is approved, not banished, and whose item is held.</summary>
    public static List<LevelUpOption> ReadyEvolutions(RunState run)
    {
        var player = run.Player;
        var options = new List<LevelUpOption>();
        foreach (var move in player.Moves.Where(m => m.Level >= m.Definition.MaxLevel))
        {
            var evolution = run.Evolutions.FirstOrDefault(e => e.EvolvesFrom == move.Definition.Id);
            if (evolution is null || run.Banished.Contains(evolution.Id) || player.Items.All(i => i.Definition.Id != evolution.EvolveItem)
                || player.Moves.Any(m => m.Definition.Id == evolution.Id))
                continue;

            options.Add(new LevelUpOption(LevelUpKind.Evolution, evolution.Id, evolution.NameIt, 0, 1,
                $"{evolution.DescriptionIt} Prende il posto di {move.Definition.NameIt}.", evolution.Id, evolution.Type,
                EvolvesFrom: move.Definition.Id, EvolveItem: evolution.EvolveItem));
        }

        return options;
    }

    public static void Apply(RunState run, LevelUpOption option)
    {
        var player = run.Player;
        switch (option.Kind)
        {
            // The cards were drawn when the level was reached; in the same tick the player may have picked the same
            // item up from the ground. A "new" card for something already held then raises it instead.
            case LevelUpKind.NewMove when player.Moves.FirstOrDefault(m => m.Definition.Id == option.Id) is { } owned:
                owned.Level = Math.Min(owned.Definition.MaxLevel, owned.Level + 1);
                break;
            case LevelUpKind.NewMove:
                var definition = run.MovePool.Single(d => d.Id == option.Id);
                player.Moves.Add(new MoveInstance(definition, Abilities.AbilitySystem.NewMoveLevel(run, definition)) { AcquiredTick = run.Ticks });
                break;
            case LevelUpKind.MoveUpgrade when player.Moves.FirstOrDefault(m => m.Definition.Id == option.Id) is { } move:
                move.Level = Math.Min(move.Definition.MaxLevel, move.Level + 1);
                break;
            case LevelUpKind.NewItem or LevelUpKind.ItemUpgrade when player.Items.FirstOrDefault(i => i.Definition.Id == option.Id) is { } held:
                held.Level = Math.Min(held.Definition.MaxLevel, held.Level + 1);
                player.RecalculateStats();
                break;
            case LevelUpKind.NewItem when player.Items.Count < MaxItems:
                player.Items.Add(new ItemInstance(run.ItemPool.Single(d => d.Id == option.Id)) { AcquiredTick = run.Ticks });
                player.RecalculateStats();
                break;
            case LevelUpKind.Heal:
                player.Heal(HealAmount);
                break;
            case LevelUpKind.Coins:
                run.AddCoins(CoinAmount);
                break;
            case LevelUpKind.Evolution:
                Evolve(run, run.Evolutions.Single(d => d.Id == option.Id));
                break;
            case LevelUpKind.EvolutionItem:
                run.EvolutionItems.Add(option.Id);
                break;
        }
    }

    /// <summary>The evolution takes the base move's slot, its acquisition time and its damage so far.</summary>
    private static void Evolve(RunState run, MoveDefinition evolution)
    {
        var moves = run.Player.Moves;
        var slot = moves.FindIndex(m => m.Definition.Id == evolution.EvolvesFrom);
        if (slot < 0)
            return;

        var from = moves[slot];
        moves[slot] = new MoveInstance(evolution) { AcquiredTick = from.AcquiredTick, DamageDealt = from.DamageDealt };
        run.AddEventOnResume(new RunEvent(RunEventType.MoveEvolved, run.Player.Position, slot, MoveId: evolution.Id));
    }

    internal static LevelUpOption ItemUpgrade(ItemInstance item) =>
        new(LevelUpKind.ItemUpgrade, item.Definition.Id, item.Definition.NameIt, item.Level, item.Level + 1, Describe(item.Definition.PerLevel), item.Definition.IconKey);

    internal static LevelUpOption MoveUpgrade(MoveInstance move)
    {
        var from = move.Stats;
        var to = move.Definition.Levels[move.Level];
        return new LevelUpOption(LevelUpKind.MoveUpgrade, move.Definition.Id, move.Definition.NameIt, move.Level, move.Level + 1,
            DescribeUpgrade(from, to), move.Definition.Id, move.Definition.Type);
    }

    /// <summary>Human-readable differences between two levels of a move, e.g. "+1 rimbalzo, potenza +2".</summary>
    public static string DescribeUpgrade(MoveLevel from, MoveLevel to)
    {
        var parts = new List<string>();
        if (to.Chains > from.Chains)
            parts.Add(to.Chains - from.Chains == 1 ? "+1 rimbalzo" : $"+{to.Chains - from.Chains} rimbalzi");
        if (to.Amount > from.Amount)
            parts.Add(to.Amount - from.Amount == 1 ? "+1 colpo" : $"+{to.Amount - from.Amount} colpi");
        if (to.Pierce > from.Pierce)
            parts.Add($"perforazione +{to.Pierce - from.Pierce}");
        if (to.Power > from.Power)
            parts.Add($"potenza +{Number(to.Power - from.Power)}");
        if (to.Cooldown < from.Cooldown)
            parts.Add($"ricarica -{Number(from.Cooldown - to.Cooldown)} s");
        if (to.Range > from.Range)
            parts.Add($"portata +{Number(to.Range - from.Range)}");
        if (to.Radius > from.Radius)
            parts.Add($"area +{Number(to.Radius - from.Radius)}");
        if (to.Duration > from.Duration)
            parts.Add($"durata +{Number(to.Duration - from.Duration)} s");
        if (to.HitInterval < from.HitInterval)
            parts.Add("colpi più frequenti");
        if (to.Speed > from.Speed)
            parts.Add($"velocità +{Number(to.Speed - from.Speed)}");
        if (to.Knockback > from.Knockback)
            parts.Add("spinta più forte");

        var text = parts.Count == 0 ? "Migliora la mossa." : string.Join(", ", parts) + ".";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>Per-level effect of an item, e.g. "PS massimi +20%".</summary>
    public static string Describe(ItemBonus b)
    {
        var parts = new List<string>();
        if (b.MaxHpPercent != 0) parts.Add($"PS massimi +{Percent(b.MaxHpPercent)}");
        if (b.MoveSpeedPercent != 0) parts.Add($"velocità +{Percent(b.MoveSpeedPercent)}");
        if (b.RegenPerSecond != 0) parts.Add($"+{Number(b.RegenPerSecond)} PS al secondo");
        if (b.PickupRadiusPercent != 0) parts.Add($"raggio di raccolta +{Percent(b.PickupRadiusPercent)}");
        if (b.PhysicalPowerPercent != 0) parts.Add($"potenza fisica +{Percent(b.PhysicalPowerPercent)}");
        if (b.SpecialPowerPercent != 0) parts.Add($"potenza speciale +{Percent(b.SpecialPowerPercent)}");
        if (b.CooldownReductionPercent != 0) parts.Add($"ricarica -{Percent(b.CooldownReductionPercent)}");
        if (b.GrowthPercent != 0) parts.Add($"esperienza +{Percent(b.GrowthPercent)}");
        if (b.AreaPercent != 0) parts.Add($"area delle mosse +{Percent(b.AreaPercent)}");
        if (b.DurationPercent != 0) parts.Add($"durata delle mosse +{Percent(b.DurationPercent)}");
        if (b.AmountBonus != 0) parts.Add(b.AmountBonus == 1 ? "+1 colpo per mossa" : $"+{b.AmountBonus} colpi per mossa");
        if (b.ProjectileSpeedPercent != 0) parts.Add($"velocità dei colpi +{Percent(b.ProjectileSpeedPercent)}");
        if (b.PhysicalResistPercent != 0) parts.Add($"danni dai nemici fisici -{Percent(b.PhysicalResistPercent)}");
        if (b.SpecialResistPercent != 0) parts.Add($"danni dai nemici speciali -{Percent(b.SpecialResistPercent)}");
        if (b.CoinPercent != 0) parts.Add($"monete +{Percent(b.CoinPercent)}");
        if (b.CritChance != 0) parts.Add($"probabilità di brutto colpo +{Percent(b.CritChance)}");
        if (b.Revives != 0) parts.Add(b.Revives == 1 ? "+1 rinascita" : $"+{b.Revives} rinascite");
        if (b.LifestealPercent != 0) parts.Add($"recuperi il {Percent(b.LifestealPercent)} dei danni inflitti");

        var text = string.Join(", ", parts) + ".";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>0.08 → "8%", 0.005 → "0,5%" (Italian decimal comma).</summary>
    private static string Percent(float fraction) => $"{(fraction * 100f).ToString("0.#", Italian).Replace('.', ',')}%";

    /// <summary>Italian decimal comma: 0.05 → "0,05".</summary>
    private static string Number(float value) => value.ToString("0.##", Italian).Replace('.', ',');
}
