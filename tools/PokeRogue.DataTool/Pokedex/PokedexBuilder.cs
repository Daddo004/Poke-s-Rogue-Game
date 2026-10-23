using PokeRogue.Core.Data;
using PokeRogue.DataTool.Csv;

namespace PokeRogue.DataTool.Pokedex;

/// <summary>
/// Turns the PokeAPI CSV dump into the game's generated data, frozen at <see cref="TargetGeneration"/>:
/// Gen 4 typings (no Fairy), Gen 4 type chart and Gen 4 move types/power.
/// </summary>
public sealed class PokedexBuilder(PokeApiCsv csv, int maxSpeciesId)
{
    public const int TargetGeneration = 4;
    private const string Source = "PokeAPI (github.com/PokeAPI/pokeapi, data/v2/csv)";

    private int _langIt;
    private int _langEn;

    public (PokedexFile Pokedex, TypeChartFile TypeChart, MoveCatalogFile Moves) Build()
    {
        var languages = csv["languages"];
        _langIt = LanguageId(languages, "it");
        _langEn = LanguageId(languages, "en");

        var moveKeys = csv["moves"].Rows.ToDictionary(r => csv["moves"].Int(r, "id"), r => csv["moves"].Str(r, "identifier"));
        return (BuildPokedex(moveKeys), BuildTypeChart(), BuildMoveCatalog());
    }

