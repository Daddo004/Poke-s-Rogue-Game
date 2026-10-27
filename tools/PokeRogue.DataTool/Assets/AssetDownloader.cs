using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PokeRogue.Core.Data;
using PokeRogue.DataTool.Download;

namespace PokeRogue.DataTool.Assets;

/// <summary>Downloads sprites, portraits, move FX, trainer sprites and cries into content/, then writes a coverage report and credits.</summary>
public sealed class AssetDownloader(Downloader downloader, string contentRoot, int maxSpeciesId)
{
    private const string SpriteCollabRaw = "https://raw.githubusercontent.com/PMDCollab/SpriteCollab/master";
    private const string PokeApiSpritesRaw = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon";
    private const string CriesRaw = "https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon";
    private const string ShowdownFxApi = "https://api.github.com/repos/smogon/pokemon-showdown-client/contents/play.pokemonshowdown.com/fx";
    private const string ShowdownTrainers = "https://play.pokemonshowdown.com/sprites/trainers";
    private const string GoogleFontsRaw = "https://raw.githubusercontent.com/google/fonts/main/ofl";

    /// <summary>
    /// OFL pixel fonts offered in the art-direction preview. Pixelify Sans comes from its upstream repository as a static
    /// Regular: the variable file on Google Fonts renders some digits wrongly with stb_truetype (5 looks like S).
    /// </summary>
    private static readonly (string File, string Url)[] Fonts =
    [
        ("Silkscreen-Regular.ttf", $"{GoogleFontsRaw}/silkscreen/Silkscreen-Regular.ttf"),
        ("PressStart2P-Regular.ttf", $"{GoogleFontsRaw}/pressstart2p/PressStart2P-Regular.ttf"),
        ("VT323-Regular.ttf", $"{GoogleFontsRaw}/vt323/VT323-Regular.ttf"),
        ("PixelifySans.ttf", "https://raw.githubusercontent.com/eifetx/Pixelify-Sans/main/fonts/ttf/PixelifySans-Regular.ttf"),
        ("Jersey10-Regular.ttf", $"{GoogleFontsRaw}/jersey10/Jersey10-Regular.ttf"),
    ];

    /// <summary>Item icons (PokeAPI identifiers) for passives, pickups, chests and evolution stones.</summary>
    private static readonly string[] ItemIcons =
    [
        "muscle-band", "wise-glasses", "iron", "zinc", "hp-up", "protein", "calcium", "carbos", "leftovers", "quick-claw",
        "wide-lens", "light-clay", "magnet", "lucky-egg", "amulet-coin", "scope-lens", "focus-sash", "metronome", "shell-bell",
        "charcoal", "mystic-water", "miracle-seed", "never-melt-ice", "black-glasses", "silk-scarf", "soft-sand", "hard-stone",
        "silver-powder", "spell-tag", "metal-coat", "twisted-spoon", "dragon-fang", "sharp-beak", "poison-barb", "black-belt",
        "potion", "super-potion", "rare-candy", "repel", "max-repel", "exp-share", "poke-ball", "great-ball", "ultra-ball", "master-ball",
        "fire-stone", "water-stone", "thunder-stone", "leaf-stone", "moon-stone", "sun-stone", "shiny-stone", "dusk-stone", "dawn-stone",
        "oval-stone", "kings-rock", "metal-coat", "dragon-scale", "up-grade", "dubious-disc", "protector", "electirizer", "magmarizer",
        "reaper-cloth", "razor-claw", "razor-fang", "deep-sea-tooth", "deep-sea-scale", "prism-scale", "pp-max", "max-elixir",
        "oran-berry", "sitrus-berry", "nugget", "big-nugget",
        // Arceus' Plates (phase 9.1).
        "flame-plate", "splash-plate", "zap-plate", "meadow-plate", "icicle-plate", "fist-plate", "toxic-plate", "earth-plate",
        "sky-plate", "mind-plate", "insect-plate", "stone-plate", "spooky-plate", "draco-plate", "dread-plate", "iron-plate",
    ];

