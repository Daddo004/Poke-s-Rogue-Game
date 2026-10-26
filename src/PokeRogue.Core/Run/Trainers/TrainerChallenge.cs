using PokeRogue.Core.Run.Enemies;

namespace PokeRogue.Core.Run.Trainers;

/// <summary>
/// A trainer's challenge in progress (phase 5.3): when the level's time runs out the trainer steps in instead of the
/// level's boss, and sends out their team one Pokémon at a time, each a boss. Beating the last wins the run and the trainer.
/// </summary>
public sealed class TrainerChallenge(TrainerDefinition trainer, ChallengeRules rules)
{
    public TrainerDefinition Trainer { get; } = trainer;

    public IReadOnlyList<BossDefinition> Team => Trainer.Team;

    /// <summary>The trainer has stepped in (the time ran out).</summary>
    public bool Started { get; private set; }

    /// <summary>How many Pokémon have been sent out; the one fighting is the last of them.</summary>
    public int SentOut { get; private set; }

    /// <summary>How many have fainted.</summary>
    public int Fainted { get; internal set; }

    /// <summary>The whole team fainted: the trainer is beaten (the meta-progression then unlocks them and, for a gym leader, gives a medal).</summary>
    public bool Won { get; internal set; }

    /// <summary>The tick the next Pokémon comes out, once the previous one has fainted.</summary>
    public int NextTick { get; private set; }

    public int NextDelayTicks { get; } = Math.Max(1, RunClock.Seconds(rules.NextDelay));

    public bool HasMore => SentOut < Team.Count;

    /// <summary>The trainer steps in: the first Pokémon comes out with the next tick (after the game's introduction).</summary>
    public void Begin(RunState run)
    {
        Started = true;
        NextTick = run.Ticks + 1;
        run.AddEvent(new RunEvent(RunEventType.TrainerChallenged, run.Player.Position, Team.Count, MoveId: Trainer.Id));
    }

    /// <summary>The next Pokémon of the team comes out, as a boss beside the player.</summary>
    public BossFight SendOut(RunState run)
    {
        var member = Team[SentOut];
        var fight = BossAi.Spawn(run, member, run.Spawner.Timeline.HpMultiplier(run.Ticks), announce: false);
        run.AddEvent(new RunEvent(RunEventType.TrainerSentOut, run.Enemies[fight.Index].Position, SentOut, member.SpeciesId, MoveId: Trainer.Id));
        SentOut++;
        return fight;
    }

    /// <summary>A Pokémon fainted at <paramref name="tick"/>: the next one waits a moment.</summary>
    internal void OnFainted(int tick)
    {
        Fainted++;
        NextTick = tick + NextDelayTicks;
    }
}
