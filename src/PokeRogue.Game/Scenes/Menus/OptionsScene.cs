using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// Opzioni from the main menu (phase 8.2): the settings on the left (the same list as in the pause), and on the right the
/// controls of the game, for keyboard, gamepad and mouse.
/// </summary>
public sealed class OptionsScene(GameServices services) : MenuScene(services)
{
    private readonly OptionsPanel _panel = new(services);

    private static readonly (string Action, string Keys, string Pad)[] Controls =
    [
        ("Muoviti", "WASD / FRECCE", "LEVETTA"),
        ("Abilità del Pokémon", "SPAZIO", "X"),
        ("Mossa speciale", "E", "Y"),
        ("Abilità dell'allenatore", "Q", "DORSALI"),
        ("Pausa e mappa", "ESC", "START"),
        ("Level-up: scegli", "1-4 / INVIO", "A"),
        ("Rilancia, salta, bandisci", "R  X  B", ""),
        ("Schermo intero", "ALT+INVIO", ""),
    ];

    /// <summary>Previews: the setting under the cursor.</summary>
    public int Cursor
    {
        get => _panel.Cursor;
        set => _panel.Cursor = value;
    }

    protected override void UpdateMenu()
    {
        if (!_panel.Update(Services.Input))
        {
            Services.Sounds.Play("menu-indietro");
            Services.Switch(new MainMenuScene(Services, cursor: MainMenuScene.OptionsRow));
        }
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        DrawHeader(batch, "OPZIONI");

        var settings = new Rectangle(16, 40, 330, 296);
        UiDraw.Frame(batch, p, settings, Services.Style.Theme);
        UiDraw.Text(batch, Small, "IMPOSTAZIONI", new Vector2(settings.X + 10, settings.Y + 8), Gold);
        _panel.Draw(batch, new Rectangle(settings.X + 8, settings.Y + 24, settings.Width - 16, 230));

        var controls = new Rectangle(354, 40, 270, 296);
        UiDraw.Frame(batch, p, controls, Services.Style.Theme);
        UiDraw.Text(batch, Small, "COMANDI", new Vector2(controls.X + 10, controls.Y + 8), Gold);
        UiDraw.Text(batch, Small, "TASTIERA", new Vector2(controls.X + 150, controls.Y + 8), Dim);
        UiDraw.Text(batch, Small, "PAD", new Vector2(controls.X + 226, controls.Y + 8), Dim);
        for (var i = 0; i < Controls.Length; i++)
        {
            var (action, keys, pad) = Controls[i];
            var y = controls.Y + 26 + i * 22;
            p.Rect(batch, new Rectangle(controls.X + 6, y - 3, controls.Width - 12, 19), Color.White * (i % 2 == 0 ? 0.04f : 0.02f));
            UiDraw.Text(batch, Small, action.ToUpperInvariant(), new Vector2(controls.X + 10, y + 3), Paper);
            UiDraw.Text(batch, Small, keys, new Vector2(controls.X + 150, y + 3), Gold);
            UiDraw.Text(batch, Small, pad, new Vector2(controls.X + 226, y + 3), Dim);
        }

        var mouse = controls.Y + 26 + Controls.Length * 22 + 8;
        UiDraw.Text(batch, Small, "MOUSE", new Vector2(controls.X + 10, mouse), Gold);
        DrawWrapped(batch, "NEI MENU: CLICCA PER SCEGLIERE, TASTO DESTRO PER TORNARE INDIETRO, ROTELLA PER SCORRERE. " +
            "SI CLICCANO ANCHE I TASTI SCRITTI IN BASSO.", controls.X + 10, mouse + 12, controls.Width - 20, 5, Dim);

        DrawFooter(batch, "[SU/GIÙ] SCEGLI     [SINISTRA/DESTRA] CAMBIA     [INVIO] CAMBIA     [ESC] INDIETRO");
    }
}
