using System.Numerics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Waves;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Block 7: worlds, levels, maps, weather, night, difficulties, legendaries and finds.</summary>
public class WorldTests
{
    private static WorldLibrary Worlds => TestContent.Worlds.Value;

    private static IReadOnlyList<SpeciesData> Pokedex => TestContent.Pokedex.Value.Species;

    private static CharacterLibrary Characters => TestContent.Characters.Value;

    private static readonly Lazy<MissionLibrary> Missions = new(() =>
        MissionLibrary.Load(TestContent.Root, Characters, TrainerTests.Trainers.Value, Worlds));

    private static MetaProgress Meta(SaveData? data = null) =>
        new(data ?? new SaveData(), ShopRules.Load(TestContent.Root), Characters, TrainerTests.Trainers.Value, Missions.Value, Pokedex, worlds: Worlds);

    private static RunState Run(WaveTimeline level, int species = 25) =>
        new(Pokedex, TestContent.TypeChart.Value, species, level, new Vector2(640, 360))
        {
            TimeLimitTicks = level.DurationTicks,
            MovePool = TestContent.Moves.Value.Enabled,
            StatusRules = TestContent.Statuses.Value,
        };

    [Fact]
    public void Four_worlds_hold_twenty_five_levels_in_unlock_order()
    {
        Assert.Equal(["kanto", "johto", "hoenn", "sinnoh"], Worlds.Worlds.Select(w => w.Id));
        Assert.Equal(25, Worlds.Levels.Count);
        Assert.Equal("bosco-smeraldo", Worlds.First.Id);
        Assert.Equal(Enumerable.Range(0, 25), Worlds.Levels.Select(l => l.Order));
        Assert.All(Worlds.Levels, l => Assert.False(string.IsNullOrWhiteSpace(l.DescriptionIt)));
    }

    [Fact]
    public void Every_level_builds_at_every_difficulty_with_a_boss_and_fifteen_minutes_of_waves()
    {
        foreach (var level in Worlds.Levels)
        {
            foreach (var difficulty in Enum.GetValues<Difficulty>())
            {
                var timeline = Worlds.Timeline(level.Id, Pokedex, TestContent.Moves.Value, difficulty);
                Assert.NotNull(timeline.Boss);
                Assert.Equal(15, timeline.Waves.Count);
                Assert.Equal(RunClock.ClockSeconds(15 * 60f), timeline.Waves[^1].ToTick);
                Assert.Equal(5 * (1 + Worlds.Difficulty(difficulty).ExtraDominants), timeline.Events.Count(e => e.Kind == WaveEventKind.Dominant));
                Assert.All(timeline.MapItems, item => Assert.True(timeline.Map.Contains(item.Position, 30f), $"{level.Id}: an item lies outside the walls."));
            }
        }
    }

    [Fact]
    public void A_harder_difficulty_brings_more_enemies_stronger_ones_and_more_dominanti()
    {
        var normal = Worlds.Timeline("monte-luna", Pokedex, TestContent.Moves.Value);
        var ultra = Worlds.Timeline("monte-luna", Pokedex, TestContent.Moves.Value, Difficulty.Ultra);

        Assert.True(ultra.Waves[10].MinAlive > normal.Waves[10].MinAlive);
        Assert.True(ultra.HpMultiplier(0) > normal.HpMultiplier(0));
        Assert.True(ultra.DamageMultiplier(0) > normal.DamageMultiplier(0));
        Assert.True(ultra.Boss!.Spec.Hp > normal.Boss!.Spec.Hp);
        var extra = ultra.Events.Where(e => e.Kind == WaveEventKind.Dominant && e.AtTick == normal.Events.First(n => n.Kind == WaveEventKind.Dominant).AtTick + WaveTimeline.ExtraDominantGapTicks);
        Assert.All(extra, e => Assert.Equal(ChestTier.Poke, e.Chest));
    }

    [Fact]
    public void Every_trainer_with_a_team_is_challenged_on_exactly_one_level()
    {
        var trainers = TrainerTests.Trainers.Value;
        foreach (var trainer in trainers.All.Where(t => t.Team.Count > 0))
            Assert.Single(Worlds.Levels, l => l.Trainers.Contains(trainer.Id));
        Assert.All(Worlds.Levels.SelectMany(l => l.Trainers), id => Assert.NotNull(trainers.Find(id)));

        // Each region's trainers are challenged in that region.
        foreach (var level in Worlds.Levels)
            Assert.All(level.Trainers, id => Assert.True(trainers[id].Region == level.World.Region || id == "red", $"{id} on {level.Id}"));
    }

