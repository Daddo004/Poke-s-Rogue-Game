using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.DataTool.Download;
using PokeRogue.DataTool.Pokedex;

var command = args.FirstOrDefault() ?? "help";
var force = args.Contains("--force");
var maxId = ReadIntOption("--max-id") ?? 493;

var contentRoot = ContentPaths.FindContentRoot(Directory.GetCurrentDirectory());
Directory.CreateDirectory(Path.Combine(contentRoot, "generated"));
Console.WriteLine($"Content root: {contentRoot}");

using var downloader = new Downloader { Force = force };

switch (command)
{
    case "data":
        await BuildDataAsync();
        break;
    case "assets":
        await DownloadAssetsAsync();
        break;
    case "all":
        await BuildDataAsync();
        await DownloadAssetsAsync();
        break;
    case "bilancio":
        SimulateBalance();
        break;
    case "banco":
        BenchMoves();
        break;
    case "personaggi":
        SimulateRoster();
        break;
    case "allenatori":
        SimulateTrainers();
        break;
    case "sfide":
        SimulateChallenges();
        break;
    case "meta":
        SimulateMeta();
        break;
    default:
        Console.WriteLine("""
            Usage: dotnet run --project tools/PokeRogue.DataTool -- <command> [options]

              data      Download the PokeAPI CSV dump and generate content/generated/*.json
              assets    Download sprites, portraits, move FX, trainer sprites and cries
              all       Run every step
              bilancio  Simulate autopilot runs and write a balance report (Milestone B)
              banco     Measure every move alone at levels 1, 4 and 8 against steady waves (phase 2.8)
              personaggi  Play every character headless and compare them (phase 4.5): --runs N (default 6),
                          --solo id,id to play only some, --out PATH (default previews/4.5/simulazione-personaggi.md)
              allenatori  Write the trainers' summary (previews/5.1/allenatori.md); with --runs N also play four characters
                          with every trainer and with none (previews/5.2/simulazione-allenatori.md)
              sfide     Play every trainer's challenge with four characters (phase 5.3): --runs N (default 2),
                        writes previews/5.3/simulazione-sfide.md
              meta      Careers from an empty save (Block 6): --carriere N (default 8) --partite N (default 20),
                        writes previews/6.2/simulazione-meta.md

            Options:
              --force       Re-download files that are already cached
              --max-id N    Last national dex number to include (default 493)
              --runs N      bilancio: runs per way of picking cards (default 40)
              --senza-pesi  bilancio: draw level-up cards all alike, as before phase 2.7
              --out PATH    bilancio, banco: report file (default previews/milestone-b/simulazione-bilanciamento.md
                            or previews/milestone-b/banco-di-prova.md)
            """);
        break;
}

void SimulateBalance()
{
    var runs = ReadIntOption("--runs") ?? 40;
    var report = new PokeRogue.DataTool.Balance.BalanceSimulation(contentRoot, weightedCards: !args.Contains("--senza-pesi")).Report(runs, Console.WriteLine);
    WriteReport(report, "simulazione-bilanciamento.md");
}

void SimulateRoster()
{
    var runs = ReadIntOption("--runs") ?? 6;
    var onlyIndex = Array.IndexOf(args, "--solo");
    var only = onlyIndex >= 0 && onlyIndex + 1 < args.Length ? args[onlyIndex + 1].Split(',') : null;
    var report = new PokeRogue.DataTool.Balance.RosterSimulation(contentRoot).Report(runs, only, Console.WriteLine);
    var outIndex = Array.IndexOf(args, "--out");
    var output = outIndex >= 0 && outIndex + 1 < args.Length
        ? args[outIndex + 1]
        : Path.Combine(Path.GetDirectoryName(contentRoot)!, "previews", "4.5", "simulazione-personaggi.md");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, report);
    Console.WriteLine($"Wrote {output}");
}

