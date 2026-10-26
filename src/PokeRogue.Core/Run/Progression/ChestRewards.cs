namespace PokeRogue.Core.Run.Progression;

/// <summary>A chest that was opened: its tier and the rewards it gave, already applied.</summary>
public sealed record ChestOpening(ChestTier Tier, IReadOnlyList<LevelUpOption> Rewards, int Tick);

/// <summary>
/// What a Dominante's chest gives (phase 3.3), as in Vampire Survivors: the item the player's own evolution needs
/// (phase 4.2), then a ready evolution, otherwise an upgrade
/// of a move or item the player already has; once everything is maxed, coins. Each reward is applied before the next
/// is drawn, so one chest can raise the same move twice.
/// </summary>
public static class ChestRewards
{
    public static int CountFor(ChestTier tier) => tier switch
    {
        ChestTier.Great => 3,
        ChestTier.Ultra => 5,
        _ => 1,
    };

    public static IReadOnlyList<LevelUpOption> Open(RunState run, int count)
    {
        var rewards = new List<LevelUpOption>();
        for (var i = 0; i < count; i++)
        {
            var reward = Next(run);
            LevelUp.Apply(run, reward);
            rewards.Add(reward);
        }

        return rewards;
    }

    private static LevelUpOption Next(RunState run)
    {
        // The item for the player's own evolution comes before anything else (phase 4.2).
        if (Characters.CharacterEvolution.ItemDue(run) is { } stage)
            return Characters.CharacterEvolution.ItemReward(run, stage);
        if (LevelUp.ReadyEvolutions(run) is [var evolution, ..])
            return evolution;

        var player = run.Player;
        var upgrades = player.Moves.Where(m => m.Level < m.Definition.MaxLevel).Select(LevelUp.MoveUpgrade)
            .Concat(player.Items.Where(i => i.Level < i.Definition.MaxLevel).Select(LevelUp.ItemUpgrade))
            .Where(o => !run.Banished.Contains(o.Id))
            .ToList();

        return upgrades.Count > 0
            ? upgrades[run.Random.Next(upgrades.Count)]
            : new LevelUpOption(LevelUpKind.Coins, "nugget", "Pepita", 0, 0, $"Ottieni {LevelUp.CoinAmount} monete.", "nugget");
    }
}
