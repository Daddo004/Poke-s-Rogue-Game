using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 4.3 preview of the abilities:
/// - pannello: the HUD's ability strip for several characters, in their different states;
/// - teletrasporto: Abra on autopilot teleports as soon as the key is ready;
/// - trasformazione: Ditto copies the Snorlax walking up to it, then turns back;
/// - muta: Dratini's shed skin blocks a hit, among Rattata that can hurt it.
/// </summary>
public sealed class AbilityPreviewScene : Scene
{
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Dim = new(160, 152, 190);

    private readonly string _variant;
    private readonly GameplayScene? _game;
    private readonly Hud _hud;
    private readonly List<(string Label, RunState Run)> _strips = [];
    private bool _started;

    public AbilityPreviewScene(GameServices services, string variant) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        _hud = new Hud(services);
        var character = _variant switch
        {
            "teletrasporto" => "abra",
            "trasformazione" => "ditto",
            "muta" => "dratini",
            _ => null,
        };
        if (character is not null)
            _game = new GameplayScene(services, character, autopilot: true, setup: Prepare);
    }

    public override IReadOnlyList<string> DebugLines => _game?.DebugLines ?? [];

    public override bool HandlesEscape => _game?.HandlesEscape ?? false;

    public override void Enter()
    {
        if (_game is not null)
        {
            _game.Enter();
            return;
        }

        // The strip in its states: active ready and recharging, Muta charging and ready, Vigore, stacks, a plain passive.
        Add("Abra: pronta", "abra", _ => { });
        Add("Abra: in ricarica", "abra", r => r.Ability.CooldownTicks = RunClock.Seconds(5f));
        Add("Ditto: trasformato", "ditto", r => r.Ability.CooldownTicks = RunClock.Seconds(30f));
        Add("Dratini: Muta in carica", "dratini", r => r.Ability.ShieldTicks = RunClock.Seconds(6f));
        Add("Dratini: Muta pronta", "dratini", r => r.Ability.ShieldReady = true);
        Add("Shieldon: Vigore", "shieldon", _ => { });
        Add("Torchic: 6 accelerazioni", "torchic", r => r.Ability.SpeedStacks = 6);
        Add("Pikachu: passiva", "pikachu", _ => { });
    }

    private void Add(string label, string character, Action<RunState> setup)
    {
        var definition = Services.Characters[character];
        var run = new RunState(Services.Pokedex.Value.Species, Services.TypeChart.Value, definition.First.Id, [], new System.Numerics.Vector2(640, 360));
        run.UseCharacter(definition);
        setup(run);
        _strips.Add((label, run));
    }

    public override void Update()
    {
        if (_game is null)
            return;

        // Abilities that soften or block hits need a player who can be hit.
        if (!_started)
        {
            _started = true;
            _game.Run.Player.Invincible = false;
            if (_variant == "muta")
                _game.Run.Ability.ShieldReady = true;
        }

        _game.Update();
    }

    public override void Draw(SpriteBatch batch)
    {
        if (_game is not null)
        {
            _game.Draw(batch);
            return;
        }

        Services.Device.Clear(new Color(36, 52, 40));
        var body = Services.Fonts.Body(Services.Style);
        var small = Services.Fonts.Small(Services.Style);
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "ABILITÀ NELL'HUD", 320, 14, new Color(248, 208, 72), Ink);
        UiDraw.TextCentered(batch, small, "SOTTO GLI STRUMENTI: NOME, TASTO DELLE ATTIVE, BARRA DI RICARICA O DI ACCUMULO", 320, 50, Dim, Ink);
        for (var i = 0; i < _strips.Count; i++)
        {
            var (label, run) = _strips[i];
            var column = i % 2;
            var row = i / 2;
            var x = 40 + column * 300;
            var y = 84 + row * 66;
            UiDraw.Text(batch, body, label, new Vector2(x, y), Paper);
            _hud.DrawAbility(batch, run, new Rectangle(x, y + 22, 154, 14));
            var description = run.Stage!.Ability.DescriptionIt;
            var lines = UiDraw.Wrap(small, description, 250);
            for (var l = 0; l < Math.Min(2, lines.Count); l++)
                UiDraw.Text(batch, small, lines[l], new Vector2(x, y + 40 + l * 10), Dim);
        }

        batch.End();
    }

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        if (_variant == "trasformazione")
            run.SpawnEnemy(keys["snorlax"], run.Player.Position + new System.Numerics.Vector2(140, -30), hpMultiplier: 50f);

        var count = _variant == "muta" ? 10 : 8;
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count + 0.2f;
            var distance = _variant == "muta" ? 70f : 170f;
            run.SpawnEnemy(keys["rattata"], run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance, hpMultiplier: 30f);
        }
    }
}
