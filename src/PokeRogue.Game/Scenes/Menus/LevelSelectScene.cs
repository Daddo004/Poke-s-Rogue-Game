using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Core.Run.Worlds;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Ui;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The level select (phase 7.1), after the trainer: the worlds as tabs on top, the world's levels on the left with a medal
/// for each difficulty won, the chosen level on the right with a picture of its map, its sky and its boss, the difficulty
/// tabs (Difficile and Ultra open in turn), the record and the missions of that difficulty. At Ultra it also shows the
/// legendaries that come instead of the boss, and the trainers challenged there: [S] picks one (phase 5.3).
/// </summary>
public sealed class LevelSelectScene : MenuScene
{
    private const int RowPitch = 38;

    private static readonly Color LockedTint = new(40, 36, 60);
    private static readonly Color[] DifficultyColors = [new(120, 220, 120), new(248, 168, 64), new(232, 96, 80)];

    private readonly CharacterDefinition _character;
    private readonly TrainerDefinition _trainer;
    private readonly WorldLibrary _worlds;
    private readonly Dictionary<string, RenderTarget2D> _pictures = new(StringComparer.Ordinal);

    private int _world;
    private int _level;
    private Difficulty _difficulty;

    /// <summary>The trainer to challenge at Ultra, as an index in the level's list; -1 for none (the boss or the legendaries).</summary>
    private int _challenge = -1;

    /// <summary>The endless mode (phase 9.3), on a level already won: [I] switches it.</summary>
    private bool _endless;

    private bool EndlessOpen => Meta.IsEndlessUnlocked(Level) && Services.Worlds.Value.EndlessSpec is not null;

    /// <summary>Previews: switches the endless mode on.</summary>
    public void PickEndless() => _endless = true;

    /// <param name="trainer">The trainer played with (in a challenge too).</param>
    /// <param name="challengeId">Opens on that trainer's level at Ultra, with the challenge picked (from the trainer select).</param>
    public LevelSelectScene(GameServices services, CharacterDefinition character, TrainerDefinition trainer, string? levelId = null,
        Difficulty? difficulty = null, string? challengeId = null) : base(services)
    {
        _character = character;
        _trainer = trainer;
        _worlds = services.Worlds.Value;

        var start = challengeId is not null ? _worlds.LevelOfTrainer(challengeId) : null;
        start ??= _worlds.Find(levelId ?? Meta.Data.LastLevel ?? "") ?? _worlds.Levels.LastOrDefault(l => Meta.IsUnlocked(l)) ?? _worlds.First;
        _world = start.World.Index;
        _level = start.Index;
        _difficulty = challengeId is not null ? Difficulty.Ultra : difficulty ?? (start.Id == Meta.Data.LastLevel ? Meta.Data.LastDifficulty : Difficulty.Normal);
        if (challengeId is not null)
            _challenge = start.Trainers.ToList().IndexOf(challengeId);
        if (!Meta.IsUnlocked(start, _difficulty) && challengeId is null)
            _difficulty = Difficulty.Normal;
    }

    private Dictionary<string, Core.Data.SpeciesData>? _byKey;

    private Core.Data.SpeciesData SpeciesOf(string key) =>
        (_byKey ??= Services.Pokedex.Value.Species.ToDictionary(s => s.Key, StringComparer.Ordinal))[key];

    private WorldInfo World => _worlds.Worlds[_world];

    private LevelInfo Level => World.Levels[_level];

    /// <summary>Previews: picks the level's <paramref name="index"/>-th trainer to challenge, as [S] would.</summary>
    public void PickChallenge(int index) => _challenge = index;

    private TrainerDefinition? Challenge =>
        _difficulty == Difficulty.Ultra && _challenge >= 0 && _challenge < Level.Trainers.Count ? Services.Trainers.Value.Find(Level.Trainers[_challenge]) : null;

    public override void OnLeave()
    {
        foreach (var picture in _pictures.Values)
            picture.Dispose();
        _pictures.Clear();
    }

