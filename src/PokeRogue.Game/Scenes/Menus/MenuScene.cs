using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// What the menus share (Block 6, completed in Block 8): the background of the art direction's character select
/// (phase 0.6), the title and the coins in the bank on top, the keys at the bottom (buttons for the mouse too), a notice
/// for what just happened, and a confirmation before spending coins. Moving, confirming and going back make their
/// sounds, and the menus play their music.
/// </summary>
public abstract class MenuScene(GameServices services) : Scene(services)
{
    protected static readonly Color Ink = new(16, 12, 28);
    protected static readonly Color Paper = new(244, 240, 228);
    protected static readonly Color Gold = new(248, 208, 72);
    protected static readonly Color Dim = new(160, 152, 190);
    protected static readonly Color Green = new(120, 220, 120);
    protected static readonly Color Red = new(232, 96, 80);
    protected static readonly Color Background = new(22, 18, 36);

    private const int NoticeTicks = 100;

    private string? _notice;
    private Color _noticeColor;
    private int _noticeAge;

    private string? _question;
    private string? _detail;
    private Action? _onYes;

    /// <summary>Ticks since the scene opened.</summary>
    protected int Age { get; private set; }

    public override bool HandlesEscape => true;

    protected MetaProgress Meta => Services.Meta;

    protected FontStashSharp.SpriteFontBase Body => Services.Fonts.Body(Services.Style);

    protected FontStashSharp.SpriteFontBase Small => Services.Fonts.Small(Services.Style);

    protected FontStashSharp.SpriteFontBase Numbers => Services.Fonts.Numbers(Services.Style);

    /// <summary>The numbers font at half size, for digits inside small text.</summary>
    protected FontStashSharp.SpriteFontBase SmallNumbers => Services.Fonts.Get(Services.Style.NumberFont, Services.Style.NumberFontSize / 2f);

    protected bool Asking => _question is not null;

    /// <summary>The music of this screen; the menus share one, so it plays on from screen to screen.</summary>
    protected virtual string? MusicTrack => Services.Music.Map.Menu;

    public sealed override void Update()
    {
        Age++;
        if (Age == 1)
        {
            Services.Music.Duck = 1f;
            Services.Music.Play(MusicTrack);
        }
        if (_notice is not null && ++_noticeAge >= NoticeTicks)
            _notice = null;

        if (_question is not null)
        {
            if (Confirm())
            {
                var yes = _onYes;
                _question = null;
                yes?.Invoke();
            }
            else if (Back())
            {
                _question = null;
            }

            return;
        }

        UpdateMenu();
    }

    protected abstract void UpdateMenu();

    public sealed override void Draw(SpriteBatch batch)
    {
        BeforeDraw(batch);
        Services.Device.Clear(Background);
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        for (var x = -360; x < Services.Screen.Width; x += 16)
            UiDraw.Line(batch, Services.Primitives, new Vector2(x, Services.Screen.Height), new Vector2(x + 360, 0), Color.White * 0.03f, 6f);

        DrawMenu(batch);
        DrawNotice(batch);
        DrawQuestion(batch);
        batch.End();
    }

    /// <summary>Draws the screen inside an open batch (point sampling).</summary>
    protected abstract void DrawMenu(SpriteBatch batch);

    /// <summary>Before the screen is drawn, with no batch open: a scene may render into its own targets here (then restore the screen's).</summary>
    protected virtual void BeforeDraw(SpriteBatch batch) { }

    // ---- Input ----

    protected bool Up() => Sound(Pressed(Keys.Up, Keys.W, Buttons.DPadUp), "menu-sposta");

    protected bool Down() => Sound(Pressed(Keys.Down, Keys.S, Buttons.DPadDown), "menu-sposta");

    protected bool Left() => Sound(Pressed(Keys.Left, Keys.A, Buttons.DPadLeft), "menu-sposta");

    protected bool Right() => Sound(Pressed(Keys.Right, Keys.D, Buttons.DPadRight), "menu-sposta");

    protected bool Confirm() => Sound(Pressed(Keys.Enter, Keys.Space, Buttons.A), "menu-conferma");

    protected bool Back() => Sound(Pressed(Keys.Escape, Keys.Back, Buttons.B), "menu-indietro");

    protected bool Pressed(Keys key) => Services.Input.Pressed(key);

