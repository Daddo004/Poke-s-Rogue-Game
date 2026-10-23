using System.Globalization;
using System.Numerics;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Phase 2.8 test bench: each move alone on Pikachu, at levels 1, 4 and 8 (an evolution at its only level), against the
/// same steady waves of the prototype species closing in from every side while Pikachu walks a slow circle.
/// DPS is the damage dealt per second: no overkill, and status damage counts for the move that inflicted it.
/// </summary>
public sealed class MoveBench(string contentRoot)
{
    private const int Seconds = 45;
    private const int Seeds = 2;

    /// <summary>Enemies on the field at every moment: each one knocked out is replaced at once, so no move can run out of targets.</summary>
    private const int Crowd = 40;

    private const float HpMultiplier = 5f;

    private static readonly int[] Levels = [1, 4, 8];

    /// <summary>The prototype spawn table (Caterpie, Weedle, Pidgey, Rattata, Spearow, Ekans) with its weights.</summary>
    private static readonly (int Species, int Weight)[] Horde = [(10, 3), (13, 3), (16, 4), (19, 5), (21, 2), (23, 2)];

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly PokedexFile _pokedex = GameDataFiles.LoadPokedex(contentRoot);

    /// <summary>
    /// Every matchup neutral: the prototype horde is mostly Bug, Flying, Poison and Normal, and with the real chart
    /// Rock and Ground would look strong only because of it. Type advantages are a reason to pick a move for a level.
    /// </summary>
    private readonly TypeChart _typeChart = new([]);
    private readonly MoveLibrary _moves = MoveLibrary.Load(contentRoot);
    private readonly StatusLibrary _statuses = StatusLibrary.Load(contentRoot);

    public MoveLibrary Moves => _moves;

    /// <summary>
    /// Average DPS and KO per minute of <paramref name="move"/> at <paramref name="level"/> over the seeds: against the
    /// crowd, or with <paramref name="single"/> against one very tough enemy, the stand-in for a boss (Block 3).
    /// </summary>
    public (float Dps, float KoPerMinute) Measure(MoveDefinition move, int level, bool single = false)
    {
        float damage = 0f, kills = 0f;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var run = new RunState(_pokedex.Species, _typeChart, 25, [new SpawnEntry(19, 1)], new Vector2(640, 360), seed)
            {
                StatusRules = _statuses,
                SkipsLeft = int.MaxValue,
            };
            run.Spawner.Enabled = false;
            run.Player.Invincible = true;
            var instance = new MoveInstance(move, level);
            run.Player.Moves.Add(instance);

            var random = new Random(seed * 7919 + 13);
            var totalWeight = Horde.Sum(h => h.Weight);
            for (var tick = 0; tick < Seconds * RunClock.TicksPerSecond; tick++)
            {
                if (single && tick == 0)
                    run.Enemies.Spawn(_pokedex.Species[18], run.Player.Position + new Vector2(120f, 0f), hpMultiplier: 1000f);

                for (var k = run.Enemies.AliveCount; k < Crowd && !single; k++)
                {
                    var roll = random.Next(totalWeight);
                    var species = Horde.First(h => (roll -= h.Weight) < 0).Species;
                    var angle = random.NextSingle() * MathF.Tau;
                    var at = run.Player.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (40f + random.NextSingle() * 180f);
                    run.Enemies.Spawn(_pokedex.Species[species - 1], at, HpMultiplier);
                }

                // Gems level Pikachu up: the bench gives every choice up, so the move stays at its level.
                while (run.IsPaused)
                    run.SkipLevelUp();

                var t = tick / (float)RunClock.TicksPerSecond;
                run.Tick(new PlayerInput(new Vector2(MathF.Cos(t * 0.8f), MathF.Sin(t * 0.8f)) * 0.5f));
            }

            damage += instance.DamageDealt;
            kills += run.Stats.Kills;
        }

