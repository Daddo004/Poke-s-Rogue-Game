using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The collection (phase 6.4), in four sections switched with [TAB]: the Pokédex by region (species seen and defeated),
/// the moves and the held items used at least once, and the missions with their progress and rewards.
/// </summary>
public sealed class CollectionScene : MenuScene
{
    public enum Section
    {
        Pokedex,
        Moves,
        Items,
        Missions,
    }

    private static readonly string[] SectionNames = ["POKÉDEX", "MOSSE", "STRUMENTI", "MISSIONI"];
    private static readonly TrainerRegion[] Regions = Enum.GetValues<TrainerRegion>();
    private static readonly Color UnknownText = new(44, 40, 66);
    private static readonly Color SeenTint = new(70, 64, 104);

    private const int DexColumns = 16;
    private const int DexCell = 22;
    private const int GridColumns = 6;

    /// <summary>Held items are few and some names long (Saggiocchiali): four to a row.</summary>
    private const int ItemColumns = 4;
    private const int MissionRows = 9;

    private readonly IReadOnlyList<MoveDefinition> _moves;
    private readonly IReadOnlyList<Core.Run.Items.ItemDefinition> _items;
    private Section _section;
    private int _region;
    private int _cursor;
    private int _scroll;

    public CollectionScene(GameServices services, Section section = Section.Pokedex) : base(services)
    {
        _section = section;
        // Base moves first, then the evolutions, as in the album.
        _moves = services.Moves.Value.Enabled.Concat(services.Moves.Value.Evolutions).ToList();
        _items = services.Items.Value.Enabled;
    }

    /// <summary>Previews: the section, region page and cursor shown.</summary>
    public void Show(Section section, int region = 0, int cursor = 0)
    {
        _section = section;
        _region = region;
        _cursor = cursor;
        _scroll = Math.Max(0, cursor - MissionRows + 1);
    }

    private int Count => _section switch
    {
        Section.Pokedex => RegionSize(Regions[_region]),
        Section.Moves => _moves.Count,
        Section.Items => _items.Count,
        _ => Meta.Missions.All.Count,
    };

    private static int RegionSize(TrainerRegion region)
    {
        var (from, to) = MetaProgress.RegionRange(region);
        return to - from + 1;
    }

    protected override void UpdateMenu()
    {
        if (Back())
        {
            Services.Switch(new MainMenuScene(Services, cursor: 2));
            return;
        }

        if (Pressed(Keys.Tab, Buttons.RightShoulder))
            Show((Section)(((int)_section + 1) % SectionNames.Length));
        if (Pressed(Keys.LeftShift, Buttons.LeftShoulder))
            Show((Section)(((int)_section + SectionNames.Length - 1) % SectionNames.Length));
        if (_section == Section.Pokedex && Pressed(Keys.E))
            Show(Section.Pokedex, (_region + 1) % Regions.Length);
        if (_section == Section.Pokedex && Pressed(Keys.Q))
            Show(Section.Pokedex, (_region + Regions.Length - 1) % Regions.Length);

        var columns = _section switch { Section.Pokedex => DexColumns, Section.Missions => 1, Section.Items => ItemColumns, _ => GridColumns };
        var move = (Left() ? -1 : 0) + (Right() ? 1 : 0) + (Up() ? -columns : 0) + (Down() ? columns : 0);
        if (_section == Section.Missions)
            move = (Up() || Left() ? -1 : 0) + (Down() || Right() ? 1 : 0);
        if (move != 0 && _cursor + move >= 0 && _cursor + move < Count)
            _cursor += move;

        if (_cursor < _scroll)
            _scroll = _cursor;
        if (_cursor >= _scroll + MissionRows)
            _scroll = _cursor - MissionRows + 1;
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        DrawHeader(batch, "COLLEZIONE");
        var p = Services.Primitives;
        for (var i = 0; i < SectionNames.Length; i++)
        {
            var tab = new Rectangle(150 + i * 96, 10, 90, 16);
            var active = (int)_section == i;
            UiDraw.Frame(batch, p, tab, active ? new UiTheme(new Color(60, 48, 24, 235), Gold, new Color(140, 110, 40), Ink, Paper, Dim) : Services.Style.Theme);
            UiDraw.TextCentered(batch, Small, SectionNames[i], tab.Center.X, tab.Y + 4, active ? Gold : Dim);
        }

        switch (_section)
        {
            case Section.Pokedex:
                DrawPokedex(batch);
                DrawFooter(batch, "[TAB] SEZIONE     [Q/E] REGIONE     [FRECCE] SCEGLI     [ESC] INDIETRO");
                break;
            case Section.Moves:
                DrawMoves(batch);
                DrawFooter(batch, "[TAB] SEZIONE     [FRECCE] SCEGLI     [ESC] INDIETRO");
                break;
            case Section.Items:
                DrawItems(batch);
                DrawFooter(batch, "[TAB] SEZIONE     [FRECCE] SCEGLI     [ESC] INDIETRO");
                break;
            default:
                DrawMissions(batch);
                DrawFooter(batch, "[TAB] SEZIONE     [SU/GIÙ] SCORRI     [ESC] INDIETRO");
                break;
        }
    }

