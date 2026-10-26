using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Run;

public class AbilityTests
{
    private static RunState Play(string characterId)
    {
        var character = TestContent.Characters.Value[characterId];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            StatusRules = TestContent.Statuses.Value,
        };
        run.Spawner.Enabled = false;
        run.UseCharacter(character);
        return run;
    }

    private static int Key(string key) => TestContent.Pokedex.Value.Species.Single(s => s.Key == key).Id;

    [Fact]
    public void Every_ability_of_the_roster_works()
    {
        var abilities = TestContent.Characters.Value.All.SelectMany(c => c.Stages).Select(s => s.Ability.Id).Distinct();

        Assert.All(abilities, id => Assert.Contains(id, AbilitySystem.Known));
    }

    [Fact]
    public void Blaze_strengthens_fire_moves_at_low_hp()
    {
        var run = Play("charmander");
        var ember = run.Player.Moves[0];
        Assert.Equal(1f, AbilitySystem.DamageMultiplier(run, ember));

        run.Player.Hp = run.Player.MaxHp * 0.2f;

        Assert.Equal(1.5f, AbilitySystem.DamageMultiplier(run, ember));
        Assert.Equal(1f, AbilitySystem.DamageMultiplier(run, new MoveInstance(TestContent.Move("aerial-ace"))));
    }

    [Fact]
    public void Adaptability_and_mold_breaker_change_the_damage_formula()
    {
        Assert.Equal(2f, AbilitySystem.Attacker(Play("eevee"), Play("eevee").Player.Attacker).Stab);

        var cranidos = Play("cranidos");
        var attacker = AbilitySystem.Attacker(cranidos, cranidos.Player.Attacker);
        var steel = new DefenderProfile(PokemonType.Steel, null, 50, 50);
        var hit = DamageCalculator.Compute(10f, MoveCategory.Physical, PokemonType.Rock, attacker, steel, TestContent.TypeChart.Value, new Random(1));

        Assert.Equal(1f, hit.Effectiveness);
    }

    [Fact]
    public void Static_paralyses_who_touches_the_player()
    {
        var run = Play("pikachu");
        var index = run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(30, 0));

        for (var i = 0; i < 40 && run.Enemies[index].Status == StatusKind.None; i++)
            AbilitySystem.OnHurt(run, new Hurt(HurtKind.Contact, 5f, false, Key("rattata"), PokemonType.Normal, index), 5f);

        Assert.Equal(StatusKind.Paralysis, run.Enemies[index].Status);
    }

    [Fact]
    public void Levitate_ignores_falling_rocks()
    {
        var run = Play("gastly");
        var rock = new Hurt(HurtKind.Hazard, 30f, false, Key("pinsir"), PokemonType.Rock);

        Assert.Equal(0f, run.HurtPlayer(rock));
        Assert.Equal(run.Player.MaxHp, run.Player.Hp);
        Assert.True(run.HurtPlayer(rock with { Kind = HurtKind.Shot, Type = PokemonType.Bug }) > 0f);
    }

    [Fact]
    public void Shed_skin_blocks_one_hit_every_fifteen_seconds()
    {
        var run = Play("dratini");
        var bite = new Hurt(HurtKind.Contact, 10f, false, Key("rattata"), PokemonType.Normal);
        for (var i = 0; i < RunClock.Seconds(AbilitySystem.ShedSkinSeconds); i++)
            run.Tick(default);

        Assert.True(run.Ability.ShieldReady);
        Assert.Equal(0f, run.HurtPlayer(bite));
        Assert.False(run.Ability.ShieldReady);
    }

    [Fact]
    public void Sturdy_survives_one_knock_out()
    {
        var run = Play("shieldon");
        var crush = new Hurt(HurtKind.Contact, 9999f, false, Key("rattata"), PokemonType.Normal);

        run.HurtPlayer(crush);
        Assert.Equal(1f, run.Player.Hp);

        for (var i = 0; i < Player.HitInvulnerabilityTicks; i++)
            run.Tick(default);
        run.HurtPlayer(crush);
        Assert.True(run.Player.IsDown);
    }

    [Fact]
    public void Teleport_jumps_ahead_then_recharges()
    {
        var run = Play("abra");
        var start = run.Player.Position;

        run.Tick(new PlayerInput(Vector2.Zero, UseAbility: true));
        var after = run.Player.Position;
        run.Tick(new PlayerInput(Vector2.Zero, UseAbility: true));

        Assert.Equal(AbilitySystem.TeleportDistance, Vector2.Distance(start, after), 2);
        Assert.Equal(after, run.Player.Position);
        Assert.True(run.Ability.CooldownTicks > 0);
        Assert.True(run.Ability.CooldownTicks < RunClock.Seconds(AbilitySystem.TeleportCooldownSeconds));
    }

    [Fact]
    public void Transform_copies_the_strongest_pokemon_around_for_a_while()
    {
        var run = Play("ditto");
        run.SpawnEnemy(Key("rattata"), run.Player.Position + new Vector2(100, 0));
        run.SpawnEnemy(Key("snorlax"), run.Player.Position + new Vector2(-150, 0));
        var hp = run.Player.MaxHp;

        run.Tick(new PlayerInput(Vector2.Zero, UseAbility: true));

        Assert.Equal("snorlax", run.Player.Form.Key);
        Assert.Equal(110f, run.Player.Attacker.Attack);
        Assert.Equal(hp, run.Player.MaxHp);

        for (var i = 0; i < RunClock.Seconds(AbilitySystem.TransformSeconds); i++)
            run.Tick(default);
        Assert.Equal("ditto", run.Player.Form.Key);
    }

    [Fact]
    public void Natural_cure_heals_on_level_up()
    {
        var run = Play("swablu");
        run.Player.Hp = 10f;

        run.GrantLevel();

        Assert.Equal(10f + run.Player.MaxHp * AbilitySystem.NaturalCureShare, run.Player.Hp, 2);
    }

    [Fact]
    public void Speed_boost_grows_every_thirty_seconds()
    {
        var run = Play("torchic");
        var speed = run.Player.MoveSpeed;

        for (var i = 0; i < RunClock.Seconds(61f); i++)
            run.Tick(new PlayerInput(new Vector2(1, 0)));

        Assert.Equal(2, run.Ability.SpeedStacks);
        Assert.Equal(1f + 2 * AbilitySystem.SpeedBoostStep, run.Player.AbilitySpeedMultiplier, 3);
        Assert.Equal(speed * (1f + 2 * AbilitySystem.SpeedBoostStep), run.Player.Velocity.Length(), 2);
    }

    [Fact]
    public void Synchronize_spreads_a_status_to_two_neighbours()
    {
        var run = Play("ralts");
        var first = run.SpawnEnemy(Key("rattata"), new Vector2(200, 0));
        var near = run.SpawnEnemy(Key("rattata"), new Vector2(220, 0));
        var nearer = run.SpawnEnemy(Key("rattata"), new Vector2(210, 0));
        var far = run.SpawnEnemy(Key("rattata"), new Vector2(400, 0));

        Assert.True(Core.Run.Statuses.StatusSystem.Apply(run, first, StatusKind.Confusion));
        AbilitySystem.OnStatusInflicted(run, first, StatusKind.Confusion, 0f, -1);

        Assert.Equal(StatusKind.Confusion, run.Enemies[near].Status);
        Assert.Equal(StatusKind.Confusion, run.Enemies[nearer].Status);
        Assert.Equal(StatusKind.None, run.Enemies[far].Status);
    }

    [Fact]
    public void Gyarados_intimidates_but_magikarp_does_not()
    {
        var run = Play("magikarp");
        var bite = new Hurt(HurtKind.Contact, 10f, false, Key("rattata"), PokemonType.Normal);
        Assert.Equal(1f, AbilitySystem.HurtMultiplier(run, bite));

        while (run.Player.Level < 18)
            run.Player.AddXp(run.Player.XpToNext - run.Player.Xp);
        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);

        Assert.Equal(0.85f, AbilitySystem.HurtMultiplier(run, bite));
    }

    [Fact]
    public void Mew_learns_new_moves_at_level_two()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, Key("mew"), [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            MovePool = TestContent.Moves.Value.Enabled,
        };
        run.UseCharacter(TestContent.Characters.Value["mew"]);

        LevelUp.Apply(run, new LevelUpOption(LevelUpKind.NewMove, "ember", "Braciere", 0, 1, "", "ember"));

        Assert.Equal(2, run.Player.Moves.Single(m => m.Definition.Id == "ember").Level);
    }

    [Fact]
    public void Download_boosts_the_weaker_defence()
    {
        var run = Play("porygon");
        run.SpawnEnemy(Key("geodude"), new Vector2(200, 0));

        run.Tick(default);

        Assert.Equal(MoveCategory.Special, run.Ability.Downloaded);
    }
}