    /// <summary>Animations a sprite can live without (the game falls back to Hurt/Attack).</summary>
    private static readonly HashSet<string> OptionalAnims = [PmdAnimations.Faint, PmdAnimations.Shoot, PmdAnimations.Sleep, PmdAnimations.Pose];

    private static readonly string[] FxExcludedPrefixes = ["bg", "client-", "mafia-", "hangman", "gender-", "closebutton", "groupchat", "faint"];

    private readonly ConcurrentBag<string> _missingSprites = [];
    private readonly ConcurrentBag<string> _missingAnims = [];
    private readonly ConcurrentBag<string> _missingPortraits = [];
    private readonly ConcurrentBag<string> _missingCries = [];
    private readonly ConcurrentBag<string> _missingTrainers = [];
    private readonly ConcurrentBag<string> _missingIcons = [];
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _credits = new();
    private int _fxCount;

    private string CacheDir => Path.Combine(contentRoot, "cache");

    public async Task RunAsync(CancellationToken ct = default)
    {
        await DownloadPokemonAsync(ct);
        await DownloadFxAsync(ct);
        await DownloadTrainersAsync(ct);
        await DownloadCriesAsync(ct);
        await Task.WhenAll(Fonts.Select(f => downloader.FetchAsync(f.Url, Path.Combine(contentRoot, "fonts", f.File), ct)));
        await DownloadIconsAsync(ct);
        WriteReport();
        WriteCredits();
    }

    private async Task DownloadPokemonAsync(CancellationToken ct)
    {
        var trackerPath = Path.Combine(CacheDir, "spritecollab", "tracker.json");
        await downloader.FetchAsync($"{SpriteCollabRaw}/tracker.json", trackerPath, ct);
        using var tracker = JsonDocument.Parse(File.ReadAllBytes(trackerPath));

        var done = 0;
        await Parallel.ForEachAsync(Enumerable.Range(1, maxSpeciesId), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (id, token) =>
        {
            var key = id.ToString("D4");
            var entry = tracker.RootElement.TryGetProperty(key, out var e) ? e : default;
            await DownloadSpriteAsync(id, entry, token);
            await DownloadPortraitAsync(id, entry, token);

            var count = Interlocked.Increment(ref done);
            if (count % 50 == 0)
                Console.WriteLine($"  Pokémon {count}/{maxSpeciesId}");
        });
    }

    private async Task DownloadSpriteAsync(int id, JsonElement entry, CancellationToken ct)
    {
        var folder = ContentPaths.SpriteFolder(contentRoot, id);
        var animDataPath = Path.Combine(folder, PmdAnimations.AnimDataFile);
        var remote = $"{SpriteCollabRaw}/sprite/{id:D4}";

        if (!await downloader.FetchAsync($"{remote}/{PmdAnimations.AnimDataFile}", animDataPath, ct))
        {
            _missingSprites.Add($"{id:D4}");
            await downloader.FetchAsync($"{PokeApiSpritesRaw}/versions/generation-iv/heartgold-soulsilver/{id}.png", Path.Combine(contentRoot, "sprites", "fallback", $"{id:D4}.png"), ct);
            return;
        }

        AddCredit(entry, "sprite_credit", $"sprite {id:D4}");
        var anims = XDocument.Load(animDataPath).Root!.Element("Anims")!.Elements("Anim")
            .ToDictionary(a => (string)a.Element("Name")!, a => (string?)a.Element("CopyOf"));

        foreach (var name in PmdAnimations.Required)
        {
            // CopyOf chains point at another animation's sheet (e.g. Sleep → Laying); fetch the sheet that actually exists.
            var sheet = name;
            for (var hops = 0; anims.TryGetValue(sheet, out var copyOf) && copyOf is not null && hops < 8; hops++)
                sheet = copyOf;

            if (!anims.ContainsKey(sheet))
            {
                _missingAnims.Add($"{id:D4}:{name}");
                continue;
            }

            var file = PmdAnimations.SheetFile(sheet);
            if (!await downloader.FetchAsync($"{remote}/{file}", Path.Combine(folder, file), ct))
                _missingAnims.Add($"{id:D4}:{name}");
        }
    }

