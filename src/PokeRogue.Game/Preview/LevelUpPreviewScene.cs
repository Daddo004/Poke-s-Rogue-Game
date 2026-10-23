using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 1.5 preview. Default: Pikachu knocks out weak dummies, walks to their gems, levels up
/// (burst, then the window) and picks an option on a script. With <c>--window</c>: a level-5 window opens at once
/// with one upgrade of each kind, for a still capture. <c>--variant lista|carte</c> picks the layout;
/// <c>--variant azioni</c> (phase 2.5) opens the cards at once and plays Rilancio, Bandisci and Salta on a script.
/// <c>--variant evoluzione</c> (phase 2.6): Tuonoshock at its last level with Calamita held; the gold card is taken,
/// the move evolves into Tuono and strikes the dummies, with the whole HUD on screen.
/// </summary>
public sealed class LevelUpPreviewScene(GameServices services, string? variant, bool windowOnly) : Scene(services)
{
    private static readonly int[] Dummies = [10, 13, 19, 10, 13, 19, 16];

    private readonly bool _actions = variant == "azioni";
    private readonly bool _evolution = variant == "evoluzione";
    private readonly RunRenderer _renderer = new(services, FloorStyle.MeadowDecorated);
    private readonly LevelUpWindow _window = new(services) { Layout = variant is "carte" or "azioni" or "evoluzione" ? LevelUpLayout.Cards : LevelUpLayout.List };
    private readonly XpBar _xpBar = new(services);
    private readonly Hud _hud = new(services);
    private RunState _run = null!;
    private LevelUpFlow _flow = null!;

    public override void Enter()
    {
        var view = new System.Numerics.Vector2(Services.Screen.Width, Services.Screen.Height);
        _run = new RunState(Services.Pokedex.Value.Species, Services.TypeChart.Value, 25, [new SpawnEntry(19, 1)], view, seed: 7)
        {
            MovePool = Services.Moves.Value.Enabled,
            ItemPool = Services.Items.Value.Enabled,
            StatusRules = _evolution ? Services.Statuses.Value : null,

            // Every evolution, even one still waiting for approval.
            Evolutions = _evolution ? Services.Moves.Value.All.Where(m => m.IsEvolution).Select(m => m with { Enabled = true }).ToList() : [],
        };
        _run.Spawner.Enabled = false;
        _run.Player.Invincible = true;
        _run.Player.Moves.Add(new MoveInstance(Services.Moves.Value[GameplayScene.StartingMoveId]));
        _renderer.Reset(_run);
        _flow = new LevelUpFlow(_run, _window);

        // A few gems of every size lying around, so all three colours are visible.
        (int X, int Y, int Value)[] scattered = [(-120, -40, 1), (-140, -20, 3), (130, 60, 1), (150, 40, 12), (-60, 90, 3), (60, -95, 1), (-170, 70, 1)];
        foreach (var (x, y, value) in scattered)
            _run.Pickups.Gems.Spawn(new System.Numerics.Vector2(x, y), value);

        if (_evolution)
            OfferEvolution();
        else if (windowOnly || _actions)
            OfferSampleChoices();
        else
            SpawnDummies();
    }

    public override void Update()
    {
        if (_flow.Active)
        {
            ScriptWindow();
            _flow.Update(input: null);
            _renderer.Update(_run, paused: true);
        }
        else
        {
            _run.Tick(new PlayerInput(_evolution ? default : Autopilot()));
            _renderer.Update(_run);
            if (_run.Enemies.AliveCount == 0 && !windowOnly)
                SpawnDummies();
        }

        _xpBar.Update(_run.Player);
        _hud.Update(_run);
    }

    public override void Draw(SpriteBatch batch)
    {
        _renderer.Draw(batch, _run);
        if (_evolution)
            _hud.Draw(batch, _run);

        batch.Begin(samplerState: SamplerState.PointClamp);
        if (!_evolution)
            _xpBar.Draw(batch, _run.Player);
        var layout = _window.Layout == LevelUpLayout.Cards ? "B carte" : "A lista";
        var caption = _evolution ? "ANTEPRIMA 2.6  -  EVOLUZIONE DI UNA MOSSA"
            : _actions ? "ANTEPRIMA  -  RILANCIA, SALTA E BANDISCI"
            : $"ANTEPRIMA 1.5  -  GEMME ESP E LEVEL-UP ({layout})";
        UiDraw.TextOutlined(batch, Services.Fonts.Small(Services.Style), caption,
            new Microsoft.Xna.Framework.Vector2(8, 344), new Microsoft.Xna.Framework.Color(244, 240, 228), new Microsoft.Xna.Framework.Color(16, 12, 28));
        batch.End();

        _window.Draw(batch);
    }

