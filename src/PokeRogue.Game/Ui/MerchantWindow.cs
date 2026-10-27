using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Merchant;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Ui;

/// <summary>
/// Kecleon's shop (phase 9.2): its portrait and what it says on the left, its wares as rows on the right with their price,
/// the run's coins on top. [INVIO] or a click buys the chosen ware, [ESC] leaves; the game stays frozen meanwhile. What
/// is spent here does not go to the bank at the end of the run.
/// </summary>
public sealed class MerchantWindow(GameServices services)
{
    private const int OpenTicks = 10;
    private const int RowHeight = 52;

    /// <summary>Scripted players buy what they need, then leave after this long.</summary>
    private const int AutoTicks = 50;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Red = new(232, 96, 80);

    private int _selected;
    private int _age;
    private string _line = "";
    private int _lineAge;

    public bool IsOpen { get; private set; }

    public void Open(RunState run)
    {
        IsOpen = true;
        _selected = Math.Max(0, run.Merchant.Stock.FindIndex(w => !w.Sold));
        _age = 0;
        Say(run.Merchant.Stock.All(w => w.Sold) ? "Ho venduto tutto! Torna la prossima volta." : "Benvenuto! Dai pure un'occhiata, ho roba buona!");
        services.Sounds.Play("pausa");
    }

    public void Close() => IsOpen = false;

    private void Say(string line)
    {
        _line = line;
        _lineAge = 0;
    }