    protected override void UpdateMenu()
    {
        if (Pressed(Keys.Q, Buttons.LeftShoulder))
            SwitchWorld(-1);
        if (Pressed(Keys.E, Buttons.RightShoulder))
            SwitchWorld(1);
        if (Up())
            Select(_level - 1);
        if (Down())
            Select(_level + 1);
        if (Left())
            SwitchDifficulty(-1);
        if (Right())
            SwitchDifficulty(1);

        // The mouse: tabs and trainers are clicked; pointing at a level selects it, a click on the selected one plays.
        if (ClickedOn("mondo") is { } world)
            SwitchWorld(world - _world);
        if (ClickedOn("difficolta") is { } difficulty)
            SwitchDifficulty(difficulty - (int)_difficulty);
        if (ClickedOn("sfida") is { } trainer)
            _challenge = _challenge == trainer ? -1 : trainer;
        if (Pressed(Keys.I, Buttons.Y) || ClickedOn("infinita") is not null)
        {
            if (EndlessOpen)
                _endless = !_endless;
            else
                Notice("LA MODALITÀ INFINITA SI APRE VINCENDO QUESTO LIVELLO", Dim);
        }
        if (Pointed("livello") is { } pointed)
            Select(pointed);
        if (ClickedOn("livello") is { } clicked)
        {
            if (clicked == _level)
                Play();
            else
                Select(clicked);
            return;
        }

        if (Back())
        {
            Services.Switch(new TrainerSelectScene(Services, _character, _trainer.Id));
        }
        else if (Pressed(Keys.S, Buttons.X))
        {
            if (_difficulty != Difficulty.Ultra)
                Notice("LE SFIDE AGLI ALLENATORI SI GIOCANO A ULTRA", Dim);
            else if (Level.Trainers.Count == 0)
                Notice("NESSUN ALLENATORE DA SFIDARE QUI", Dim);
            else
                _challenge = _challenge + 1 >= Level.Trainers.Count ? -1 : _challenge + 1;
        }
        else if (Confirm())
        {
            Play();
        }
    }

    private void Play()
    {
        var level = Level;
        if (!Meta.IsUnlocked(level))
        {
            Notice(Meta.Worlds?.Previous(level) is { } before ? $"PRIMA VINCI {before.NameIt.ToUpperInvariant()}" : "BLOCCATO", Red);
            return;
        }

        if (!Meta.IsUnlocked(level, _difficulty))
        {
            Notice(_difficulty == Difficulty.Hard ? "PRIMA VINCI QUESTO LIVELLO A NORMALE" : "PRIMA VINCI QUESTO LIVELLO A DIFFICILE", Red);
            return;
        }

        // With a Plate found, it is chosen next (phase 9.1). An endless run (phase 9.3) has no challenge.
        var endless = _endless && EndlessOpen;
        var challenge = endless ? null : Challenge;
        Services.Switch(Meta.UnlockedPlates().Count > 0 && Services.Plates.Value.File.Enabled
            ? new PlateSelectScene(Services, _character, _trainer, level.Id, _difficulty, challenge, endless)
            : MainMenuScene.Run(Services, _character, _trainer, level.Id, _difficulty, challenge, endless: endless));
    }

    private void SwitchWorld(int delta)
    {
        var next = Math.Clamp(_world + delta, 0, _worlds.Worlds.Count - 1);
        if (next == _world)
            return;
        _world = next;
        _level = 0;
        _challenge = -1;
        if (!Meta.IsUnlocked(Level, _difficulty))
            _difficulty = Difficulty.Normal;
    }

    private void Select(int index)
    {
        if (index < 0 || index >= World.Levels.Count)
            return;
        _level = index;
        _challenge = -1;
        if (!Meta.IsUnlocked(Level, _difficulty))
            _difficulty = Difficulty.Normal;
        if (!EndlessOpen)
            _endless = false;
    }

    private void SwitchDifficulty(int delta)
    {
        var next = (Difficulty)Math.Clamp((int)_difficulty + delta, 0, 2);
        if (next == _difficulty)
            return;
        _difficulty = next;
        _challenge = -1;
    }

