using System.Numerics;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run;

/// <summary>
/// A simple stand-in player for previews, benchmarks and balance simulations:
/// collect nearby gems, step away when enemies get too close, otherwise hold position and let moves work.
/// At level-ups it builds towards evolutions, as a player would (<see cref="ChooseLevelUp"/>).
/// </summary>
public static class AutoPilot
{
    /// <summary>
    /// The card a simple player takes: an evolution; else the upgrade of its most advanced move; else an item that one
    /// of its moves evolves with; else a new move while it has few; else an item upgrade; else the first card.
    /// </summary>
    public static int ChooseLevelUp(RunState run)
    {
        if (run.LevelUpChoices is not { Count: > 0 } choices)
            return 0;

        var best = 0;
        for (var i = 1; i < choices.Count; i++)
        {
            if (Score(run, choices[i]) > Score(run, choices[best]))
                best = i;
        }

        return best;
    }

    private static int Score(RunState run, LevelUpOption option) => option.Kind switch
    {
        LevelUpKind.Evolution => 1000,
        LevelUpKind.MoveUpgrade => 500 + option.FromLevel,
        LevelUpKind.NewItem when run.Evolutions.Any(e => e.EvolveItem == option.Id && run.Player.Moves.Any(m => m.Definition.Id == e.EvolvesFrom)) => 400,
        LevelUpKind.NewMove when run.Player.Moves.Count < 3 => 300,
        LevelUpKind.ItemUpgrade => 200,
        _ => 100,
    };

    private const float GemReach = 150f;

    /// <summary>With no gem close by, head for one this far: after fleeing a crowd, go back for what was left behind.</summary>
    private const float FarGemReach = 600f;

    private const float PersonalSpace = 36f;
    private const float CrowdRadius = 80f;

    public static Vector2 Input(RunState run)
    {
        var player = run.Player.Position;

        // Flee first: too many bodies around means taking hits.
        var crowd = Vector2.Zero;
        var nearest = float.MaxValue;
        foreach (ref readonly var e in run.Enemies.Items)
        {
            if (!e.IsAlive)
                continue;

            var offset = e.Position - player;
            var distance = offset.Length();
            nearest = MathF.Min(nearest, distance);
            if (distance < CrowdRadius && distance > 0.01f)
                crowd += offset / distance * (CrowdRadius - distance);
        }

        // An invincible player (previews, simulations) has nothing to fear: it walks through the crowd to the gems.
        if (nearest < PersonalSpace && crowd != Vector2.Zero && !run.Player.Invincible)
            return Vector2.Normalize(-crowd);

        var target = NearestGem(run, player, GemReach);
        if (target == Vector2.Zero)
            target = NearestGem(run, player, FarGemReach);
        return target == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(target);
    }

    /// <summary>Offset to the nearest gem within <paramref name="reach"/> that is not already flying in; zero when none.</summary>
    private static Vector2 NearestGem(RunState run, Vector2 player, float reach)
    {
        var best = reach * reach;
        var target = Vector2.Zero;
        foreach (ref readonly var gem in run.Pickups.Gems.Items)
        {
            if (!gem.Active || gem.Magnetized)
                continue;

            var d = Vector2.DistanceSquared(gem.Position, player);
            if (d < best)
            {
                best = d;
                target = gem.Position - player;
            }
        }

        return target;
    }
}