    [Fact]
    public void Every_legendary_to_beat_waits_on_a_level_at_ultra()
    {
        var legendaries = Worlds.Levels.SelectMany(l => l.File.Legendaries.Select(x => x.Pokemon)).ToList();
        Assert.Equal(legendaries.Count, legendaries.Distinct().Count());

        // Darkrai is the reaper of the endless mode, still to come.
        foreach (var character in Characters.All.Where(c => c.Unlock.Kind == UnlockKind.Defeat && c.Id != "darkrai"))
            Assert.Contains(character.First.Key, legendaries);
        Assert.All(legendaries, key => Assert.Equal(UnlockKind.Defeat, Characters.All.Single(c => c.First.Key == key).Unlock.Kind));
    }

    [Fact]
    public void Every_fossil_and_egg_lies_on_one_level()
    {
        var finds = Worlds.Levels.SelectMany(l => l.File.Finds.Select(f => f.Find!)).ToList();
        foreach (var character in Characters.All.Where(c => c.Unlock.Kind is UnlockKind.Fossil or UnlockKind.Egg))
            Assert.Single(finds, f => f == character.Id);
        Assert.All(finds, id => Assert.Contains(Characters[id].Unlock.Kind, new[] { UnlockKind.Fossil, UnlockKind.Egg }));
    }

    [Fact]
    public void A_corridor_keeps_bodies_between_its_walls_and_obstacles_push_out()
    {
        var map = new LevelMap(new MapSpec { Shape = MapShape.Corridor, Axis = CorridorAxis.Horizontal, Width = 400, Obstacles = 0.5f, Seed = 3 });
        var outside = new Vector2(5000, 900);
        map.Resolve(ref outside, 10f);
        Assert.Equal(190f, outside.Y);

        var obstacle = map.ObstaclesIn(-3000, -200, 3000, 200).First();
        var inside = obstacle.Position + new Vector2(1, 0);
        var push = map.Resolve(ref inside, 6f);
        Assert.NotEqual(Vector2.Zero, push);
        Assert.False(map.Blocked(inside, 5.9f));

        // The same obstacles every time, none near the start.
        Assert.Equal(map.ObstaclesIn(-3000, -200, 3000, 200), new LevelMap(map.Spec).ObstaclesIn(-3000, -200, 3000, 200));
        Assert.All(map.ObstaclesIn(-500, -200, 500, 200), o => Assert.True(o.Position.Length() >= LevelMap.StartClearance));
    }

    [Fact]
    public void In_a_corridor_enemies_spawn_inside_it_and_the_player_cannot_leave_it()
    {
        var level = Worlds.Timeline("isole-spumarine", Pokedex, TestContent.Moves.Value);
        var run = Run(level);
        for (var i = 0; i < 600; i++)
            run.Tick(new PlayerInput(new Vector2(0.3f, -1f)));

        Assert.True(run.Map.Contains(run.Player.Position, 0f));
        Assert.True(run.Enemies.AliveCount > 0);
        for (var i = 0; i < run.Enemies.HighWater; i++)
        {
            if (run.Enemies[i].IsAlive && run.Enemies[i].Behavior != EnemyBehavior.Straight)
                Assert.True(run.Map.Contains(run.Enemies[i].Position, 0f), $"enemy at {run.Enemies[i].Position}");
        }
    }

    [Fact]
    public void Rain_makes_water_moves_stronger_and_fire_ones_weaker()
    {
        var level = Worlds.Timeline("isole-vorticose", Pokedex, TestContent.Moves.Value);
        var run = Run(level);
        run.Spawner.Enabled = false;
        run.Tick(default);

        Assert.Equal(WeatherKind.Rain, run.Weather);
        Assert.Contains(run.Events, e => e.Type == RunEventType.WeatherChanged);
        Assert.Equal(1.3f, WeatherSystem.DamageMultiplier(run, PokemonType.Water), 3);
        Assert.Equal(0.7f, WeatherSystem.DamageMultiplier(run, PokemonType.Fire), 3);
    }

