using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.Core.Tests.Run;

public class TrainerTests
{
    internal static readonly Lazy<TrainerLibrary> Trainers = new(() => TrainerLibrary.Load(TestContent.Root, TestContent.Pokedex.Value.Species, TestContent.Moves.Value));

    /// <summary>Pikachu with <paramref name="trainerId"/>, no enemies, and no moves firing in the way of what is measured.</summary>
    private static RunState Play(string? trainerId)
    {
        var character = TestContent.Characters.Value["pikachu"];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            StatusRules = TestContent.Statuses.Value,
        };
        run.Spawner.Enabled = false;
        run.UseCharacter(character);
        run.Player.Moves.Clear();
        if (trainerId is not null)
            run.UseTrainer(Trainers.Value[trainerId]);
        return run;
    }

    private static MoveInstance Move(string id) => new(TestContent.Move(id));

    private static int Key(string key) => TestContent.Pokedex.Value.Species.Single(s => s.Key == key).Id;

    private static void Press(RunState run) => run.Tick(new PlayerInput(Vector2.Zero, UseTrainer: true));

    private static void Wait(RunState run, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            run.Tick(default);
    }

    [Fact]
    public void The_roster_has_ash_and_every_gym_leader_elite_four_and_champion_of_gen_1_to_4()
    {
        var all = Trainers.Value.All;

        Assert.Equal(53, all.Count);
        Assert.Equal(UnlockKind.Start, Trainers.Value[TrainerLibrary.DefaultId].Unlock.Kind);
        Assert.All(all.Where(t => t.Id != TrainerLibrary.DefaultId), t => Assert.Equal(UnlockKind.Defeat, t.Unlock.Kind));
        Assert.Equal(33, all.Count(t => t.Role == TrainerRole.GymLeader));
        Assert.All(Enum.GetValues<TrainerActiveKind>(), kind => Assert.Contains(all, t => t.Active.Kind == kind));
    }

    [Fact]
    public void Every_trainer_has_a_portrait()
    {
        Assert.All(Trainers.Value.All, t => Assert.True(File.Exists(Path.Combine(TestContent.Root, t.SpritePath)), t.SpritePath));
    }

    [Fact]
    public void A_bad_trainer_file_is_refused()
    {
        var ash = Trainers.Value[TrainerLibrary.DefaultId];
        var spec = new TrainerSpec
        {
            Id = "ash", NameIt = "Ash", Region = TrainerRegion.Kanto, Role = TrainerRole.Base,
            Passive = ash.Passive, Unlock = ash.Unlock,
            Active = new TrainerActiveSpec { Kind = TrainerActiveKind.Shield, NameIt = "Scudo", Cooldown = 30, Duration = 10 },
        };

        Assert.Throws<InvalidDataException>(() => TrainerLibrary.From(new TrainerFile(1, [spec])));
        Assert.Throws<InvalidDataException>(() => TrainerLibrary.From(new TrainerFile(1, [spec with { Active = spec.Active with { Charges = 3, Duration = 40 } }])));
        Assert.Throws<InvalidDataException>(() => TrainerLibrary.From(new TrainerFile(1, [spec with { Id = "brock", Active = spec.Active with { Charges = 3 } }])));
        Assert.NotNull(TrainerLibrary.From(new TrainerFile(1, [spec with { Active = spec.Active with { Charges = 3 } }])));
    }

    [Fact]
    public void The_passive_adds_its_bonuses_and_boosts_its_type()
    {
        Assert.Equal(Play(null).Player.Growth + 0.1f, Play("ash").Player.Growth, 3);

        var surge = Play("ltsurge");
        Assert.Equal(1.15f, TrainerSystem.DamageMultiplier(surge, Move("thundershock")));
        Assert.Equal(1f, TrainerSystem.DamageMultiplier(surge, Move("ember")));
    }

    [Fact]
    public void The_key_starts_the_cooldown_and_works_again_when_it_ends()
    {
        var run = Play("ash");
        Assert.True(TrainerSystem.CanUse(run));

        Press(run);
        Assert.False(TrainerSystem.CanUse(run));
        Assert.Contains(run.Events, e => e.Type == RunEventType.TrainerAbility && (TrainerEffect)(int)e.Value == TrainerEffect.Used);

        Wait(run, RunClock.Seconds(40f));
        Assert.True(TrainerSystem.CanUse(run));
        Assert.Equal(1f, TrainerSystem.Gauge(run));
    }

    [Fact]
    public void Frenzy_shortens_move_cooldowns_while_it_lasts()
    {
        var run = Play("ash");
        Press(run);
        Assert.Equal(0.65f, run.Player.TrainerCooldownMultiplier, 3);

        Wait(run, RunClock.Seconds(8f));
        Assert.Equal(1f, run.Player.TrainerCooldownMultiplier);
    }

    [Fact]
    public void A_shield_blocks_three_hits_then_breaks()
    {
        var run = Play("brock");
        Press(run);
        var hit = new Hurt(HurtKind.Contact, 20f, false, Key("rattata"), PokemonType.Normal);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(0f, run.HurtPlayer(hit));
            Wait(run, Player.HitInvulnerabilityTicks);
        }

        Assert.True(run.HurtPlayer(hit) > 0f);
    }

    [Fact]
    public void Intangible_stops_every_hit_for_a_while()
    {
        var run = Play("janine");
        Press(run);
        var hit = new Hurt(HurtKind.Shot, 20f, true, Key("beedrill"), PokemonType.Poison);

        Assert.Equal(0f, run.HurtPlayer(hit));
        Wait(run, RunClock.Seconds(4f));
        Assert.True(run.HurtPlayer(hit) > 0f);
    }

    [Fact]
    public void Heal_restores_a_share_of_the_max_hp()
    {
        var run = Play("whitney");
        run.Player.Hp = 10f;

        Press(run);

        Assert.Equal(10f + run.Player.MaxHp * 0.35f, run.Player.Hp, 1);
    }

    [Fact]
    public void A_wave_hurts_and_pushes_the_enemies_around()
    {
        var run = Play("misty");
        var near = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(60, 0), hpMultiplier: 10f);
        var far = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(300, 0), hpMultiplier: 10f);
        var full = run.Enemies[near].MaxHp;

        Press(run);

        Assert.Equal(full - MathF.Round(full * 0.5f), run.Enemies[near].Hp);
        Assert.True(run.Enemies[near].Knockback.X > 0f);
        Assert.Equal(run.Enemies[far].MaxHp, run.Enemies[far].Hp);
    }

    [Fact]
    public void Slow_time_slows_the_enemies()
    {
        var run = Play("sabrina");
        var index = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(200, 0));
        Press(run);
        var start = run.Enemies[index].Position.X;
        Wait(run, 30);
        var slowed = start - run.Enemies[index].Position.X;

        var normal = Play("ash");
        var other = normal.SpawnEnemy(Key("rattata"), normal.Player.Position + new Vector2(200, 0));
        Wait(normal, 31);
        var walked = 200f - (normal.Enemies[other].Position.X - normal.Player.Position.X);

        Assert.InRange(slowed, walked * 0.25f, walked * 0.45f);
    }

    [Fact]
    public void A_status_storm_reaches_the_whole_screen()
    {
        var run = Play("koga");
        var near = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(80, 40));
        var edge = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(-300, 150));
        var outside = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(600, 0));

        Press(run);

        Assert.Equal(StatusKind.Poison, run.Enemies[near].Status);
        Assert.Equal(StatusKind.Poison, run.Enemies[edge].Status);
        Assert.Equal(StatusKind.None, run.Enemies[outside].Status);
    }

    [Fact]
    public void Type_boost_strengthens_its_type_most()
    {
        var run = Play("volkner");
        Press(run);

        Assert.Equal(1.15f * 1.5f, TrainerSystem.DamageMultiplier(run, Move("thundershock")), 3);
        Assert.Equal(1f + 0.5f / 3f, TrainerSystem.DamageMultiplier(run, Move("ember")), 3);
    }

    [Fact]
    public void Fortune_doubles_experience_and_pulls_every_gem()
    {
        var run = Play("roxanne");
        run.Pickups.Gems.Spawn(run.Player.Position + new Vector2(500, 0), 3);
        Press(run);

        Assert.All(run.Pickups.Gems.Items.ToArray().Where(g => g.Active), g => Assert.True(g.Magnetized));
        Assert.Equal(2f, TrainerSystem.GainMultiplier(run));
    }

    [Fact]
    public void Haste_makes_the_player_run_faster()
    {
        var run = Play("blue");
        var speed = run.Player.MoveSpeed;
        Press(run);
        run.Tick(new PlayerInput(Vector2.UnitX));

        Assert.Equal(speed * 1.5f, run.Player.Velocity.Length(), 1);
    }
}