        return (damage / Seeds / Seconds, kills / Seeds / Seconds * 60f);
    }

    public string Report(Action<string> progress)
    {
        var baseMoves = _moves.All.Where(m => !m.IsEvolution).ToList();
        var rows = new List<(MoveDefinition Move, (float Dps, float Ko)[] ByLevel, float Single)>();
        foreach (var move in baseMoves)
        {
            rows.Add((move, Levels.Select(level => Measure(move, level)).ToArray(), Measure(move, Levels[^1], single: true).Dps));
            progress($"{move.NameIt}: {string.Join("  ", rows[^1].ByLevel.Select(r => r.Dps.ToString("0.0", Invariant)))}  singolo {rows[^1].Single.ToString("0.0", Invariant)}");
        }

        var evolutions = _moves.All.Where(m => m.IsEvolution).Select(e => (Move: e, Result: Measure(e, 1), Single: Measure(e, 1, single: true).Dps)).ToList();
        foreach (var (move, result, single) in evolutions)
            progress($"{move.NameIt}: {result.Dps.ToString("0.0", Invariant)}  singolo {single.ToString("0.0", Invariant)}");

        var attacks = rows.Where(r => !IsSupport(r.Move)).ToList();
        var medians = Enumerable.Range(0, Levels.Length).Select(i => Median(attacks.Select(r => r.ByLevel[i].Dps))).ToArray();
        var singleMedian = Median(attacks.Select(r => r.Single));

        var text = new StringBuilder();
        text.AppendLine("# Banco di prova delle mosse (fase 2.8)");
        text.AppendLine();
        text.AppendLine($"Generato il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- banco`.");
        text.AppendLine();
        text.AppendLine($"Ogni mossa da sola su Pikachu, ai livelli {string.Join(", ", Levels)} (le evolute al loro unico livello), per {Seconds} secondi, " +
            $"contro una folla costante di {Crowd} nemici delle sei specie del prototipo (PS ×{HpMultiplier.ToString(Invariant)}): ogni KO viene sostituito subito da un nemico nuovo " +
            "a 40–220 px, così nessuna mossa resta senza bersagli. I nemici inseguono Pikachu, che cammina in un cerchio lento. " +
            $"DPS = danni al secondo (senza danni in eccesso; i danni dello stato contano per la mossa che l'ha inflitto), media di {Seeds} partite. " +
            "Tutti i tipi sono neutri fra loro, così conta la mossa e non la tabella dei tipi; le mosse Elettro hanno ancora lo STAB ×1,5 di Pikachu.");
        text.AppendLine();
        text.AppendLine($"Una seconda misura mette la mossa al livello 8 contro un solo nemico molto resistente (PS ×1000), che fa le veci di un boss del Blocco 3: " +
            "lì contano le mosse a bersaglio singolo.");
        text.AppendLine();
        text.AppendLine($"Mediana delle mosse d'attacco contro la folla: {Dps(medians[0])} al livello 1, {Dps(medians[1])} al livello 4, {Dps(medians[2])} al livello 8; " +
            $"contro il bersaglio singolo, al livello 8: {Dps(singleMedian)}. " +
            "Le mosse di supporto (Tuononda, Ipnosi: danni minimi e stato frequente, scelta della fase 2.4) sono escluse dalle mediane.");
        text.AppendLine();
        text.AppendLine("| Mossa | Tipo | Archetipo | Folla Lv 1 | Folla Lv 4 | Folla Lv 8 | KO/min Lv 8 | Lv 8 / mediana | Singolo Lv 8 | Singolo / mediana |");
        text.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (move, byLevel, single) in rows.OrderByDescending(r => r.ByLevel[^1].Dps))
        {
            var name = IsSupport(move) ? $"{move.NameIt} (supporto)" : move.NameIt;
            text.AppendLine($"| {name} | {TypeName(move.Type)} | {ArchetypeName(move)} | {Dps(byLevel[0].Dps)} | {Dps(byLevel[1].Dps)} | {Dps(byLevel[2].Dps)} | " +
                $"{byLevel[2].Ko.ToString("0", Invariant)} | {Ratio(byLevel[2].Dps / medians[^1])} | {Dps(single)} | {Ratio(single / singleMedian)} |");
        }

        text.AppendLine();
        text.AppendLine("| Evoluzione | Da | Folla | × la mossa base al Lv 8 | Singolo | × la mossa base al Lv 8 |");
        text.AppendLine("|---|---|---:|---:|---:|---:|");
        foreach (var (move, result, single) in evolutions.OrderByDescending(e => e.Result.Dps))
        {
            var from = rows.Single(r => r.Move.Id == move.EvolvesFrom);
            text.AppendLine($"| {move.NameIt} | {from.Move.NameIt} | {Dps(result.Dps)} | {Ratio(result.Dps / from.ByLevel[^1].Dps)} | {Dps(single)} | {Ratio(single / from.Single)} |");
        }

        return text.ToString();
    }

    /// <summary>Status moves turned into weak damaging moves with frequent status in phase 2.4 (the user's choice).</summary>
    public static bool IsSupport(MoveDefinition move) => move.Id is "thunder-wave" or "hypnosis";

    private static string Dps(float value) => value.ToString("0.0", Invariant).Replace('.', ',');

    private static string Ratio(float value) => "×" + value.ToString("0.00", Invariant).Replace('.', ',');

    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0f : sorted[sorted.Count / 2];
    }

    private static string ArchetypeName(MoveDefinition move) => move.Archetype switch
    {
        MoveArchetype.Projectile => "Proiettile",
        MoveArchetype.Homing => "A ricerca",
        MoveArchetype.Boomerang => "Boomerang",
        MoveArchetype.Orbit => "Orbita",
        MoveArchetype.Aura => "Aura",
        MoveArchetype.Slash => "Fendente",
        MoveArchetype.Beam => "Raggio",
        MoveArchetype.Strike => "Dall'alto",
        MoveArchetype.Arc => "Catena",
        MoveArchetype.Zone => move.SpawnAtPlayer ? "Scia" : "Zona",
        MoveArchetype.Ring => "Anello",
        _ => move.Archetype.ToString(),
    };

    private static string TypeName(PokemonType type) => type switch
    {
        PokemonType.Normal => "Normale",
        PokemonType.Fire => "Fuoco",
        PokemonType.Water => "Acqua",
        PokemonType.Grass => "Erba",
        PokemonType.Electric => "Elettro",
        PokemonType.Ice => "Ghiaccio",
        PokemonType.Fighting => "Lotta",
        PokemonType.Poison => "Veleno",
        PokemonType.Ground => "Terra",
        PokemonType.Flying => "Volante",
        PokemonType.Psychic => "Psico",
        PokemonType.Bug => "Coleottero",
        PokemonType.Rock => "Roccia",
        PokemonType.Ghost => "Spettro",
        PokemonType.Dragon => "Drago",
        PokemonType.Dark => "Buio",
        PokemonType.Steel => "Acciaio",
        _ => type.ToString(),
    };
}
