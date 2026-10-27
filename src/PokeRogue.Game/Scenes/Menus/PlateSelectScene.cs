using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Plates;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The Plate for the run (phase 9.1), after the level, once at least one has been found: the sixteen tablets in a grid
/// (those still hidden show where they lie), "Nessuna" first, and the chosen one's card on the right. The choice is
/// remembered for the next runs.
/// </summary>
public sealed class PlateSelectScene : MenuScene
{
    private const int Columns = 6;
    private const int Cell = 40;
    private const int Gap = 6;
    private static readonly Point GridOrigin = new(20, 52);

    private readonly CharacterDefinition _character;
    private readonly TrainerDefinition _trainer;
    private readonly string _levelId;
    private readonly Difficulty _difficulty;
    private readonly TrainerDefinition? _challenge;
    private readonly PlateLibrary _plates;

    /// <summary>0 = none, then the Plates in the file's order.</summary>
    private int _selected;

    public PlateSelectScene(GameServices services, CharacterDefinition character, TrainerDefinition trainer, string levelId, Difficulty difficulty,
        TrainerDefinition? challenge) : base(services)
    {
        _character = character;
        _trainer = trainer;
        _levelId = levelId;
        _difficulty = difficulty;
        _challenge = challenge;
        _plates = services.Plates.Value;
        var last = _plates.All.ToList().FindIndex(p => p.Id == Meta.Data.LastPlate && Meta.IsUnlocked(p));
        _selected = last + 1;
    }

    private int Count => _plates.All.Count + 1;

    private PlateDefinition? Selected => _selected == 0 ? null : _plates.All[_selected - 1];

    /// <summary>Previews: selects a cell (0 = none).</summary>
    public void Select(int index) => _selected = Math.Clamp(index, 0, Count - 1);

    protected override void UpdateMenu()
    {
        if (Left())
            Select(_selected - 1);
        if (Right())
            Select(_selected + 1);
        if (Up())
            Select(_selected - Columns);
        if (Down())
            Select(_selected + Columns);
        if (Pointed("lastra") is { } pointed)
            Select(pointed);
        if (ClickedOn("lastra") is { } clicked)
        {
            if (clicked == _selected)
                Play();
            else
                Select(clicked);
            return;
        }

        if (Back())
            Services.Switch(new LevelSelectScene(Services, _character, _trainer, _levelId, _difficulty, _challenge?.Id));
        else if (Confirm())
            Play();
    }

    private void Play()
    {
        if (Selected is { } plate && !Meta.IsUnlocked(plate))
        {
            Notice("NON L'HAI ANCORA TROVATA", Red);
            return;
        }

        Meta.ChoosePlate(Selected?.Id);
        Services.Switch(MainMenuScene.Run(Services, _character, _trainer, _levelId, _difficulty, _challenge, Selected?.Id));
    }

    private Rectangle CellAt(int index) => new(
        GridOrigin.X + index % Columns * (Cell + Gap),
        GridOrigin.Y + index / Columns * (Cell + Gap), Cell, Cell);

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        DrawHeader(batch, "SCEGLI UNA LASTRA");
        var found = Meta.UnlockedPlates().Count;
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"LASTRE TROVATE: {found} SU {_plates.All.Count}", new Vector2(GridOrigin.X, 36), Dim);

        for (var i = 0; i < Count; i++)
        {
            var cell = CellAt(i);
            Clickable(cell, "lastra", i);
            if (i == 0)
            {
                p.Rect(batch, cell, Ink);
                p.Outline(batch, new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2), Dim * 0.6f);
                UiDraw.TextCentered(batch, Small, "NESSUNA", cell.Center.X, cell.Center.Y - 4, Dim);
            }
            else
            {
                var plate = _plates.All[i - 1];
                PlateArt.DrawCell(batch, Services, cell, plate, Meta.IsUnlocked(plate), Age + i * 7);
            }

            DrawHover(batch, cell);
            if (i == _selected)
                DrawCursor(batch, cell);
        }

        DrawDetail(batch, new Rectangle(330, 52, 290, 204));
        DrawFooter(batch, "[INVIO] GIOCA   [ESC] INDIETRO");
    }

    /// <summary>The chosen Plate's card; a hidden one tells where to look; "Nessuna" says what playing without means.</summary>
    private void DrawDetail(SpriteBatch batch, Rectangle area)
    {
        var p = Services.Primitives;
        if (Selected is not { } plate)
        {
            UiDraw.Frame(batch, p, area, Services.Style.Theme);
            UiDraw.TextCentered(batch, Body, "Nessuna Lastra", area.Center.X, area.Y + 14, Paper);
            DrawWrapped(batch, "Giochi senza Lastra. Dal minuto 8 il primo forziere ti offre comunque di sceglierne una fra quelle trovate.",
                area.X + 12, area.Y + 40, area.Width - 24, 6, Dim);
            return;
        }

        if (!Meta.IsUnlocked(plate))
        {
            UiDraw.Frame(batch, p, area, Services.Style.Theme);
            UiDraw.TextCentered(batch, Body, "Lastra nascosta", area.Center.X, area.Y + 14, Paper);
            Services.ItemIcons.Draw(batch, plate.Icon, new Rectangle(area.Center.X - 24, area.Y + 40, 48, 48), alpha: 0.2f);
            var level = Services.Worlds.Value.Find(plate.Level);
            var where = level is null ? plate.Level : $"{level.NameIt} ({level.World.NameIt})";
            DrawWrapped(batch, $"Una Lastra di tipo {TypePalette.NameIt(plate.Type)} è nascosta in {where}. Cercala sulla mappa: una freccia la indica quando sei vicino.",
                area.X + 12, area.Y + 100, area.Width - 24, 6, Dim);
            return;
        }

        PlateArt.DrawCard(batch, Services, area, plate, _plates.File.TypePower, Age, highlight: true);
        var foundIn = Services.Worlds.Value.Find(plate.Level)?.NameIt ?? plate.Level;
        DrawWrapped(batch, $"Trovata: {foundIn}.", area.X + 7, area.Bottom - 26, area.Width - 14, 1, Dim);
        DrawWrapped(batch, "Dal minuto 8 il primo forziere ti offre una seconda Lastra.", area.X + 7, area.Bottom + 8, area.Width - 14, 2, Dim);
    }
}