    protected override void BeforeDraw(SpriteBatch batch)
    {
        // Each level's picture is rendered once, the first time it is shown.
        var level = Level;
        if (_pictures.ContainsKey(level.Id))
            return;

        var target = new RenderTarget2D(Services.Device, 196, 110);
        LevelPicture.Render(Services, batch, level.File, target, Services.Screen.Target);
        _pictures[level.Id] = target;
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        DrawHeader(batch, "SCEGLI IL LIVELLO");
        DrawWorldTabs(batch);
        DrawLevels(batch);
        DrawDetail(batch, new Rectangle(262, 50, 362, 286));
        DrawEndlessSwitch(batch, new Rectangle(470, 30, 154, 16));
        DrawFooter(batch, "[SU/GIÙ] LIVELLO   [Q/E] MONDO   [SINISTRA/DESTRA] DIFFICOLTÀ   " +
            (_difficulty == Difficulty.Ultra && Level.Trainers.Count > 0 && !_endless ? "[S] SFIDA   " : "") + "[I] INFINITA   [INVIO] GIOCA   [ESC] INDIETRO");
    }

    /// <summary>The endless mode's switch (phase 9.3), top right: dark violet when on, dim until the level is won.</summary>
    private void DrawEndlessSwitch(SpriteBatch batch, Rectangle tab)
    {
        var p = Services.Primitives;
        var open = EndlessOpen;
        var violet = new Color(150, 96, 220);
        Clickable(tab, "infinita");
        p.Rect(batch, tab, _endless ? new Color(52, 30, 78) : new Color(34, 30, 54));
        p.Outline(batch, tab, _endless ? violet : Under(tab) ? Paper : new Color(70, 64, 100));
        var text = !open ? "INFINITA: VINCI IL LIVELLO" : _endless ? "MODALITÀ INFINITA: SÌ" : "MODALITÀ INFINITA: NO";
        UiDraw.TextCentered(batch, Small, text, tab.Center.X, tab.Y + 4, !open ? Dim : _endless ? Color.Lerp(violet, Color.White, 0.4f) : Paper);
    }

