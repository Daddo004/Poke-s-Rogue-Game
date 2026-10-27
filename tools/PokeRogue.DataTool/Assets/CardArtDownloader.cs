using System.Text;
using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Moves;
using PokeRogue.DataTool.Download;

namespace PokeRogue.DataTool.Assets;

/// <summary>
/// The collectible cards (phase 10), all from web APIs. The cards are real Pokémon TCG cards in their Italian print, from
/// TCGdex (api.tcgdex.net): for each playable species it asks for every card showing it, then each card's details (its
/// rarity, expansion, illustrator), sorts them into the game's rarities by content/data/cards.json and keeps up to each
/// rarity's quota, mixing eras (TCG Pocket's cards first). The choice goes to content/generated/carte.json and the pictures
/// to content/cards/tcg. The packs show each line's Pokémon HOME render, asked of PokeAPI (pokeapi.co/api/v2/pokemon).
/// Every answer is cached in content/cache.
/// </summary>
public sealed class CardArtDownloader(Downloader downloader, string contentRoot)
{
    private const string Tcgdex = "https://api.tcgdex.net/v2/it";
    private const string PokeApi = "https://pokeapi.co/api/v2/pokemon";

    /// <summary>The order eras are drawn in when a species has more cards than its quota: TCG Pocket first, then newest to oldest.</summary>
    private static readonly string[] Eras = ["tcgp", "sv", "swsh", "sm", "xy", "bw", "hgss", "pl", "dp", "ex", "ecard", "neo", "base"];

    public async Task RunAsync(CancellationToken ct = default)
    {
        var pokedex = GameDataFiles.LoadPokedex(contentRoot).Species;
        var characters = CharacterLibrary.Load(contentRoot, pokedex, MoveLibrary.Load(contentRoot));
        var rules = CardCatalog.LoadRules(contentRoot);
        var species = characters.All.SelectMany(c => c.Stages).Select(s => s.Species.Id).Distinct().Order().ToList();

        Console.WriteLine($"Cards: asking TCGdex about {species.Count} species...");
        var chosen = new List<TcgCard>();
        var unknown = new HashSet<string>();
        var done = 0;
        await Parallel.ForEachAsync(species, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, token) =>
        {
            var cards = await CardsOfAsync(id, rules, token);
            lock (chosen)
            {
                chosen.AddRange(Choose(cards, rules));
                foreach (var card in cards.Where(c => c.Tier == CardRarity.Base && !rules.Rarities[0].ApiRarities.Contains(c.Rarity)))
                    unknown.Add(card.Rarity);
            }

            if (Interlocked.Increment(ref done) % 25 == 0)
                Console.WriteLine($"  {done}/{species.Count}");
        });

        if (unknown.Count > 0)
            Console.WriteLine($"  Rarities in no list of cards.json, counted as Base: {string.Join(", ", unknown.Order())}");

        Console.WriteLine($"Cards: downloading the pictures of {chosen.Count}...");
        var missing = new List<string>();
        await Parallel.ForEachAsync(chosen, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (card, token) =>
        {
            if (!await downloader.FetchAsync(card.ImageUrl, ContentPaths.TcgCard(contentRoot, card.Id), token))
            {
                lock (missing)
                    missing.Add(card.Id);
            }
        });

        // A card the API lists but whose picture is not there is left out. The rest go in the order of the Pokédex, then
        // of the rarities, as the catalogue reads them.
        // A card showing two species of the roster (a TAG TEAM) is kept once, under the first.
        chosen = chosen.Where(c => !missing.Contains(c.Id))
            .OrderBy(c => c.Species).ThenBy(c => c.Tier).ThenBy(c => c.Id, StringComparer.Ordinal)
            .DistinctBy(c => c.Id).ToList();
        var file = new TcgCardFile(1, "TCGdex (api.tcgdex.net/v2/it), Italian prints", chosen);
        File.WriteAllText(ContentPaths.Generated(contentRoot, CardCatalog.TcgFileName), JsonSerializer.Serialize(file, DataJsonContext.Default.TcgCardFile), new UTF8Encoding(false));
        Console.WriteLine($"Cards: {chosen.Count} kept ({string.Join(", ", chosen.GroupBy(c => c.Tier).Select(g => $"{g.Key} {g.Count()}"))}).");