    private PokedexFile BuildPokedex(Dictionary<int, string> moveKeys)
    {
        var speciesTable = csv["pokemon_species"];
        var pokemonTable = csv["pokemon"];

        var speciesNames = Names(csv["pokemon_species_names"], "pokemon_species_id", "name");
        var genera = Names(csv["pokemon_species_names"], "pokemon_species_id", "genus");
        var defaultPokemon = pokemonTable.Rows
            .Where(r => pokemonTable.Bool(r, "is_default"))
            .ToDictionary(r => pokemonTable.Int(r, "species_id"));

        var stats = csv["pokemon_stats"].Rows
            .GroupBy(r => csv["pokemon_stats"].Int(r, "pokemon_id"))
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => csv["pokemon_stats"].Int(r, "stat_id"), r => csv["pokemon_stats"].Int(r, "base_stat")));

        var types = TypingsAtTargetGeneration();
        var evolutions = BuildEvolutions(moveKeys);
        var abilities = BuildAbilities();

        var species = new List<SpeciesData>();
        foreach (var row in speciesTable.Rows)
        {
            var id = speciesTable.Int(row, "id");
            if (id > maxSpeciesId)
                continue;

            var pokemon = defaultPokemon[id];
            var pokemonId = pokemonTable.Int(pokemon, "id");
            var s = stats[pokemonId];
            var nameEn = speciesNames.GetValueOrDefault((id, _langEn)) ?? speciesTable.Str(row, "identifier");

            species.Add(new SpeciesData(
                Id: id,
                Key: speciesTable.Str(row, "identifier"),
                NameIt: speciesNames.GetValueOrDefault((id, _langIt)) ?? nameEn,
                NameEn: nameEn,
                GenusIt: genera.GetValueOrDefault((id, _langIt)) ?? genera.GetValueOrDefault((id, _langEn)) ?? "",
                Generation: speciesTable.Int(row, "generation_id"),
                Types: types[pokemonId],
                BaseStats: new BaseStats(s[1], s[2], s[3], s[4], s[5], s[6]),
                HeightDm: pokemonTable.Int(pokemon, "height"),
                WeightHg: pokemonTable.Int(pokemon, "weight"),
                IsBaby: speciesTable.Bool(row, "is_baby"),
                IsLegendary: speciesTable.Bool(row, "is_legendary"),
                IsMythical: speciesTable.Bool(row, "is_mythical"),
                CaptureRate: speciesTable.Int(row, "capture_rate"),
                EvolvesFromSpeciesId: speciesTable.IntOrNull(row, "evolves_from_species_id"),
                EvolutionChainId: speciesTable.Int(row, "evolution_chain_id"),
                Evolutions: evolutions.GetValueOrDefault(id) ?? [],
                Abilities: abilities.GetValueOrDefault(pokemonId) ?? []));
        }

        species.Sort((a, b) => a.Id.CompareTo(b.Id));
        return new PokedexFile(1, Source, species);
    }

    /// <summary>Current typings, overridden by pokemon_types_past rows still valid in the target generation (e.g. Clefairy = Normal).</summary>
    private Dictionary<int, IReadOnlyList<PokemonType>> TypingsAtTargetGeneration()
    {
        var current = csv["pokemon_types"];
        var past = csv["pokemon_types_past"];

        var typeIds = current.Rows
            .GroupBy(r => current.Int(r, "pokemon_id"))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => current.Int(r, "slot")).Select(r => current.Int(r, "type_id")).ToList());

        foreach (var group in past.Rows.GroupBy(r => past.Int(r, "pokemon_id")))
        {
            var applicable = group.Where(r => past.Int(r, "generation_id") >= TargetGeneration).ToList();
            if (applicable.Count == 0)
                continue;

            var generation = applicable.Min(r => past.Int(r, "generation_id"));
            typeIds[group.Key] = applicable
                .Where(r => past.Int(r, "generation_id") == generation)
                .OrderBy(r => past.Int(r, "slot"))
                .Select(r => past.Int(r, "type_id"))
                .ToList();
        }

        // Default forms share their species id, so only those within range must map onto the 17 Gen 4 types.
        var result = new Dictionary<int, IReadOnlyList<PokemonType>>();
        foreach (var (pokemonId, ids) in typeIds)
        {
            var types = ids.Select(PokemonTypes.FromPokeApiId).ToList();
            if (types.Any(t => t is null))
            {
                if (pokemonId <= maxSpeciesId)
                    throw new InvalidDataException($"Pokémon {pokemonId} has a type that does not exist in Gen {TargetGeneration}: {string.Join(",", ids)}.");
                continue;
            }

            result[pokemonId] = types.Select(t => t!.Value).ToList();
        }

        return result;
    }

    private Dictionary<int, IReadOnlyList<EvolutionData>> BuildEvolutions(Dictionary<int, string> moveKeys)
    {
        var evo = csv["pokemon_evolution"];
        var species = csv["pokemon_species"];
        var evolvesFrom = species.Rows.ToDictionary(r => species.Int(r, "id"), r => species.IntOrNull(r, "evolves_from_species_id"));
        var triggers = csv["evolution_triggers"].Rows.ToDictionary(r => csv["evolution_triggers"].Int(r, "id"), r => csv["evolution_triggers"].Str(r, "identifier"));
        var itemKeys = csv["items"].Rows.ToDictionary(r => csv["items"].Int(r, "id"), r => csv["items"].Str(r, "identifier"));
        var itemNames = Names(csv["item_names"], "item_id", "name");
        var locations = csv["locations"].Rows.ToDictionary(r => csv["locations"].Int(r, "id"), r => csv["locations"].Str(r, "identifier"));

        var result = new Dictionary<int, List<EvolutionData>>();
        foreach (var row in evo.Rows)
        {
            var to = evo.Int(row, "evolved_species_id");
            if (to > maxSpeciesId || evolvesFrom[to] is not { } from)
                continue;

            var trigger = triggers[evo.Int(row, "evolution_trigger_id")] switch
            {
                "level-up" => EvolutionTrigger.LevelUp,
                "use-item" => EvolutionTrigger.UseItem,
                "trade" => EvolutionTrigger.Trade,
                _ => EvolutionTrigger.Other,
            };

            var itemId = evo.IntOrNull(row, "trigger_item_id");
            var heldItemId = evo.IntOrNull(row, "held_item_id");
            var knownMoveId = evo.IntOrNull(row, "known_move_id");
            var knownMoveTypeId = evo.IntOrNull(row, "known_move_type_id");
            var locationId = evo.IntOrNull(row, "location_id");
            var timeOfDay = evo.Str(row, "time_of_day");

            var data = new EvolutionData(
                ToSpeciesId: to,
                Trigger: trigger,
                MinLevel: evo.IntOrNull(row, "minimum_level"),
                ItemKey: itemId is { } i ? itemKeys[i] : null,
                ItemNameIt: itemId is { } j ? itemNames.GetValueOrDefault((j, _langIt)) ?? itemNames.GetValueOrDefault((j, _langEn)) : null,
                HeldItemKey: heldItemId is { } h ? itemKeys[h] : null,
                TimeOfDay: timeOfDay.Length > 0 ? timeOfDay : null,
                MinHappiness: evo.IntOrNull(row, "minimum_happiness"),
                KnownMoveKey: knownMoveId is { } m ? moveKeys[m] : null,
                KnownMoveType: knownMoveTypeId is { } t ? PokemonTypes.FromPokeApiId(t) : null,
                LocationKey: locationId is { } l ? locations[l] : null,
                GenderId: evo.IntOrNull(row, "gender_id"),
                RelativePhysicalStats: evo.IntOrNull(row, "relative_physical_stats"));

            if (!result.TryGetValue(from, out var list))
                result[from] = list = [];

            // Location-based methods are repeated once per game; keep one entry per distinct method.
            var dedupKey = data with { LocationKey = data.LocationKey is null ? null : "location" };
            if (!list.Any(e => (e with { LocationKey = e.LocationKey is null ? null : "location" }) == dedupKey))
                list.Add(data);
        }

        return result.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EvolutionData>)kv.Value.OrderBy(e => e.ToSpeciesId).ThenBy(e => e.Trigger).ToList());
    }

    private Dictionary<int, IReadOnlyList<AbilityRef>> BuildAbilities()
    {
        var table = csv["pokemon_abilities"];
        var abilities = csv["abilities"];
        var abilityInfo = abilities.Rows.ToDictionary(
            r => abilities.Int(r, "id"),
            r => (Key: abilities.Str(r, "identifier"), Generation: abilities.Int(r, "generation_id")));
        var names = Names(csv["ability_names"], "ability_id", "name");

        return table.Rows
            .GroupBy(r => table.Int(r, "pokemon_id"))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AbilityRef>)g
                    .OrderBy(r => table.Int(r, "slot"))
                    .Select(r => (Id: table.Int(r, "ability_id"), Hidden: table.Bool(r, "is_hidden")))
                    .Where(a => abilityInfo[a.Id].Generation <= TargetGeneration)
                    .Select(a => new AbilityRef(
                        abilityInfo[a.Id].Key,
                        names.GetValueOrDefault((a.Id, _langIt)) ?? names.GetValueOrDefault((a.Id, _langEn)) ?? abilityInfo[a.Id].Key,
                        a.Hidden))
                    .ToList());
    }

    private TypeChartFile BuildTypeChart()
    {
        var efficacy = csv["type_efficacy"];
        var past = csv["type_efficacy_past"];
        var factors = new Dictionary<(PokemonType Attack, PokemonType Defense), float>();

        foreach (var row in efficacy.Rows)
        {
            if (PokemonTypes.FromPokeApiId(efficacy.Int(row, "damage_type_id")) is { } a &&
                PokemonTypes.FromPokeApiId(efficacy.Int(row, "target_type_id")) is { } d)
                factors[(a, d)] = efficacy.Int(row, "damage_factor") / 100f;
        }

        foreach (var group in past.Rows.GroupBy(r => (A: past.Int(r, "damage_type_id"), D: past.Int(r, "target_type_id"))))
        {
            var applicable = group.Where(r => past.Int(r, "generation_id") >= TargetGeneration).ToList();
            if (applicable.Count == 0 ||
                PokemonTypes.FromPokeApiId(group.Key.A) is not { } a ||
                PokemonTypes.FromPokeApiId(group.Key.D) is not { } d)
                continue;

            var row = applicable.MinBy(r => past.Int(r, "generation_id"))!;
            factors[(a, d)] = past.Int(row, "damage_factor") / 100f;
        }

        var typeNames = Names(csv["type_names"], "type_id", "name");
        var types = PokemonTypes.All
            .Select(t => new TypeInfo(t, typeNames[((int)t + 1, _langIt)], typeNames[((int)t + 1, _langEn)]))
            .ToList();

        var matchups = factors
            .Where(kv => Math.Abs(kv.Value - 1f) > 0.001f)
            .OrderBy(kv => kv.Key.Attack).ThenBy(kv => kv.Key.Defense)
            .Select(kv => new TypeMatchup(kv.Key.Attack, kv.Key.Defense, kv.Value))
            .ToList();

        return new TypeChartFile(1, Source, types, matchups);
    }

    private MoveCatalogFile BuildMoveCatalog()
    {
        var moves = csv["moves"];
        var changelog = csv["move_changelog"];
        var versionGroups = csv["version_groups"];
        var vgInfo = versionGroups.Rows.ToDictionary(
            r => versionGroups.Int(r, "id"),
            r => (Generation: versionGroups.Int(r, "generation_id"), Order: versionGroups.Int(r, "order")));

        // Changelog rows hold the values *before* a change; the first change after the target generation tells the target-generation value.
        var laterChanges = changelog.Rows
            .Where(r => vgInfo[changelog.Int(r, "changed_in_version_group_id")].Generation > TargetGeneration)
            .GroupBy(r => changelog.Int(r, "move_id"))
            .ToDictionary(g => g.Key, g => g.OrderBy(r => vgInfo[changelog.Int(r, "changed_in_version_group_id")].Order).ToList());

        var names = Names(csv["move_names"], "move_id", "name");
        var result = new List<MoveCatalogEntry>();

        foreach (var row in moves.Rows)
        {
            var id = moves.Int(row, "id");
            var generation = moves.Int(row, "generation_id");
            if (id >= 10000 || generation > TargetGeneration)
                continue;

            var typeId = moves.Int(row, "type_id");
            var power = moves.IntOrNull(row, "power");
            var accuracy = moves.IntOrNull(row, "accuracy");

            if (laterChanges.TryGetValue(id, out var changes))
            {
                typeId = changes.Select(c => changelog.IntOrNull(c, "type_id")).FirstOrDefault(v => v is not null) ?? typeId;
                power = changes.Select(c => changelog.IntOrNull(c, "power")).FirstOrDefault(v => v is not null) ?? power;
                accuracy = changes.Select(c => changelog.IntOrNull(c, "accuracy")).FirstOrDefault(v => v is not null) ?? accuracy;
            }

            // Curse was "???"-typed in Gen 2–4; treat it as its modern Ghost type.
            var type = PokemonTypes.FromPokeApiId(typeId) ?? PokemonTypes.FromPokeApiId(moves.Int(row, "type_id")) ?? PokemonType.Normal;
            var category = moves.Int(row, "damage_class_id") switch
            {
                2 => MoveCategory.Physical,
                3 => MoveCategory.Special,
                _ => MoveCategory.Status,
            };

            var key = moves.Str(row, "identifier");
            var nameEn = names.GetValueOrDefault((id, _langEn)) ?? key;
            result.Add(new MoveCatalogEntry(id, key, names.GetValueOrDefault((id, _langIt)) ?? nameEn, nameEn, generation, type, category, power, accuracy));
        }

        return new MoveCatalogFile(1, Source, result);
    }

    private static int LanguageId(CsvTable languages, string identifier) =>
        languages.Int(languages.Rows.Single(r => languages.Str(r, "identifier") == identifier), "id");

    private static Dictionary<(int Id, int Lang), string> Names(CsvTable table, string idColumn, string valueColumn)
    {
        var result = new Dictionary<(int, int), string>();
        foreach (var row in table.Rows)
        {
            var value = table.Str(row, valueColumn);
            if (value.Length > 0)
                result[(table.Int(row, idColumn), table.Int(row, "local_language_id"))] = value;
        }

        return result;
    }
}
