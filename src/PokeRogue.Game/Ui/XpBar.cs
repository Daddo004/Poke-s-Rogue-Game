using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>Experience bar across the top of the screen with the level on the right (HUD A, approved in 0.6).</summary>
public sealed class XpBar(GameServices services)
{
    public static readonly Color Fill = new(72, 152, 248);
    private static readonly Color Back = new(24, 20, 44);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);

    private float _shown;
    private int _flashTicks;
    private int _lastLevel = 1;

    /// <summary>Eases the displayed fill and flashes white on level-up.</summary>
    public void Update(Player player)
    {
        var target = player.Xp / player.XpToNext;
        if (player.Level != _lastLevel)
        {
            _lastLevel = player.Level;
            _flashTicks = 16;
            _shown = 0f;
        }

        _shown += (target - _shown) * 0.25f;
        if (_flashTicks > 0)
            _flashTicks--;
    }

    public void Draw(SpriteBatch batch, Player player)
    {
        var p = services.Primitives;
        var bar = new Rectangle(2, 2, services.Screen.Width - 4, 8);
        var fill = _flashTicks > 0 && _flashTicks / 2 % 2 == 0 ? Color.White : Fill;
        UiDraw.Bar(batch, p, bar, _flashTicks > 0 ? 1f : _shown, fill, Back, Ink);

        var font = services.Fonts.Numbers(services.Style);
        var text = $"Lv {player.Level}";
        var size = UiDraw.Measure(font, text);
        UiDraw.TextOutlined(batch, font, text, new Vector2(bar.Right - size.X - 2, bar.Bottom), Paper, Ink);
    }
}