        // The packs: the HOME render of each line's first species.
        foreach (var first in characters.All.Select(c => c.First.Id).Distinct())
        {
            if (await HomeRenderAsync(first, ct) is not { } url || !await downloader.FetchAsync(url, ContentPaths.CardArt(contentRoot, first, "home"), ct))
                missing.Add($"home {first:D4}");
        }

        Console.WriteLine(missing.Count == 0 ? "Cards: complete." : $"Cards: missing {string.Join(", ", missing.Order())}");
    }

    /// <summary>Every card of the species the API has a picture of, with its details.</summary>
    private async Task<List<TcgCard>> CardsOfAsync(int speciesId, CardFile rules, CancellationToken ct)
    {
        using var list = JsonDocument.Parse(await CachedAsync($"{Tcgdex}/cards?dexId=eq:{speciesId}", $"list-{speciesId}.json", ct));
        var cards = new List<TcgCard>();
        foreach (var brief in list.RootElement.EnumerateArray())
        {
            if (!brief.TryGetProperty("image", out var image) || image.GetString() is not { Length: > 0 } imageUrl)
                continue;

            var id = brief.GetProperty("id").GetString()!;
            using var detail = JsonDocument.Parse(await CachedAsync($"{Tcgdex}/cards/{Uri.EscapeDataString(id)}", $"card-{Safe(id)}.json", ct));
            var root = detail.RootElement;
            var rarity = root.TryGetProperty("rarity", out var r) ? r.GetString() ?? "Nessuna" : "Nessuna";
            cards.Add(new TcgCard
            {
                Id = id,
                Species = speciesId,
                Name = root.GetProperty("name").GetString() ?? "",
                Rarity = rarity,
                Tier = CardCatalog.TierOf(rules, rarity),
                Set = root.TryGetProperty("set", out var set) && set.TryGetProperty("name", out var setName) ? setName.GetString() ?? "" : "",
                Illustrator = root.TryGetProperty("illustrator", out var illustrator) ? illustrator.GetString() : null,
                Image = imageUrl,
            });
        }

        return cards;
    }

    /// <summary>Up to each rarity's quota, one era after another so a species' cards come from different times.</summary>
    private static IEnumerable<TcgCard> Choose(List<TcgCard> cards, CardFile rules)
    {
        foreach (var spec in rules.Rarities)
        {
            var byEra = cards.Where(c => c.Tier == spec.Rarity)
                .GroupBy(EraOf)
                .OrderBy(g => Array.IndexOf(Eras, g.Key) is var i and >= 0 ? i : Eras.Length)
                .Select(g => new Queue<TcgCard>(g.OrderBy(c => c.Id, StringComparer.Ordinal)))
                .ToList();
            var taken = 0;
            while (taken < spec.Quota && byEra.Any(q => q.Count > 0))
            {
                foreach (var queue in byEra.Where(q => q.Count > 0))
                {
                    if (taken == spec.Quota)
                        break;
                    yield return queue.Dequeue();
                    taken++;
                }
            }
        }
    }

    /// <summary>The series in the picture's address: https://assets.tcgdex.net/it/{series}/{set}/{number}.</summary>
    private static string EraOf(TcgCard card)
    {
        var parts = new Uri(card.Image).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : "";
    }

    private async Task<string?> HomeRenderAsync(int speciesId, CancellationToken ct)
    {
        using var answer = JsonDocument.Parse(await CachedAsync($"{PokeApi}/{speciesId}", Path.Combine("..", "pokeapi-api", $"pokemon-{speciesId}.json"), ct));
        return answer.RootElement.GetProperty("sprites").GetProperty("other").TryGetProperty("home", out var home) &&
               home.TryGetProperty("front_default", out var url) && url.ValueKind == JsonValueKind.String
            ? url.GetString()
            : null;
    }

    private async Task<string> CachedAsync(string url, string name, CancellationToken ct)
    {
        var path = Path.GetFullPath(Path.Combine(contentRoot, "cache", "tcgdex", name));
        if (downloader.Force || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, await downloader.GetStringAsync(url, ct), ct);
        }

        return await File.ReadAllTextAsync(path, ct);
    }

    private static string Safe(string id) => string.Concat(id.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_'));
}
