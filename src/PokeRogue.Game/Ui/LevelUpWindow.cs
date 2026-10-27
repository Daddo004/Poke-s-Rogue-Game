using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The level-up choice window, in two layouts under review in phase 1.5:
/// <see cref="LevelUpLayout.List"/> (Vampire Survivors rows) and <see cref="LevelUpLayout.Cards"/> (three cards side by side).
/// </summary>
public enum LevelUpLayout
{
    List,
    Cards,
}

/// <summary>What the player did in the level-up window (phase 2.5 adds Rilancio, Salta and Bandisci).</summary>
public enum LevelUpCommand
{
    Choose,
    Reroll,
    Skip,
    Banish,
}

public readonly record struct LevelUpAction(LevelUpCommand Command, int Index = 0);

public sealed class LevelUpWindow(GameServices services)
{
    public const int OpenTicks = 14;
    public const int ConfirmTicks = 12;
    private const int CardStagger = 4;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color BanishRed = new(232, 72, 56);
    private static readonly UiTheme Selected = UiTheme.Dark with { Fill = new Color(58, 50, 92), Highlight = new Color(120, 110, 176) };

    /// <summary>Evolution cards (phase 2.6): a warm panel with a gold rim, brighter when selected.</summary>
    private static readonly UiTheme Evolution = UiTheme.Dark with { Fill = new Color(52, 40, 34, 240), Border = Gold, Highlight = new Color(150, 116, 48) };

    private static readonly UiTheme EvolutionSelected = Evolution with { Fill = new Color(78, 60, 40, 240), Highlight = new Color(196, 156, 64) };

    private IReadOnlyList<LevelUpOption> _options = [];
    private int _level;
    private int _confirmAge = -1;
    private bool _banishMode;
    private LevelUpAction? _pending;

    /// <summary>Ticks since the current cards were dealt (opening, Rilancio or Bandisci deal them again).</summary>
    private int _dealAge;

    public LevelUpLayout Layout { get; set; } = services.Style.LevelUpWindowLayout;

    public bool IsOpen { get; private set; }

    public int Cursor { get; private set; }

    /// <summary>Ticks since the window opened.</summary>
    public int Age { get; private set; }

    public bool Interactive => IsOpen && _confirmAge < 0 && Age >= OpenTicks;

    /// <summary>Rilancio, Salta and Bandisci charges left, shown on their buttons (set by <see cref="LevelUpFlow"/>).</summary>
    public (int Rerolls, int Skips, int Banishes) Charges { get; set; }

    /// <summary>True while the next confirmation banishes the selected card instead of taking it.</summary>
    public bool BanishMode => _banishMode;

    public void Open(IReadOnlyList<LevelUpOption> options, int level)
    {
        _options = options;
        _level = level;
        Age = 0;
        _dealAge = 0;
        Cursor = 0;
        _confirmAge = -1;
        _banishMode = false;
        _pending = null;
        IsOpen = true;
    }

