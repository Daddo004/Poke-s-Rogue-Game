using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.Core.Tests;

internal static class TestContent
{
    public static readonly string Root = ContentPaths.FindContentRoot();

    public static readonly Lazy<PokedexFile> Pokedex = new(() => GameDataFiles.LoadPokedex(Root));

    public static readonly Lazy<TypeChart> TypeChart = new(() => new TypeChart(GameDataFiles.LoadTypeChart(Root).Matchups));

    public static readonly Lazy<MoveLibrary> Moves = new(() => MoveLibrary.Load(Root));

    public static readonly Lazy<StatusLibrary> Statuses = new(() => StatusLibrary.Load(Root));

    public static readonly Lazy<ItemLibrary> Items = new(() => ItemLibrary.Load(Root));

    public static readonly Lazy<PokeRogue.Core.Run.Progression.LevelUpRules> LevelUpRules = new(() => PokeRogue.Core.Run.Progression.LevelUpRules.Load(Root));

    public static ItemDefinition Item(string id) => Items.Value[id];

    public static SpeciesData Species(int id) => Pokedex.Value.Species.Single(s => s.Id == id);

    public static MoveDefinition Move(string id) => Moves.Value[id];
}
