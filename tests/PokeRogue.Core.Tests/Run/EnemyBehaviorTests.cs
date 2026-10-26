using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 3.2: enemy behaviours, with every profile of content/data/enemies.json switched on.</summary>
public class EnemyBehaviorTests
{
    private static RunState NewRun()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            EnemyRules = TestContent.Enemies.Value.WithDisabled(),
        };
        run.Spawner.Enabled = false;
        return run;
    }

    private static int Id(string key) => TestContent.Pokedex.Value.Species.Single(s => s.Key == key).Id;

    private static List<RunEvent> Run(RunState run, int ticks, Action<RunState>? each = null)
    {
        var events = new List<RunEvent>();
        for (var i = 0; i < ticks && !run.IsOver; i++)
        {
            run.Tick(default);
            each?.Invoke(run);
            events.AddRange(run.Events);
        }

        return events;
    }

    [Fact]
    public void Profiles_waiting_for_approval_stay_out_of_real_runs()
    {
        var library = TestContent.Enemies.Value;

        Assert.All(library.All.Where(p => !p.Spec.Enabled), p => Assert.Null(library.For(p.SpeciesId)));
        Assert.NotNull(library.WithDisabled().For(Id("voltorb")));
        Assert.Null(library.For(Id("rattata")));
    }

    [Fact]
    public void Bad_profiles_are_refused()
    {
        var pokedex = TestContent.Pokedex.Value.Species;
        var moves = TestContent.Moves.Value;
        EnemyLibrary Load(EnemySpec spec) => EnemyLibrary.From(new EnemyFile(1, [spec]), pokedex, moves);

        Assert.Throws<InvalidDataException>(() => Load(new EnemySpec { Pokemon = "missingno", Behavior = EnemyBehavior.Chase }));
        Assert.Throws<InvalidDataException>(() => Load(new EnemySpec { Pokemon = "beedrill", Behavior = EnemyBehavior.Ranged }));
        Assert.Throws<InvalidDataException>(() => Load(new EnemySpec { Pokemon = "zubat", Behavior = EnemyBehavior.Straight }));
        Assert.Throws<InvalidDataException>(() => Load(new EnemySpec { Pokemon = "abra", Behavior = EnemyBehavior.Teleport, HopMin = 200, HopMax = 100 }));
    }

    [Fact]
    public void A_shooter_keeps_its_distance_and_its_shots_hurt_the_player()
    {
        var run = NewRun();
        var index = run.SpawnEnemy(Id("beedrill"), new Vector2(400, 0));

        var events = Run(run, 60 * 8);

        var distance = run.Enemies[index].Position.Length();
        Assert.InRange(distance, 90f, 180f);
        Assert.Contains(events, e => e.Type == RunEventType.EnemyShotFired && e.MoveId == "poison-sting");
        Assert.Contains(events, e => e.Type == RunEventType.PlayerHurt);
        Assert.Contains(events, e => e.Type == RunEventType.EnemyShotEnded && e.Value == 1f);
    }

    [Fact]
    public void A_charger_takes_aim_then_rushes_past_the_player()
    {
        var run = NewRun();
        run.Player.Invincible = true;
        var index = run.SpawnEnemy(Id("tauros"), new Vector2(300, 0));
        var walk = run.Enemies[index].Speed;

        var actions = new List<EnemyAction>();
        var fastest = 0f;
        var farthestLeft = 0f;
        Run(run, 60 * 6, r =>
        {
            ref readonly var e = ref r.Enemies[index];
            actions.Add(e.Action);
            fastest = MathF.Max(fastest, e.Velocity.Length());
            farthestLeft = MathF.Min(farthestLeft, e.Position.X);
        });

        var windUp = actions.IndexOf(EnemyAction.WindUp);
        var dash = actions.IndexOf(EnemyAction.Dash);
        Assert.True(windUp >= 0 && dash > windUp, "it winds up before the charge");
        Assert.Equal(TestContent.Enemies.Value.WithDisabled().For(Id("tauros"))!.WindUpTicks, dash - windUp);
        Assert.Contains(EnemyAction.Recover, actions.Skip(dash));
        Assert.Equal(walk * 4f, fastest, 1);
        Assert.True(farthestLeft < -20f, $"the charge stops at x = {farthestLeft:0}, short of running past the player");
    }

    [Fact]
    public void Tanks_are_sturdy_and_barely_pushed()
    {
        var run = NewRun();
        var tank = run.SpawnEnemy(Id("metapod"), new Vector2(300, 0));
        var plain = run.Enemies.Spawn(TestContent.Species(11), new Vector2(-300, 0));

        Assert.Equal(run.Enemies[plain].MaxHp * 3f, run.Enemies[tank].MaxHp, 2);
        Assert.Equal(run.Enemies[plain].Speed * 0.6f, run.Enemies[tank].Speed, 2);

        run.DamageEnemy(tank, 1f, new Vector2(100, 0));
        run.DamageEnemy(plain, 1f, new Vector2(100, 0));
        Assert.Equal(15f, run.Enemies[tank].Knockback.X, 2);
        Assert.Equal(100f, run.Enemies[plain].Knockback.X, 2);
    }

    [Fact]
    public void A_kamikaze_lights_its_fuse_then_blows_up_without_a_gem()
    {
        var run = NewRun();
        var index = run.SpawnEnemy(Id("voltorb"), new Vector2(120, 0));

        var events = Run(run, 60 * 5);

        Assert.Contains(events, e => e.Type == RunEventType.EnemyExploded);
        Assert.Contains(events, e => e.Type == RunEventType.PlayerHurt && e.SpeciesId == Id("voltorb"));
        Assert.Equal(EnemyState.Inactive, run.Enemies[index].State);
        Assert.Equal(0, run.Stats.Kills);
        Assert.Equal(0, run.Pickups.Gems.ActiveCount);
    }

    [Fact]
    public void A_frozen_kamikaze_does_not_go_off()
    {
        var run = NewRun();
        run.StatusRules = TestContent.Statuses.Value;
        var index = run.SpawnEnemy(Id("voltorb"), new Vector2(30, 0));
        Run(run, 2);
        Assert.Equal(EnemyAction.Fuse, run.Enemies[index].Action);

        Core.Run.Statuses.StatusSystem.Apply(run, index, PokeRogue.Core.Combat.StatusKind.Freeze);
        var events = Run(run, 60);

        Assert.DoesNotContain(events, e => e.Type == RunEventType.EnemyExploded);
        Assert.True(run.Enemies[index].IsAlive);
    }

    [Fact]
    public void A_teleporter_hops_around_the_player()
    {
        var run = NewRun();
        run.Player.Invincible = true;
        var index = run.SpawnEnemy(Id("abra"), new Vector2(150, 0));
        var spec = TestContent.Enemies.Value.WithDisabled().For(Id("abra"))!.Spec;

        var events = Run(run, 60 * 10);

        var hops = events.Where(e => e.Type == RunEventType.EnemyTeleported).ToList();
        Assert.True(hops.Count >= 2, $"{hops.Count} hops in 10 seconds");
        Assert.All(hops, h => Assert.InRange(h.Target.Length(), spec.HopMin - 0.01f, spec.HopMax + 0.01f));
        Assert.True(run.Enemies[index].IsAlive);
    }

    [Fact]
    public void Stampedes_keep_their_course_even_with_a_charger_profile()
    {
        var waves = Core.Run.Waves.WaveTimeline.From(new Core.Run.Waves.WaveFile
        {
            Id = "prova",
            Events = [new Core.Run.Waves.WaveEventSpec { At = "0:00", Kind = Core.Run.Waves.WaveEventKind.Stampede, Pokemon = "tauros", Count = 3 }],
        }, TestContent.Pokedex.Value.Species);
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, waves, new Vector2(640, 360))
        {
            EnemyRules = TestContent.Enemies.Value.WithDisabled(),
        };

        run.Tick(default);

        Assert.All(run.Enemies.Items.ToArray().Where(e => e.IsAlive), e =>
        {
            Assert.Equal(EnemyBehavior.Straight, e.Behavior);
            Assert.Null(e.Profile);
        });
    }
}
