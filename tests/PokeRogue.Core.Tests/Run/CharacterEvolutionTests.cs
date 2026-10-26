using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Run;

public class CharacterEvolutionTests
{
    private static RunState Play(string characterId, int level = 1)
    {
        var character = TestContent.Characters.Value[characterId];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, character.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360));
        run.Spawner.Enabled = false;
        run.UseCharacter(character);
        while (run.Player.Level < level)
            run.Player.AddXp(run.Player.XpToNext - run.Player.Xp);
        return run;
    }

    [Fact]
    public void A_stage_is_ready_at_its_level()
    {
        Assert.Empty(CharacterEvolution.Ready(Play("charmander", 13)));
        var run = Play("charmander", 14);

        var stage = Assert.Single(CharacterEvolution.Ready(run));
        Assert.Equal("charmeleon", stage.Species.Key);
    }

    [Fact]
    public void Evolving_changes_species_and_stats_and_heals_the_extra_hp()
    {
        var run = Play("charmander", 14);
        run.Player.Hp -= 10f;
        var hpBefore = run.Player.Hp;
        var maxBefore = run.Player.MaxHp;

        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);

        Assert.Equal("charmeleon", run.Player.Species.Key);
        Assert.Equal("charmeleon", run.Stage!.Species.Key);
        Assert.True(run.Player.MaxHp > maxBefore);
        Assert.Equal(hpBefore + run.Player.MaxHp - maxBefore, run.Player.Hp, 3);
        Assert.Empty(CharacterEvolution.Ready(run));
        Assert.Equal("ember", Assert.Single(run.Player.Moves).Definition.Id);

        run.Tick(default);
        var evolved = Assert.Single(run.Events, e => e.Type == RunEventType.PlayerEvolved);
        Assert.Equal(4, (int)evolved.Value);
        Assert.Equal(5, evolved.SpeciesId);
    }

    [Fact]
    public void Types_follow_the_new_species()
    {
        var run = Play("charmander", 32);
        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);
        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);

        Assert.Equal("charizard", run.Player.Species.Key);
        Assert.Equal(Core.Data.PokemonType.Flying, run.Player.Attacker.Type2);
    }

    [Fact]
    public void A_stopped_evolution_comes_back_at_the_next_level()
    {
        var run = Play("charmander", 14);

        CharacterEvolution.Decline(run);
        Assert.Empty(CharacterEvolution.Ready(run));

        run.Player.AddXp(run.Player.XpToNext);
        Assert.Single(CharacterEvolution.Ready(run));
    }

    [Fact]
    public void Eevee_chooses_among_seven()
    {
        Assert.Equal(7, CharacterEvolution.Ready(Play("eevee", 20)).Count);
    }

    [Fact]
    public void Stone_evolutions_need_the_item_from_a_chest()
    {
        var run = Play("pikachu", 30);
        Assert.Empty(CharacterEvolution.Ready(run));
        Assert.Equal("raichu", CharacterEvolution.ItemDue(run)!.Species.Key);

        var opening = run.OpenChest(ChestTier.Poke, Vector2.Zero);

        var reward = Assert.Single(opening.Rewards);
        Assert.Equal(LevelUpKind.EvolutionItem, reward.Kind);
        Assert.Equal("Pietratuono", reward.NameIt);
        Assert.Null(CharacterEvolution.ItemDue(run));
        Assert.Equal("raichu", Assert.Single(CharacterEvolution.Ready(run)).Species.Key);

        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);
        Assert.Empty(run.EvolutionItems);
    }

    [Fact]
    public void Chests_give_the_item_only_from_its_level()
    {
        Assert.Null(CharacterEvolution.ItemDue(Play("pikachu", CharacterEvolution.SecondStageItemLevel - 1)));
        Assert.NotNull(CharacterEvolution.ItemDue(Play("pikachu", CharacterEvolution.SecondStageItemLevel)));
    }

    [Fact]
    public void Kirlia_chooses_when_it_holds_the_dawn_stone()
    {
        var run = Play("ralts", 27);
        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);
        Assert.Equal("gardevoir", Assert.Single(CharacterEvolution.Ready(run)).Species.Key);

        run.EvolutionItems.Add("dawn-stone");

        Assert.Equal(new[] { "gardevoir", "gallade" }, CharacterEvolution.Ready(run).Select(s => s.Species.Key));
    }

    [Fact]
    public void Gyarados_takes_its_own_ability()
    {
        var run = Play("magikarp", 18);
        CharacterEvolution.Evolve(run, CharacterEvolution.Ready(run)[0]);

        Assert.Equal("intimidate", run.Stage!.Ability.Id);
    }
}
