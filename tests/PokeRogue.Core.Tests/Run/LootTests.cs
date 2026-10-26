using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Phase 3.5: breakables, items on the ground, shinies.</summary>
public class LootTests
{
    private static RunState NewRun(LootFile? file = null)
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            LootRules = file is null ? TestContent.Loot.Value : LootRules.From(file),
        };
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        return run;
    }

    private static LootFile Field(float grass, float rock, float dropChance = 0f) => new()
    {
        CellSize = 100,
        Grass = new BreakableSpec { Chance = grass, Hp = 1, DropChance = dropChance },
        Rock = new BreakableSpec { Chance = rock, Hp = 40, Defense = 80, DropChance = dropChance },
        Drops = [new LootDropSpec { Kind = LootKind.Potion }],
    };

    private static List<int> Props(RunState run) =>
        Enumerable.Range(0, run.Enemies.HighWater).Where(i => run.Enemies[i] is { IsAlive: true, Rank: EnemyRank.Prop }).ToList();

    [Fact]
    public void Breakables_fill_the_map_around_the_player_and_do_not_count_as_enemies()
    {
        var run = NewRun(Field(grass: 0.5f, rock: 0.2f));

        run.Tick(default);

        var props = Props(run);
        Assert.InRange(props.Count, 10, 80);
        Assert.Contains(props, i => run.Enemies[i].Breakable == BreakableKind.Grass);
        Assert.Contains(props, i => run.Enemies[i].Breakable == BreakableKind.Rock);
        Assert.Equal(0, run.Enemies.AliveCount);

        // They stand still.
        var before = props.Select(i => run.Enemies[i].Position).ToList();
        for (var i = 0; i < 30; i++)
            run.Tick(default);
        Assert.Equal(before, props.Select(i => run.Enemies[i].Position));
    }

    [Fact]
    public void The_same_map_comes_back_and_broken_props_stay_broken()
    {
        var run = NewRun(Field(grass: 0.5f, rock: 0f));
        run.Tick(default);
        var first = Props(run).Select(i => run.Enemies[i].Position).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();

        // Break one, walk far away and come back.
        var broken = Props(run)[0];
        var brokenAt = run.Enemies[broken].Position;
        run.DamageEnemy(broken, 999f);
        Assert.Contains(run.Events, e => e.Type == RunEventType.PropBroken);
        run.Player.Position = new Vector2(5000, 5000);
        run.AdvanceTime(14);
        run.Tick(default);
        run.Player.Position = Vector2.Zero;
        run.AdvanceTime(14);
        run.Tick(default);

        var again = Props(run).Select(i => run.Enemies[i].Position).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        Assert.Equal(first.Where(p => p != brokenAt), again);
        Assert.Equal(0, run.Stats.Kills);
        Assert.Equal(0, run.Pickups.Gems.ActiveCount);
    }

    [Fact]
    public void Moves_never_aim_at_props_but_break_what_they_pass_through()
    {
        var run = NewRun(Field(0f, 0f));
        var grass = run.Enemies.SpawnProp(BreakableKind.Grass, new Vector2(40, 0), 1f, 10f, PokeRogue.Core.Data.PokemonType.Grass, 1);
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("ember")));

        // Nothing to aim at: the move waits.
        for (var i = 0; i < 120; i++)
            run.Tick(default);
        Assert.True(run.Enemies[grass].IsAlive);
        Assert.DoesNotContain(run.MoveSystem.Projectiles.Items.ToArray(), p => p.Active);

        // A Rattata behind the grass: the shot flies through it.
        run.Enemies.Spawn(TestContent.Species(19), new Vector2(120, 0), hpMultiplier: 50f);
        for (var i = 0; i < 120; i++)
            run.Tick(default);
        Assert.False(run.Enemies[grass].IsAlive);
    }

    [Fact]
    public void A_broken_prop_may_leave_an_item_that_the_player_picks_up()
    {
        var run = NewRun(Field(0f, 0f, dropChance: 1f));
        run.Player.Hp = 10f;
        var rock = run.Enemies.SpawnProp(BreakableKind.Rock, new Vector2(4, 0), 1f, 80f, PokeRogue.Core.Data.PokemonType.Rock, 7);

        run.DamageEnemy(rock, 999f);
        Assert.Single(run.Loot.Items);
        run.Tick(default);

        Assert.Empty(run.Loot.Items);
        Assert.Contains(run.Events, e => e.Type == RunEventType.LootCollected && (int)e.Value == (int)LootKind.Potion);
        Assert.Equal(10f + run.Player.MaxHp * 0.3f, run.Player.Hp, 1);
    }

    [Fact]
    public void Each_item_does_its_job()
    {
        var run = NewRun();
        var file = run.LootRules!.File;

        run.Loot.Drop(Vector2.Zero, LootKind.Nugget);
        run.Tick(default);
        Assert.Equal(file.NuggetValue, run.Player.Coins);

        var level = run.Player.Level;
        run.Loot.Drop(Vector2.Zero, LootKind.RareCandy);
        run.Tick(default);
        Assert.Equal(level + 1, run.Player.Level);
        while (run.IsPaused)
            run.ChooseLevelUp(0);

        run.Pickups.Gems.Spawn(new Vector2(900, 0), 5);
        run.Loot.Drop(Vector2.Zero, LootKind.ExpShare);
        run.Tick(default);
        Assert.True(run.Pickups.Gems.Items[0].Magnetized);

        var near = run.Enemies.Spawn(TestContent.Species(19), new Vector2(150, 0), hpMultiplier: 100f);
        var far = run.Enemies.Spawn(TestContent.Species(19), new Vector2(900, 0), hpMultiplier: 100f);
        run.Loot.Drop(Vector2.Zero, LootKind.Repel);
        run.Tick(default);
        Assert.False(run.Enemies[near].IsAlive);
        Assert.True(run.Enemies[far].IsAlive);
    }

    [Fact]
    public void Coins_fly_to_the_player_from_the_pickup_radius()
    {
        var run = NewRun();
        run.Loot.Drop(new Vector2(run.Player.PickupRadius - 5f, 0), LootKind.Coin);

        for (var i = 0; i < 30 && run.Loot.Items.Count > 0; i++)
            run.Tick(default);

        Assert.Empty(run.Loot.Items);
        Assert.Equal(run.LootRules!.File.CoinValue, run.Player.Coins);
    }

    [Fact]
    public void A_shiny_is_much_sturdier_and_leaves_a_big_nugget_and_a_held_item()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            LootRules = TestContent.Loot.Value,
            ItemPool = TestContent.Items.Value.Enabled,
        };
        run.Spawner.Enabled = false;
        var plain = run.Enemies.Spawn(TestContent.Species(19), new Vector2(300, 0));
        var shiny = run.Enemies.Spawn(TestContent.Species(19), new Vector2(-300, 0));
        Spawner.MakeShiny(run, shiny);

        Assert.Equal(run.Enemies[plain].MaxHp * 10f, run.Enemies[shiny].MaxHp, 2);
        Assert.Equal(run.Enemies[plain].ContactDamage * 1.5f, run.Enemies[shiny].ContactDamage, 2);
        Assert.Equal(run.Enemies[plain].XpValue * 5, run.Enemies[shiny].XpValue);

        run.DamageEnemy(shiny, float.MaxValue);
        Assert.Contains(run.Loot.Items, l => l.Kind == LootKind.BigNugget);
        Assert.Contains(run.Loot.Items, l => l.Kind == LootKind.HeldItem && l.ItemId is not null);
    }

    [Fact]
    public void Defeated_pokemon_now_and_then_leave_a_berry_that_heals()
    {
        var run = NewRun(Field(0f, 0f) with { EnemyFoodChance = 1f, FoodHeal = 30f });
        run.Player.Hp = 20f;
        var rattata = run.Enemies.Spawn(TestContent.Species(19), new Vector2(4, 0));

        run.DamageEnemy(rattata, float.MaxValue);
        Assert.Contains(run.Loot.Items, l => l.Kind == LootKind.OranBerry);
        run.Tick(default);

        Assert.Equal(50f, run.Player.Hp, 1);
    }

    [Fact]
    public void A_held_item_on_the_ground_is_gained_then_levelled_up()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            LootRules = LootRules.From(Field(0f, 0f)),
            ItemPool = TestContent.Items.Value.Enabled,
        };
        run.Spawner.Enabled = false;

        run.Loot.Drop(Vector2.Zero, LootKind.HeldItem, "magnet");
        run.Tick(default);
        Assert.Equal(1, Assert.Single(run.Player.Items, i => i.Definition.Id == "magnet").Level);

        run.Loot.Drop(Vector2.Zero, LootKind.HeldItem, "magnet");
        run.Tick(default);
        Assert.Equal(2, Assert.Single(run.Player.Items, i => i.Definition.Id == "magnet").Level);
        Assert.True(run.Player.PickupRadius > PickupSystem.BasePickupRadius);
    }

    [Fact]
    public void The_level_lays_out_its_own_items_at_the_start()
    {
        var waves = Core.Run.Waves.WaveTimeline.Load(TestContent.Root, "bosco-smeraldo", TestContent.Pokedex.Value.Species, TestContent.Moves.Value);
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25, waves, new Vector2(640, 360))
        {
            LootRules = TestContent.Loot.Value,
        };
        run.Spawner.Enabled = false;

        run.Tick(default);

        Assert.Equal(waves.MapItems.Count, run.Loot.Items.Count);
        Assert.Contains(run.Loot.Items, l => l.Kind == LootKind.HeldItem && l.ItemId == "magnet");
        Assert.All(run.Loot.Items, l => Assert.True(l.Position.Length() > 300f, "map items lie away from the start"));
    }

    [Fact]
    public void Bad_loot_rules_are_refused()
    {
        Assert.Throws<InvalidDataException>(() => LootRules.From(Field(0.8f, 0.5f)));
        Assert.Throws<InvalidDataException>(() => LootRules.From(Field(0.2f, 0.1f) with { Drops = [] }));
        Assert.Throws<InvalidDataException>(() => LootRules.From(Field(0.2f, 0.1f) with { CellSize = 10 }));
    }
}