    /// <summary>Scripted player: move the cursor twice, then confirm.</summary>
    private void ScriptWindow()
    {
        if (!_window.IsOpen)
            return;

        if (_actions)
        {
            ScriptActions();
            return;
        }

        // Evolution: a long look at the gold card (the cursor starts on it), then take it.
        if (_evolution)
        {
            if (_window.Age == LevelUpWindow.OpenTicks + 110)
                _window.Confirm();
            return;
        }

        if (windowOnly)
            return;

        switch (_window.Age)
        {
            case LevelUpWindow.OpenTicks + 30:
            case LevelUpWindow.OpenTicks + 55:
                _window.MoveCursor(1);
                break;
            case LevelUpWindow.OpenTicks + 85:
                _window.Confirm();
                break;
        }
    }

    /// <summary>Phase 2.5 script: Rilancio, then Bandisci on the middle card, then Salta.</summary>
    private void ScriptActions()
    {
        switch (_window.Age)
        {
            case LevelUpWindow.OpenTicks + 40:
                _window.RequestReroll();
                break;
            case LevelUpWindow.OpenTicks + 90:
                _window.ToggleBanish();
                break;
            case LevelUpWindow.OpenTicks + 110:
                _window.MoveCursor(1);
                break;
            case LevelUpWindow.OpenTicks + 140:
                _window.Confirm();
                break;
            case LevelUpWindow.OpenTicks + 210:
                _window.RequestSkip();
                break;
        }
    }

    /// <summary>Walk to the nearest gem within reach, otherwise stand still and let Thundershock work.</summary>
    private System.Numerics.Vector2 Autopilot()
    {
        var player = _run.Player.Position;
        var best = System.Numerics.Vector2.Zero;
        var bestDistance = 110f * 110f;
        foreach (ref readonly var gem in _run.Pickups.Gems.Items)
        {
            if (!gem.Active || gem.Magnetized)
                continue;

            var d = System.Numerics.Vector2.DistanceSquared(gem.Position, player);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = gem.Position - player;
            }
        }

        return best == System.Numerics.Vector2.Zero ? best : System.Numerics.Vector2.Normalize(best) * 0.8f;
    }

    private void SpawnDummies()
    {
        for (var i = 0; i < Dummies.Length; i++)
        {
            var angle = i * MathF.Tau / Dummies.Length;
            var position = _run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle) * 85f, MathF.Sin(angle) * 60f);
            var index = _run.Enemies.Spawn(Services.Pokedex.Value.Species[Dummies[i] - 1], position, hpMultiplier: 0.5f);
            _run.Enemies[index].Speed = 12f;
        }
    }

    /// <summary>A level-16 player with Tuonoshock maxed, Braciere, Calamita and PS-Su: the next level-up offers Tuono first.</summary>
    private void OfferEvolution()
    {
        var player = _run.Player;
        var items = Services.Items.Value;
        while (player.Level < 16)
            player.AddXp(player.XpToNext);
        player.Moves[0].Level = MoveLibrary.MaxLevels;
        player.Moves.Add(new MoveInstance(Services.Moves.Value["ember"], 4));
        player.Items.Add(new ItemInstance(items["magnet"], 2));
        player.Items.Add(new ItemInstance(items["hp-up"], 3));
        player.RecalculateStats();
        SpawnDummies();
        foreach (var index in Enumerable.Range(0, _run.Enemies.HighWater))
            _run.Enemies[index].Speed = 0f;

        _run.OfferLevelUp(LevelUp.Generate(_run, RunState.LevelUpChoiceCount));
    }

    /// <summary>A level-5 player holding Thundershock Lv 2, PS-Su and Avanzi, offered one option of each kind.</summary>
    private void OfferSampleChoices()
    {
        var player = _run.Player;
        var items = Services.Items.Value;
        var (magnet, leftovers) = (items["magnet"], items["leftovers"]);
        player.AddXp(5 + 15 + 25 + 35);
        player.Moves[0].Level = 2;
        player.Items.Add(new ItemInstance(items["hp-up"]));
        player.Items.Add(new ItemInstance(leftovers));
        player.RecalculateStats();

        var thundershock = player.Moves[0];
        _run.OfferLevelUp(
        [
            new LevelUpOption(LevelUpKind.MoveUpgrade, "thundershock", thundershock.Definition.NameIt, 2, 3,
                LevelUp.DescribeUpgrade(thundershock.Definition.Levels[1], thundershock.Definition.Levels[2]), "thundershock", thundershock.Definition.Type),
            new LevelUpOption(LevelUpKind.NewItem, magnet.Id, magnet.NameIt, 0, 1, $"{magnet.DescriptionIt} {LevelUp.Describe(magnet.PerLevel)}", magnet.IconKey),
            new LevelUpOption(LevelUpKind.ItemUpgrade, leftovers.Id, leftovers.NameIt, 1, 2, LevelUp.Describe(leftovers.PerLevel), leftovers.IconKey),
        ]);
    }
}
