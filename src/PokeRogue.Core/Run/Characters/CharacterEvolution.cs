using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run.Characters;

/// <summary>
/// The player's own evolution during a run (phase 4.2). A stage evolves at its run level, or once its item is found in
/// a Dominante's chest. When more than one evolution is ready (Eevee, Kirlia with the Pietralbore) the player chooses;
/// the player may also stop it, as with B in the games, and it is offered again at the next level.
/// </summary>
public static class CharacterEvolution
{
    /// <summary>Run level from which chests give the item for a second stage (around minute 5) and for a third (around minute 9).</summary>
    public const int SecondStageItemLevel = 14;

    public const int ThirdStageItemLevel = 30;

    public static int ItemLevel(CharacterStage target) => target.Depth <= 1 ? SecondStageItemLevel : ThirdStageItemLevel;

    /// <summary>The evolutions the player can take now; none while one was stopped at this level.</summary>
    public static IReadOnlyList<CharacterStage> Ready(RunState run)
    {
        if (run.Character is not { } character || run.Stage is not { } stage || run.EvolutionDeclinedAtLevel == run.Player.Level)
            return [];

        return character.EvolutionsOf(stage)
            .Where(s => s.Level is { } level ? run.Player.Level >= level : run.EvolutionItems.Contains(s.Item!))
            .ToList();
    }

    /// <summary>The evolution whose item the next chest should give, if the player has reached its level and lacks it.</summary>
    public static CharacterStage? ItemDue(RunState run)
    {
        if (run.Character is not { } character || run.Stage is not { } stage)
            return null;

        return character.EvolutionsOf(stage).FirstOrDefault(s =>
            s.Item is { } item && !run.EvolutionItems.Contains(item) && run.Player.Level >= ItemLevel(s));
    }

    /// <summary>A chest reward: the item <paramref name="target"/> evolves with.</summary>
    public static LevelUpOption ItemReward(RunState run, CharacterStage target) =>
        new(LevelUpKind.EvolutionItem, target.Item!, target.ItemNameIt ?? target.Item!, 0, 0,
            $"{run.Player.Species.NameIt} può evolversi in {target.Species.NameIt}.", target.Item!);

    /// <summary>Becomes <paramref name="target"/>; the item it needed is used up. PlayerEvolved comes out when the game resumes.</summary>
    public static void Evolve(RunState run, CharacterStage target)
    {
        var from = run.Player.Species;
        run.Player.Evolve(target.Species);
        run.Stage = target;
        if (target.Item is { } item)
            run.EvolutionItems.Remove(item);
        run.EvolutionDeclinedAtLevel = 0;
        run.AddEventOnResume(new RunEvent(RunEventType.PlayerEvolved, run.Player.Position, from.Id, target.Species.Id));
    }

    /// <summary>The player stopped the evolution: it is offered again from the next level.</summary>
    public static void Decline(RunState run) => run.EvolutionDeclinedAtLevel = run.Player.Level;
}
