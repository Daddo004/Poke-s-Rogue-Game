using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Tests.Run;

public class MoveSystemTests
{
    private static RunState NewRun(string moveId, int level = 1)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360));
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        run.Player.Moves.Add(new MoveInstance(TestContent.Move(moveId), level));
        return run;
    }

    [Fact]
    public void Projectile_travels_to_the_nearest_enemy_and_damages_it()
    {
        var run = NewRun("ember");
        var near = run.Enemies.Spawn(TestContent.Species(19), new Vector2(100, 0), hpMultiplier: 50);
        var far = run.Enemies.Spawn(TestContent.Species(19), new Vector2(-200, 0), hpMultiplier: 50);

        var hits = new List<RunEvent>();
        for (var i = 0; i < 40; i++)
        {
            run.Tick(default);
            hits.AddRange(run.Events.Where(e => e.Type == RunEventType.EnemyHit));
        }

        Assert.NotEmpty(hits);
        Assert.True(run.Enemies[near].Hp < run.Enemies[near].MaxHp);
        Assert.Equal(run.Enemies[far].MaxHp, run.Enemies[far].Hp);
        Assert.True(run.Player.Moves[0].DamageDealt > 0);
    }

    [Fact]
    public void Move_waits_while_no_enemy_is_in_range()
    {
        var run = NewRun("ember");
        run.Enemies.Spawn(TestContent.Species(19), new Vector2(2000, 0));

        run.Tick(default);

        Assert.Equal(0, run.Player.Moves[0].Cooldown);
        Assert.Equal(0, run.MoveSystem.Projectiles.ActiveCount);
    }

    [Fact]
    public void Pierce_hits_enemies_in_a_line_once_each()
    {
        var run = NewRun("ember", level: 5); // amount 3, pierce 2
        var a = run.Enemies.Spawn(TestContent.Species(19), new Vector2(60, 0), hpMultiplier: 100);
        var b = run.Enemies.Spawn(TestContent.Species(19), new Vector2(110, 0), hpMultiplier: 100);
        run.Enemies[a].Speed = run.Enemies[b].Speed = 0;

        var hitsBySlot = new Dictionary<int, int>();
        for (var i = 0; i < 30; i++)
        {
            run.Tick(default);
            foreach (var e in run.Events.Where(e => e.Type == RunEventType.EnemyHit))
            {
                var slot = e.Position.X < 85 ? a : b;
                hitsBySlot[slot] = hitsBySlot.GetValueOrDefault(slot) + 1;
            }
        }

        Assert.True(hitsBySlot.GetValueOrDefault(a) >= 1);
        Assert.True(hitsBySlot.GetValueOrDefault(b) >= 1);
    }

    [Fact]
    public void Arc_chains_through_nearby_enemies_in_one_use()
    {
        var run = NewRun("thundershock", level: 4); // 2 chains
        int[] slots =
        [
            run.Enemies.Spawn(TestContent.Species(19), new Vector2(60, 0), hpMultiplier: 100),
            run.Enemies.Spawn(TestContent.Species(19), new Vector2(110, 0), hpMultiplier: 100),
            run.Enemies.Spawn(TestContent.Species(19), new Vector2(160, 0), hpMultiplier: 100),
        ];

        run.Tick(default);

        Assert.Equal(3, run.Events.Count(e => e.Type == RunEventType.ArcSegment));
        Assert.All(slots, s => Assert.True(run.Enemies[s].Hp < run.Enemies[s].MaxHp));
    }
}