    private async Task DownloadPortraitAsync(int id, JsonElement entry, CancellationToken ct)
    {
        if (await downloader.FetchAsync($"{SpriteCollabRaw}/portrait/{id:D4}/Normal.png", ContentPaths.Portrait(contentRoot, id), ct))
            AddCredit(entry, "portrait_credit", $"portrait {id:D4}");
        else
            _missingPortraits.Add($"{id:D4}");
    }

    private async Task DownloadFxAsync(CancellationToken ct)
    {
        var listPath = Path.Combine(CacheDir, "showdown-fx.json");
        if (!File.Exists(listPath) || downloader.Force)
            File.WriteAllText(listPath, await downloader.GetStringAsync(ShowdownFxApi, ct));

        using var list = JsonDocument.Parse(File.ReadAllText(listPath));
        var files = list.RootElement.EnumerateArray()
            .Select(f => (Name: f.GetProperty("name").GetString()!, Url: f.GetProperty("download_url").GetString()))
            .Where(f => f.Url is not null && (f.Name.EndsWith(".png") || f.Name.EndsWith(".gif")))
            .Where(f => !FxExcludedPrefixes.Any(p => f.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Task.WhenAll(files.Select(async f =>
        {
            if (await downloader.FetchAsync(f.Url!, Path.Combine(contentRoot, "fx", f.Name), ct))
                Interlocked.Increment(ref _fxCount);
        }));
    }

    private async Task DownloadTrainersAsync(CancellationToken ct)
    {
        await Task.WhenAll(TrainerSpriteList.All.Select(async t =>
        {
            foreach (var candidate in t.Candidates)
            {
                if (await downloader.FetchAsync($"{ShowdownTrainers}/{candidate}.png", Path.Combine(contentRoot, "trainers", $"{t.Key}.png"), ct))
                    return;
            }

            _missingTrainers.Add(t.Key);
        }));
    }

    private async Task DownloadCriesAsync(CancellationToken ct)
    {
        // Legacy cries match the Gen 1–4 games; fall back to the modern recording.
        await Parallel.ForEachAsync(Enumerable.Range(1, maxSpeciesId), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (id, token) =>
        {
            var path = Path.Combine(contentRoot, "cries", $"{id:D4}.ogg");
            if (!await downloader.FetchAsync($"{CriesRaw}/legacy/{id}.ogg", path, token) &&
                !await downloader.FetchAsync($"{CriesRaw}/latest/{id}.ogg", path, token))
                _missingCries.Add($"{id:D4}");
        });
    }

    private async Task DownloadIconsAsync(CancellationToken ct)
    {
        var pokeApi = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites";
        var items = ItemIcons.Distinct().Select(async key =>
        {
            if (!await downloader.FetchAsync($"{pokeApi}/items/{key}.png", Path.Combine(contentRoot, "icons", "items", $"{key}.png"), ct))
                _missingIcons.Add($"item:{key}");
        });

        // Gen 4 (HGSS) type badges, saved by type name.
        var types = PokemonTypes.All.Select(async type =>
        {
            if (!await downloader.FetchAsync($"{pokeApi}/types/generation-iv/heartgold-soulsilver/{(int)type + 1}.png", Path.Combine(contentRoot, "icons", "types", $"{type}.png"), ct))
                _missingIcons.Add($"type:{type}");
        });

        await Task.WhenAll(items.Concat(types));
    }

    private void AddCredit(JsonElement entry, string property, string what)
    {
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty(property, out var credit))
            return;

        var ids = new List<string>();
        if (credit.TryGetProperty("primary", out var primary) && primary.GetString() is { Length: > 0 } p)
            ids.Add(p);
        if (credit.TryGetProperty("secondary", out var secondary))
            ids.AddRange(secondary.EnumerateArray().Select(s => s.GetString()).OfType<string>());

        foreach (var id in ids)
            _credits.GetOrAdd(id, _ => []).Add(what);
    }

    private void WriteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Asset report");
        sb.AppendLine();
        sb.AppendLine($"- PMD sprites: {maxSpeciesId - _missingSprites.Count}/{maxSpeciesId} (missing → HGSS fallback): {Join(_missingSprites)}");
        var required = _missingAnims.Where(a => !OptionalAnims.Contains(a[(a.IndexOf(':') + 1)..])).ToList();
        sb.AppendLine($"- Missing required animations (Idle/Walk/Attack/Charge/Hurt): {required.Count}: {Join(required)}");
        sb.AppendLine($"- Missing optional animations (Faint/Shoot/Sleep/Pose, with fallbacks): {_missingAnims.Count - required.Count}");
        sb.AppendLine($"- Portraits: {maxSpeciesId - _missingPortraits.Count}/{maxSpeciesId}; missing: {Join(_missingPortraits)}");
        sb.AppendLine($"- Cries: {maxSpeciesId - _missingCries.Count}/{maxSpeciesId}; missing: {Join(_missingCries)}");
        sb.AppendLine($"- Trainers: {TrainerSpriteList.All.Count - _missingTrainers.Count}/{TrainerSpriteList.All.Count}; missing: {Join(_missingTrainers)}");
        sb.AppendLine($"- Move FX images: {_fxCount}");
        sb.AppendLine($"- Icons (items + type badges): missing {Join(_missingIcons)}");

        var path = Path.Combine(contentRoot, "generated", "asset-report.md");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Console.Write(sb.ToString());
    }