    // ---- Pokédex ----

    private void DrawPokedex(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var region = Regions[_region];
        var (from, to) = MetaProgress.RegionRange(region);
        var dex = Meta.Data.Pokedex;
        var seen = Enumerable.Range(from, to - from + 1).Count(id => dex.GetValueOrDefault(id)?.Seen > 0);
        var defeated = Enumerable.Range(from, to - from + 1).Count(id => dex.GetValueOrDefault(id)?.Defeated > 0);

        var grid = new Rectangle(16, 36, DexColumns * DexCell + 8, 10 * DexCell + 36);
        UiDraw.Frame(batch, p, grid, Services.Style.Theme);
        UiDraw.Text(batch, Body, region.ToString(), new Vector2(grid.X + 8, grid.Y + 4), Paper);
        var counts = $"VISTI {seen}   SCONFITTI {defeated}   SU {to - from + 1}";
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, counts, new Vector2(grid.Right - 8 - UiDraw.Measure(Small, counts).X, grid.Y + 9), Dim);

        for (var i = 0; i <= to - from; i++)
        {
            var id = from + i;
            var cell = new Rectangle(grid.X + 4 + i % DexColumns * DexCell, grid.Y + 26 + i / DexColumns * DexCell, DexCell - 2, DexCell - 2);
            var entry = dex.GetValueOrDefault(id);
            p.Rect(batch, cell, entry is { Seen: > 0 } ? new Color(40, 34, 64) : new Color(28, 24, 44));
            if (entry is { Seen: > 0 } && Services.Sprites.Portrait(id) is { } portrait)
                batch.Draw(portrait, cell, entry.Defeated > 0 ? Color.White : SeenTint);
            if (i == _cursor)
                DrawCursor(batch, cell);
        }

        // Legend under the grid.
        var y = grid.Bottom + 8;
        p.Rect(batch, new Rectangle(grid.X + 4, y, 8, 8), new Color(28, 24, 44));
        UiDraw.Text(batch, Small, "NON INCONTRATO", new Vector2(grid.X + 16, y), Dim);
        p.Rect(batch, new Rectangle(grid.X + 110, y, 8, 8), SeenTint);
        UiDraw.Text(batch, Small, "VISTO", new Vector2(grid.X + 122, y), Dim);
        p.Rect(batch, new Rectangle(grid.X + 170, y, 8, 8), Gold);
        UiDraw.Text(batch, Small, "SCONFITTO (A COLORI)", new Vector2(grid.X + 182, y), Dim);

