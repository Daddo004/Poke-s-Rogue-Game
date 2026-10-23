using PokeRogue.DataTool.Csv;
using PokeRogue.DataTool.Download;

namespace PokeRogue.DataTool.Pokedex;

/// <summary>Fetches and caches the PokeAPI CSV dump (github.com/PokeAPI/pokeapi, data/v2/csv).</summary>
public sealed class PokeApiCsv(Downloader downloader, string cacheDir)
{
    private const string BaseUrl = "https://raw.githubusercontent.com/PokeAPI/pokeapi/master/data/v2/csv";

    public static readonly string[] RequiredTables =
    [
        "languages", "version_groups",
        "pokemon", "pokemon_species", "pokemon_species_names", "pokemon_stats",
        "pokemon_types", "pokemon_types_past", "types", "type_names", "type_efficacy", "type_efficacy_past",
        "pokemon_evolution", "evolution_triggers", "items", "item_names", "locations",
        "moves", "move_names", "move_changelog", "abilities", "ability_names", "pokemon_abilities",
    ];

    private readonly Dictionary<string, CsvTable> _tables = new(StringComparer.Ordinal);

    public async Task DownloadAllAsync(CancellationToken ct = default)
    {
        await Task.WhenAll(RequiredTables.Select(async name =>
        {
            if (!await downloader.FetchAsync($"{BaseUrl}/{name}.csv", PathOf(name), ct))
                throw new FileNotFoundException($"PokeAPI table '{name}' not found upstream.");
        }));
    }

    public CsvTable this[string name]
    {
        get
        {
            if (!_tables.TryGetValue(name, out var table))
                _tables[name] = table = CsvTable.Load(PathOf(name));
            return table;
        }
    }

    private string PathOf(string name) => Path.Combine(cacheDir, name + ".csv");
}