    private void WriteCredits()
    {
        var names = LoadCreditNames();
        var sb = new StringBuilder();
        sb.AppendLine("# Crediti");
        sb.AppendLine();
        sb.AppendLine("PokeRogue è un progetto personale, non commerciale e non distribuito. Pokémon © Nintendo / Creatures Inc. / GAME FREAK inc.");
        sb.AppendLine();
        sb.AppendLine("## Sprite e ritratti — PMD Sprite Collab (CC BY-NC 4.0)");
        sb.AppendLine("https://github.com/PMDCollab/SpriteCollab");
        sb.AppendLine();
        foreach (var (id, works) in _credits.OrderBy(kv => names.GetValueOrDefault(kv.Key, kv.Key), StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"- {names.GetValueOrDefault(id, id)} ({works.Count})");

        sb.AppendLine();
        sb.AppendLine("## Dati");
        sb.AppendLine("- PokeAPI — https://github.com/PokeAPI/pokeapi");
        sb.AppendLine("- Versi: PokeAPI cries — https://github.com/PokeAPI/cries");
        sb.AppendLine("- Carte collezionabili: carte del Gioco di Carte Collezionabili Pokémon (edizione italiana), tramite l'API di TCGdex — https://tcgdex.dev; illustrazioni dei rispettivi autori");
        sb.AppendLine("- Bustine: render di Pokémon HOME, tramite l'API di PokeAPI — https://pokeapi.co");
        sb.AppendLine();
        sb.AppendLine("## Effetti e allenatori");
        sb.AppendLine("- Pokémon Showdown client (fx, sprite allenatori) — https://github.com/smogon/pokemon-showdown-client");
        sb.AppendLine();
        sb.AppendLine("## Font (SIL Open Font License)");
        sb.AppendLine("- Silkscreen, Press Start 2P, VT323, Pixelify Sans, Jersey 10 — https://github.com/google/fonts");

        File.WriteAllText(Path.Combine(contentRoot, "generated", "CREDITS.md"), sb.ToString(), new UTF8Encoding(false));
    }

    private Dictionary<string, string> LoadCreditNames()
    {
        var path = Path.Combine(CacheDir, "spritecollab", "credit_names.txt");
        if (!File.Exists(path))
            return [];

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2 && parts[0].Length > 0)
            {
                result.TryAdd(parts[1], parts[0]);
                result.TryAdd(parts[0], parts[0]);
            }
        }

        return result;
    }

    private static string Join(IEnumerable<string> items)
    {
        var list = items.Order().ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }
}
