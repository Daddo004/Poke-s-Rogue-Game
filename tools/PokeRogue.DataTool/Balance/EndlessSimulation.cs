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
/// Phase 9.3: the endless mode played headless on a level by a few characters, as <see cref="RosterSimulation"/> plays them:
/// the invincible pilot (does Darkrai come, how long it lasts, how often it is beaten in ten minutes) and the one that can be
/// hurt (the minute it falls). The report never changes the data.
/// </summary>
public sealed class EndlessSimulation(string contentRoot)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public string Report(int runs, IReadOnlyList<string> characterIds, string levelId, Action<string> progress)
    {
        var roster = new RosterSimulation(contentRoot);
        var worlds = WorldLibrary.Load(contentRoot);
        var pokedex = GameDataFiles.LoadPokedex(contentRoot).Species;
        var moves = MoveLibrary.Load(contentRoot);
        var endless = worlds.Endless(pokedex, moves) ?? throw new InvalidDataException("worlds.json has no \"endless\" block.");
        var timeline = worlds.Timeline(levelId, pokedex, moves);

        var results = new ConcurrentDictionary<string, (List<RosterSimulation.RunResult> Strong, List<RosterSimulation.RunResult> Mortal)>();
        var done = 0;
        Parallel.ForEach(characterIds, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, id =>
        {
            var character = roster.Characters[id];
            var strong = new List<RosterSimulation.RunResult>();
            var mortal = new List<RosterSimulation.RunResult>();
            for (var seed = 1; seed <= runs; seed++)
            {
                strong.Add(roster.Play(character, seed, mortal: false, level: timeline, endless: endless));
                mortal.Add(roster.Play(character, seed, mortal: true, level: timeline, endless: endless));
            }

            results[id] = (strong, mortal);
            progress($"{Interlocked.Increment(ref done)}/{characterIds.Count} {character.NameIt}");
        });

        var text = new StringBuilder();
        text.AppendLine("# Simulazione della modalità infinita (fase 9.3)");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- infinita --runs {runs} --livello {levelId}`.");
        text.AppendLine();
        text.AppendLine($"{worlds[levelId].NameIt} a Normale in modalità infinita, {runs} partite per personaggio. Il pilota invincibile gioca fino a dieci minuti " +
            "dopo l'arrivo del Mietitore: quante volte Darkrai arriva, quanto dura quando è battuto, quante volte resta in piedi. Il pilota che subisce danni " +
            "dice fino a che minuto si arriva. Ash, nessun potenziamento del negozio, nessuna Lastra: il pilota non è un giocatore vero.");
        text.AppendLine();
        text.AppendLine("| Personaggio | Livello finale | KO | Darkrai arriva | Darkrai battuto | Durata (s) | Resiste fino al minuto |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var id in characterIds)
        {
            var (strong, mortal) = results[id];
            var beaten = strong.Where(r => r.ReaperSeconds is not null).Select(r => (int)r.ReaperSeconds!.Value).ToList();
            text.AppendLine($"| {roster.Characters[id].NameIt} | {RosterSimulation.Median(strong.Select(r => r.Level))} | {RosterSimulation.Median(strong.Select(r => r.Kills))} | " +
                $"{strong.Count(r => r.ReaperCame)}/{runs} | {beaten.Count}/{runs} | {(beaten.Count > 0 ? RosterSimulation.Median(beaten).ToString(Invariant) : "–")} | " +
                $"{RosterSimulation.Median(mortal.Select(RosterSimulation.Survival))} |");
        }

        return text.ToString();
    }
}
