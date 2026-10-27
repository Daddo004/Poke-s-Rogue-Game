using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 5.1/5.2 preview of the trainers:
/// - scheda: the HUD's trainer card for several trainers, in its states (ready, recharging, running, a shield with charges);
/// - album: every trainer with portrait, type, passive and active ability, eight to a page (--pagina N);
/// - one variant per archetype (scudo, cura, onda, tempo, intangibile, tipo, fortuna, frenesia, stato, scatto): Pikachu on
///   autopilot with a trainer of that archetype, who presses Q after half a second (--allenatore picks another trainer).
/// </summary>
public sealed class TrainerPreviewScene : Scene
{
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Gold = new(248, 208, 72);

    /// <summary>The trainer shown for each archetype variant.</summary>
    private static readonly Dictionary<string, (string Trainer, TrainerActiveKind Kind)> Archetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scudo"] = ("brock", TrainerActiveKind.Shield),
        ["cura"] = ("whitney", TrainerActiveKind.Heal),
        ["onda"] = ("misty", TrainerActiveKind.Wave),
        ["tempo"] = ("sabrina", TrainerActiveKind.SlowTime),
        ["intangibile"] = ("phoebe", TrainerActiveKind.Intangible),
        ["tipo"] = ("volkner", TrainerActiveKind.TypeBoost),
        ["fortuna"] = ("roxanne", TrainerActiveKind.Fortune),
        ["frenesia"] = ("ash", TrainerActiveKind.Frenzy),
        ["stato"] = ("koga", TrainerActiveKind.StatusStorm),
        ["scatto"] = ("blue", TrainerActiveKind.Haste),
    };

    private const int PressTick = 30;

    private readonly string _variant;
    private readonly int _page;
    private readonly GameplayScene? _game;
    private readonly Hud _hud;
    private readonly List<(string Label, RunState Run)> _cards = [];
    private int _tick;

    public TrainerPreviewScene(GameServices services, string variant, string? trainerId, int page) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        _page = Math.Max(1, page);
        _hud = new Hud(services);
        if (Archetypes.TryGetValue(_variant, out var archetype))
        {
            _game = new GameplayScene(services, "pikachu", autopilot: true, setup: Prepare, trainerId: trainerId ?? archetype.Trainer)
            {
                AutoPilotTrainer = false,
            };
        }
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

        if (_variant != "scheda")
            return;

        Add("Ash: pronta", "ash", _ => { });
        Add("Ash: in ricarica", "ash", r => r.TrainerState.CooldownTicks = RunClock.Seconds(25f));
        Add("Brock: scudo, 2 colpi su 3", "brock", r =>
        {
            r.TrainerState.CooldownTicks = RunClock.Seconds(36f);
            r.TrainerState.EffectTicks = RunClock.Seconds(6f);
            r.TrainerState.ShieldCharges = 2;
        });
        Add("Sabrina: tempo rallentato", "sabrina", r =>
        {
            r.TrainerState.CooldownTicks = RunClock.Seconds(46f);
            r.TrainerState.EffectTicks = RunClock.Seconds(3f);
        });
        Add("Camilla: pronta", "cynthia", _ => { });
        Add("Koga: in ricarica", "koga", r => r.TrainerState.CooldownTicks = RunClock.Seconds(8f));
        Add("Lance: pronta", "lance", _ => { });
        Add("Tell e Pat: in ricarica", "tateandliza", r => r.TrainerState.CooldownTicks = RunClock.Seconds(32f));
    }

    private void Add(string label, string trainer, Action<RunState> setup)
    {
        var definition = Services.Characters["pikachu"];
        var run = new RunState(Services.Pokedex.Value.Species, Services.TypeChart.Value, definition.First.Id, [], new System.Numerics.Vector2(640, 360));
        run.UseCharacter(definition);
        run.UseTrainer(Services.Trainers.Value[trainer]);
        setup(run);
        _cards.Add((label, run));
    }

    public override void Update()
    {
        if (_game is null)
            return;

        // Shields and dodges need a player who can be hit; the heal needs one already hurt.
        if (_tick == 0)
        {
            _game.Run.Player.Invincible = _variant is not ("scudo" or "intangibile" or "cura");
            if (_variant == "cura")
                _game.Run.Player.Hp = _game.Run.Player.MaxHp * 0.3f;
        }

        if (++_tick == PressTick)
            _game.PressTrainer();
        _game.Update();

        // The heal is about the PS coming back, not about surviving the crowd.
        if (_variant == "cura" && _tick < PressTick)
            _game.Run.Player.Hp = MathF.Max(_game.Run.Player.Hp, _game.Run.Player.MaxHp * 0.3f);
    }

    public override void Draw(SpriteBatch batch)
    {
        if (_game is not null)
        {
            _game.Draw(batch);
            return;
        }

        Services.Device.Clear(new Color(36, 52, 40));
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        if (_variant == "album")
            DrawAlbum(batch);
        else
            DrawCards(batch);
        batch.End();
    }

    private void DrawCards(SpriteBatch batch)
    {
        var body = Services.Fonts.Body(Services.Style);
        var small = Services.Fonts.Small(Services.Style);
        UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "SCHEDA DELL'ALLENATORE", 320, 10, Gold, Ink);
        UiDraw.TextCentered(batch, small, "IN BASSO A SINISTRA: RITRATTO, TASTO Q, ABILITA' ATTIVA, STATO E BARRA DI RICARICA O DI DURATA", 320, 46, Dim, Ink);
        for (var i = 0; i < _cards.Count; i++)
        {
            var (label, run) = _cards[i];
            var x = 40 + i % 2 * 300;
            var y = 70 + i / 2 * 72;
            UiDraw.Text(batch, body, label, new Vector2(x, y), Paper);
            _hud.DrawTrainer(batch, run, new Rectangle(x, y + 22, 150, 44));
            var lines = UiDraw.Wrap(small, run.Trainer!.Active.DescriptionIt, 128);
            var numbers = Services.Fonts.Get(Services.Style.NumberFont, Services.Style.NumberFontSize / 2f);
            for (var l = 0; l < Math.Min(4, lines.Count); l++)
                UiDraw.TextWithNumbers(batch, small, numbers, lines[l], new Vector2(x + 158, y + 26 + l * 10), Dim);
        }
    }

    /// <summary>Eight trainers a page: head, name, region and role, type, passive and active ability with their texts.</summary>
    private void DrawAlbum(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var body = Services.Fonts.Body(Services.Style);
        var small = Services.Fonts.Small(Services.Style);
        var numbers = Services.Fonts.Get(Services.Style.NumberFont, Services.Style.NumberFontSize / 2f);
        var all = Services.Trainers.Value.All;
        var pages = (all.Count + 7) / 8;
        var page = Math.Min(_page, pages);
        UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "ALLENATORI", 320, 4, Gold, Ink);
        var pageText = $"PAGINA {page} DI {pages}";
        UiDraw.TextWithNumbers(batch, small, numbers, pageText, new Vector2(320 - UiDraw.Measure(small, pageText).X / 2f, 34), Dim);

        foreach (var (trainer, i) in all.Skip((page - 1) * 8).Take(8).Select((t, i) => (t, i)))
        {
            var box = new Rectangle(8 + i % 2 * 316, 46 + i / 2 * 78, 308, 74);
            UiDraw.Frame(batch, p, box, Services.Style.Theme);
            var color = Vfx.TrainerVfx.ColorOf(trainer);
            var head = new Rectangle(box.X + 5, box.Y + 5, 36, 36);
            p.Rect(batch, head, Color.Lerp(color, Ink, 0.35f));
            if (Services.TrainerPortraits.Sprite(trainer) is { } sprite)
                batch.Draw(sprite, head, Services.TrainerPortraits.Head(trainer), Color.White);

            var x = head.Right + 6;
            UiDraw.Text(batch, body, trainer.NameIt, new Vector2(x, box.Y + 1), Paper);
            UiDraw.Text(batch, small, $"{Role(trainer.Role)} - {trainer.Region.ToString().ToUpperInvariant()}", new Vector2(x, box.Y + 19), Dim);
            if (trainer.Type is { } type)
                UiDraw.Pill(batch, p, small, new Rectangle(box.Right - 54, box.Y + 6, 48, 11), TypePalette.Of(type), TypePalette.NameIt(type).ToUpperInvariant());

            UiDraw.Text(batch, small, trainer.Passive.NameIt.ToUpperInvariant(), new Vector2(x, box.Y + 31), Paper);
            UiDraw.TextWithNumbers(batch, small, numbers, trainer.Passive.DescriptionIt, new Vector2(x, box.Y + 41), Dim);
            UiDraw.Text(batch, small, $"Q  {trainer.Active.NameIt.ToUpperInvariant()}", new Vector2(box.X + 6, box.Y + 52), Gold);
            var every = $"OGNI {trainer.Active.Cooldown:0} S";
            UiDraw.TextWithNumbers(batch, small, numbers, every, new Vector2(box.Right - 10 - UiDraw.Measure(small, every).X, box.Y + 52), Dim);
            var lines = UiDraw.Wrap(small, trainer.Active.DescriptionIt, 296);
            UiDraw.TextWithNumbers(batch, small, numbers, lines.Count > 0 ? lines[0] + (lines.Count > 1 ? "..." : "") : "", new Vector2(box.X + 6, box.Y + 62), Dim);
        }
    }

    private static string Role(TrainerRole role) => role switch
    {
        TrainerRole.Base => "ALLENATORE DI BASE",
        TrainerRole.GymLeader => "CAPOPALESTRA",
        TrainerRole.EliteFour => "SUPERQUATTRO",
        TrainerRole.Champion => "CAMPIONE",
        _ => "LEGGENDA",
    };

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        var center = run.Player.Position;
        var random = new Random(4);
        switch (_variant)
        {
            case "scudo" or "intangibile":
                // A tight ring that keeps touching the player.
                Ring(run, keys["rattata"], 10, 60f, 30f);
                break;
            case "cura":
                Ring(run, keys["rattata"], 8, 170f, 30f);
                break;
            case "onda" or "stato":
                for (var i = 0; i < 40; i++)
                {
                    var angle = random.NextSingle() * MathF.Tau;
                    var distance = 50f + random.NextSingle() * 200f;
                    run.SpawnEnemy(keys[i % 3 == 0 ? "pidgey" : "rattata"], center + new System.Numerics.Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance * 0.6f), hpMultiplier: 6f);
                }

                break;
            case "tempo":
                Ring(run, keys["rattata"], 16, 200f, 30f);
                Ring(run, keys["beedrill"], 4, 150f, 30f);
                break;
            case "fortuna":
                // Some levels up front, so the gems flying in do not stop the preview with level-ups.
                run.Player.AddXp(6000f);
                for (var i = 0; i < 60; i++)
                    run.Pickups.Gems.Spawn(center + new System.Numerics.Vector2(random.NextSingle() * 600f - 300f, random.NextSingle() * 320f - 160f), 1 + random.Next(4));
                Ring(run, keys["rattata"], 6, 180f, 30f);
                break;
            case "scatto":
                for (var i = 0; i < 12; i++)
                    run.Pickups.Gems.Spawn(center + new System.Numerics.Vector2(200f + i * 40f, (i % 2) * 30f - 15f), 2);
                Ring(run, keys["rattata"], 6, 160f, 30f);
                break;
            default:
                Ring(run, keys["rattata"], 14, 150f, 30f);
                break;
        }
    }

    private static void Ring(RunState run, int speciesId, int count, float distance, float hp)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count + 0.2f;
            run.SpawnEnemy(speciesId, run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance, hpMultiplier: hp);
        }
    }
}