    private bool Pressed(Keys a, Keys b, Buttons button) => Services.Input.Pressed(a) || Services.Input.Pressed(b) || Services.Input.Pressed(button);

    protected bool Pressed(Keys key, Buttons button) => Services.Input.Pressed(key) || Services.Input.Pressed(button);

    private bool Sound(bool pressed, string sound)
    {
        if (pressed)
            Services.Sounds.Play(sound);
        return pressed;
    }

    // ---- Pointer (phase 8.1) ----

    /// <summary>Something drawn that the mouse can point at and click: a cell, a row, a tab.</summary>
    protected void Clickable(Rectangle area, string tag, int index = 0) => Services.Input.Region(area, tag, index);

    /// <summary>The cell tagged <paramref name="tag"/> the mouse has just moved onto (it moves the cursor there).</summary>
    protected int? Pointed(string tag) => Services.Input.HoverMoved(tag);

    /// <summary>The cell tagged <paramref name="tag"/> clicked this tick.</summary>
    protected int? ClickedOn(string tag)
    {
        var index = Services.Input.Click(tag);
        if (index is not null)
            Services.Sounds.Play("menu-conferma");
        return index;
    }

    /// <summary>The mouse is over <paramref name="area"/> (a lighter rim).</summary>
    protected bool Under(Rectangle area) => Services.Input.PointerAwake && area.Contains(Services.Input.Pointer);

    // ---- Shared pieces ----

    /// <summary>A notice at the bottom for a moment ("MONETE INSUFFICIENTI", "ACQUISTATO!").</summary>
    protected void Notice(string text, Color color)
    {
        _notice = text;
        _noticeColor = color;
        _noticeAge = 0;
        if (color == Red)
            Services.Sounds.Play("menu-errore");
        else if (color == Green)
            Services.Sounds.Play("acquisto");
    }

    /// <summary>Asks before doing something that spends or gives back coins: [INVIO] does it, [ESC] does not.</summary>
    protected void Ask(string question, string detail, Action onYes)
    {
        _question = question;
        _detail = detail;
        _onYes = onYes;
    }

    /// <summary>Asks before a purchase, then reports how it went.</summary>
    protected void AskToBuy(string what, int price, Func<Purchase> buy, string? done = null)
    {
        if (Meta.Coins < price)
        {
            Notice($"MONETE INSUFFICIENTI: SERVONO {UiFormat.Number(price)}", Red);
            return;
        }

        Ask($"Acquistare {what}?", $"COSTA {UiFormat.Number(price)} MONETE, NE HAI {UiFormat.Number(Meta.Coins)}", () => Report(buy(), done ?? "ACQUISTATO!"));
    }

    protected void Report(Purchase purchase, string done)
    {
        switch (purchase)
        {
            case Purchase.Done:
                Notice(done, Green);
                break;
            case Purchase.NotEnoughCoins:
                Notice("MONETE INSUFFICIENTI", Red);
                break;
            case Purchase.NeedsSecondary:
                Notice("PRIMA SBLOCCA LA MOSSA SECONDARIA", Red);
                break;
            case Purchase.NeedsMedals:
                Notice("SERVONO PIÙ MEDAGLIE: SCONFIGGI I CAPIPALESTRA IN SFIDA", Red);
                break;
            case Purchase.Owned:
                Notice("GIÀ TUO", Dim);
                break;
            default:
                Notice("NON È IN VENDITA", Red);
                break;
        }
    }

    /// <summary>The screen's title top-left and the coins in the bank top-right, as in the art direction.</summary>
    protected void DrawHeader(SpriteBatch batch, string title)
    {
        UiDraw.TextOutlined(batch, Body, title, new Vector2(16, 10), Paper, Ink);
        DrawCoins(batch, Services.Screen.Width - 16, 10);
    }

    /// <summary>The bank's coins, right-aligned at <paramref name="right"/>.</summary>
    protected void DrawCoins(SpriteBatch batch, int right, int y)
    {
        var coins = UiFormat.Number(Meta.Coins);
        var width = UiDraw.Measure(Numbers, coins).X;
        UiDraw.TextOutlined(batch, Numbers, coins, new Vector2(right - width, y - 3), Gold, Ink);
        batch.Draw(Services.Pickups.Coin, new Vector2(right - width - 10, y + 5), Color.White);
    }