    [Fact]
    public void An_ability_brings_its_own_weather_for_the_whole_run()
    {
        var level = Worlds.Timeline("monte-camino", Pokedex, TestContent.Moves.Value);
        var kyogre = Characters["kyogre"];
        var run = new RunState(Pokedex, TestContent.TypeChart.Value, kyogre.First.Id, level, new Vector2(640, 360));
        run.UseCharacter(kyogre);
        run.Spawner.Enabled = false;
        run.Tick(default);

        Assert.Equal(WeatherKind.Sun, level.WeatherAt(run.Ticks));
        Assert.Equal(WeatherKind.Rain, run.Weather);
    }

    [Fact]
    public void A_sandstorm_wears_down_the_enemies_and_slows_a_player_that_does_not_stand_it()
    {
        var run = Run(Worlds.Timeline("deserto", Pokedex, TestContent.Moves.Value));
        run.Spawner.Enabled = false;
        var pidgey = run.SpawnEnemy(16, new Vector2(300, 0));
        var geodude = run.SpawnEnemy(74, new Vector2(-300, 0));
        var hp = run.Player.Hp;
        for (var i = 0; i < 120; i++)
            run.Tick(default);

        Assert.Equal(hp, run.Player.Hp);
        Assert.Equal(WeatherSystem.PlayerSlow, run.Player.WeatherSpeedMultiplier);
        Assert.True(run.Enemies[pidgey].Hp < run.Enemies[pidgey].MaxHp);
        Assert.Equal(run.Enemies[geodude].MaxHp, run.Enemies[geodude].Hp);
    }

    [Fact]
    public void At_night_the_waves_send_their_night_species()
    {
        var level = Worlds.Timeline("bosco-di-lecci", Pokedex, TestContent.Moves.Value);
        Assert.False(level.IsNight(RunClock.ClockSeconds(4 * 60f)));
        Assert.True(level.IsNight(RunClock.ClockSeconds(7 * 60f)));
        Assert.Equal(0f, level.NightAmount(RunClock.ClockSeconds(11 * 60f + 30f)));

        var run = Run(level);
        run.AdvanceTime(RunClock.ClockSeconds(7 * 60f));
        for (var i = 0; i < 60; i++)
            run.Tick(default);

        Assert.True(run.IsNight);
        var night = level.WaveAt(run.Ticks)!.NightSpecies!.Select(s => s.SpeciesId).ToHashSet();
        for (var i = 0; i < run.Enemies.HighWater; i++)
        {
            ref var e = ref run.Enemies[i];
            if (e.IsAlive && e.Rank == EnemyRank.Normal && e.Behavior == EnemyBehavior.Chase && e.ExpiresAtTick == 0)
                Assert.Contains(e.SpeciesId, night);
        }
    }

    [Fact]
    public void At_ultra_the_legendaries_come_one_after_another_and_unlock()
    {
        var level = Worlds.Timeline("torre-bruciata", Pokedex, TestContent.Moves.Value, Difficulty.Ultra);
        var run = Run(level);
        run.Spawner.Enabled = false;
        run.Player.Invincible = true;
        run.UseLegendaries(level.Legendaries.Select(l => l.Boss));
        run.AdvanceTime(level.DurationTicks);

        var beaten = new List<int>();
        for (var i = 0; i < 4000 && !run.IsOver; i++)
        {
            run.Tick(default);
            if (run.Boss is { DefeatedTick: null } fight && run.Enemies[fight.Index].IsAlive)
            {
                Assert.True(fight.IsLegendary);
                if (!beaten.Contains(fight.Definition.SpeciesId))
                    beaten.Add(fight.Definition.SpeciesId);
                run.DamageEnemy(fight.Index, float.MaxValue);
            }
        }

        Assert.Equal(RunOutcome.Victory, run.Outcome);
        Assert.Equal(["raikou", "entei", "suicune"], beaten.Select(id => Pokedex[id - 1].Key));
        Assert.Equal(beaten, run.Stats.LegendariesDefeated);

        var meta = Meta();
        var report = meta.RecordRun(RunRecord.From(run, "torre-bruciata", Difficulty.Ultra) with { CharacterId = "pikachu" });
        Assert.True(meta.IsUnlocked(Characters["raikou"]));
        Assert.Contains(report.NewCharacters, c => c.Id == "suicune");
    }