        DrawSpecies(batch, from + _cursor, new Rectangle(grid.Right + 8, 36, Services.Screen.Width - 16 - grid.Right - 8, grid.Height));
    }

    private void DrawSpecies(SpriteBatch batch, int id, Rectangle panel)
    {
        var p = Services.Primitives;
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        var species = Services.Species(id);
        var entry = Meta.Data.Pokedex.GetValueOrDefault(id);
        var known = entry is { Seen: > 0 };
        var x = panel.X + 10;

        var frame = new Rectangle(panel.Center.X - 42, panel.Y + 10, 84, 84);
        UiDraw.Frame(batch, p, frame, Services.Style.Theme);
        if (known && Services.Sprites.Portrait(id) is { } portrait)
            batch.Draw(portrait, new Rectangle(frame.X + 2, frame.Y + 2, 80, 80), entry!.Defeated > 0 ? Color.White : SeenTint);
        else
            UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "?", frame.Center.X, frame.Y + 24, Dim);

        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"#{id:D3}", new Vector2(x, panel.Y + 104), Dim);
        if (!known || species is null)
        {
            UiDraw.Text(batch, Body, "???", new Vector2(x, panel.Y + 114), Dim);
            UiDraw.Text(batch, Small, "NON ANCORA INCONTRATO", new Vector2(x, panel.Y + 138), Dim);
            return;
        }

        UiDraw.Text(batch, Body, species.NameIt, new Vector2(x, panel.Y + 114), Paper);
        UiDraw.Text(batch, Small, species.GenusIt.ToUpperInvariant(), new Vector2(x, panel.Y + 134), Dim);
        for (var i = 0; i < species.Types.Count; i++)
            DrawTypePill(batch, new Rectangle(x + i * 52, panel.Y + 146, 48, 11), species.Types[i]);

        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"VISTO {UiFormat.Number(entry!.Seen)} VOLTE", new Vector2(x, panel.Y + 168), Paper);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"SCONFITTO {UiFormat.Number(entry.Defeated)} VOLTE", new Vector2(x, panel.Y + 180), entry.Defeated > 0 ? Paper : Dim);

        if (Services.Characters.WithSpecies(id) is { } character)
        {
            var unlocked = Meta.IsUnlocked(character);
            UiDraw.Text(batch, Small, "GIOCABILE", new Vector2(x, panel.Y + 200), Gold);
            DrawWrapped(batch, $"NELLA LINEA DI {character.NameIt.ToUpperInvariant()}, {(unlocked ? "GIÀ TUA" : "ANCORA DA SBLOCCARE")}.",
                x, panel.Y + 212, panel.Width - 20, 2, unlocked ? Green : Dim);
        }
    }

    // ---- Moves and items ----

    private void DrawMoves(SpriteBatch batch)
    {
        var found = Meta.Data.MovesFound.ToHashSet();
        DrawGrid(batch, _moves.Count, GridColumns, $"MOSSE USATE {_moves.Count(m => found.Contains(m.Id))}/{_moves.Count}", i =>
        {
            var move = _moves[i];
            return (found.Contains(move.Id), move.NameIt, (batch2, area) => Services.MoveIcons.Draw(batch2, Services.Primitives, area, move.Id, move.Type), move.IsEvolution);
        });

        var selected = _moves[_cursor];
        var panel = DetailPanel(batch);
        var x = panel.X + 10;
        if (!found.Contains(selected.Id))
        {
            Unknown(batch, panel, selected.IsEvolution ? "MOSSA EVOLUTA NON ANCORA OTTENUTA" : "MOSSA NON ANCORA USATA");
            return;
        }

        var icon = new Rectangle(x, panel.Y + 10, 36, 36);
        UiDraw.Frame(batch, Services.Primitives, icon, Services.Style.Theme);
        Services.MoveIcons.Draw(batch, Services.Primitives, new Rectangle(icon.X + 2, icon.Y + 2, 32, 32), selected.Id, selected.Type, glyphScale: 2);
        UiDraw.Text(batch, Body, selected.NameIt, new Vector2(x + 44, panel.Y + 10), selected.IsEvolution ? Gold : Paper);
        DrawTypePill(batch, new Rectangle(x + 44, panel.Y + 30, 48, 11), selected.Type);
        var category = selected.Category switch { MoveCategory.Physical => "FISICA", MoveCategory.Special => "SPECIALE", _ => "STATO" };
        UiDraw.Text(batch, Small, category, new Vector2(x + 98, panel.Y + 32), Dim);
        DrawWrapped(batch, selected.DescriptionIt, x, panel.Y + 56, panel.Width - 20, 6, Paper);
        if (selected.IsEvolution && selected.EvolvesFrom is { } baseId)
        {
            var item = selected.EvolveItem is { } itemId ? Services.Items.Value.All.FirstOrDefault(d => d.Id == itemId)?.NameIt : null;
            UiDraw.Text(batch, Small, "EVOLUZIONE", new Vector2(x, panel.Y + 130), Gold);
            DrawWrapped(batch, $"{Services.Moves.Value[baseId].NameIt} al massimo, con {item ?? "il suo strumento"}, da un forziere.", x, panel.Y + 142, panel.Width - 20, 3, Dim);
        }
    }

    private void DrawItems(SpriteBatch batch)
    {
        var found = Meta.Data.ItemsFound.ToHashSet();
        DrawGrid(batch, _items.Count, ItemColumns, $"STRUMENTI USATI {_items.Count(i => found.Contains(i.Id))}/{_items.Count}", i =>
        {
            var item = _items[i];
            return (found.Contains(item.Id), item.NameIt, (batch2, area) => Services.ItemIcons.Draw(batch2, item.IconKey, area), false);
        });

        var selected = _items[_cursor];
        var panel = DetailPanel(batch);
        var x = panel.X + 10;
        if (!found.Contains(selected.Id))
        {
            Unknown(batch, panel, "STRUMENTO NON ANCORA USATO");
            return;
        }

        var icon = new Rectangle(x, panel.Y + 10, 36, 36);
        UiDraw.Frame(batch, Services.Primitives, icon, Services.Style.Theme);
        Services.ItemIcons.Draw(batch, selected.IconKey, new Rectangle(icon.X + 6, icon.Y + 6, 24, 24));
        UiDraw.Text(batch, Body, selected.NameIt, new Vector2(x + 44, panel.Y + 10), Paper);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"LIVELLO MASSIMO {selected.MaxLevel}", new Vector2(x + 44, panel.Y + 32), Dim);
        DrawWrapped(batch, selected.DescriptionIt, x, panel.Y + 56, panel.Width - 20, 3, Paper);
        DrawWrapped(batch, $"Per livello: {Core.Run.Progression.LevelUp.Describe(selected.PerLevel)}", x, panel.Y + 92, panel.Width - 20, 4, Dim);
    }

    /// <summary>A grid of tiles, <paramref name="columns"/> to a row: the icon and the name of what was found, a dark "???" for the rest.</summary>
    private void DrawGrid(SpriteBatch batch, int count, int columns, string title, Func<int, (bool Found, string Name, Action<SpriteBatch, Rectangle> Icon, bool Gold)> tile)
    {
        var p = Services.Primitives;
        var grid = new Rectangle(16, 36, 384, 300);
        UiDraw.Frame(batch, p, grid, Services.Style.Theme);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, title, new Vector2(grid.X + 8, grid.Y + 8), Dim);
        for (var i = 0; i < count; i++)
        {
            var (found, name, icon, gold) = tile(i);
            var pitch = (grid.Width - 12) / columns;
            var cell = new Rectangle(grid.X + 6 + i % columns * pitch, grid.Y + 22 + i / columns * 34, pitch - 4, 30);
            p.Rect(batch, cell, found ? new Color(40, 34, 64) : new Color(28, 24, 44));
            if (found)
            {
                icon(batch, new Rectangle(cell.Center.X - 8, cell.Y + 2, 16, 16));
                var shown = UiDraw.Measure(Small, name).X > cell.Width - 4 ? name[..Math.Min(name.Length, 9)] : name;
                UiDraw.TextCentered(batch, Small, shown.ToUpperInvariant(), cell.Center.X, cell.Y + 20, gold ? Gold : Paper);
            }
            else
            {
                UiDraw.TextCentered(batch, Small, "???", cell.Center.X, cell.Y + 11, UnknownText);
            }

            if (i == _cursor)
                DrawCursor(batch, cell);
        }
    }

    private Rectangle DetailPanel(SpriteBatch batch)
    {
        var panel = new Rectangle(408, 36, 216, 300);
        UiDraw.Frame(batch, Services.Primitives, panel, Services.Style.Theme);
        return panel;
    }

    private void Unknown(SpriteBatch batch, Rectangle panel, string text)
    {
        UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "?", panel.Center.X, panel.Y + 40, Dim);
        UiDraw.TextCentered(batch, Small, text, panel.Center.X, panel.Y + 90, Dim);
    }

    // ---- Missions ----

    private void DrawMissions(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var missions = Meta.Missions.All;
        var done = missions.Count(Meta.IsDone);
        var panel = new Rectangle(16, 36, 608, 300);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"COMPIUTE {done}/{missions.Count}", new Vector2(panel.X + 8, panel.Y + 8), Dim);

        for (var row = 0; row < MissionRows && _scroll + row < missions.Count; row++)
        {
            var i = _scroll + row;
            var mission = missions[i];
            var completed = Meta.IsDone(mission);
            var box = new Rectangle(panel.X + 6, panel.Y + 22 + row * 30, panel.Width - 18, 28);
            p.Rect(batch, box, completed ? new Color(46, 60, 40) : Color.White * 0.04f);
            if (i == _cursor)
                DrawCursor(batch, box);

            // A tick for the done ones, an empty box for the others.
            var check = new Rectangle(box.X + 6, box.Y + 8, 11, 11);
            p.Rect(batch, check, Ink);
            p.Outline(batch, check, completed ? Green : Dim, 1);
            if (completed)
            {
                UiDraw.Line(batch, p, new Vector2(check.X + 2, check.Y + 6), new Vector2(check.X + 4, check.Y + 9), Green, 2f);
                UiDraw.Line(batch, p, new Vector2(check.X + 4, check.Y + 9), new Vector2(check.X + 9, check.Y + 2), Green, 2f);
            }

            UiDraw.Text(batch, Body, mission.NameIt, new Vector2(box.X + 24, box.Y + 1), completed ? Paper : Dim);
            DrawWrapped(batch, mission.DescriptionIt, box.X + 24, box.Y + 18, 330, 1, completed ? Dim : new Color(130, 122, 160));

            var reward = Hud.RewardText(mission, Services);
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, reward, new Vector2(box.Right - 8 - UiDraw.Measure(Small, reward).X, box.Y + 4), completed ? Green : Gold);
            if (!completed)
            {
                var (current, target) = Meta.Progress(mission);
                var bar = new Rectangle(box.Right - 160, box.Y + 17, 80, 6);
                UiDraw.Bar(batch, p, bar, target > 0 ? current / (float)target : 0f, Gold, new Color(40, 32, 20), Ink);
                var text = $"{UiFormat.Number(Math.Min(current, target))}/{UiFormat.Number(target)}";
                UiDraw.Text(batch, SmallNumbers, text, new Vector2(box.Right - 8 - UiDraw.Measure(SmallNumbers, text).X, box.Y + 14), Dim);
            }
        }

        // Scroll bar.
        var track = new Rectangle(panel.Right - 8, panel.Y + 22, 3, MissionRows * 30 - 2);
        p.Rect(batch, track, Color.White * 0.08f);
        var thumb = Math.Max(8, track.Height * MissionRows / Math.Max(MissionRows, missions.Count));
        p.Rect(batch, new Rectangle(track.X, track.Y + (track.Height - thumb) * _scroll / Math.Max(1, missions.Count - MissionRows), 3, thumb), Dim);
    }
}