    /// <summary>What the endless mode holds, in place of the missions: the loop, the Reaper, and the record.</summary>
    private void DrawEndless(SpriteBatch batch, int x, int y, int width)
    {
        var best = Meta.Data.EndlessBest.GetValueOrDefault(Level.Id);
        if (Services.Worlds.Value.EndlessSpec?.Reaper is { } reaper && Services.Sprites.Portrait(SpeciesOf(reaper.Pokemon).Id) is { } darkrai)
        {
            UiDraw.Frame(batch, Services.Primitives, new Rectangle(x, y, 44, 44), Services.Style.Theme);
            batch.Draw(darkrai, new Rectangle(x + 2, y + 2, 40, 40), Color.White);
        }

        var text = "Dopo il boss la partita continua: le ondate tornano, sempre più forti. Alle 30:00 arriva Darkrai, il Mietitore, " +
            "che si arrabbia ogni minuto di più: sconfiggilo per vincere e per averlo come Pokémon.";
        DrawWrapped(batch, text, x + 52, y, width - 52, 5, Paper);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, best > 0 ? $"RECORD IN MODALITÀ INFINITA {UiFormat.Clock(best)}" : "MAI GIOCATO IN MODALITÀ INFINITA",
            new Vector2(x, y + 56), Dim);
    }

    private void DrawWorldTabs(SpriteBatch batch)
    {
        var p = Services.Primitives;
        for (var i = 0; i < _worlds.Worlds.Count; i++)
        {
            var world = _worlds.Worlds[i];
            var tab = new Rectangle(16 + i * 60, 30, 56, 16);
            var open = Meta.IsUnlocked(world.Levels[0]);
            var selected = i == _world;
            Clickable(tab, "mondo", i);
            p.Rect(batch, tab, selected ? new Color(60, 48, 24) : new Color(34, 30, 54));
            p.Outline(batch, tab, selected ? Gold : Under(tab) ? Paper : new Color(70, 64, 100));
            UiDraw.TextCentered(batch, Small, world.NameIt.ToUpperInvariant(), tab.Center.X, tab.Y + 4, selected ? Gold : open ? Paper : Dim);
        }

        UiDraw.Text(batch, Small, "Q", new Vector2(4, 34), Dim);
        UiDraw.Text(batch, Small, "E", new Vector2(258, 34), Dim);
    }

    private void DrawLevels(SpriteBatch batch)
    {
        var p = Services.Primitives;
        for (var i = 0; i < World.Levels.Count; i++)
        {
            var level = World.Levels[i];
            var row = new Rectangle(16, 52 + i * RowPitch, 236, RowPitch - 4);
            var open = Meta.IsUnlocked(level);
            var selected = i == _level;
            Clickable(row, "livello", i);
            UiDraw.Frame(batch, p, row, selected ? new UiTheme(new Color(60, 48, 24, 235), Gold, new Color(140, 110, 40), Ink, Paper, Dim) : Services.Style.Theme);
            if (selected)
                DrawCursor(batch, row);
            else
                DrawHover(batch, row);

            // The boss' face, dark until the level opens.
            var face = new Rectangle(row.X + 3, row.Y + 3, 28, 28);
            p.Rect(batch, face, new Color(24, 20, 40));
            if (level.File.Boss is { } boss && Services.Sprites.Portrait(SpeciesOf(boss.Pokemon).Id) is { } portrait)
                batch.Draw(portrait, face, open ? Color.White : LockedTint);

            UiDraw.Text(batch, Body, open ? level.NameIt : "???", new Vector2(row.X + 37, row.Y + 3), open ? Paper : Dim);
            UiDraw.Text(batch, Small, open ? ShapeText(level) : "BLOCCATO", new Vector2(row.X + 37, row.Y + 21), Dim);

            // A medal for each difficulty won.
            for (var d = 0; d < 3; d++)
            {
                var medal = new Rectangle(row.Right - 40 + d * 12, row.Y + 22, 8, 8);
                var won = Meta.IsWon(level, (Difficulty)d);
                p.Rect(batch, new Rectangle(medal.X - 1, medal.Y - 1, 10, 10), Ink);
                p.Rect(batch, medal, won ? DifficultyColors[d] : new Color(50, 44, 76));
                if (won)
                    p.Rect(batch, new Rectangle(medal.X + 1, medal.Y + 1, 3, 2), Color.White * 0.7f);
            }
        }
    }

    /// <summary>"MAPPA APERTA", "CORRIDOIO", "ARENA", and the sky: rain, sun, night…</summary>
    private static string ShapeText(LevelInfo level)
    {
        var file = level.File;
        var shape = file.Map.Shape switch
        {
            MapShape.Corridor => "CORRIDOIO",
            MapShape.Arena => "ARENA",
            _ => "MAPPA APERTA",
        };
        var sky = new List<string>();
        foreach (var kind in file.Weather.Select(w => w.Kind).Distinct())
            sky.Add(WeatherName(kind));
        if (file.Night is not null)
            sky.Add("NOTTE");
        else if (file.Darkness >= 0.4f)
            sky.Add("BUIO");
        return sky.Count == 0 ? shape : $"{shape}, {string.Join(", ", sky)}";
    }

    private static string WeatherName(WeatherKind kind) => kind switch
    {
        WeatherKind.Rain => "PIOGGIA",
        WeatherKind.Sun => "SOLE",
        WeatherKind.Sandstorm => "SABBIA",
        WeatherKind.Hail => "GRANDINE",
        _ => "SERENO",
    };

    private void DrawDetail(SpriteBatch batch, Rectangle panel)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var level = Level;
        var open = Meta.IsUnlocked(level);
        UiDraw.Frame(batch, p, panel, theme);

        // The map's picture, and beside it the name, the world and the boss.
        var picture = new Rectangle(panel.X + 8, panel.Y + 8, 196, 110);
        p.Rect(batch, new Rectangle(picture.X - 1, picture.Y - 1, picture.Width + 2, picture.Height + 2), Ink);
        if (_pictures.TryGetValue(level.Id, out var target))
            batch.Draw(target, picture, open ? Color.White : LockedTint);
        if (!open)
            UiDraw.TextCentered(batch, Body, "BLOCCATO", picture.Center.X, picture.Center.Y - 8, Dim, Ink);

        var x = picture.Right + 8;
        UiDraw.Text(batch, Body, open ? level.NameIt : "???", new Vector2(x, panel.Y + 8), theme.Text);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"{level.World.NameIt.ToUpperInvariant()}, LIVELLO {level.Index + 1}", new Vector2(x, panel.Y + 26), theme.TextDim);
        DrawWrapped(batch, ShapeText(level), x, panel.Y + 38, panel.Right - x - 8, 2, theme.TextDim);
        if (level.File.Boss is { } boss && open)
        {
            var species = SpeciesOf(boss.Pokemon);
            var face = new Rectangle(x, panel.Y + 62, 40, 40);
            UiDraw.Frame(batch, p, new Rectangle(face.X - 2, face.Y - 2, 44, 44), theme);
            if (Services.Sprites.Portrait(species.Id) is { } portrait)
                batch.Draw(portrait, face, Color.White);
            UiDraw.Text(batch, Small, "BOSS", new Vector2(face.Right + 6, face.Y + 4), Gold);
            UiDraw.Text(batch, Small, species.NameIt.ToUpperInvariant(), new Vector2(face.Right + 6, face.Y + 16), theme.Text);
            DrawWrapped(batch, boss.TitleIt.ToUpperInvariant(), face.Right + 6, face.Y + 26, panel.Right - face.Right - 14, 2, theme.TextDim, 9);
        }

        DrawWrapped(batch, open ? level.DescriptionIt : HowToOpen(level), panel.X + 8, panel.Y + 124, panel.Width - 16, 2, open ? theme.Text : theme.TextDim);
        DrawDifficultyTabs(batch, new Rectangle(panel.X + 8, panel.Y + 148, panel.Width - 16, 16));
        DrawRecord(batch, panel.X + 8, panel.Y + 170);
        if (_endless)
            DrawEndless(batch, panel.X + 8, panel.Y + 186, panel.Width - 16);
        else if (_difficulty == Difficulty.Ultra)
            DrawUltra(batch, panel.X + 8, panel.Y + 186, panel.Width - 16);
        else
            DrawMissions(batch, panel.X + 8, panel.Y + 186, panel.Width - 16, 6);
    }

    private string HowToOpen(LevelInfo level) =>
        Meta.Worlds?.Previous(level) is { } before ? $"SI APRE VINCENDO {before.NameIt.ToUpperInvariant()}." : "";

    private void DrawDifficultyTabs(SpriteBatch batch, Rectangle strip)
    {
        var p = Services.Primitives;
        var width = (strip.Width - 8) / 3;
        for (var d = 0; d < 3; d++)
        {
            var difficulty = (Difficulty)d;
            var tab = new Rectangle(strip.X + d * (width + 4), strip.Y, width, strip.Height);
            var open = Meta.IsUnlocked(Level, difficulty);
            var selected = difficulty == _difficulty;
            var color = DifficultyColors[d];
            Clickable(tab, "difficolta", d);
            p.Rect(batch, tab, selected ? Color.Lerp(color, Ink, 0.55f) : new Color(30, 26, 48));
            p.Outline(batch, tab, selected ? color : Under(tab) ? Paper : new Color(70, 64, 100));
            var name = _worlds.Difficulty(difficulty).NameIt.ToUpperInvariant();
            UiDraw.TextCentered(batch, Small, open ? name : $"{name} (BLOCCATA)", tab.Center.X, tab.Y + 4, open ? (selected ? Paper : color) : Dim);
        }
    }

    private void DrawRecord(SpriteBatch batch, int x, int y)
    {
        var record = Meta.Data.Levels.GetValueOrDefault($"{Level.Id}/{_difficulty}");
        var line = record is null || record.Runs == 0
            ? "MAI GIOCATO A QUESTA DIFFICOLTÀ"
            : $"PARTITE {record.Runs}   VITTORIE {record.Wins}   RECORD {UiFormat.Clock(record.BestTicks)}   KO {UiFormat.Number(record.BestKills)}";
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, line, new Vector2(x, y), Dim);
    }

    private void DrawMissions(SpriteBatch batch, int x, int y, int width, int max)
    {
        var missions = Meta.Missions.All.Where(m => m.Level == Level.Id && m.Difficulty == _difficulty).Take(max).ToList();
        UiDraw.Text(batch, Small, "MISSIONI", new Vector2(x, y), Gold);
        for (var i = 0; i < missions.Count; i++)
        {
            var mission = missions[i];
            var done = Meta.IsDone(mission);
            var at = new Vector2(x, y + 13 + i * 18);
            Services.Primitives.Rect(batch, new Rectangle((int)at.X, (int)at.Y + 1, 7, 7), done ? Green : new Color(50, 44, 76));
            UiDraw.Text(batch, Small, mission.DescriptionIt.ToUpperInvariant(), at + new Vector2(12, 0), done ? Green : Paper);
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, Hud.RewardText(mission, Services), at + new Vector2(12, 9), Dim);
        }
    }

    /// <summary>At Ultra: the legendaries that come instead of the boss, then the trainers to challenge; the mission underneath.</summary>
    private void DrawUltra(SpriteBatch batch, int x, int y, int width)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var legendaries = Level.File.Legendaries;
        var challenge = Challenge;
        var label = challenge is not null ? $"SFIDA: {challenge.NameIt.ToUpperInvariant()} AL POSTO DEL BOSS"
            : legendaries.Count > 0 ? "AL POSTO DEL BOSS ARRIVANO I LEGGENDARI" : "IL BOSS È PIÙ FORTE";
        UiDraw.Text(batch, Small, label, new Vector2(x, y), challenge is not null ? Red : Gold);

        var cx = x;
        foreach (var legendary in legendaries)
        {
            var species = SpeciesOf(legendary.Pokemon);
            var character = Services.Characters.All.FirstOrDefault(c => c.First.Id == species.Id);
            var owned = character is not null && Meta.IsUnlocked(character);
            var waiting = legendary.Requires.Any(id => Services.Characters.Find(id) is { } c && !Meta.IsUnlocked(c));
            var face = new Rectangle(cx, y + 12, 30, 30);
            UiDraw.Frame(batch, p, new Rectangle(face.X - 2, face.Y - 2, 34, 34), theme);
            if (Services.Sprites.Portrait(species.Id) is { } portrait)
                batch.Draw(portrait, face, owned ? Color.White : waiting ? LockedTint : new Color(110, 100, 150));
            if (owned)
                p.Rect(batch, new Rectangle(face.Right - 7, face.Bottom - 7, 6, 6), Green);
            else if (waiting)
                UiDraw.TextOutlined(batch, Small, "?", new Vector2(face.Center.X - 2, face.Center.Y - 5), Dim, Ink);
            cx += 36;
        }

        if (legendaries.Any(l => l.Requires.Count > 0 && l.Requires.Any(id => Services.Characters.Find(id) is { } c && !Meta.IsUnlocked(c))))
            DrawWrapped(batch, "COMPARE DOPO AVER SBLOCCATO I TRE REGI", cx + 4, y + 18, x + width - cx - 4, 2, Dim);

        // The trainers challenged here.
        var trainers = Level.Trainers.Select(id => Services.Trainers.Value.Find(id)).OfType<TrainerDefinition>().ToList();
        if (trainers.Count > 0)
        {
            var tx = x + width - trainers.Count * 34 + 2;
            KeyHints.Draw(batch, Services, "[S] SFIDE", new Vector2(tx - KeyHints.Measure(Services, "[S] SFIDE") - 6, y + 22), Paper);
            for (var i = 0; i < trainers.Count; i++)
            {
                var trainer = trainers[i];
                var head = new Rectangle(tx + i * 34, y + 12, 30, 30);
                Clickable(head, "sfida", i);
                p.Rect(batch, head, Color.Lerp(TrainerVfx.ColorOf(trainer), Ink, 0.4f));
                if (Services.TrainerPortraits.Sprite(trainer) is { } sprite)
                    batch.Draw(sprite, head, Services.TrainerPortraits.Head(trainer), Meta.IsUnlocked(trainer) ? Color.White : new Color(120, 110, 150));
                p.Outline(batch, new Rectangle(head.X - 1, head.Y - 1, head.Width + 2, head.Height + 2), Meta.IsDefeated(trainer) ? Green : Ink);
                if (i == _challenge)
                    DrawCursor(batch, head);
                else
                    DrawHover(batch, head);
            }
        }

        DrawMissions(batch, x, y + 50, width, 2);
    }
}
