using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The shop's upgrades for every run (phase 6.2), like the PowerUps of Vampire Survivors: the sixth move slot, Fortuna's
/// fourth card, more Rilancio, Salta and Bandisci (those need medals), and small bonuses. A tile per upgrade with its
/// grades; below, what the selected one does. [R] gives back every coin spent here.
/// </summary>
public sealed class UpgradesScene(GameServices services) : MenuScene(services)
{
    private const int Columns = 4;

    private int _cursor;

    private IReadOnlyList<UpgradeSpec> Upgrades => Meta.Shop.Upgrades;

    /// <summary>Previews: the upgrade under the cursor.</summary>
    public int Cursor
    {
        get => _cursor;
        set => _cursor = Math.Clamp(value, 0, Upgrades.Count - 1);
    }

    protected override void UpdateMenu()
    {
        if (Left() && _cursor > 0)
            _cursor--;
        if (Right() && _cursor < Upgrades.Count - 1)
            _cursor++;
        if (Up() && _cursor >= Columns)
            _cursor -= Columns;
        if (Down() && _cursor + Columns < Upgrades.Count)
            _cursor += Columns;
        if (Pointed("griglia") is { } pointed)
            _cursor = pointed;

        // A click on the selected tile buys it, as [INVIO]; on another one it selects it.
        var clicked = ClickedOn("griglia");
        if (clicked is { } tile && tile != _cursor)
        {
            _cursor = tile;
            return;
        }

        var upgrade = Upgrades[_cursor];
        if (Back())
            Services.Switch(new MainMenuScene(Services, cursor: 1));
        else if (Pressed(Keys.R, Buttons.X))
        {
            if (Meta.Data.UpgradeCoins == 0)
                Notice("NESSUN POTENZIAMENTO DA RIMBORSARE", Dim);
            else
                Ask("Rimborsare tutti i potenziamenti?", $"TORNANO {UiFormat.Number(Meta.Data.UpgradeCoins)} MONETE",
                    () => Notice($"RIMBORSATE {UiFormat.Number(Meta.RefundUpgrades())} MONETE", Green));
        }
        else if (clicked is not null || Confirm())
        {
            if (Meta.UpgradePrice(upgrade) is not { } price)
                Notice("GRADI AL MASSIMO", Dim);
            else if (Meta.Medals < Meta.MedalsNeeded(upgrade))
                Notice($"SERVONO {Medals(Meta.MedalsNeeded(upgrade))}: SCONFIGGI I CAPIPALESTRA IN SFIDA", Red);
            else
                AskToBuy(upgrade.NameIt, price, () => Meta.BuyUpgrade(upgrade), $"{upgrade.NameIt.ToUpperInvariant()} POTENZIATO!");
        }
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        DrawHeader(batch, "POTENZIAMENTI");
        UiDraw.Text(batch, Small, "VALGONO IN OGNI PARTITA, CON OGNI POKÉMON E ALLENATORE", new Vector2(16, 28), Dim);

        for (var i = 0; i < Upgrades.Count; i++)
        {
            var tile = new Rectangle(16 + i % Columns * 152, 44 + i / Columns * 92, 144, 84);
            DrawTile(batch, Upgrades[i], i, tile, i == _cursor);
        }

        var selected = Upgrades[_cursor];
        var panel = new Rectangle(16, 236, 608, 92);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        UiDraw.Text(batch, Body, selected.NameIt, new Vector2(panel.X + 10, panel.Y + 6), Paper);
        DrawWrapped(batch, selected.DescriptionIt, panel.X + 10, panel.Y + 28, 380, 2, Dim);

        var grade = Meta.UpgradeGrade(selected);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"GRADO {grade}/{selected.MaxGrade}", new Vector2(panel.X + 10, panel.Y + 52), grade > 0 ? Green : Dim);
        if (Meta.MedalsNeeded(selected) is > 0 and var medals)
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"IL PROSSIMO GRADO VUOLE {Medals(medals)} (NE HAI {Meta.Medals})",
                new Vector2(panel.X + 10, panel.Y + 64), Meta.Medals >= medals ? Dim : Red);

        if (Meta.Data.UpgradeCoins > 0)
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"SPESE QUI: {UiFormat.Number(Meta.Data.UpgradeCoins)} MONETE, [R] PER RIAVERLE",
                new Vector2(panel.Right - 10 - UiDraw.Measure(Small, $"SPESE QUI: {UiFormat.Number(Meta.Data.UpgradeCoins)} MONETE, [R] PER RIAVERLE").X, panel.Y + 76), Dim);

        DrawFooter(batch, "[FRECCE] SCEGLI     [INVIO] COMPRA     [R] RIMBORSA TUTTO     [ESC] INDIETRO");
        if (Meta.UpgradePrice(selected) is not null)
            KeyHints.Draw(batch, Services, "[INVIO] COMPRA", new Vector2(panel.Right - 10 - KeyHints.Measure(Services, "[INVIO] COMPRA"), panel.Y + 10), Gold);
    }

    private void DrawTile(SpriteBatch batch, UpgradeSpec upgrade, int index, Rectangle tile, bool selected)
    {
        var p = Services.Primitives;
        var grade = Meta.UpgradeGrade(upgrade);
        var maxed = grade >= upgrade.MaxGrade;
        Clickable(tile, "griglia", index);
        UiDraw.Frame(batch, p, tile, Services.Style.Theme);
        if (selected)
            DrawCursor(batch, tile);
        else
            DrawHover(batch, tile);

        var icon = new Rectangle(tile.X + 8, tile.Y + 8, 28, 28);
        UiDraw.Frame(batch, p, icon, maxed ? new UiTheme(new Color(60, 48, 24, 235), Gold, new Color(140, 110, 40), Ink, Paper, Dim) : Services.Style.Theme);
        if (upgrade.Icon is { } key)
            Services.ItemIcons.Draw(batch, key, new Rectangle(icon.X + 2, icon.Y + 2, 24, 24));
        else
            DrawKeyBadge(batch, upgrade.Kind, icon);

        UiDraw.Text(batch, Body, upgrade.NameIt, new Vector2(tile.X + 42, tile.Y + 8), grade > 0 ? Paper : Dim);
        DrawPips(batch, tile.X + 43, tile.Y + 30, grade, upgrade.MaxGrade, 5);
        DrawWrapped(batch, Effect(upgrade, grade), tile.X + 10, tile.Y + 44, tile.Width - 20, 2, grade > 0 ? Green : new Color(110, 104, 140));

        var bottom = tile.Bottom - 16;
        if (maxed)
            UiDraw.Text(batch, Small, "AL MASSIMO", new Vector2(tile.X + 10, bottom), Gold);
        else if (Meta.MedalsNeeded(upgrade) is > 0 and var medals && Meta.Medals < medals)
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, Medals(medals), new Vector2(tile.X + 10, bottom), Red);
        else if (Meta.UpgradePrice(upgrade) is { } price)
            DrawPrice(batch, price, tile.Right - 10, bottom);
    }

    private static string Medals(int count) => count == 1 ? "1 MEDAGLIA" : $"{count} MEDAGLIE";

    /// <summary>What the upgrade gives now, in a few words.</summary>
    private static string Effect(UpgradeSpec upgrade, int grade) => upgrade.Kind switch
    {
        UpgradeKind.MoveSlot => grade > 0 ? "ORA: 6 MOSSE PER PARTITA" : "ORA: 5 MOSSE PER PARTITA",
        UpgradeKind.Choices => grade > 0 ? "ORA: 4 CARTE AL LEVEL-UP" : "ORA: 3 CARTE AL LEVEL-UP",
        UpgradeKind.Rerolls => $"ORA: {Core.Run.RunState.StartingLevelUpCharges + grade} RILANCI PER PARTITA",
        UpgradeKind.Skips => $"ORA: {Core.Run.RunState.StartingLevelUpCharges + grade} SALTA PER PARTITA",
        UpgradeKind.Banishes => $"ORA: {Core.Run.RunState.StartingLevelUpCharges + grade} BANDISCI PER PARTITA",
        _ => grade > 0 ? $"ORA: {Core.Run.Progression.LevelUp.Describe(upgrade.PerGrade * grade).TrimEnd('.').ToUpperInvariant()}" : "NON ANCORA COMPRATO",
    };

    /// <summary>Rilancio, Salta and Bandisci have no item: the key they use in the level-up window, in a badge.</summary>
    private void DrawKeyBadge(SpriteBatch batch, UpgradeKind kind, Rectangle box)
    {
        var letter = kind switch { UpgradeKind.Rerolls => "R", UpgradeKind.Skips => "X", _ => "B" };
        var color = kind == UpgradeKind.Banishes ? new Color(232, 72, 56) : kind == UpgradeKind.Skips ? new Color(120, 180, 255) : Gold;
        var inner = new Rectangle(box.X + 5, box.Y + 5, box.Width - 10, box.Height - 10);
        Services.Primitives.Rect(batch, inner, Color.Lerp(color, Ink, 0.45f));
        Services.Primitives.Outline(batch, inner, color, 1);
        var size = UiDraw.Measure(Body, letter);
        UiDraw.TextOutlined(batch, Body, letter, new Vector2(inner.Center.X - size.X / 2f, inner.Center.Y - size.Y / 2f - 1), Paper, Ink);
    }
}
