using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Tests.Run;

/// <summary>One behaviour test per archetype, using the real moves of content/data/moves.json.</summary>
public class MoveArchetypeTests
{
    private static RunState NewRun(string moveId, int level = 1)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360));
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        run.Player.Moves.Add(new MoveInstance(TestContent.Move(moveId), level));
        return run;
    }

    /// <summary>A tough, motionless Rattata, so hits can be counted.</summary>
    private static int Dummy(RunState run, float x, float y)
    {
        var index = run.Enemies.Spawn(TestContent.Species(19), new Vector2(x, y), hpMultiplier: 1000);
        run.Enemies[index].Speed = 0;
        return index;
    }

    private static Dictionary<int, int> CountHits(RunState run, int ticks, params int[] enemies)
    {
        var hits = enemies.ToDictionary(e => e, _ => 0);
        for (var t = 0; t < ticks; t++)
        {
            var before = enemies.ToDictionary(e => e, e => run.Enemies[e].Hp);
            run.Tick(default);
            foreach (var e in enemies)
            {
                if (run.Enemies[e].Hp < before[e])
                    hits[e]++;
            }
        }

        return hits;
    }

    [Fact]
    public void Homing_shots_follow_a_target_that_moved_away()
    {
        var run = NewRun("aerial-ace");
        var target = Dummy(run, 100, 0);

        run.Tick(default); // fired towards +X
        run.Enemies[target].Position = new Vector2(40, 90);

        Assert.True(CountHits(run, 90, target)[target] >= 1);
    }

    [Fact]
    public void Straight_shots_do_not_follow()
    {
        var run = NewRun("ember");
        var target = Dummy(run, 100, 0);

        run.Tick(default);
        run.Enemies[target].Position = new Vector2(40, 90);

        // Ember keeps firing at the new position, so only the first shot can tell: it must fly straight past.
        run.Player.Moves[0].Cooldown = 10_000;
        Assert.Equal(0, CountHits(run, 60, target)[target]);
    }

    [Fact]
    public void Boomerang_hits_on_the_way_out_and_back_then_returns()
    {
        var run = NewRun("bonemerang");
        var target = Dummy(run, 60, 0);

        run.Tick(default);
        run.Player.Moves[0].Cooldown = 10_000;
        var hits = CountHits(run, 170, target)[target];

        Assert.Equal(2, hits);
        Assert.Equal(0, run.MoveSystem.Projectiles.ActiveCount);
    }

    [Fact]
    public void Orbit_hits_repeatedly_at_its_hit_interval()
    {
        var run = NewRun("razor-leaf");
        var target = Dummy(run, 46, 0);

        // 2.5 s of orbiting with a 0.4 s hit delay: several hits, never two within the delay.
        var hits = CountHits(run, 150, target)[target];

        Assert.InRange(hits, 3, 7);
    }

    [Fact]
    public void Aura_hits_everything_inside_and_nothing_outside()
    {
        var run = NewRun("smog");
        var inside = Dummy(run, 20, 0);
        var outside = Dummy(run, 120, 0);

        var hits = CountHits(run, 1, inside, outside);

        Assert.Equal(1, hits[inside]);
        Assert.Equal(0, hits[outside]);
        Assert.Contains(run.Events, e => e.Type == RunEventType.AuraPulse);
    }

    [Fact]
    public void Area_bonus_grows_the_aura()
    {
        var run = NewRun("smog");
        var edge = Dummy(run, 55, 0);
        Assert.Equal(0, CountHits(run, 1, edge)[edge]);

        run.Player.Items.Add(new ItemInstance(new ItemDefinition("test-lens", "Lente", "wide-lens", "", 5, new ItemBonus(AreaPercent: 0.5f))));
        run.Player.RecalculateStats();
        run.Player.Moves[0].Cooldown = 0;

        Assert.Equal(1, CountHits(run, 1, edge)[edge]);
    }

    [Fact]
    public void Slash_hits_the_facing_side_only_at_level_one()
    {
        var run = NewRun("vine-whip");
        var front = Dummy(run, 0, 40);  // the player faces down at the start
        var behind = Dummy(run, 0, -40);

        var hits = CountHits(run, 1, front, behind);

        Assert.Equal(1, hits[front]);
        Assert.Equal(0, hits[behind]);
    }

    [Fact]
    public void Slash_level_two_also_hits_behind_after_a_short_delay()
    {
        var run = NewRun("vine-whip", level: 2);
        var behind = Dummy(run, 0, -40);

        Assert.Equal(1, CountHits(run, 20, behind)[behind]);
    }

    [Fact]
    public void Beam_hits_along_its_line_and_misses_the_side()
    {
        var run = NewRun("aurora-beam");
        var near = Dummy(run, 50, 0);
        var far = Dummy(run, 150, 0);
        var aside = Dummy(run, 100, 60);

        var hits = CountHits(run, 36, near, far, aside);

        Assert.True(hits[near] >= 2, $"near hit {hits[near]} times");
        Assert.True(hits[far] >= 2, $"far hit {hits[far]} times");
        Assert.Equal(0, hits[aside]);
    }

    [Fact]
    public void Strike_lands_after_its_warning()
    {
        var run = NewRun("rock-slide");
        var target = Dummy(run, 100, 0);

        run.Tick(default);
        Assert.Contains(run.MoveSystem.Pending, p => p.Kind == PendingKind.Strike);

        // 0.45 s warning (27 ticks): nothing yet, then exactly one impact on this target.
        Assert.Equal(0, CountHits(run, 20, target)[target]);
        Assert.Equal(1, CountHits(run, 20, target)[target]);
    }

    [Fact]
    public void Zone_on_an_enemy_keeps_hitting_while_it_lasts()
    {
        var run = NewRun("mud-bomb");
        var target = Dummy(run, 100, 0);

        // 2.5 s zone with a 0.5 s hit delay.
        var hits = CountHits(run, 150, target)[target];

        Assert.InRange(hits, 4, 6);
    }

    [Fact]
    public void Trail_zones_drop_where_the_player_stands()
    {
        var run = NewRun("flame-wheel");

        run.Tick(default);

        var zone = Assert.Single(run.MoveSystem.Zones);
        Assert.True(Vector2.Distance(zone.Position, run.Player.Position) <= zone.Radius);
    }

    [Fact]
    public void Ring_hits_each_enemy_once_within_its_size()
    {
        var run = NewRun("earthquake");
        var near = Dummy(run, 60, 0);
        var edge = Dummy(run, 0, 120);
        var beyond = Dummy(run, 200, 0);

        run.Tick(default);
        run.Player.Moves[0].Cooldown = 10_000;
        var hits = CountHits(run, 60, near, edge, beyond);

        Assert.Equal(1, hits[near]);
        Assert.Equal(1, hits[edge]);
        Assert.Equal(0, hits[beyond]);
    }
}
