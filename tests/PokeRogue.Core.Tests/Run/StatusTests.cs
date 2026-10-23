using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.Core.Tests.Run;

public class StatusTests
{
    private static RunState NewRun(StatusLibrary? rules = null)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            StatusRules = rules ?? TestContent.Statuses.Value.WithAllEnabled(),
        };
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        return run;
    }

    /// <summary>A sturdy Rattata, so damage over time never knocks it out mid-test.</summary>
    private static int Spawn(RunState run, Vector2 position, int species = 19) =>
        run.Enemies.Spawn(TestContent.Species(species), position, hpMultiplier: 100f);

    private static void Tick(RunState run, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            run.Tick(default);
    }

    private static StatusSpec Rule(StatusKind kind) => TestContent.Statuses.Value[kind]!;

    [Fact]
    public void Every_status_has_rules()
    {
        var library = TestContent.Statuses.Value;

        Assert.All(Enum.GetValues<StatusKind>().Where(k => k != StatusKind.None), k => Assert.NotNull(library[k]));
        Assert.Null(library[StatusKind.None]);
    }

    [Fact]
    public void Every_status_approved_in_phase_2_3_is_enabled()
    {
        Assert.All(TestContent.Statuses.Value.All, s => Assert.True(s.Enabled, $"{s.Kind} is disabled"));
    }

    [Fact]
    public void Burn_ticks_a_share_of_the_hit_that_caused_it()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(300, 0));
        var hp = run.Enemies[index].Hp;
        var burn = Rule(StatusKind.Burn);

        Assert.True(StatusSystem.Apply(run, index, StatusKind.Burn, hitDamage: 20f));
        Tick(run, burn.DurationTicks);

        var pulses = burn.DurationTicks / burn.TickIntervalTicks;
        Assert.Equal(hp - pulses * 20f * burn.HitDamageShare, run.Enemies[index].Hp, 2);
        Assert.Equal(StatusKind.None, run.Enemies[index].Status);
    }

    [Fact]
    public void Poison_takes_a_share_of_max_hp_every_second()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(300, 0));
        var maxHp = run.Enemies[index].MaxHp;

        StatusSystem.Apply(run, index, StatusKind.Poison);
        Tick(run, RunClock.TicksPerSecond * 2);

        Assert.Equal(maxHp * (1f - 2f * Rule(StatusKind.Poison).MaxHpShare), run.Enemies[index].Hp, 2);
        Assert.Contains(run.Events, e => e.Type == RunEventType.StatusDamage && e.Status == StatusKind.Poison);
    }

    [Fact]
    public void Burned_enemies_hit_softer_and_frozen_or_sleeping_ones_not_at_all()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(300, 0));

        (StatusKind Kind, float Share)[] cases = [(StatusKind.Burn, 0.5f), (StatusKind.Freeze, 0f), (StatusKind.Sleep, 0f), (StatusKind.Poison, 1f)];
        foreach (var (kind, share) in cases)
        {
            StatusSystem.End(run, index);
            StatusSystem.Apply(run, index, kind, hitDamage: 10f);

            var velocity = Vector2.UnitX;
            var contact = 10f;
            StatusSystem.Steer(run, run.Enemies[index], ref velocity, ref contact);
            Assert.Equal(10f * share, contact, 3);
        }
    }

    [Fact]
    public void Paralysis_halves_speed()
    {
        var run = NewRun();
        var normal = Spawn(run, new Vector2(300, 0));
        var paralyzed = Spawn(run, new Vector2(-300, 0));
        StatusSystem.Apply(run, paralyzed, StatusKind.Paralysis);

        // Shorter than the first lock-up roll.
        Tick(run, 30);

        var walked = 300f - run.Enemies[normal].Position.X;
        var slowed = run.Enemies[paralyzed].Position.X + 300f;
        Assert.Equal(walked * Rule(StatusKind.Paralysis).Speed, slowed, 1);
    }

    [Fact]
    public void A_paralysis_lock_up_stops_the_enemy_on_the_spot()
    {
        var run = NewRun(StatusLibrary.From(new StatusFile(1, [Rule(StatusKind.Paralysis) with { Chance = 1f }])));
        var index = Spawn(run, new Vector2(300, 0));
        StatusSystem.Apply(run, index, StatusKind.Paralysis);

        Tick(run, Rule(StatusKind.Paralysis).TickIntervalTicks);
        var position = run.Enemies[index].Position;
        Tick(run, 10);

        Assert.True(run.Enemies[index].IsImmobilized);
        Assert.Equal(position, run.Enemies[index].Position);
    }

    [Fact]
    public void Frozen_enemies_stay_put_until_a_fire_move_thaws_them()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(200, 0));
        StatusSystem.Apply(run, index, StatusKind.Freeze);
        var start = run.Enemies[index].Position;

        Tick(run, 60);
        Assert.Equal(start, run.Enemies[index].Position);

        run.HitEnemy(index, new MoveInstance(TestContent.Move("ember")), Vector2.UnitX);
        Assert.NotEqual(StatusKind.Freeze, run.Enemies[index].Status);
        Assert.Contains(run.Events, e => e.Type == RunEventType.StatusEnded && e.Status == StatusKind.Freeze);
    }

    [Fact]
    public void Sleep_wears_off_after_its_duration()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(200, 0));
        StatusSystem.Apply(run, index, StatusKind.Sleep);

        Tick(run, Rule(StatusKind.Sleep).DurationTicks - 1);
        Assert.Equal(StatusKind.Sleep, run.Enemies[index].Status);

        Tick(run, 1);
        Assert.Equal(StatusKind.None, run.Enemies[index].Status);
        Assert.Contains(run.Events, e => e.Type == RunEventType.StatusEnded && e.Status == StatusKind.Sleep);
    }

    [Fact]
    public void Confused_enemies_wander_instead_of_chasing()
    {
        var run = NewRun();
        var chasing = Spawn(run, new Vector2(0, 200));
        var confused = Spawn(run, new Vector2(0, -200));
        StatusSystem.Apply(run, confused, StatusKind.Confusion);

        Tick(run, 40);

        var chased = 200f - run.Enemies[chasing].Position.Length();
        var approached = 200f - run.Enemies[confused].Position.Length();
        Assert.True(approached < chased, $"confused {approached:0.0} px, chasing {chased:0.0} px");
    }

    [Fact]
    public void One_status_at_a_time_and_the_same_one_refreshes()
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(300, 0));

        Assert.True(StatusSystem.Apply(run, index, StatusKind.Poison));
        Tick(run, 60);

        Assert.False(StatusSystem.Apply(run, index, StatusKind.Burn));
        Assert.True(StatusSystem.Apply(run, index, StatusKind.Poison));
        Assert.Equal(Rule(StatusKind.Poison).DurationTicks, run.Enemies[index].StatusTicks);
    }

    [Theory]
    [InlineData(4, StatusKind.Burn)]
    [InlineData(23, StatusKind.Poison)]
    [InlineData(81, StatusKind.Poison)]
    [InlineData(25, StatusKind.Paralysis)]
    [InlineData(124, StatusKind.Freeze)]
    public void Types_are_immune_as_in_the_games(int species, StatusKind kind)
    {
        var run = NewRun();
        var index = Spawn(run, new Vector2(300, 0), species);

        Assert.False(StatusSystem.Apply(run, index, kind));
        Assert.Equal(StatusKind.None, run.Enemies[index].Status);
    }

    [Fact]
    public void Moves_inflict_their_status_only_once_it_is_enabled()
    {
        var sureEmber = TestContent.Move("ember") with { StatusChance = 1f };

        var disabled = NewRun(StatusLibrary.From(new StatusFile(1, [Rule(StatusKind.Burn) with { Enabled = false }])));
        var a = Spawn(disabled, new Vector2(300, 0));
        disabled.HitEnemy(a, new MoveInstance(sureEmber), Vector2.UnitX);
        Assert.Equal(StatusKind.None, disabled.Enemies[a].Status);

        var enabled = NewRun();
        var b = Spawn(enabled, new Vector2(300, 0));
        enabled.HitEnemy(b, new MoveInstance(sureEmber), Vector2.UnitX);
        Assert.Equal(StatusKind.Burn, enabled.Enemies[b].Status);
        Assert.Contains(enabled.Events, e => e.Type == RunEventType.StatusApplied && e.Status == StatusKind.Burn);
    }

    [Fact]
    public void Status_damage_is_credited_to_the_move_that_inflicted_it()
    {
        var run = NewRun();
        var ember = new MoveInstance(TestContent.Move("ember") with { StatusChance = 1f });
        run.Player.Moves.Add(ember);

        // Out of Ember's range, so only the burn adds damage after the first hit.
        var index = Spawn(run, new Vector2(400, 0));
        run.HitEnemy(index, ember, Vector2.UnitX);
        var afterHit = ember.DamageDealt;
        Tick(run, RunClock.TicksPerSecond);

        Assert.True(ember.DamageDealt > afterHit);
    }
}
