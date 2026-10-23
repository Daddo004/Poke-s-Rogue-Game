using System.Text.Json;

namespace PokeRogue.Core.Data;

public static class GameDataFiles
{
    public const string Pokedex = "pokedex.json";
    public const string TypeChart = "typechart.json";
    public const string MoveCatalog = "moves-catalog.json";

    public static PokedexFile LoadPokedex(string contentRoot) =>
        Load(ContentPaths.Generated(contentRoot, Pokedex), DataJsonContext.Default.PokedexFile);

    public static TypeChartFile LoadTypeChart(string contentRoot) =>
        Load(ContentPaths.Generated(contentRoot, TypeChart), DataJsonContext.Default.TypeChartFile);

    public static MoveCatalogFile LoadMoveCatalog(string contentRoot) =>
        Load(ContentPaths.Generated(contentRoot, MoveCatalog), DataJsonContext.Default.MoveCatalogFile);

    private static T Load<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidDataException($"{path} is empty.");
    }
}