    /// <summary>The keys of the screen along the bottom; those naming one key are buttons for the mouse too.</summary>
    protected void DrawFooter(SpriteBatch batch, string keys) =>
        KeyHints.Draw(batch, Services, keys, new Vector2(16, Services.Screen.Height - 16), Dim);

    /// <summary>A price with the coin: gold when affordable, red when not.</summary>
    protected void DrawPrice(SpriteBatch batch, int price, int right, int y)
    {
        var text = UiFormat.Number(price);
        var width = UiDraw.Measure(SmallNumbers, text).X;
        UiDraw.Text(batch, SmallNumbers, text, new Vector2(right - width, y - 2), Meta.Coins >= price ? Gold : Red);
        batch.Draw(Services.Pickups.Coin, new Vector2(right - width - 9, y), Color.White);
    }

    /// <summary>Grade pips: filled gold for those owned, dark for those still to buy.</summary>
    protected void DrawPips(SpriteBatch batch, int x, int y, int owned, int max, int size = 4)
    {
        var p = Services.Primitives;
        for (var i = 0; i < max; i++)
        {
            var pip = new Rectangle(x + i * (size + 2), y, size, size);
            p.Rect(batch, new Rectangle(pip.X - 1, pip.Y - 1, pip.Width + 2, pip.Height + 2), Ink);
            p.Rect(batch, pip, i < owned ? Gold : new Color(60, 54, 88));
        }
    }

    /// <summary>A pale outline around the cell under the mouse (the selected one has the gold cursor instead).</summary>
    protected void DrawHover(SpriteBatch batch, Rectangle cell)
    {
        if (Under(cell) && !Asking)
            Services.Primitives.Outline(batch, new Rectangle(cell.X - 1, cell.Y - 1, cell.Width + 2, cell.Height + 2), Paper * 0.7f, 1);
    }

    /// <summary>A blinking gold outline around the selected cell.</summary>
    protected void DrawCursor(SpriteBatch batch, Rectangle cell) =>
        Services.Primitives.Outline(batch, new Rectangle(cell.X - 2, cell.Y - 2, cell.Width + 4, cell.Height + 4),
            Age / 20 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.5f), 2);

    protected void DrawTypePill(SpriteBatch batch, Rectangle r, Core.Data.PokemonType type) =>
        UiDraw.Pill(batch, Services.Primitives, Small, r, TypePalette.Of(type), TypePalette.NameIt(type).ToUpperInvariant());

    /// <summary>Wrapped small text, at most <paramref name="maxLines"/> lines.</summary>
    protected void DrawWrapped(SpriteBatch batch, string text, int x, int y, int width, int maxLines, Color color, int pitch = 10)
    {
        var lines = UiDraw.Wrap(Small, text, width);
        for (var i = 0; i < Math.Min(maxLines, lines.Count); i++)
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, lines[i], new Vector2(x, y + i * pitch), color);
    }

    private void DrawNotice(SpriteBatch batch)
    {
        if (_notice is null)
            return;

        var fade = MathHelper.Clamp((NoticeTicks - _noticeAge) / 20f, 0f, 1f);
        var width = (int)UiDraw.Measure(Small, _notice).X + 24;
        var box = new Rectangle(Services.Screen.Width / 2 - width / 2, Services.Screen.Height - 42, width, 18);
        Services.Primitives.Rect(batch, box, Ink * (0.9f * fade));
        Services.Primitives.Outline(batch, box, _noticeColor * fade, 1);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, _notice, new Vector2(box.X + 12, box.Y + 5), _noticeColor * fade);
    }

    private void DrawQuestion(SpriteBatch batch)
    {
        if (_question is null)
            return;

        var p = Services.Primitives;
        p.Rect(batch, new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height), Color.Black * 0.55f);
        Services.Input.Blocker(new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height));
        var panel = new Rectangle(150, 130, 340, 84);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        UiDraw.TextCentered(batch, Body, _question, panel.Center.X, panel.Y + 12, Paper);
        if (_detail is { } detail)
        {
            var width = UiDraw.Measure(Small, detail).X;
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, detail, new Vector2(panel.Center.X - width / 2f, panel.Y + 38), Gold);
        }

        KeyHints.Draw(batch, Services, "[INVIO] SÌ     [ESC] NO", new Vector2(panel.Center.X, panel.Bottom - 18), Dim, centered: true);
    }
}
