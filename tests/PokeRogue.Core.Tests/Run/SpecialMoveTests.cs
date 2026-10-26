using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Specials;

namespace PokeRogue.Core.Tests.Run;

public class SpecialMoveTests
{
    private static readonly Lazy<SpecialMoves> Specials = new(() => SpecialMoves.Load(TestContent.Root));

    private static RunState Play(string characterId)
    {
        var character = TestContent.Characters.Value[characterId];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360))
        {
            SpecialRules = Specials.Value,
            StatusRules = TestContent.Statuses.Value,
        };
        run.Spawner.Enabled = false;
        run.UseCharacter(character);
        return run;
    }

    private static int Key(string key) => TestContent.Pokedex.Value.Species.Single(s => s.Key == key).Id;

    [Fact]
    public void Every_type_has_a_special_move()
    {
        Assert.Equal(17, Specials.Value.All.Count);
        Assert.Equal("Idropompa", Specials.Value[PokemonType.Water].NameIt);
        Assert.All(TestContent.Characters.Value.All, c => Assert.NotNull(Specials.Value[c.Special]));
    }

    [Fact]
    public void Damage_dealt_fills_the_gauge_in_enemies_worth_of_hp()
    {
        var run = Play("charmander");
        var index = run.SpawnEnemy(Key("rattata"), new Vector2(100, 0), hpMultiplier: 1000f);
        var maxHp = run.Enemies[index].MaxHp;

        SpecialMoves.Charge(run, maxHp * 3f, maxHp);

        Assert.Equal(3f / Specials.Value.File.ChargeEnemies, run.Special.Charge, 4);
    }

    [Fact]
    public void The_key_waits_for_a_full_gauge_then_hits_everything_on_screen()
    {
        var run = Play("squirtle");
        var near = run.SpawnEnemy(Key("charmander"), new Vector2(100, 0));
        var resisted = run.SpawnEnemy(Key("bulbasaur"), new Vector2(-100, 50));
        var offScreen = run.SpawnEnemy(Key("rattata"), new Vector2(600, 0));

        run.Tick(new PlayerInput(Vector2.Zero, UseSpecial: true));
        Assert.False(run.SpecialPending);

        run.Special.Charge = 1f;
        run.Tick(new PlayerInput(Vector2.Zero, UseSpecial: true));
        Assert.True(run.SpecialPending);
        Assert.True(run.IsPaused);
        Assert.Contains(run.Events, e => e.Type == RunEventType.SpecialStarted);

        var resistedHp = run.Enemies[resisted].MaxHp;
        run.UnleashSpecial();

        Assert.False(run.Enemies[near].IsAlive);
        Assert.Equal(resistedHp * (1f - Specials.Value.File.RegularDamage * 0.5f), run.Enemies[resisted].Hp, 0);
        Assert.Equal(run.Enemies[offScreen].MaxHp, run.Enemies[offScreen].Hp);
        Assert.Equal(0f, run.Special.Charge);
        Assert.True(run.Special.LockTicks > 0);
    }

    [Fact]
    public void The_gauge_does_not_fill_while_locked()
    {
        var run = Play("pikachu");
        run.Special.LockTicks = 10;

        SpecialMoves.Charge(run, 100f, 10f);

        Assert.Equal(0f, run.Special.Charge);
    }

    [Fact]
    public void Dominanti_lose_only_a_small_share()
    {
        var run = Play("pikachu");
        var index = run.SpawnEnemy(Key("pidgeotto"), new Vector2(80, 0), hpMultiplier: 20f);
        Spawner.MakeDominant(run, index, ChestTier.Poke);
        var maxHp = run.Enemies[index].MaxHp;
        run.Special.Charge = 1f;
        run.Tick(new PlayerInput(Vector2.Zero, UseSpecial: true));

        run.UnleashSpecial();

        var effectiveness = DamageCalculator.Effectiveness(TestContent.TypeChart.Value, PokemonType.Electric, PokemonType.Normal, PokemonType.Flying);
        Assert.Equal(maxHp - MathF.Round(maxHp * Specials.Value.File.EliteDamage * effectiveness), run.Enemies[index].Hp, 0);
    }
}
