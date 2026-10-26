using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Ui;

public enum PauseChoice
{
    Resume,
    Restart,
    Quit,
}

/// <summary>
/// Pause (Esc), phase 3.6: on the left resume, two settings (damage numbers, full screen), restart and quit, with the
/// current build; on the right the map of the level around the player, with what is still there to find. The full
/// pause screen comes in 8.4.
/// </summary>
public sealed class PauseMenu(GameServices services, Hud hud)
{
    private enum Row
    {
        Resume,
        DamageNumbers,
        FullScreen,
        Restart,
        Quit,
    }

    private static readonly Row[] Rows = Enum.GetValues<Row>();

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color MapGrass = new(28, 58, 38);
    private static readonly Color MapExplored = new(54, 100, 62);
    private static readonly Color MapGrid = new(70, 120, 76);

    /// <summary>World pixels per map pixel.</summary>
    private const float MapScale = 12f;

    private int _age;

    public bool IsOpen { get; private set; }

    public int Cursor { get; private set; }

    public void Open()
    {
        IsOpen = true;
        Cursor = 0;
        _age = 0;
    }

    public void Close() => IsOpen = false;

    public void MoveCursor(int delta) => Cursor = (Cursor + delta + Rows.Length) % Rows.Length;

    public PauseChoice? Update(InputState? input)
    {
        if (!IsOpen)
            return null;

        _age++;
        if (input is null || _age < 2)
            return null;

        if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.Start) || input.Pressed(Buttons.B))
            return PauseChoice.Resume;
        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W) || input.Pressed(Buttons.DPadUp))
            MoveCursor(-1);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S) || input.Pressed(Buttons.DPadDown))
            MoveCursor(1);

        var toggle = input.Pressed(Keys.Left) || input.Pressed(Keys.Right) || input.Pressed(Keys.A) || input.Pressed(Keys.D);
        var confirm = input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A);
        if (!confirm && !toggle)
            return null;

        var settings = services.Settings;
        switch (Rows[Cursor])
        {
            case Row.DamageNumbers:
                settings.ShowDamageNumbers = !settings.ShowDamageNumbers;
                settings.Save();
                return null;
            case Row.FullScreen:
                services.SetFullScreen(!settings.FullScreen);
                return null;
            case Row.Resume when confirm:
                return PauseChoice.Resume;
            case Row.Restart when confirm:
                return PauseChoice.Restart;
            case Row.Quit when confirm:
                return PauseChoice.Quit;
            default:
                return null;
        }
    }

    public void Draw(SpriteBatch batch, RunState run, ExploredMap explored)
    {
        if (!IsOpen)
            return;

        var p = services.Primitives;
        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * 0.6f);

        DrawMenu(batch, run, new Rectangle(14, 36, 232, 292));
        DrawMap(batch, run, explored, new Rectangle(256, 36, 370, 292));

        var small = services.Fonts.Small(services.Style);
        UiDraw.TextCentered(batch, small, "[ESC] RIPRENDI     [SU/GIU] SCEGLI     [INVIO] CONFERMA     [SINISTRA/DESTRA] CAMBIA", services.Screen.Width / 2f, 338, Dim, Ink);
        batch.End();
    }

    private void DrawMenu(SpriteBatch batch, RunState run, Rectangle panel)
    {
        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        var body = services.Fonts.Body(services.Style);
        var numbers = services.Fonts.Numbers(services.Style);
        var settings = services.Settings;

        UiDraw.Frame(batch, p, panel, services.Style.Theme);
        UiDraw.TextCentered(batch, services.Fonts.Title(services.Style), "PAUSA", panel.Center.X, panel.Y + 6, Paper, Ink);
        UiDraw.TextCentered(batch, numbers, $"Lv {run.Player.Level}   {UiFormat.Clock(run.Ticks)}", panel.Center.X, panel.Y + 44, Dim);

        for (var i = 0; i < Rows.Length; i++)
        {
            var row = new Rectangle(panel.X + 14, panel.Y + 70 + i * 22, panel.Width - 28, 19);
            var selected = i == Cursor;
            if (selected)
            {
                p.Rect(batch, row, Color.White * 0.08f);
                p.Outline(batch, row, _age / 8 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.6f), 1);
            }

            var color = selected ? Paper : Dim;
            switch (Rows[i])
            {
                case Row.DamageNumbers:
                    DrawSetting(batch, row, "Numeri dei danni", settings.ShowDamageNumbers, color);
                    break;
                case Row.FullScreen:
                    DrawSetting(batch, row, "Schermo intero", settings.FullScreen, color);
                    break;
                default:
                    var label = Rows[i] switch { Row.Resume => "Riprendi", Row.Restart => "Ricomincia", _ => "Esci dal gioco" };
                    UiDraw.TextCentered(batch, body, label, row.Center.X, row.Y + 2, color);
                    break;
            }
        }

        // Current build: five moves (the sixth slot is locked until the meta-progression), six held items.
        UiDraw.Text(batch, small, "SQUADRA", new Vector2(panel.X + 14, panel.Y + 186), Dim);
        for (var i = 0; i < Hud.Slots; i++)
        {
            var moveSlot = new Rectangle(panel.X + 14 + i * 35, panel.Y + 198, 24, 24);
            if (i < LevelUp.MaxMoves)
                hud.DrawMoveSlot(batch, moveSlot, i < run.Player.Moves.Count ? run.Player.Moves[i] : null);
            else
                hud.DrawLockedSlot(batch, moveSlot);

            var itemSlot = new Rectangle(panel.X + 14 + i * 35, panel.Y + 228, 24, 24);
            UiDraw.Frame(batch, p, itemSlot, services.Style.Theme);
            if (i < run.Player.Items.Count)
            {
                var item = run.Player.Items[i];
                hud.DrawItemIcon(batch, item.Definition.IconKey, itemSlot);
                hud.Pips(batch, itemSlot, item.Level, item.Definition.MaxLevel);
            }
        }
    }

    /// <summary>A setting row: its name on the left, SÌ or NO on the right (gold when on).</summary>
    private void DrawSetting(SpriteBatch batch, Rectangle row, string label, bool on, Color color)
    {
        var body = services.Fonts.Body(services.Style);
        UiDraw.Text(batch, body, label, new Vector2(row.X + 8, row.Y + 2), color);
        var value = on ? "SÌ" : "NO";
        UiDraw.Text(batch, body, value, new Vector2(row.Right - 8 - UiDraw.Measure(body, value).X, row.Y + 2), on ? Gold : Dim);
    }

    /// <summary>
    /// The map around the player: the ground already walked lighter, the start as a cross, and what is still there to
    /// find: held items (gold diamonds), other items (white dots), chests, Dominanti, shinies and the boss.
    /// What lies beyond the edge is pinned to the edge, faded.
    /// </summary>
    private void DrawMap(SpriteBatch batch, RunState run, ExploredMap explored, Rectangle panel)
    {
        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        UiDraw.Frame(batch, p, panel, services.Style.Theme);
        UiDraw.Text(batch, services.Fonts.Body(services.Style), "MAPPA", new Vector2(panel.X + 10, panel.Y + 5), Paper);
        var name = run.Spawner.Timeline.NameIt.ToUpperInvariant();
        UiDraw.Text(batch, small, name, new Vector2(panel.Right - 10 - UiDraw.Measure(small, name).X, panel.Y + 9), Dim);

        var area = new Rectangle(panel.X + 8, panel.Y + 24, panel.Width - 16, panel.Height - 46);
        var centre = run.Player.Position;
        p.Rect(batch, area, MapGrass);

        Vector2 ToMap(System.Numerics.Vector2 world) =>
            new(area.Center.X + (world.X - centre.X) / MapScale, area.Center.Y + (world.Y - centre.Y) / MapScale);

        // Walked ground.
        var cellPixels = ExploredMap.CellSize / MapScale;
        foreach (var (cx, cy) in explored.Cells)
        {
            var at = ToMap(new System.Numerics.Vector2(cx * ExploredMap.CellSize, cy * ExploredMap.CellSize));
            var cell = new Rectangle((int)at.X, (int)at.Y, (int)MathF.Ceiling(cellPixels), (int)MathF.Ceiling(cellPixels));
            cell = Rectangle.Intersect(cell, area);
            if (cell.Width > 0 && cell.Height > 0)
                p.Rect(batch, cell, MapExplored);
        }

        // A faint grid every 500 px of world, so distances read.
        const float grid = 500f;
        var left = MathF.Floor((centre.X - area.Width / 2f * MapScale) / grid) * grid;
        for (var x = left; x < centre.X + area.Width / 2f * MapScale; x += grid)
        {
            var mx = (int)ToMap(new System.Numerics.Vector2(x, 0)).X;
            if (mx > area.X && mx < area.Right)
                p.Rect(batch, new Rectangle(mx, area.Y, 1, area.Height), MapGrid * 0.35f);
        }

        var top = MathF.Floor((centre.Y - area.Height / 2f * MapScale) / grid) * grid;
        for (var y = top; y < centre.Y + area.Height / 2f * MapScale; y += grid)
        {
            var my = (int)ToMap(new System.Numerics.Vector2(0, y)).Y;
            if (my > area.Y && my < area.Bottom)
                p.Rect(batch, new Rectangle(area.X, my, area.Width, 1), MapGrid * 0.35f);
        }

        // The start.
        var start = ToMap(System.Numerics.Vector2.Zero);
        if (area.Contains(start))
        {
            UiDraw.Line(batch, p, start + new Vector2(-3, -3), start + new Vector2(3, 3), Paper * 0.6f);
            UiDraw.Line(batch, p, start + new Vector2(-3, 3), start + new Vector2(3, -3), Paper * 0.6f);
        }

        foreach (var loot in run.Loot.Items)
        {
            if (loot.Kind is LootKind.Coin or LootKind.Nugget)
                continue;
            if (loot.Kind == LootKind.HeldItem)
                Marker(batch, area, ToMap(loot.Position), EnemyVfx.ShinyGold, MarkerShape.Diamond);
            else
                Marker(batch, area, ToMap(loot.Position), Paper, MarkerShape.Dot);
        }

        foreach (var chest in run.Pickups.Chests)
            Marker(batch, area, ToMap(chest.Position), RunRenderer.ChestColor(chest.Tier), MarkerShape.Ball);

        foreach (ref readonly var e in run.Enemies.Items)
        {
            if (!e.IsAlive)
                continue;
            if (e.Rank == EnemyRank.Boss)
                Marker(batch, area, ToMap(e.Position), EnemyVfx.AuraRed, MarkerShape.Big);
            else if (e.Rank == EnemyRank.Dominant)
                Marker(batch, area, ToMap(e.Position), EnemyVfx.AuraRed, MarkerShape.Ball);
            else if (e.Shiny)
                Marker(batch, area, ToMap(e.Position), EnemyVfx.ShinyGold, MarkerShape.Star);
        }

        // The player, blinking gently.
        var you = ToMap(centre);
        p.Rect(batch, new Rectangle((int)you.X - 3, (int)you.Y - 3, 7, 7), Ink);
        p.Rect(batch, new Rectangle((int)you.X - 2, (int)you.Y - 2, 5, 5), _age / 20 % 2 == 0 ? new Color(255, 230, 80) : new Color(255, 250, 200));

        // Legend.
        var y0 = area.Bottom + 6;
        var x0 = area.X + 2;
        foreach (var (label, color, shape) in new (string, Color, MarkerShape)[]
                 {
                     ("TU", new Color(255, 230, 80), MarkerShape.Ball),
                     ("STRUMENTO", EnemyVfx.ShinyGold, MarkerShape.Diamond),
                     ("OGGETTO", Paper, MarkerShape.Dot),
                     ("FORZIERE", RunRenderer.ChestColor(ChestTier.Poke), MarkerShape.Ball),
                     ("DOMINANTE", EnemyVfx.AuraRed, MarkerShape.Ball),
                     ("CROMATICO", EnemyVfx.ShinyGold, MarkerShape.Star),
                 })
        {
            Marker(batch, new Rectangle(0, 0, 9999, 9999), new Vector2(x0 + 3, y0 + 4), color, shape);
            UiDraw.Text(batch, small, label, new Vector2(x0 + 9, y0), Dim);
            x0 += 14 + (int)UiDraw.Measure(small, label).X;
        }
    }

    private enum MarkerShape
    {
        Dot,
        Ball,
        Diamond,
        Star,
        Big,
    }

    /// <summary>A map marker; outside <paramref name="area"/> it is pinned to the edge, faded.</summary>
    private void Marker(SpriteBatch batch, Rectangle area, Vector2 at, Color color, MarkerShape shape)
    {
        var p = services.Primitives;
        var inside = area.Contains(at);
        if (!inside)
        {
            at = new Vector2(MathHelper.Clamp(at.X, area.X + 3, area.Right - 4), MathHelper.Clamp(at.Y, area.Y + 3, area.Bottom - 4));
            color *= 0.55f;
        }

        var x = (int)at.X;
        var y = (int)at.Y;
        switch (shape)
        {
            case MarkerShape.Dot:
                p.Rect(batch, new Rectangle(x - 1, y - 1, 3, 3), color);
                break;
            case MarkerShape.Ball:
                p.Rect(batch, new Rectangle(x - 3, y - 2, 6, 5), Ink);
                p.Rect(batch, new Rectangle(x - 2, y - 3, 4, 7), Ink);
                p.Rect(batch, new Rectangle(x - 2, y - 2, 4, 5), color);
                break;
            case MarkerShape.Diamond:
                p.Rect(batch, new Rectangle(x, y - 3, 1, 7), color);
                p.Rect(batch, new Rectangle(x - 1, y - 2, 3, 5), color);
                p.Rect(batch, new Rectangle(x - 2, y - 1, 5, 3), color);
                p.Rect(batch, new Rectangle(x - 3, y, 7, 1), color);
                break;
            case MarkerShape.Star:
                p.Rect(batch, new Rectangle(x, y - 3, 1, 7), color);
                p.Rect(batch, new Rectangle(x - 3, y, 7, 1), color);
                p.Rect(batch, new Rectangle(x - 1, y - 1, 3, 3), color);
                break;
            case MarkerShape.Big:
                p.Rect(batch, new Rectangle(x - 5, y - 5, 11, 11), Ink);
                p.Rect(batch, new Rectangle(x - 4, y - 4, 9, 9), _age / 10 % 2 == 0 ? color : Color.Lerp(color, Color.White, 0.4f));
                break;
        }
    }
}
