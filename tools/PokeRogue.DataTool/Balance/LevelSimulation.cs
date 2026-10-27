using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Block 7: every level at every difficulty, played headless by a few characters as <see cref="RosterSimulation"/> plays
/// them: an invincible pilot (how many KO, how long the boss or the legendaries of Ultra last) and one that can be hurt
/// (the minute it falls, how often it wins). The report compares the levels and the difficulties; it never changes the data.
/// </summary>
public sealed class LevelSimulation(string contentRoot)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Characters of different kinds: a starter of each type, Pikachu, and a sturdy one.</summary>
    public static readonly string[] DefaultCharacters = ["pikachu", "charmander", "squirtle", "bulbasaur"];

    public string Report(int runs, IReadOnlyList<string> characterIds, string? onlyLevel, Action<string> progress)
    {
        var roster = new RosterSimulation(contentRoot);
        var worlds = WorldLibrary.Load(contentRoot);
        var pokedex = GameDataFiles.LoadPokedex(contentRoot).Species;
        var moves = MoveLibrary.Load(contentRoot);
        var characters = characterIds.Select(id => roster.Characters[id]).ToList();
        var levels = worlds.Levels.Where(l => onlyLevel is null || l.Id == onlyLevel).ToList();
        var cells = levels.SelectMany(l => Enum.GetValues<Difficulty>().Select(d => (Level: l, Difficulty: d))).ToList();

        var results = new ConcurrentDictionary<(string, Difficulty), (List<RosterSimulation.RunResult> Strong, List<RosterSimulation.RunResult> Mortal)>();
        var done = 0;
        Parallel.ForEach(cells, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, cell =>
        {
            var timeline = worlds.Timeline(cell.Level.Id, pokedex, moves, cell.Difficulty);
            var strong = new List<RosterSimulation.RunResult>();
            var mortal = new List<RosterSimulation.RunResult>();
            foreach (var character in characters)
            {
                for (var seed = 1; seed <= runs; seed++)
                {
                    strong.Add(roster.Play(character, seed, mortal: false, level: timeline, difficulty: cell.Difficulty));
                    mortal.Add(roster.Play(character, seed, mortal: true, level: timeline, difficulty: cell.Difficulty));
                }
            }

            results[(cell.Level.Id, cell.Difficulty)] = (strong, mortal);
            progress($"{Interlocked.Increment(ref done)}/{cells.Count} {cell.Level.NameIt} {cell.Difficulty}");
        });

        var text = new StringBuilder();
        text.AppendLine("# Simulazione dei livelli (Blocco 7)");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- livelli --runs {runs}`.");
        text.AppendLine();
        text.AppendLine($"Ogni livello, a ogni difficoltà, è giocato da {string.Join(", ", characters.Select(c => c.NameIt))} ({runs} partite ciascuno), " +
            "col pilota automatico invincibile (quanti KO, quanto dura il boss, a Ultra i leggendari uno dopo l'altro) e con un pilota che subisce danni e " +
            "scappa dalla folla (il minuto in cui cade, quante volte vince). Come in `personaggi`: Ash, nessun potenziamento del negozio, mossa speciale " +
            "compresa. Il pilota non è un giocatore vero: i numeri servono a confrontare livelli e difficoltà tra loro.");
        text.AppendLine();
        text.AppendLine("| Mondo | Livello | Difficoltà | Livello finale | KO | Boss (s) | Resiste fino al minuto | Vince |");
        text.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");
        foreach (var (level, difficulty) in cells)
        {
            var (strong, mortal) = results[(level.Id, difficulty)];
            var boss = strong.Where(r => r.BossSeconds is not null).Select(r => (int)r.BossSeconds!.Value).ToList();
            var unbeaten = strong.Count(r => r.BossSeconds is null);
            text.AppendLine($"| {level.World.NameIt} | {level.NameIt} | {worlds.Difficulty(difficulty).NameIt} | " +
                $"{RosterSimulation.Median(strong.Select(r => r.Level))} | {RosterSimulation.Median(strong.Select(r => r.Kills))} | " +
                $"{(boss.Count > 0 ? RosterSimulation.Median(boss).ToString(Invariant) : "–")}{(unbeaten > 0 ? $" ({unbeaten} non battuto)" : "")} | " +
                $"{RosterSimulation.Median(mortal.Select(RosterSimulation.Survival))} | {mortal.Count(r => r.Outcome == RunOutcome.Victory)}/{mortal.Count} |");
        }

        return text.ToString();
    }
}
