using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// In-run HUD, layout A approved in 0.6: XP bar and level, move and item slots top-left, timer top-centre,
/// coins and KO top-right; the ability and special-move strips under the items (phases 4.3 and 4.4); the trainer's card
/// bottom-left (phase 5.2).
/// </summary>
public sealed class Hud
{
    public const int Slots = 6;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);

    /// <summary>The endless mode's violet (phase 9.3).</summary>
    public static readonly Color Endless = new(170, 110, 240);

    /// <summary>How long a slot flashes white after its move evolves.</summary>
    private const int EvolveFlashTicks = 40;

    private readonly GameServices _services;
    private readonly Texture2D _skull;
    private readonly Texture2D _star;
    private readonly int[] _evolveFlash = new int[Slots];

    public Hud(GameServices services)
    {
        _services = services;
        XpBar = new XpBar(services);
        _skull = PixelArt.FromPattern(services.Device,
        [
            ".oooo.",
            "owwwwo",
            "owowow",
            "owwwwo",
            ".owwo.",
            ".o.o..",
        ], new Dictionary<char, Color> { ['o'] = Ink, ['w'] = Paper });
        _star = PixelArt.FromPatternOutlined(services.Device, ["..y..", ".yyy.", "yyyyy", ".yyy.", ".y.y."], new Dictionary<char, Color> { ['y'] = Gold }, Ink);
        _skyIcons = SkyIcons(services.Device);
    }

    /// <summary>The weather's pictograms beside the timer (phase 7.2): sun, rain, sand, hail, and the moon at night.</summary>
    private readonly Dictionary<string, Texture2D> _skyIcons;

    private static Dictionary<string, Texture2D> SkyIcons(GraphicsDevice device)
    {
        var palette = new Dictionary<char, Color>
        {
            ['y'] = new(255, 214, 72), ['o'] = new(240, 150, 40), ['w'] = new(236, 240, 248), ['g'] = new(160, 170, 190), ['b'] = new(110, 170, 255),
            ['s'] = new(222, 186, 120), ['d'] = new(170, 130, 70), ['m'] = new(240, 232, 180),
        };
        Texture2D Icon(string[] rows) => PixelArt.FromPatternOutlined(device, rows, palette, Ink);
        return new Dictionary<string, Texture2D>
        {
            ["Sun"] = Icon(["y..y..y", ".yyyyy.", ".yoooy.", "yyoooyy", ".yoooy.", ".yyyyy.", "y..y..y"]),
            ["Rain"] = Icon([".wwww..", "wwwwww.", "gggggg.", ".......", "b.b.b..", ".b.b.b.", "b.b.b.."]),
            ["Sandstorm"] = Icon(["sssss..", ".......", "..ssssd", ".......", "ssssd..", ".......", "..sssss"]),
            ["Hail"] = Icon([".wwww..", "wwwwww.", "gggggg.", ".......", "w..w..w", ".......", ".w..w.."]),
            ["Night"] = Icon(["..mmm..", ".mm....", "mm.....", "mm.....", "mm.....", ".mm..m.", "..mmm.."]),
        };
    }

    private readonly Queue<(string Text, Color Color)> _announcements = new();

    /// <summary>A notice under the timer, after the one showing (the level's name, the weather, a find).</summary>
    public void Announce(string text, Color color)
    {
        if (_bannerAge < 0)
            StartBanner(text, color);
        else
            _announcements.Enqueue((text, color));
    }

    private void StartBanner(string text, Color color)
    {
        _banner = text;
        _bannerColor = color;
        _bannerAge = 0;
    }

    /// <summary>The level's name (and its difficulty) as the run begins (phase 7.1).</summary>
    public void ShowLevel(string name, string? difficulty)
    {
        _announcements.Clear();
        _bannerAge = -1;
        Announce(difficulty is null ? name.ToUpperInvariant() : $"{name.ToUpperInvariant()}  ({difficulty.ToUpperInvariant()})", Paper);
    }

    /// <summary>What a change of weather or of the time of day says.</summary>
    public static (string Text, Color Color)? SkyNotice(RunEvent e) => e.Type switch
    {
        RunEventType.WeatherChanged => (Core.Run.Worlds.WeatherKind)(int)e.Value switch
        {
            Core.Run.Worlds.WeatherKind.Rain => ("INIZIA A PIOVERE!", new Color(130, 180, 255)),
            Core.Run.Worlds.WeatherKind.Sun => ("IL SOLE SPLENDE FORTE!", new Color(255, 190, 80)),
            Core.Run.Worlds.WeatherKind.Sandstorm => ("SI ALZA UNA TEMPESTA DI SABBIA!", new Color(230, 196, 130)),
            Core.Run.Worlds.WeatherKind.Hail => ("INIZIA A GRANDINARE!", new Color(200, 226, 255)),
            _ => ("IL CIELO SI RASSERENA", Paper),
        },
        RunEventType.NightChanged => e.Value > 0 ? ("CALA LA NOTTE", new Color(170, 160, 240)) : ("SORGE IL SOLE", new Color(255, 200, 140)),
        _ => null,
    };

    public XpBar XpBar { get; }

    public void Update(RunState run)
    {
        XpBar.Update(run.Player);
        for (var i = 0; i < Slots; i++)
        {
            if (_evolveFlash[i] > 0)
                _evolveFlash[i]--;
        }

        if (_bannerAge >= 0 && ++_bannerAge >= BannerTicks)
            _bannerAge = -1;
        if (_bannerAge < 0 && _announcements.TryDequeue(out var next))
            StartBanner(next.Text, next.Color);
        UpdateMissions();

        if (_trainerCutIn >= 0 && ++_trainerCutIn >= CutInTicks)
            _trainerCutIn = -1;
        if (_trainerFlash > 0)
            _trainerFlash--;
        if (_plateFlash > 0)
            _plateFlash--;

        foreach (var e in run.Events)
        {
            // A Plate found on the map or taken from a chest (phase 9.1), and Kecleon's visits (phase 9.2).
            if (e.Type == RunEventType.PlateEffect && e.Value < 0f && _services.Plates.Value.Find(e.MoveId) is { } plate)
            {
                _plateFlash = FlashTicks;
                Announce(e.Value < -1f ? $"{plate.NameIt.ToUpperInvariant()}: {TypePalette.NameIt(plate.Type).ToUpperInvariant()} PIÙ FORTE!"
                    : $"HAI TROVATO {plate.NameIt.ToUpperInvariant()}!", Color.Lerp(TypePalette.Of(plate.Type), Color.White, 0.35f));
            }

            // The endless mode (phase 9.3).
            if (e.Type == RunEventType.EndlessContinues)
                Announce("MODALITÀ INFINITA: SI CONTINUA!", Endless);
            if (e.Type == RunEventType.ReaperWarning)
                Announce("IL MIETITORE STA ARRIVANDO...", Endless);

            if (e.Type == RunEventType.MerchantArrived)
                Announce("È ARRIVATO KECLEON IL MERCANTE!", Vfx.MerchantVfx.Green);
            if (e.Type == RunEventType.MerchantLeft)
                Announce("KECLEON SE N'È ANDATO", Dim);

            if (e.Type == RunEventType.TrainerAbility)
            {
                switch ((Core.Run.Trainers.TrainerEffect)(int)e.Value)
                {
                    case Core.Run.Trainers.TrainerEffect.Used:
                        _trainerCutIn = 0;
                        _trainerFlash = FlashTicks;
                        _trainerFlashColor = Color.White;
                        break;
                    case Core.Run.Trainers.TrainerEffect.Ready:
                        _trainerFlash = FlashTicks;
                        _trainerFlashColor = Gold;
                        break;
                }
            }

            if (e.Type == RunEventType.MoveEvolved && (int)e.Value is >= 0 and < Slots)
                _evolveFlash[(int)e.Value] = EvolveFlashTicks;
            if (e.Type == RunEventType.DominantAppeared && _services.Species(e.SpeciesId) is { } species)
            {
                _banner = $"{species.NameIt.ToUpperInvariant()} DOMINANTE IN ARRIVO!";
                _bannerColor = Vfx.EnemyVfx.AuraRed;
                _bannerAge = 0;
            }

            if (e.Type == RunEventType.ShinyAppeared && _services.Species(e.SpeciesId) is { } shiny && _bannerAge < 0)
            {
                _banner = $"UN {shiny.NameIt.ToUpperInvariant()} CROMATICO!";
                _bannerColor = Vfx.EnemyVfx.ShinyGold;
                _bannerAge = 0;
            }

            if (SkyNotice(e) is { } sky)
                Announce(sky.Text, sky.Color);
            if (e.Type == RunEventType.FindCollected && e.MoveId is { } find)
            {
                Announce(find == "aerodactyl" ? "HAI TROVATO L'AMBRA ANTICA!" : FindArt.IsEgg(find) ? "HAI TROVATO UN UOVO!" : "HAI TROVATO UN FOSSILE!",
                    Vfx.EnemyVfx.ShinyGold);
            }
        }
    }

    /// <summary>How long the arrival of a Dominante is announced under the timer.</summary>
    private const int BannerTicks = 180;

    private string _banner = "";
    private Color _bannerColor = Vfx.EnemyVfx.AuraRed;
    private int _bannerAge = -1;

    /// <summary>What the boss bar shows: it drains slowly after each hit, behind the real PS.</summary>
    private float _bossTrail = 1f;

    /// <summary>
    /// The boss' PS (phase 3.4): a wide red bar under the timer with its name, notches where the next phases begin,
    /// and a pale stretch that drains after each hit, so big hits read.
    /// </summary>
    private void DrawBossBar(SpriteBatch batch, RunState run)
    {
        if (run.Boss is not { } fight)
            return;

        var boss = run.Enemies[fight.Index];
        var ratio = boss.State == EnemyState.Alive ? MathHelper.Clamp(boss.Hp / boss.MaxHp, 0f, 1f) : 0f;
        _bossTrail = _bossTrail > ratio ? MathF.Max(ratio, _bossTrail - 0.004f) : ratio;

        var p = _services.Primitives;
        var bar = new Rectangle((_width - 300) / 2, 30, 300, 9);
        p.Rect(batch, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), Ink);
        p.Rect(batch, bar, new Color(40, 24, 32));
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _bossTrail), bar.Height), new Color(255, 236, 200));
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), bar.Height), Vfx.EnemyVfx.AuraRed);
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), 2), new Color(255, 160, 120));

        foreach (var phase in fight.Definition.Phases.Skip(1))
            p.Rect(batch, new Rectangle(bar.X + (int)(bar.Width * phase.Below), bar.Y - 2, 1, bar.Height + 4), Paper);

        var name = _services.Species(fight.Definition.SpeciesId)?.NameIt.ToUpperInvariant() ?? "";
        if (run.Challenge is { } challenge)
            name += $" DI {challenge.Trainer.NameIt.ToUpperInvariant()}";
        UiDraw.TextOutlined(batch, _services.Fonts.Small(_services.Style), name, new Vector2(bar.X, bar.Bottom + 3), Paper, Ink);

        if (run.Challenge is { } trainer)
            DrawTeam(batch, trainer, bar);
    }

    /// <summary>
    /// A challenge's team (phase 5.3), as in the games: the trainer's head right of the bar, and under its right end a Poké
    /// Ball for each Pokémon on a dark strip, faded and struck through once it has fainted.
    /// </summary>
    private void DrawTeam(SpriteBatch batch, Core.Run.Trainers.TrainerChallenge challenge, Rectangle bar)
    {
        var p = _services.Primitives;
        var trainer = challenge.Trainer;
        var head = new Rectangle(bar.Right + 5, bar.Y - 6, 20, 20);
        UiDraw.Frame(batch, p, new Rectangle(head.X - 1, head.Y - 1, head.Width + 2, head.Height + 2), _services.Style.Theme);
        p.Rect(batch, head, Color.Lerp(Vfx.TrainerVfx.ColorOf(trainer), Ink, 0.35f));
        if (_services.TrainerPortraits.Sprite(trainer) is { } sprite)
            batch.Draw(sprite, head, _services.TrainerPortraits.Head(trainer), Color.White);

        var count = challenge.Team.Count;
        p.Rect(batch, new Rectangle(bar.Right - count * 12 - 1, bar.Bottom + 2, count * 12 + 2, 12), Ink * 0.75f);
        for (var i = 0; i < count; i++)
        {
            var slot = new Rectangle(bar.Right - (count - i) * 12 + 1, bar.Bottom + 3, 10, 10);
            var fainted = i < challenge.Fainted;
            var waiting = i >= challenge.SentOut;
            _services.ItemIcons.Draw(batch, "poke-ball", slot, alpha: fainted ? 0.4f : waiting ? 0.75f : 1f);
            if (fainted)
                p.Rect(batch, new Rectangle(slot.X + 1, slot.Center.Y, slot.Width - 2, 1), Ink);
        }
    }

    /// <summary>How long a completed mission's notice stays (Block 6), sliding in and out included.</summary>
    private const int MissionTicks = 240;

    private readonly Queue<Core.Meta.MissionSpec> _missions = new();
    private Core.Meta.MissionSpec? _mission;
    private int _missionAge;

    /// <summary>A mission was completed during the run: its notice comes in at the right edge, one after another.</summary>
    public void ShowMission(Core.Meta.MissionSpec mission) => _missions.Enqueue(mission);

    public void ClearMissions()
    {
        _missions.Clear();
        _mission = null;
    }

    private void UpdateMissions()
    {
        if (_mission is not null && ++_missionAge >= MissionTicks)
            _mission = null;
        if (_mission is null && _missions.TryDequeue(out var next))
        {
            _mission = next;
            _missionAge = 0;
            _services.Sounds.Play("missione");
        }
    }

    /// <summary>
    /// The notice of a completed mission (Block 6), under the coins and KO: "MISSIONE COMPIUTA!" in gold, its name, and what
    /// it gives at the end of the run. It slides in from the right edge and back out.
    /// </summary>
    private void DrawMission(SpriteBatch batch)
    {
        if (_mission is not { } mission)
            return;

        var p = _services.Primitives;
        var small = _services.Fonts.Small(_services.Style);
        var numbers = _services.Fonts.Get(_services.Style.NumberFont, _services.Style.NumberFontSize / 2f);
        var body = _services.Fonts.Body(_services.Style);
        const int width = 184, height = 42;
        var slide = MathF.Min(1f, MathF.Min(_missionAge / 12f, (MissionTicks - _missionAge) / 12f));
        var eased = 1f - (1f - slide) * (1f - slide);
        var x = (int)MathF.Round(_width - 4 - width * eased);
        var box = new Rectangle(x, 46, width, height);
        UiDraw.Frame(batch, p, box, EvolvedTheme);
        if (_missionAge < 20)
            p.Rect(batch, box, Color.White * (0.5f * (1f - _missionAge / 20f)));

        UiDraw.Text(batch, small, "MISSIONE COMPIUTA!", new Vector2(box.X + 8, box.Y + 5), Gold);
        UiDraw.Text(batch, body, mission.NameIt, new Vector2(box.X + 8, box.Y + 13), Paper);
        UiDraw.TextWithNumbers(batch, small, numbers, RewardText(mission, _services), new Vector2(box.X + 8, box.Y + 31), new Color(160, 152, 190));
    }

    /// <summary>What a mission gives, in a few words: "+500 MONETE", "SBLOCCA DITTO".</summary>
    public static string RewardText(Core.Meta.MissionSpec mission, GameServices services)
    {
        var parts = new List<string>();
        if (mission.Reward.Coins > 0)
            parts.Add($"+{UiFormat.Number(mission.Reward.Coins)} MONETE");
        if (mission.Reward.Character is { } c && services.Characters.Find(c) is { } character)
            parts.Add($"SBLOCCA {character.NameIt.ToUpperInvariant()}");
        if (mission.Reward.Trainer is { } t && services.Trainers.Value.Find(t) is { } trainer)
            parts.Add($"SBLOCCA {trainer.NameIt.ToUpperInvariant()}");
        return string.Join("  ", parts);
    }

    /// <summary>The announcement of a Dominante, sliding in under the timer, blinking twice, then fading out.</summary>
    private void DrawBanner(SpriteBatch batch)
    {
        if (_bannerAge < 0)
            return;

        var font = _services.Fonts.Body(_services.Style);
        var slide = MathF.Max(0f, 1f - _bannerAge / 10f);
        var fade = MathHelper.Clamp((BannerTicks - _bannerAge) / 30f, 0f, 1f);
        var blink = _bannerAge < 40 && _bannerAge / 10 % 2 == 1 ? 0.6f : 1f;
        var y = 26 - slide * 8f;
        UiDraw.TextCentered(batch, font, _banner, _width / 2f, y, _bannerColor * (fade * blink), Ink * fade);
    }

    /// <summary>
    /// The HUD sizes Opzioni offers at a screen zoom, in percent: whole monitor pixels per HUD pixel from half the zoom up
    /// to all of it, so every size stays sharp (×4: 50, 75, 100; ×3: 67, 100; ×2: 50, 100).
    /// </summary>
    public static int[] Sizes(int zoom)
    {
        var from = Math.Max(1, (zoom + 1) / 2);
        return Enumerable.Range(from, zoom - from + 1).Select(k => (int)MathF.Round(100f * k / zoom)).ToArray();
    }

    /// <summary>Monitor pixels per HUD pixel for the player's size at <paramref name="zoom"/>.</summary>
    public static int PixelSize(int percent, int zoom) => Math.Clamp((int)MathF.Round(zoom * percent / 100f), Math.Max(1, (zoom + 1) / 2), Math.Max(1, zoom));

    /// <summary>The size the HUD is really drawn at, in percent: the player's, rounded to whole monitor pixels.</summary>
    public static int EffectivePercent(int percent, int zoom) => (int)MathF.Round(100f * PixelSize(percent, zoom) / Math.Max(1, zoom));

    /// <summary>The HUD's layout size: the screen's, larger when the HUD is drawn smaller, so its corners stay in the screen's.</summary>
    private int _width = VirtualScreen.DefaultWidth;
    private int _height = VirtualScreen.DefaultHeight;

    public void Draw(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var theme = _services.Style.Theme;
        var player = run.Player;

        // Opzioni "Dimensione HUD" (playtest of 2026-10-27): the HUD is laid out on a larger canvas and drawn scaled down,
        // a whole number of monitor pixels per HUD pixel; its text is rasterised at that size too.
        var zoom = _services.Screen.Zoom;
        var pixels = PixelSize(_services.Settings.HudPercent, zoom);
        var scale = pixels / (float)zoom;
        _width = (int)MathF.Round(_services.Screen.Width / scale);
        _height = (int)MathF.Round(_services.Screen.Height / scale);
        _services.Fonts.Zoom = pixels;
        var numbers = _services.Fonts.Numbers(_services.Style);

        batch.BeginVirtual(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(scale, scale, 1f));
        XpBar.Draw(batch, player, _width);

        for (var i = 0; i < Slots; i++)
        {
            var moveSlot = new Rectangle(3 + i * 26, 13, 24, 24);
            if (i < run.MaxMoves)
                DrawMoveSlot(batch, moveSlot, i < player.Moves.Count ? player.Moves[i] : null);
            else
                DrawLockedSlot(batch, moveSlot);
            if (_evolveFlash[i] > 0)
                p.Rect(batch, moveSlot, Color.White * (0.8f * _evolveFlash[i] / EvolveFlashTicks));

            var itemSlot = new Rectangle(3 + i * 26, 40, 24, 24);
            UiDraw.Frame(batch, p, itemSlot, theme);
            if (i < player.Items.Count)
            {
                var item = player.Items[i];
                DrawItemIcon(batch, item.Definition.IconKey, itemSlot);
                Pips(batch, itemSlot, item.Level, item.Definition.MaxLevel);
            }
        }

        DrawAbility(batch, run, new Rectangle(3, 67, Slots * 26 - 2, 14));
        DrawSpecial(batch, run, new Rectangle(3, 83, Slots * 26 - 2, 14));
        DrawTrainerCutIn(batch, run);
        DrawTrainer(batch, run, new Rectangle(4, _height - 48, 150, 44));

        // In the endless mode past the boss, the clock turns violet (phase 9.3).
        var clock = UiFormat.Clock(run.Ticks);
        UiDraw.TextCentered(batch, numbers, clock, _width / 2f, 9, run.Endless && run.PastBoss ? Color.Lerp(Endless, Color.White, 0.35f) : Paper, Ink);

        // The sky beside the timer (phase 7.2): the weather, then the moon at night.
        var sky = new Vector2(_width / 2f + UiDraw.Measure(numbers, clock).X / 2f + 5, 11);
        if (run.Weather != Core.Run.Worlds.WeatherKind.Clear && _skyIcons.TryGetValue(run.Weather.ToString(), out var weatherIcon))
        {
            batch.Draw(weatherIcon, sky, Color.White);
            sky.X += weatherIcon.Width + 2;
        }

        if (run.IsNight)
            batch.Draw(_skyIcons["Night"], sky, Color.White);

        // Coins and KO, right-aligned under the level.
        var right = _width - 4f;
        var kills = UiFormat.Number(run.Stats.Kills);
        var killsWidth = UiDraw.Measure(numbers, kills).X;
        UiDraw.TextOutlined(batch, numbers, kills, new Vector2(right - killsWidth, 26), Paper, Ink);
        batch.Draw(_skull, new Vector2(right - killsWidth - 10, 33), Color.White);

        var coins = UiFormat.Number(player.Coins);
        var coinsRight = right - killsWidth - 18;
        var coinsWidth = UiDraw.Measure(numbers, coins).X;
        UiDraw.TextOutlined(batch, numbers, coins, new Vector2(coinsRight - coinsWidth, 26), Gold, Ink);
        batch.Draw(_services.Pickups.Coin, new Vector2(coinsRight - coinsWidth - 10, 33), Color.White);
        DrawPlates(batch, run, new Vector2(right, 48));

        DrawBanner(batch);
        DrawBossBar(batch, run);
        DrawMission(batch);
        batch.End();
        _services.Fonts.Zoom = zoom;
    }

    /// <summary>
    /// The Plates held (phase 9.1), right-aligned under the coins: each tablet in a small frame of its type's colour, with a
    /// white flash for a moment when it comes.
    /// </summary>
    public void DrawPlates(SpriteBatch batch, RunState run, Vector2 topRight)
    {
        var p = _services.Primitives;
        for (var i = 0; i < run.Plates.Count; i++)
        {
            var plate = run.Plates[i];
            var color = TypePalette.Of(plate.Type);
            var box = new Rectangle((int)topRight.X - 22 - i * 24, (int)topRight.Y, 22, 22);
            p.Rect(batch, box, Ink);
            p.Rect(batch, new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), Color.Lerp(color, Ink, 0.6f));
            p.Outline(batch, new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2), color);
            _services.ItemIcons.Draw(batch, plate.Icon, new Rectangle(box.X + 2, box.Y + 2, 18, 18));
            if (_plateFlash > 0 && i == run.Plates.Count - 1)
                p.Rect(batch, box, Color.White * (0.8f * _plateFlash / (float)FlashTicks));
        }
    }

    private int _plateFlash;

    /// <summary>
    /// The ability strip under the items (phase 4.3): its name, and for an active one the key, gold when it is ready. A thin
    /// gauge along the bottom shows a recharge (active abilities, Muta), Vigore still unused, or the stacks of the ones that grow.
    /// </summary>
    public void DrawAbility(SpriteBatch batch, RunState run, Rectangle strip)
    {
        if (run.Stage?.Ability is not { } ability)
            return;

        var p = _services.Primitives;
        var small = _services.Fonts.Small(_services.Style);
        var gauge = Core.Run.Abilities.AbilitySystem.Gauge(run) is { } g ? MathHelper.Clamp(g, 0f, 1f) : (float?)null;
        var ready = gauge >= 1f;
        UiDraw.Frame(batch, p, strip, ability.Active && ready ? EvolvedTheme : _services.Style.Theme);

        UiDraw.Text(batch, small, ability.NameIt.ToUpperInvariant(), new Vector2(strip.X + 4, strip.Y + 3), ability.Active && ready ? Gold : Paper);
        if (ability.Active)
        {
            const string key = "SPAZIO";
            var color = ready ? Gold : new Color(110, 104, 140);
            UiDraw.Text(batch, small, key, new Vector2(strip.Right - 4 - UiDraw.Measure(small, key).X, strip.Y + 3), color);
        }

        if (gauge is { } fill)
        {
            var bar = new Rectangle(strip.X + 2, strip.Bottom - 3, strip.Width - 4, 1);
            p.Rect(batch, bar, Ink * 0.8f);
            p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fill), 1), ready ? Gold : new Color(150, 200, 255));
        }
    }

    /// <summary>
    /// The special gauge under the ability (phase 4.4): it fills in the type's colour with the damage dealt; full, it
    /// shines and shows the key. After a use it stays dark for a while.
    /// </summary>
    public void DrawSpecial(SpriteBatch batch, RunState run, Rectangle strip)
    {
        if (run.SpecialMove is not { } special)
            return;

        var p = _services.Primitives;
        var small = _services.Fonts.Small(_services.Style);
        var state = run.Special;
        var pal = Vfx.TypeFx.For(special.Type);
        var full = state.IsFull;
        var shine = full ? 0.5f + 0.5f * MathF.Sin(run.Ticks * 0.15f) : 0f;
        UiDraw.Frame(batch, p, strip, full ? EvolvedTheme : _services.Style.Theme);

        var inner = new Rectangle(strip.X + 2, strip.Y + 2, strip.Width - 4, strip.Height - 4);
        p.Rect(batch, new Rectangle(inner.X, inner.Y, (int)(inner.Width * state.Charge), inner.Height),
            (full ? Color.Lerp(pal.Dark, pal.Main, shine) : pal.Dark) * (state.LockTicks > 0 ? 0.3f : 0.85f));
        if (full)
            p.Rect(batch, new Rectangle(inner.X, inner.Y, inner.Width, 1), Color.White * (0.6f * shine));

        var name = state.LockTicks > 0 ? "IN RICARICA" : special.NameIt.ToUpperInvariant();
        UiDraw.TextOutlined(batch, small, name, new Vector2(strip.X + 4, strip.Y + 3), state.LockTicks > 0 ? new Color(150, 142, 176) : Paper, Ink);
        if (full)
        {
            const string key = "E";
            UiDraw.TextOutlined(batch, small, key, new Vector2(strip.Right - 4 - UiDraw.Measure(small, key).X, strip.Y + 3), Gold, Ink);
        }
    }

    private const int CutInTicks = 60;
    private const int FlashTicks = 14;
    private static readonly Color Dim = new(110, 104, 140);

    /// <summary>Age of the trainer's figure rising over the card when the key is pressed; -1 when not showing.</summary>
    private int _trainerCutIn = -1;
    private int _trainerFlash;
    private Color _trainerFlashColor = Color.White;

    /// <summary>
    /// The trainer's card, bottom left as in the art direction of phase 0.6 (phase 5.2): the head on the type's colour,
    /// darkened from the top while the key recharges, the key Q in a corner; the name, the active ability (gold when ready),
    /// what it is doing now, and a thin gauge: the recharge, or what is left of the effect while it runs.
    /// </summary>
    public void DrawTrainer(SpriteBatch batch, RunState run, Rectangle card)
    {
        if (run.Trainer is not { } trainer)
            return;

        var p = _services.Primitives;
        var small = _services.Fonts.Small(_services.Style);
        var body = _services.Fonts.Body(_services.Style);
        var state = run.TrainerState;
        var color = Vfx.TrainerVfx.ColorOf(trainer);
        var gauge = Core.Run.Trainers.TrainerSystem.Gauge(run) ?? 1f;
        var ready = state.CooldownTicks == 0;
        UiDraw.Frame(batch, p, card, ready ? EvolvedTheme : _services.Style.Theme);

        // The head on a field of the type's colour, darkened from the top by the recharge still to go.
        var head = new Rectangle(card.X + 4, card.Y + 4, card.Height - 8, card.Height - 8);
        p.Rect(batch, head, Color.Lerp(color, Ink, 0.35f));
        p.Rect(batch, new Rectangle(head.X, head.Y + head.Height / 2, head.Width, head.Height - head.Height / 2), Color.Lerp(color, Ink, 0.5f));
        if (_services.TrainerPortraits.Sprite(trainer) is { } sprite)
            batch.Draw(sprite, head, _services.TrainerPortraits.Head(trainer), Color.White);
        if (!ready)
            p.Rect(batch, new Rectangle(head.X, head.Y, head.Width, (int)MathF.Ceiling(head.Height * (1f - gauge))), new Color(8, 6, 16) * 0.67f);
        if (_trainerFlash > 0)
            p.Rect(batch, head, _trainerFlashColor * (0.7f * _trainerFlash / FlashTicks));
        UiDraw.TextOutlined(batch, small, "Q", new Vector2(head.Right - 7, head.Bottom - 9), ready ? Gold : Dim, Ink);

        var x = head.Right + 6;
        UiDraw.Text(batch, body, trainer.NameIt, new Vector2(x, card.Y + 2), Paper);
        UiDraw.Text(batch, small, trainer.Active.NameIt.ToUpperInvariant(), new Vector2(x, card.Y + 20), ready ? Gold : new Color(160, 152, 190));

        var running = state.IsActive;
        var (status, statusColor) = running ? ("ATTIVA", Color.Lerp(color, Color.White, 0.3f)) : ready ? ("PRONTA", Gold) : ("IN RICARICA", Dim);
        UiDraw.Text(batch, small, status, new Vector2(x, card.Y + 30), statusColor);

        // The shield's charges left, as small squares after the word.
        if (running && trainer.Active.Kind == Core.Run.Trainers.TrainerActiveKind.Shield)
        {
            var pips = x + (int)UiDraw.Measure(small, status).X + 4;
            for (var i = 0; i < trainer.Active.Charges; i++)
                p.Rect(batch, new Rectangle(pips + i * 5, card.Y + 32, 3, 4), i < state.ShieldCharges ? statusColor : Ink * 0.8f);
        }

        var bar = new Rectangle(x, card.Bottom - 4, card.Right - 5 - x, 1);
        var fill = running ? Core.Run.Trainers.TrainerSystem.EffectLeft(run) : gauge;
        p.Rect(batch, bar, Ink * 0.8f);
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fill), 1), running ? statusColor : ready ? Gold : new Color(150, 200, 255));
    }

    /// <summary>
    /// The trainer steps in when the key is pressed: their whole figure rises from behind the card, stays a moment, then
    /// fades. The game does not stop.
    /// </summary>
    private void DrawTrainerCutIn(SpriteBatch batch, RunState run)
    {
        if (_trainerCutIn < 0 || run.Trainer is not { } trainer || _services.TrainerPortraits.Sprite(trainer) is not { } sprite)
            return;

        var rise = MathF.Min(1f, _trainerCutIn / 8f);
        rise = 1f - (1f - rise) * (1f - rise);
        var alpha = _trainerCutIn > CutInTicks - 16 ? (CutInTicks - _trainerCutIn) / 16f : 1f;
        var cardTop = _height - 48;
        var y = cardTop + 4 - (sprite.Height - 6) * rise;
        batch.Draw(sprite, new Vector2(10, MathF.Round(y)), Color.White * alpha);
    }

    /// <summary>A 24 px move slot with the icon and its level marks; an evolved move (phase 2.6) has a gold frame and a star instead.</summary>
    public void DrawMoveSlot(SpriteBatch batch, Rectangle slot, Core.Run.Moves.MoveInstance? move)
    {
        var p = _services.Primitives;
        var evolved = move?.Definition.IsEvolution == true;
        UiDraw.Frame(batch, p, slot, evolved ? EvolvedTheme : _services.Style.Theme);
        if (move is null)
            return;

        _services.MoveIcons.Draw(batch, p, new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4), move.Definition.Id, move.Definition.Type);
        if (evolved)
            batch.Draw(_star, new Vector2(slot.X + 1, slot.Bottom - _star.Height - 1), Color.White);
        else
            Pips(batch, slot, move.Level, move.Definition.MaxLevel);
    }

    private Texture2D? _lock;

    /// <summary>
    /// The sixth move slot (phase 3.6): a run holds five moves; the sixth will be bought with the meta-progression, so its
    /// slot shows darkened with a small padlock.
    /// </summary>
    public void DrawLockedSlot(SpriteBatch batch, Rectangle slot)
    {
        var p = _services.Primitives;
        UiDraw.Frame(batch, p, slot, _services.Style.Theme);
        p.Rect(batch, new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4), Ink * 0.55f);
        _lock ??= PixelArt.FromPatternOutlined(_services.Device,
        [
            ".ggg.",
            "g...g",
            "g...g",
            "ggggg",
            "gg.gg",
            "ggggg",
        ], new Dictionary<char, Color> { ['g'] = new(150, 142, 176) }, Ink);
        batch.Draw(_lock, new Vector2(slot.Center.X - _lock.Width / 2, slot.Center.Y - _lock.Height / 2), Color.White);
    }

    /// <summary>Frame of an evolved move's slot: the usual panel with a gold rim.</summary>
    public UiTheme EvolvedTheme => _services.Style.Theme with { Border = Gold, Highlight = new Color(176, 136, 48) };

    /// <summary>Item icons are 30 px; the centre 24 px fill a slot.</summary>
    public void DrawItemIcon(SpriteBatch batch, string iconKey, Rectangle slot) =>
        _services.ItemIcons.Draw(batch, iconKey, new Rectangle(slot.X, slot.Y, ItemIcons.Crop.Width, ItemIcons.Crop.Height));

    /// <summary>Level marks along the bottom of a slot, grouped by four.</summary>
    public void Pips(SpriteBatch batch, Rectangle slot, int level, int max)
    {
        var p = _services.Primitives;
        var x0 = slot.X + 3;
        for (var i = 0; i < max; i++)
            p.Rect(batch, new Rectangle(x0 + i * 2 + i / 4, slot.Bottom - 5, 1, 2), i < level ? Color.White : Ink * 0.7f);
    }
}