    [Fact]
    public void Regigigas_waits_for_the_three_regi()
    {
        var level = Worlds.Timeline("tempio-nevoso", Pokedex, TestContent.Moves.Value, Difficulty.Ultra);
        Assert.Empty(Meta().Legendaries(level));
        Assert.Single(Meta(new SaveData { Characters = ["regirock", "regice", "registeel"] }).Legendaries(level));
    }

    [Fact]
    public void A_fossil_waits_in_the_shop_and_an_egg_hatches_at_the_end_of_the_run()
    {
        var meta = Meta();
        var record = new RunRecord { LevelId = "monte-luna", CharacterId = "pikachu", Over = true, Finds = ["omanyte", "togepi"] };
        var report = meta.RecordRun(record);

        Assert.Contains("omanyte", meta.Data.Fossils);
        Assert.False(meta.IsUnlocked(Characters["omanyte"]));
        Assert.Equal(1200, meta.Price(Characters["omanyte"]));
        Assert.True(meta.IsUnlocked(Characters["togepi"]));
        Assert.Contains(report.Fossils, c => c.Id == "omanyte");
        Assert.Contains("omanyte", meta.FoundFinds());
    }

    [Fact]
    public void Picking_up_a_find_records_it_and_a_found_one_is_not_laid_again()
    {
        var level = Worlds.Timeline("monte-luna", Pokedex, TestContent.Moves.Value);
        var run = new RunState(Pokedex, TestContent.TypeChart.Value, 25, level, new Vector2(640, 360))
        {
            LootRules = TestContent.Loot.Value,
            SkipFinds = new HashSet<string> { "kabuto" },
        };
        run.Spawner.Enabled = false;
        run.Tick(default);

        Assert.Single(run.Loot.Items, l => l.Kind == LootKind.Find);
        var omanyte = run.Loot.Items.Single(l => l.Kind == LootKind.Find);
        Assert.Equal("omanyte", omanyte.ItemId);
        run.Player.Position = omanyte.Position;
        run.Tick(default);

        Assert.Equal(["omanyte"], run.Stats.Finds);
    }

    [Fact]
    public void Levels_open_one_after_another_and_difficulties_in_turn()
    {
        var data = new SaveData();
        var meta = Meta(data);
        var bosco = Worlds["bosco-smeraldo"];
        var luna = Worlds["monte-luna"];

        Assert.True(meta.IsUnlocked(bosco));
        Assert.False(meta.IsUnlocked(bosco, Difficulty.Hard));
        Assert.False(meta.IsUnlocked(luna));

        var report = meta.RecordRun(new RunRecord { LevelId = "bosco-smeraldo", CharacterId = "pikachu", Over = true, Won = true });
        Assert.True(meta.IsUnlocked(luna));
        Assert.True(meta.IsUnlocked(bosco, Difficulty.Hard));
        Assert.False(meta.IsUnlocked(bosco, Difficulty.Ultra));
        Assert.Contains("monte-luna/Normal", report.Opened);
        Assert.Contains("bosco-smeraldo/Hard", report.Opened);
        Assert.Equal("bosco-smeraldo", data.LastLevel);
    }

    [Fact]
    public void Every_level_has_a_mission_to_win_it_at_each_difficulty()
    {
        foreach (var level in Worlds.Levels)
        {
            foreach (var difficulty in Enum.GetValues<Difficulty>())
                Assert.Single(Missions.Value.All, m => m.Kind == MissionKind.WinLevel && m.Level == level.Id && m.Difficulty == difficulty);
        }

        Assert.NotNull(Missions.Value.Find("bosco-normale"));
        Assert.NotNull(Missions.Value.Find("monte-luna-ultra"));
    }

    [Fact]
    public void Completing_every_level_of_johto_unlocks_celebi()
    {
        var data = new SaveData();
        foreach (var level in Worlds.Worlds[1].Levels.SkipLast(1))
            data.LevelOf(level.Id, Difficulty.Normal).Wins = 1;
        var meta = Meta(data);
        var mission = Missions.Value.Find("johto")!;
        Assert.Equal((5, 6), meta.Progress(mission));

        meta.RecordRun(new RunRecord { LevelId = "monte-argento", CharacterId = "pikachu", Over = true, Won = true });
        Assert.True(meta.IsUnlocked(Characters["celebi"]));
    }
}