void SimulateTrainers()
{
    var previews = Path.Combine(Path.GetDirectoryName(contentRoot)!, "previews");
    var simulation = new PokeRogue.DataTool.Balance.TrainerSimulation(contentRoot);
    var summary = Path.Combine(previews, "5.1", "allenatori.md");
    Directory.CreateDirectory(Path.GetDirectoryName(summary)!);
    File.WriteAllText(summary, simulation.Summary());
    Console.WriteLine($"Wrote {summary}");

    if (ReadIntOption("--runs") is not { } runs)
        return;

    var report = Path.Combine(previews, "5.2", "simulazione-allenatori.md");
    Directory.CreateDirectory(Path.GetDirectoryName(report)!);
    File.WriteAllText(report, simulation.Report(runs, Console.WriteLine));
    Console.WriteLine($"Wrote {report}");
}

void SimulateChallenges()
{
    var runs = ReadIntOption("--runs") ?? 2;
    var report = Path.Combine(Path.GetDirectoryName(contentRoot)!, "previews", "5.3", "simulazione-sfide.md");
    Directory.CreateDirectory(Path.GetDirectoryName(report)!);
    File.WriteAllText(report, new PokeRogue.DataTool.Balance.TrainerSimulation(contentRoot).ChallengeReport(runs, Console.WriteLine));
    Console.WriteLine($"Wrote {report}");
}

void SimulateMeta()
{
    var report = Path.Combine(Path.GetDirectoryName(contentRoot)!, "previews", "6.2", "simulazione-meta.md");
    Directory.CreateDirectory(Path.GetDirectoryName(report)!);
    File.WriteAllText(report, new PokeRogue.DataTool.Balance.MetaSimulation(contentRoot)
        .Report(ReadIntOption("--carriere") ?? 8, ReadIntOption("--partite") ?? 20, Console.WriteLine));
    Console.WriteLine($"Wrote {report}");
}

void BenchMoves() => WriteReport(new PokeRogue.DataTool.Balance.MoveBench(contentRoot).Report(Console.WriteLine), "banco-di-prova.md");

void WriteReport(string report, string defaultName)
{
    var outIndex = Array.IndexOf(args, "--out");
    var output = outIndex >= 0 && outIndex + 1 < args.Length
        ? args[outIndex + 1]
        : Path.Combine(Path.GetDirectoryName(contentRoot)!, "previews", "milestone-b", defaultName);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, report);
    Console.WriteLine($"Wrote {output}");
}

async Task BuildDataAsync()
{
    var csv = new PokeApiCsv(downloader, Path.Combine(contentRoot, "cache", "pokeapi"));
    Console.WriteLine($"Downloading {PokeApiCsv.RequiredTables.Length} PokeAPI tables...");
    await csv.DownloadAllAsync();

    var (pokedex, typeChart, moves) = new PokedexBuilder(csv, maxId).Build();
    Write(GameDataFiles.Pokedex, pokedex, DataJsonContext.Default.PokedexFile);
    Write(GameDataFiles.TypeChart, typeChart, DataJsonContext.Default.TypeChartFile);
    Write(GameDataFiles.MoveCatalog, moves, DataJsonContext.Default.MoveCatalogFile);

    Console.WriteLine($"Pokédex: {pokedex.Species.Count} species, {pokedex.Species.Sum(s => s.Evolutions.Count)} evolution methods");
    Console.WriteLine($"Type chart: {typeChart.Types.Count} types, {typeChart.Matchups.Count} non-neutral matchups");
    Console.WriteLine($"Move catalog: {moves.Moves.Count} moves");
}

async Task DownloadAssetsAsync()
{
    Console.WriteLine("Downloading assets (cached files are skipped)...");
    await new PokeRogue.DataTool.Assets.AssetDownloader(downloader, contentRoot, maxId).RunAsync();
}

void Write<T>(string fileName, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
{
    var path = ContentPaths.Generated(contentRoot, fileName);
    File.WriteAllText(path, JsonSerializer.Serialize(value, typeInfo));
    Console.WriteLine($"Wrote {Path.GetRelativePath(contentRoot, path)}");
}

int? ReadIntOption(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : null;
}