    /// <summary>New cards after Rilancio or Bandisci: they are dealt again, the cursor stays where it was.</summary>
    public void Refresh(IReadOnlyList<LevelUpOption> options)
    {
        _options = options;
        _dealAge = 0;
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, options.Count - 1));
        _banishMode = false;
    }

    public void Close() => IsOpen = false;

    public void MoveCursor(int delta)
    {
        if (Interactive && _options.Count > 0)
        {
            Cursor = (Cursor + delta + _options.Count) % _options.Count;
            services.Sounds.Play("menu-sposta");
        }
    }

    public void Confirm()
    {
        if (Interactive)
        {
            _confirmAge = 0;
            services.Sounds.Play(_banishMode ? "menu-indietro" : "menu-conferma");
        }
    }

    /// <summary>Scripted players (autopilot): take the card at <paramref name="index"/>.</summary>
    public void ConfirmAt(int index)
    {
        if (!Interactive || (uint)index >= (uint)_options.Count)
            return;

        Cursor = index;
        _confirmAge = 0;
    }

    /// <summary>Rilancio: new cards, if a charge is left.</summary>
    public void RequestReroll()
    {
        if (Interactive && !_banishMode && Charges.Rerolls > 0)
        {
            _pending = new LevelUpAction(LevelUpCommand.Reroll);
            services.Sounds.Play("menu-conferma");
        }
    }

    /// <summary>Salta: no card this time, if a charge is left.</summary>
    public void RequestSkip()
    {
        if (Interactive && !_banishMode && Charges.Skips > 0)
        {
            _pending = new LevelUpAction(LevelUpCommand.Skip);
            services.Sounds.Play("menu-indietro");
        }
    }

    /// <summary>Bandisci: enters (or leaves) the mode where confirming banishes the selected card.</summary>
    public void ToggleBanish()
    {
        if (Interactive && (_banishMode || Charges.Banishes > 0))
        {
            _banishMode = !_banishMode;
            services.Sounds.Play("menu-sposta");
        }
    }

    /// <summary>Advances animations and reads <paramref name="input"/> (null when scripted). Returns what the player did, once its flash ends.</summary>
    public LevelUpAction? Update(InputState? input)
    {
        if (!IsOpen)
            return null;

        Age++;
        _dealAge++;
        if (_confirmAge >= 0)
        {
            if (++_confirmAge < ConfirmTicks)
                return null;

            _confirmAge = -1;
            if (_banishMode)
            {
                _banishMode = false;
                return new LevelUpAction(LevelUpCommand.Banish, Cursor);
            }

            IsOpen = false;
            return new LevelUpAction(LevelUpCommand.Choose, Cursor);
        }

        if (_pending is { } pending)
        {
            _pending = null;
            return pending;
        }

        if (input is null)
            return null;

        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W) || input.Pressed(Keys.Left) || input.Pressed(Keys.A) || input.Pressed(Buttons.DPadUp) || input.Pressed(Buttons.DPadLeft))
            MoveCursor(-1);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S) || input.Pressed(Keys.Right) || input.Pressed(Keys.D) || input.Pressed(Buttons.DPadDown) || input.Pressed(Buttons.DPadRight))
            MoveCursor(1);
        for (var i = 0; i < _options.Count; i++)
        {
            if (input.Pressed(Keys.D1 + i))
            {
                Cursor = i;
                Confirm();
            }
        }

        // The mouse (phase 8.1): pointing at a card selects it, a click takes it (or banishes it in Bandisci).
        if (Interactive && input.HoverMoved("carta") is { } pointed && pointed != Cursor)
        {
            Cursor = pointed;
            services.Sounds.Play("menu-sposta");
        }

        if (Interactive && input.Click("carta") is { } clicked)
        {
            Cursor = clicked;
            Confirm();
        }

        if (input.Pressed(Keys.R) || input.Pressed(Buttons.X))
            RequestReroll();
        if (input.Pressed(Keys.X) || input.Pressed(Buttons.Y))
            RequestSkip();
        if (input.Pressed(Keys.B) || input.Pressed(Buttons.RightShoulder))
            ToggleBanish();
        if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
            Confirm();
        return null;
    }

    public void Draw(SpriteBatch batch)
    {
        if (!IsOpen)
            return;

        var p = services.Primitives;
        var open = Ease(Age / (float)OpenTicks);
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * (0.55f * open));
        if (Layout == LevelUpLayout.Cards)
            DrawCards(batch);
        else
            DrawList(batch);
        batch.End();
    }

    private SpriteFontBase Body => services.Fonts.Body(services.Style);

    private SpriteFontBase Small => services.Fonts.Small(services.Style);

    private void DrawList(SpriteBatch batch)
    {
        var p = services.Primitives;
        var slide = (int)MathF.Round((1f - Ease(Age / (float)OpenTicks)) * 18f);
        var panel = new Rectangle(164, 56 + slide, 312, 250);
        UiDraw.Frame(batch, p, panel, UiTheme.Dark);

        DrawTitle(batch, panel.Center.X, panel.Y + 6);
        UiDraw.TextCentered(batch, Small, "SCEGLI UN POTENZIAMENTO", panel.Center.X, panel.Y + 26, Dim);

        for (var i = 0; i < _options.Count; i++)
        {
            var row = new Rectangle(panel.X + 8, panel.Y + 40 + i * 68, panel.Width - 16, 62);
            var selected = i == Cursor;
            services.Input.Region(row, "carta", i);
            UiDraw.Frame(batch, p, row, selected ? Selected : UiTheme.Dark);
            if (selected)
                p.Outline(batch, new Rectangle(row.X - 1, row.Y - 1, row.Width + 2, row.Height + 2), Blink(), 1);

            var option = _options[i];
            var iconSlot = new Rectangle(row.X + 8, row.Y + 13, 36, 36);
            UiDraw.Frame(batch, p, iconSlot, UiTheme.Dark);
            DrawIcon(batch, option, new Rectangle(iconSlot.X + 3, iconSlot.Y + 3, 30, 30), scale: 1);

            UiDraw.Text(batch, Body, option.NameIt, new Vector2(row.X + 52, row.Y + 3), Paper);
            DrawBadge(batch, option, row.Right - 8, row.Y + 7, alignRight: true);
            DrawCategory(batch, option, new Rectangle(row.X + 52, row.Y + 22, 58, 10));

            var lines = UiDraw.Wrap(Small, option.DescriptionIt, row.Width - 62);
            for (var l = 0; l < System.Math.Min(2, lines.Count); l++)
                UiDraw.Text(batch, Small, lines[l], new Vector2(row.X + 52, row.Y + 37 + l * 10), Dim);

            if (selected && _confirmAge >= 0)
                p.Rect(batch, row, Flash() * (0.7f * (1f - _confirmAge / (float)ConfirmTicks)));
        }

        DrawActions(batch, panel.Center.X, panel.Bottom + 6);
        KeyHints.Draw(batch, services, "[SU/GIÙ] SCEGLI     [INVIO] CONFERMA", new Vector2(panel.Center.X, panel.Bottom + 28), Dim, centered: true, outline: Ink);
    }

    private void DrawCards(SpriteBatch batch)
    {
        var p = services.Primitives;
        var centerX = services.Screen.Width / 2;
        DrawTitle(batch, centerX, 30);
        if (_banishMode)
            UiDraw.TextCentered(batch, Small, "SCEGLI COSA BANDIRE: NON TORNERÀ PIÙ IN QUESTA PARTITA", centerX, 50, BanishRed, Ink);
        else
            UiDraw.TextCentered(batch, Small, "SCEGLI UN POTENZIAMENTO", centerX, 50, Dim, Ink);

        // Fortuna's fourth card (Block 6) makes them a little narrower, so four still fit.
        const int height = 200;
        var width = _options.Count > 3 ? 142 : 156;
        var gap = _options.Count > 3 ? 10 : 14;
        var left = centerX - (_options.Count * width + (_options.Count - 1) * gap) / 2;
        for (var i = 0; i < _options.Count; i++)
        {
            var appear = Ease((_dealAge - i * CardStagger) / (float)OpenTicks);
            if (appear <= 0f)
                continue;

            var selected = i == Cursor;
            var y = 74 + (int)MathF.Round((1f - appear) * 26f) - (selected ? 6 : 0);
            var card = new Rectangle(left + i * (width + gap), y, width, height);
            services.Input.Region(new Rectangle(card.X, 68, card.Width, height + 12), "carta", i);
            var accent = _banishMode ? BanishRed : Gold;
            var option = _options[i];
            var evolution = option.Kind == LevelUpKind.Evolution;

            // Evolution cards glow gold even when not selected, so they cannot be missed.
            if (selected)
                p.Rect(batch, new Rectangle(card.X - 4, card.Y - 4, card.Width + 8, card.Height + 8), accent * 0.12f);
            else if (evolution)
                p.Rect(batch, new Rectangle(card.X - 4, card.Y - 4, card.Width + 8, card.Height + 8), Gold * (0.07f + 0.05f * Pulse()));
            UiDraw.Frame(batch, p, card, evolution ? selected ? EvolutionSelected : Evolution : selected ? Selected : UiTheme.Dark);
            if (selected)
                p.Outline(batch, new Rectangle(card.X - 1, card.Y - 1, card.Width + 2, card.Height + 2), _banishMode ? BanishRed : Blink(), 1);

            var iconBox = new Rectangle(card.Center.X - 34, card.Y + 12, 68, 68);
            UiDraw.Frame(batch, p, iconBox, evolution ? Evolution : UiTheme.Dark);
            DrawIcon(batch, option, new Rectangle(iconBox.X + 4, iconBox.Y + 4, 60, 60), scale: 2);
            if (evolution)
                DrawIconSparkles(batch, iconBox);

            UiDraw.TextCentered(batch, Body, option.NameIt, card.Center.X, card.Y + 84, evolution ? Gold : Paper);
            DrawBadge(batch, option, card.Center.X, card.Y + 103, alignRight: false);
            var descriptionY = card.Y + 134;
            if (evolution)
            {
                DrawRecipe(batch, option, card.Center.X, card.Y + 114);
                descriptionY = card.Y + 142;
            }
            else
            {
                DrawCategory(batch, option, new Rectangle(card.Center.X - 30, card.Y + 116, 60, 10));
            }

            var lines = UiDraw.Wrap(Small, option.DescriptionIt, width - 16);
            for (var l = 0; l < System.Math.Min(5, lines.Count); l++)
                UiDraw.TextCentered(batch, Small, lines[l], card.Center.X, descriptionY + l * 11, Dim);

            if (!selected)
                p.Rect(batch, card, Color.Black * 0.22f);
            if (selected && _banishMode)
                DrawBanishMark(batch, card);
            if (selected && _confirmAge >= 0)
                p.Rect(batch, card, Flash() * (0.7f * (1f - _confirmAge / (float)ConfirmTicks)));
        }

        DrawActions(batch, centerX, 286);
        KeyHints.Draw(batch, services, "[SINISTRA/DESTRA] SCEGLI     [INVIO] CONFERMA", new Vector2(centerX, 308), Dim, centered: true, outline: Ink);
    }

    /// <summary>Rilancio, Salta and Bandisci as three buttons with their key and the charges left; spent ones fade.</summary>
    private void DrawActions(SpriteBatch batch, int centerX, int y)
    {
        var p = services.Primitives;
        var numbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);
        (string Label, int Charges, bool Active)[] buttons =
        [
            ("[R] RILANCIA", Charges.Rerolls, false),
            ("[X] SALTA", Charges.Skips, false),
            (_banishMode ? "[B] ANNULLA" : "[B] BANDISCI", Charges.Banishes, _banishMode),
        ];

        const int width = 112, height = 16, gap = 8;
        var left = centerX - (buttons.Length * width + (buttons.Length - 1) * gap) / 2;
        for (var i = 0; i < buttons.Length; i++)
        {
            var (label, charges, active) = buttons[i];
            var box = new Rectangle(left + i * (width + gap), y, width, height);
            var usable = charges > 0 || active;
            services.Input.KeyRegion(box, i switch { 0 => Keys.R, 1 => Keys.X, _ => Keys.B });
            UiDraw.Frame(batch, p, box, UiTheme.Dark);
            if (active)
                p.Outline(batch, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2), BanishRed, 1);
            else if (usable && services.Input.PointerAwake && box.Contains(services.Input.Pointer))
                p.Outline(batch, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2), Gold, 1);

            var color = active ? BanishRed : usable ? Paper : Dim * 0.6f;
            UiDraw.Text(batch, Small, label, new Vector2(box.X + 6, box.Y + 4), color);
            var count = charges.ToString();
            UiDraw.Text(batch, numbers, count, new Vector2(box.Right - 6 - UiDraw.Measure(numbers, count).X, box.Y + 2), usable ? Gold : Dim * 0.6f);
        }
    }

    /// <summary>A red diagonal cross over the card about to be banished.</summary>
    private void DrawBanishMark(SpriteBatch batch, Rectangle card)
    {
        var p = services.Primitives;
        var alpha = Age / 6 % 2 == 0 ? 0.55f : 0.35f;
        UiDraw.Line(batch, p, new Vector2(card.X + 10, card.Y + 10), new Vector2(card.Right - 10, card.Bottom - 10), BanishRed * alpha, 4f);
        UiDraw.Line(batch, p, new Vector2(card.Right - 10, card.Y + 10), new Vector2(card.X + 10, card.Bottom - 10), BanishRed * alpha, 4f);
        UiDraw.TextCentered(batch, Small, "BANDISCI", card.Center.X, card.Bottom - 16, BanishRed, Ink);
    }

    /// <summary>The confirm flash: white to take a card, red to banish it.</summary>
    private Color Flash() => _banishMode ? BanishRed : Color.White;

    private void DrawTitle(SpriteBatch batch, int centerX, int y)
    {
        // The title carries the level number, so it uses the numbers font (approved in 1.5).
        var font = services.Fonts.Numbers(services.Style);
        var title = $"LIVELLO {_level}!";
        UiDraw.TextCentered(batch, font, title, centerX, y - 2, Gold, Ink);

        // Two twinkling sparkles either side of the title.
        var half = UiDraw.Measure(font, title).X / 2f;
        for (var side = -1; side <= 1; side += 2)
        {
            var phase = (Age + (side > 0 ? 10 : 0)) % 24;
            var size = phase < 12 ? 1 + phase / 4 : 1 + (23 - phase) / 4;
            var at = new Vector2(centerX + side * (half + 10), y + 7);
            DrawSparkle(batch, at, size);
        }
    }

    private void DrawSparkle(SpriteBatch batch, Vector2 at, int size)
    {
        var p = services.Primitives;
        p.Rect(batch, new Rectangle((int)at.X - size, (int)at.Y, size * 2 + 1, 1), Gold);
        p.Rect(batch, new Rectangle((int)at.X, (int)at.Y - size, 1, size * 2 + 1), Gold);
        p.Rect(batch, new Rectangle((int)at.X, (int)at.Y, 1, 1), Color.White);
    }

    private void DrawIcon(SpriteBatch batch, LevelUpOption option, Rectangle area, int scale)
    {
        if (option.MoveType is { } type)
        {
            services.MoveIcons.Draw(batch, services.Primitives, area, option.Id, type, glyphScale: scale * 2);
            return;
        }

        if (services.ItemIcons.Get(option.IconKey) is { } icon)
        {
            var size = new Vector2(icon.Width, icon.Height) * scale;
            batch.Draw(icon, new Vector2(area.Center.X - (int)(size.X / 2), area.Center.Y - (int)(size.Y / 2)), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>What the evolution is made of: the maxed move plus the held item.</summary>
    private void DrawRecipe(SpriteBatch batch, LevelUpOption option, int centerX, int y)
    {
        if (option.EvolvesFrom is not { } baseId || option.EvolveItem is not { } itemId)
            return;

        var p = services.Primitives;
        var baseMove = services.Moves.Value[baseId];
        var item = services.Items.Value[itemId];
        const int tile = 24, plus = 14;
        var left = centerX - (tile * 2 + plus) / 2;

        var moveSlot = new Rectangle(left, y, tile, tile);
        UiDraw.Frame(batch, p, moveSlot, UiTheme.Dark);
        services.MoveIcons.Draw(batch, p, new Rectangle(moveSlot.X + 2, moveSlot.Y + 2, tile - 4, tile - 4), baseMove.Id, baseMove.Type);
        UiDraw.TextCentered(batch, Body, "+", left + tile + plus / 2f, y + 3, Gold, Ink);

        var itemSlot = new Rectangle(left + tile + plus, y, tile, tile);
        UiDraw.Frame(batch, p, itemSlot, UiTheme.Dark);
        services.ItemIcons.Draw(batch, item.IconKey, new Rectangle(itemSlot.Center.X - ItemIcons.Crop.Width / 2, itemSlot.Center.Y - ItemIcons.Crop.Height / 2, ItemIcons.Crop.Width, ItemIcons.Crop.Height));
    }

    /// <summary>Four sparkles twinkling in turn at the corners of an evolution's icon box.</summary>
    private void DrawIconSparkles(SpriteBatch batch, Rectangle box)
    {
        Point[] corners = [new(box.X + 2, box.Y + 2), new(box.Right - 3, box.Y + 2), new(box.Right - 3, box.Bottom - 3), new(box.X + 2, box.Bottom - 3)];
        for (var k = 0; k < corners.Length; k++)
        {
            var phase = (Age + k * 9) % 36;
            if (phase < 18)
                DrawSparkle(batch, corners[k].ToVector2(), phase < 9 ? 1 + phase / 3 : 1 + (17 - phase) / 3);
        }
    }

    /// <summary>0 to 1 and back, about once a second.</summary>
    private float Pulse() => 0.5f + 0.5f * MathF.Sin(Age * 0.1f);

    private void DrawBadge(SpriteBatch batch, LevelUpOption option, int x, int y, bool alignRight)
    {
        var (text, color) = option.Kind switch
        {
            LevelUpKind.Evolution => ("EVOLUZIONE!", Gold),
            LevelUpKind.NewMove or LevelUpKind.NewItem => ("NUOVO!", Gold),
            LevelUpKind.MoveUpgrade or LevelUpKind.ItemUpgrade => ($"LV {option.ToLevel}", Paper),
            _ => ("PREMIO", new Color(120, 220, 120)),
        };

        var width = UiDraw.Measure(Small, text).X;
        var left = alignRight ? x - width : x - width / 2f;
        UiDraw.TextOutlined(batch, Small, text, new Vector2(left, y), color, Ink);
    }

    private void DrawCategory(SpriteBatch batch, LevelUpOption option, Rectangle r)
    {
        if (option.MoveType is { } type)
            UiDraw.Pill(batch, services.Primitives, Small, r, TypePalette.Of(type), TypePalette.NameIt(type).ToUpperInvariant());
        else if (option.Kind is LevelUpKind.NewItem or LevelUpKind.ItemUpgrade)
            UiDraw.Pill(batch, services.Primitives, Small, r, new Color(112, 120, 150), "STRUMENTO");
        else
            UiDraw.Pill(batch, services.Primitives, Small, r, new Color(96, 152, 96), "PREMIO");
    }

    private Color Blink() => Age / 8 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.6f);

    private static float Ease(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