    /// <summary>Buys and moves the choice; true once the player leaves (the run is told).</summary>
    public bool Update(RunState run, InputState? input)
    {
        if (!IsOpen)
            return false;

        _age++;
        _lineAge++;
        var stock = run.Merchant.Stock;
        if (_age < OpenTicks || stock.Count == 0 && input is null)
            return Leave(run, _age >= OpenTicks);

        if (input is null)
        {
            // The autopilot buys a potion when hurt, then goes.
            var potion = stock.FindIndex(w => w is { Kind: WareKind.Potion, Sold: false });
            if (_age == AutoTicks / 2 && potion >= 0 && run.Player.Hp < run.Player.MaxHp * 0.6f)
                Buy(run, potion);
            return Leave(run, _age >= AutoTicks);
        }

        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W) || input.Pressed(Buttons.DPadUp))
            Move(-1, stock.Count);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S) || input.Pressed(Buttons.DPadDown))
            Move(1, stock.Count);
        if (input.HoverMoved("merce") is { } pointed)
            _selected = pointed;
        for (var i = 0; i < Math.Min(stock.Count, 4); i++)
        {
            if (input.Pressed(Keys.D1 + i))
            {
                _selected = i;
                Buy(run, i);
            }
        }

        if (input.Click("merce") is { } clicked)
        {
            _selected = clicked;
            Buy(run, clicked);
        }
        else if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
            Buy(run, _selected);

        return Leave(run, input.Pressed(Keys.Escape) || input.Pressed(Buttons.B));
    }

    private bool Leave(RunState run, bool leave)
    {
        if (!leave)
            return false;

        services.Sounds.Play("menu-indietro");
        run.CloseShop();
        Close();
        return true;
    }

    private void Buy(RunState run, int index)
    {
        if ((uint)index >= (uint)run.Merchant.Stock.Count)
            return;

        var ware = run.Merchant.Stock[index];
        if (ware.Sold)
        {
            Say("Quello l'hai già comprato!");
            services.Sounds.Play("menu-errore");
        }
        else if (run.Player.Coins < ware.Price)
        {
            Say("Non ti bastano le monete...");
            services.Sounds.Play("menu-errore");
        }
        else if (run.BuyWare(index))
        {
            Say(ware.Kind switch
            {
                WareKind.Potion => "Ecco a te! Ti sentirai subito meglio.",
                WareKind.RareCandy => "Una caramella speciale! Gnam!",
                WareKind.EvolutionItem => "Con questo potrai evolverti!",
                WareKind.Repel => "Fiuuu! Spariti tutti!",
                _ => "Grazie mille! Torna presto!",
            });
            services.Sounds.Play("acquisto");
        }
        else
        {
            Say("Mi spiace, non puoi portarlo con te.");
            services.Sounds.Play("menu-errore");
        }
    }

    private void Move(int delta, int count)
    {
        if (count == 0)
            return;
        _selected = (_selected + delta + count) % count;
        services.Sounds.Play("menu-sposta");
    }

    public void Draw(SpriteBatch batch, RunState run)
    {
        if (!IsOpen)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        var height = services.Screen.Height;
        var body = services.Fonts.Body(services.Style);
        var small = services.Fonts.Small(services.Style);
        var numbers = services.Fonts.Numbers(services.Style);
        var smallNumbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);
        var open = MathHelper.Clamp(_age / (float)OpenTicks, 0f, 1f);

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, width, height), Color.Black * (0.6f * open));
        services.Input.Blocker(new Rectangle(0, 0, width, height));

        var panel = new Rectangle(40, 30 + (int)((1f - open) * 20f), 560, 290);
        UiDraw.Frame(batch, p, panel, services.Style.Theme with { Border = MerchantVfx.Green });
        UiDraw.TextOutlined(batch, body, "KECLEON IL MERCANTE", new Vector2(panel.X + 12, panel.Y + 8), MerchantVfx.Green, Ink);

        // The run's coins, top right.
        var coins = UiFormat.Number(run.Player.Coins);
        var coinsWidth = UiDraw.Measure(numbers, coins).X;
        UiDraw.TextOutlined(batch, numbers, coins, new Vector2(panel.Right - 12 - coinsWidth, panel.Y + 5), Gold, Ink);
        batch.Draw(services.Pickups.Coin, new Vector2(panel.Right - 22 - coinsWidth, panel.Y + 13), Color.White);

        // Kecleon's portrait and what it says.
        var face = new Rectangle(panel.X + 14, panel.Y + 36, 88, 88);
        p.Rect(batch, face, Ink);
        p.Rect(batch, new Rectangle(face.X + 2, face.Y + 2, face.Width - 4, face.Height - 4), new Color(52, 88, 60));
        if (services.Sprites.Portrait(MerchantVfx.SpeciesId) is { } portrait)
            batch.Draw(portrait, new Rectangle(face.X + 4, face.Y + 4, face.Width - 8, face.Height - 8), new Rectangle(0, 0, Math.Min(40, portrait.Width), Math.Min(40, portrait.Height)), Color.White);
        var speech = new Rectangle(panel.X + 10, face.Bottom + 8, 100, 56);
        p.Rect(batch, speech, Paper);
        p.Outline(batch, speech, Ink);
        var shown = _line[..Math.Min(_line.Length, _lineAge * 2)];
        var y = speech.Y + 5;
        foreach (var line in UiDraw.Wrap(small, shown, speech.Width - 10))
        {
            UiDraw.Text(batch, small, line, new Vector2(speech.X + 5, y), Ink);
            y += 10;
        }

        // The wares.
        var stock = run.Merchant.Stock;
        for (var i = 0; i < stock.Count; i++)
        {
            var ware = stock[i];
            var row = new Rectangle(panel.X + 122, panel.Y + 36 + i * (RowHeight + 6), panel.Width - 134, RowHeight);
            services.Input.Region(row, "merce", i);
            var affordable = run.Player.Coins >= ware.Price;
            UiDraw.Frame(batch, p, row, services.Style.Theme with { Border = i == _selected ? Gold : services.Style.Theme.Border });
            var iconBox = new Rectangle(row.X + 6, row.Y + 6, 40, 40);
            p.Rect(batch, iconBox, Ink * 0.6f);
            services.ItemIcons.Draw(batch, ware.IconKey, new Rectangle(iconBox.X + 4, iconBox.Y + 4, 32, 32), alpha: ware.Sold ? 0.3f : 1f);
            UiDraw.Text(batch, small, $"{i + 1}", new Vector2(row.X + 3, row.Y + 2), Dim);

            var nameColor = ware.Sold ? Dim : Paper;
            UiDraw.Text(batch, body, ware.NameIt, new Vector2(iconBox.Right + 8, row.Y + 5), nameColor);
            var textY = row.Y + 23;
            foreach (var line in UiDraw.Wrap(small, ware.DescriptionIt, row.Width - 130).Take(2))
            {
                UiDraw.TextWithNumbers(batch, small, smallNumbers, line, new Vector2(iconBox.Right + 8, textY), ware.Sold ? Dim * 0.7f : Dim);
                textY += 10;
            }

            if (ware.Sold)
            {
                UiDraw.TextOutlined(batch, body, "VENDUTO", new Vector2(row.Right - 74, row.Y + 17), Red, Ink);
                continue;
            }

            var price = UiFormat.Number(ware.Price);
            var priceWidth = UiDraw.Measure(numbers, price).X;
            UiDraw.TextOutlined(batch, numbers, price, new Vector2(row.Right - 10 - priceWidth, row.Y + 14), affordable ? Gold : Red, Ink);
            batch.Draw(services.Pickups.Coin, new Vector2(row.Right - 20 - priceWidth, row.Y + 22), Color.White * (affordable ? 1f : 0.5f));
        }

        if (stock.Count == 0)
            UiDraw.TextCentered(batch, body, "Non c'è più niente da comprare.", panel.X + 122 + (panel.Width - 134) / 2f, panel.Y + 120, Dim);

        UiDraw.TextCentered(batch, small, "LE MONETE SPESE QUI NON VANNO IN BANCA A FINE PARTITA", width / 2f, panel.Bottom - 16, Dim);
        KeyHints.Draw(batch, services, "[INVIO] COMPRA   [ESC] ESCI", new Vector2(width / 2f, panel.Bottom + 10), Paper, centered: true, outline: Ink);
        batch.End();
    }
}
