using PokeRogue.Core.Data;

namespace PokeRogue.Core.Tests.Data;

public class TypeChartTests
{
    private static TypeChart Chart => TestContent.TypeChart.Value;

    [Theory]
    [InlineData(PokemonType.Fire, PokemonType.Grass, 2f)]
    [InlineData(PokemonType.Water, PokemonType.Fire, 2f)]
    [InlineData(PokemonType.Fire, PokemonType.Water, 0.5f)]
    [InlineData(PokemonType.Electric, PokemonType.Ground, 0f)]
    [InlineData(PokemonType.Ghost, PokemonType.Normal, 0f)]
    [InlineData(PokemonType.Normal, PokemonType.Ghost, 0f)]
    [InlineData(PokemonType.Ghost, PokemonType.Steel, 0.5f)] // Gen 2–5 rule
    [InlineData(PokemonType.Dark, PokemonType.Steel, 0.5f)]  // Gen 2–5 rule
    [InlineData(PokemonType.Bug, PokemonType.Poison, 0.5f)]  // Gen 1 had 2x
    [InlineData(PokemonType.Dragon, PokemonType.Dragon, 2f)]
    [InlineData(PokemonType.Normal, PokemonType.Normal, 1f)]
    public void Single_type_matchups_follow_generation_4(PokemonType attack, PokemonType defense, float expected)
    {
        Assert.Equal(expected, Chart.Factor(attack, defense));
    }

    [Fact]
    public void Dual_types_multiply()
    {
        Assert.Equal(4f, Chart.Factor(PokemonType.Fire, [PokemonType.Bug, PokemonType.Steel]));
        Assert.Equal(0.25f, Chart.Factor(PokemonType.Grass, [PokemonType.Fire, PokemonType.Flying]));
        Assert.Equal(0f, Chart.Factor(PokemonType.Ground, [PokemonType.Electric, PokemonType.Flying]));
    }

    [Fact]
    public void Constructor_defaults_unlisted_matchups_to_neutral()
    {
        var chart = new TypeChart([new TypeMatchup(PokemonType.Fire, PokemonType.Grass, 2f)]);

        Assert.Equal(2f, chart.Factor(PokemonType.Fire, PokemonType.Grass));
        Assert.Equal(1f, chart.Factor(PokemonType.Water, PokemonType.Rock));
    }
}
