using System.Numerics;
using PokeRogue.Core.Run;

namespace PokeRogue.Core.Tests.Run;

public class RunStateTests
{
    private static readonly Vector2 View = new(640, 360);

    private static RunState NewRun(bool spawning = false)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], View);
        run.Spawner.Enabled = spawning;
        return run;
    }

    [Fact]
    public void Pikachu_run_stats_come_from_base_stats()
    {
        var player = NewRun().Player;

        Assert.Equal(82f, player.MaxHp);
        Assert.Equal(100.5f, player.MoveSpeed, 3);
    }

    [Fact]
    public void Player_moves_at_its_speed_and_faces_the_input()
    {
        var run = NewRun();
        for (var i = 0; i < 60; i++)
            run.Tick(new PlayerInput(new Vector2(-1, 0)));

        Assert.Equal(-run.Player.MoveSpeed, run.Player.Position.X, 2);
        Assert.Equal(Geometry.Direction8.Left, run.Player.Facing);
    }

    [Fact]
    public void Enemies_chase_the_player()
    {
        var run = NewRun();
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(200, 0));

        for (var i = 0; i < 60; i++)
            run.Tick(default);

        Assert.InRange(run.Enemies[index].Position.X, 100f, 160f);
    }

    [Fact]
    public void Contact_damage_is_followed_by_invulnerability()
    {
        var run = NewRun();
        run.Enemies.Spawn(TestContent.Species(19), new Vector2(1, 0));

        run.Tick(default);
        var afterFirstHit = run.Player.Hp;
        for (var i = 0; i < Player.HitInvulnerabilityTicks - 2; i++)
            run.Tick(default);

        Assert.True(afterFirstHit < run.Player.MaxHp);
        Assert.Equal(afterFirstHit, run.Player.Hp);
        Assert.Contains(run.Enemies.Items.ToArray(), e => e.IsAlive);
    }

    [Fact]
    public void Defeated_enemies_faint_then_free_their_slot()
    {
        var run = NewRun();
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0));

        run.DamageEnemy(index, 9999f);

        Assert.Equal(EnemyState.Fainting, run.Enemies[index].State);
        Assert.Equal(0, run.Enemies.AliveCount);
        Assert.Contains(run.Events, e => e.Type == RunEventType.EnemyDefeated);

        for (var i = 0; i < Enemy.FaintTicks; i++)
            run.Tick(default);

        Assert.Equal(EnemyState.Inactive, run.Enemies[index].State);
    }

    [Fact]
    public void Spawner_places_enemies_just_outside_the_screen()
    {
        var run = NewRun();
        var random = new Random(4);
        for (var i = 0; i < 500; i++)
        {
            var p = run.Spawner.OffscreenPoint(random, Vector2.Zero);
            var onVertical = MathF.Abs(MathF.Abs(p.X) - (View.X / 2 + Spawner.SpawnMargin)) < 0.01f;
            var onHorizontal = MathF.Abs(MathF.Abs(p.Y) - (View.Y / 2 + Spawner.SpawnMargin)) < 0.01f;
            Assert.True(onVertical || onHorizontal, $"{p} is not on the spawn ring");
        }
    }

    [Fact]
    public void Spawner_respects_max_alive()
    {
        var run = NewRun(spawning: true);
        run.Spawner.MaxAlive = 20;
        run.Spawner.SpawnsPerSecond = 120;

        for (var i = 0; i < 120; i++)
            run.Tick(default);

        Assert.Equal(20, run.Enemies.AliveCount);
    }

    [Fact]
    public void A_crit_bonus_makes_critical_hits_more_likely()
    {
        var run = NewRun();
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0), hpMultiplier: 100f);
        var sure = new Core.Run.Moves.MoveInstance(TestContent.Move("karate-chop") with { CritBonus = 1f });

        run.HitEnemy(index, sure, Vector2.UnitX);

        Assert.Contains(run.Events, e => e.Type == RunEventType.EnemyHit && e.Critical);
        Assert.Equal(0.25f, TestContent.Move("karate-chop").CritBonus);
    }

    [Fact]
    public void Draining_moves_give_back_part_of_the_damage_as_hp()
    {
        var run = NewRun();
        run.Player.Hp -= 40f;
        var before = run.Player.Hp;
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0), hpMultiplier: 100f);

        run.HitEnemy(index, new Core.Run.Moves.MoveInstance(TestContent.Move("leech-life")), Vector2.UnitX);

        var dealt = run.Events.Single(e => e.Type == RunEventType.EnemyHit).Value;
        Assert.Equal(before + dealt * TestContent.Move("leech-life").Drain, run.Player.Hp, 3);
        Assert.True(run.Player.HpRecovered > 0f);
    }

    [Fact]
    public void Overlapping_enemies_are_pushed_apart()
    {
        var run = NewRun();
        var a = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0));
        var b = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0.5f));

        for (var i = 0; i < 30; i++)
            run.Tick(default);

        Assert.True(Vector2.Distance(run.Enemies[a].Position, run.Enemies[b].Position) > run.Enemies[a].Radius);
    }
}
