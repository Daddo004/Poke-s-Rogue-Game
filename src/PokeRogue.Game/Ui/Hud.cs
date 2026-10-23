using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// In-run HUD, layout A approved in 0.6: XP bar and level, move and item slots top-left, timer top-centre,
/// coins and KO top-right. The trainer card and the special-move gauge join in phases 5.2 and 4.4.
/// </summary>
public sealed class Hud
{
    public const int Slots = 6;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);

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
    }

    public XpBar XpBar { get; }

    public void Update(RunState run)
    {
        XpBar.Update(run.Player);
        for (var i = 0; i < Slots; i++)
        {
            if (_evolveFlash[i] > 0)
                _evolveFlash[i]--;
        }

        foreach (var e in run.Events)
        {
            if (e.Type == RunEventType.MoveEvolved && (int)e.Value is >= 0 and < Slots)
                _evolveFlash[(int)e.Value] = EvolveFlashTicks;
        }
    }

    public void Draw(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var theme = _services.Style.Theme;
        var numbers = _services.Fonts.Numbers(_services.Style);
        var player = run.Player;

        batch.Begin(samplerState: SamplerState.PointClamp);
        XpBar.Draw(batch, player);

        for (var i = 0; i < Slots; i++)
        {
            var moveSlot = new Rectangle(3 + i * 26, 13, 24, 24);
            DrawMoveSlot(batch, moveSlot, i < player.Moves.Count ? player.Moves[i] : null);
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

        var clock = UiFormat.Clock(run.Ticks);
        UiDraw.TextCentered(batch, numbers, clock, _services.Screen.Width / 2f, 9, Paper, Ink);

        // Coins and KO, right-aligned under the level.
        var right = _services.Screen.Width - 4f;
        var kills = UiFormat.Number(run.Stats.Kills);
        var killsWidth = UiDraw.Measure(numbers, kills).X;
        UiDraw.TextOutlined(batch, numbers, kills, new Vector2(right - killsWidth, 26), Paper, Ink);
        batch.Draw(_skull, new Vector2(right - killsWidth - 10, 33), Color.White);

        var coins = UiFormat.Number(player.Coins);
        var coinsRight = right - killsWidth - 18;
        var coinsWidth = UiDraw.Measure(numbers, coins).X;
        UiDraw.TextOutlined(batch, numbers, coins, new Vector2(coinsRight - coinsWidth, 26), Gold, Ink);
        batch.Draw(_services.Pickups.Coin, new Vector2(coinsRight - coinsWidth - 10, 33), Color.White);

        batch.End();
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
