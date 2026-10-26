using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.Core.Tests.Run;

public class ChallengeTests
{
    private static TrainerLibrary Trainers => TrainerTests.Trainers.Value;

    private static RunState Challenge(string trainerId)
    {
        var character = TestContent.Characters.Value["pikachu"];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            TimeLimitTicks = 10,
        };
        run.Spawner.Enabled = false;
        run.UseCharacter(character);
        run.Player.Moves.Clear();
        run.Player.Invincible = true;
        run.ChallengeTrainer(Trainers[trainerId], Trainers.Challenge!);
        return run;
    }

    private static List<RunEvent> Until(RunState run, Func<RunState, bool> done, int limit = 2000)
    {
        var events = new List<RunEvent>();
        for (var i = 0; i < limit && !done(run); i++)
        {
            run.Tick(default);
            events.AddRange(run.Events);
        }

        return events;
    }

    [Fact]
    public void Every_trainer_but_ash_has_a_team_that_grows_with_their_rank()
    {
        foreach (var trainer in Trainers.All)
        {
            var expected = trainer.Role switch
            {
                TrainerRole.Base => 0,
                TrainerRole.GymLeader => 3,
                TrainerRole.EliteFour => 4,
                _ => 5,
            };
            Assert.True(expected == trainer.Team.Count, $"{trainer.Id} has {trainer.Team.Count} Pokémon");
        }

        var brock = Trainers["brock"].Team;
        Assert.Equal(3, brock[^1].Phases.Count);
        Assert.Equal(2, brock[0].Phases.Count);
    }

    [Fact]
    public void Rock_and_ground_pokemon_throw_rocks_instead_of_shots()
    {
        var geodude = Trainers["brock"].Team[0];
        var staryu = Trainers["misty"].Team[0];

        Assert.DoesNotContain(geodude.Phases.SelectMany(p => p.Attacks), a => a.Spec.Kind == BossAttackKind.Volley);
        Assert.Contains(staryu.Phases.SelectMany(p => p.Attacks), a => a.Spec.Kind == BossAttackKind.Volley && a.Move?.Id == "bubble");
    }

    [Fact]
    public void The_trainer_steps_in_at_the_time_limit_and_sends_out_the_team_in_order()
    {
        var run = Challenge("brock");
        var events = Until(run, r => r.Boss is not null);

        Assert.Contains(events, e => e.Type == RunEventType.TrainerChallenged && e.MoveId == "brock");
        Assert.DoesNotContain(events, e => e.Type == RunEventType.BossAppeared);
        Assert.Equal(Trainers["brock"].Team[0].SpeciesId, run.Enemies[run.Boss!.Index].SpeciesId);

        // The first one faints: the second comes out a moment later, not at once.
        run.DamageEnemy(run.Boss.Index, float.MaxValue);
        run.Tick(default);
        Assert.Equal(1, run.Challenge!.SentOut);
        events = Until(run, r => r.Challenge!.SentOut == 2);
        Assert.Contains(events, e => e.Type == RunEventType.TrainerSentOut && (int)e.Value == 1);
        Assert.Equal(Trainers["brock"].Team[1].SpeciesId, run.Enemies[run.Boss.Index].SpeciesId);
        Assert.False(run.IsOver);
    }

    [Fact]
    public void Beating_the_whole_team_beats_the_trainer_and_wins()
    {
        var run = Challenge("brock");
        var events = new List<RunEvent>();
        while (!run.IsOver && run.Ticks < 5000)
        {
            if (run.Boss is { DefeatedTick: null } fight)
                run.DamageEnemy(fight.Index, float.MaxValue);
            run.Tick(default);
            events.AddRange(run.Events);
        }

        Assert.Equal(RunOutcome.Victory, run.Outcome);
        Assert.True(run.Challenge!.Won);
        Assert.Equal(3, run.Challenge.Fainted);
        Assert.Contains(events, e => e.Type == RunEventType.TrainerDefeated && e.MoveId == "brock");
    }

    [Fact]
    public void Ash_cannot_be_challenged()
    {
        var run = Challenge("misty");

        Assert.Throws<ArgumentException>(() => run.ChallengeTrainer(Trainers["ash"], Trainers.Challenge!));
    }
}
