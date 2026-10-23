using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 1.6 preview: the real <see cref="GameplayScene"/> with a mid-run build (Lv 12, 05:42, 312 KO) and a scripted beat:
/// partita (HUD), pausa, sconfitta, vittoria or risultati. With <c>--evoluta</c> (phase 2.6) Tuonoshock has already
/// evolved into Tuono, to show the gold slot and "EVO" on the results.
/// </summary>
public sealed class HudPreviewScene : Scene
{
    private readonly GameplayScene _game;
    private readonly string _variant;
    private readonly bool _evolved;
    private int _tick;

    public HudPreviewScene(GameServices services, string? variant, bool evolved = false) : base(services)
    {
        _variant = variant ?? "partita";
        _evolved = evolved;
        _game = new GameplayScene(services, initialSpawn: 70, autopilot: true, setup: PrepareBuild);
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        _tick++;
        switch (_variant)
        {
            // Retries while a level-up window is open (pause is refused then).
            case "pausa" when _tick >= 150 && !_game.IsPauseOpen:
                _game.OpenPause();
                break;

            // The banner waits for a key press, as it would for a player.
            case "sconfitta" when _tick == 60:
                _game.Autopilot = false;
                _game.ForceEnd(RunOutcome.Defeat);
                break;
            case "vittoria" when _tick == 60:
                _game.Autopilot = false;
                _game.ForceEnd(RunOutcome.Victory);
                break;

            // Autopilot skips the banner straight to the results.
            case "risultati" when _tick == 30:
                _game.ForceEnd(RunOutcome.Victory);
                break;
        }

        _game.Update();
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    /// <summary>A believable run at 05:42: six moves (the most the tables must fit), four held items, 312 KO across six species.</summary>
    private void PrepareBuild(RunState run)
    {
        var items = Services.Items.Value;
        var player = run.Player;
        run.AdvanceTime(342 * RunClock.TicksPerSecond);

        var thundershock = player.Moves[0];
        thundershock.Level = 4;
        thundershock.DamageDealt = 3850;
        if (_evolved)
            player.Moves[0] = new MoveInstance(Services.Moves.Value["thunder"]) { DamageDealt = 5920 };

        (string Id, int Level, int Second, float Damage)[] learned =
            [("ember", 3, 50, 2140), ("razor-leaf", 2, 100, 1620), ("mud-bomb", 2, 150, 1380), ("aurora-beam", 1, 230, 760), ("earthquake", 1, 290, 410)];
        foreach (var (id, level, second, damage) in learned)
            player.Moves.Add(new MoveInstance(run.MovePool.Single(m => m.Id == id), level) { AcquiredTick = second * RunClock.TicksPerSecond, DamageDealt = damage });

        player.Items.Add(new ItemInstance(items["carbos"], 2) { AcquiredTick = 40 * RunClock.TicksPerSecond });
        player.Items.Add(new ItemInstance(items["leftovers"]) { AcquiredTick = 95 * RunClock.TicksPerSecond });
        player.Items.Add(new ItemInstance(items["magnet"]) { AcquiredTick = 160 * RunClock.TicksPerSecond });
        player.Items.Add(new ItemInstance(items["lucky-egg"]) { AcquiredTick = 250 * RunClock.TicksPerSecond });
        player.RecalculateStats();

        player.AddXp(5 + 15 + 25 + 35 + 45 + 55 + 65 + 75 + 85 + 95 + 105 + 60);
        run.AddCoins(128);
        player.Hp -= 30;
        player.Heal(30);
        run.Stats.RecordDamageTaken(245);

        (int Species, int Count)[] kills = [(19, 120), (16, 80), (10, 54), (13, 38), (21, 12), (23, 8)];
        foreach (var (species, count) in kills)
        {
            for (var i = 0; i < count; i++)
                run.Stats.RecordKill(species);
        }
    }
}
