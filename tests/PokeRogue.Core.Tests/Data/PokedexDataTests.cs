using PokeRogue.Core.Data;

namespace PokeRogue.Core.Tests.Data;

public class PokedexDataTests
{
    [Fact]
    public void Contains_all_493_species_of_generations_1_to_4_with_italian_names()
    {
        var species = TestContent.Pokedex.Value.Species;

        Assert.Equal(Enumerable.Range(1, 493), species.Select(s => s.Id));
        Assert.All(species, s =>
        {
            Assert.InRange(s.Generation, 1, 4);
            Assert.False(string.IsNullOrWhiteSpace(s.NameIt));
            Assert.InRange(s.Types.Count, 1, 2);
        });
    }

    [Fact]
    public void Bulbasaur_has_grass_poison_typing_and_its_base_stats()
    {
        var bulbasaur = TestContent.Species(1);

        Assert.Equal([PokemonType.Grass, PokemonType.Poison], bulbasaur.Types);
        Assert.Equal(new BaseStats(45, 49, 49, 65, 65, 45), bulbasaur.BaseStats);
        Assert.Equal(2, Assert.Single(bulbasaur.Evolutions).ToSpeciesId);
        Assert.Equal(16, bulbasaur.Evolutions[0].MinLevel);
    }

    [Fact]
    public void Eevee_has_seven_evolutions_up_to_generation_4()
    {
        var targets = TestContent.Species(133).Evolutions.Select(e => e.ToSpeciesId).Distinct().Order();

        Assert.Equal([134, 135, 136, 196, 197, 470, 471], targets);
    }

    [Theory]
    [InlineData(35, new[] { PokemonType.Normal })]                      // Clefairy: Fairy only from Gen 6
    [InlineData(122, new[] { PokemonType.Psychic })]                    // Mr. Mime
    [InlineData(81, new[] { PokemonType.Electric, PokemonType.Steel })] // Magnemite: Steel since Gen 2
    public void Uses_generation_4_typings(int id, PokemonType[] expected)
    {
        Assert.Equal(expected, TestContent.Species(id).Types);
    }

    [Fact]
    public void Evolution_targets_stay_within_generation_4()
    {
        Assert.All(TestContent.Pokedex.Value.Species.SelectMany(s => s.Evolutions), e => Assert.InRange(e.ToSpeciesId, 1, 493));
    }
}
