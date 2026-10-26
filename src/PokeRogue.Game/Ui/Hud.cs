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

        if (_bannerAge >= 0 && ++_bannerAge >= BannerTicks)
            _bannerAge = -1;

        foreach (var e in run.Events)
        {
            if (e.Type == RunEventType.MoveEvolved && (int)e.Value is >= 0 and < Slots)
                _evolveFlash[(int)e.Value] = EvolveFlashTicks;
            if (e.Type == RunEventType.DominantAppeared && _services.Species(e.SpeciesId) is { } species)
            {
                _banner = $"{species.NameIt.ToUpperInvariant()} DOMINANTE IN ARRIVO!";
                _bannerColor = Vfx.EnemyVfx.AuraRed;
                _bannerAge = 0;
            }

            if (e.Type == RunEventType.ShinyAppeared && _services.Species(e.SpeciesId) is { } shiny && _bannerAge < 0)
            {
                _banner = $"UN {shiny.NameIt.ToUpperInvariant()} CROMATICO!";
                _bannerColor = Vfx.EnemyVfx.ShinyGold;
                _bannerAge = 0;
            }
        }
    }

    /// <summary>How long the arrival of a Dominante is announced under the timer.</summary>
    private const int BannerTicks = 180;

    private string _banner = "";
    private Color _bannerColor = Vfx.EnemyVfx.AuraRed;
    private int _bannerAge = -1;

    /// <summary>What the boss bar shows: it drains slowly after each hit, behind the real PS.</summary>
    private float _bossTrail = 1f;

    /// <summary>
    /// The boss' PS (phase 3.4): a wide red bar under the timer with its name, notches where the next phases begin,
    /// and a pale stretch that drains after each hit, so big hits read.
    /// </summary>
    private void DrawBossBar(SpriteBatch batch, RunState run)
    {
        if (run.Boss is not { } fight)
            return;

        var boss = run.Enemies[fight.Index];
        var ratio = boss.State == EnemyState.Alive ? MathHelper.Clamp(boss.Hp / boss.MaxHp, 0f, 1f) : 0f;
        _bossTrail = _bossTrail > ratio ? MathF.Max(ratio, _bossTrail - 0.004f) : ratio;

        var p = _services.Primitives;
        var bar = new Rectangle(170, 30, 300, 9);
        p.Rect(batch, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), Ink);
        p.Rect(batch, bar, new Color(40, 24, 32));
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _bossTrail), bar.Height), new Color(255, 236, 200));
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), bar.Height), Vfx.EnemyVfx.AuraRed);
        p.Rect(batch, new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), 2), new Color(255, 160, 120));

        foreach (var phase in fight.Definition.Phases.Skip(1))
            p.Rect(batch, new Rectangle(bar.X + (int)(bar.Width * phase.Below), bar.Y - 2, 1, bar.Height + 4), Paper);

        var name = _services.Species(fight.Definition.SpeciesId)?.NameIt.ToUpperInvariant() ?? "";
        UiDraw.TextOutlined(batch, _services.Fonts.Small(_services.Style), name, new Vector2(bar.X, bar.Bottom + 3), Paper, Ink);
    }

    /// <summary>The announcement of a Dominante, sliding in under the timer, blinking twice, then fading out.</summary>
    private void DrawBanner(SpriteBatch batch)
    {
        if (_bannerAge < 0)
            return;

        var font = _services.Fonts.Body(_services.Style);
        var slide = MathF.Max(0f, 1f - _bannerAge / 10f);
        var fade = MathHelper.Clamp((BannerTicks - _bannerAge) / 30f, 0f, 1f);
        var blink = _bannerAge < 40 && _bannerAge / 10 % 2 == 1 ? 0.6f : 1f;
        var y = 26 - slide * 8f;
        UiDraw.TextCentered(batch, font, _banner, _services.Screen.Width / 2f, y, _bannerColor * (fade * blink), Ink * fade);
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
            if (i < Core.Run.Progression.LevelUp.MaxMoves)
                DrawMoveSlot(batch, moveSlot, i < player.Moves.Count ? player.Moves[i] : null);
            else
                DrawLockedSlot(batch, moveSlot);
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

        DrawBanner(batch);
        DrawBossBar(batch, run);
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

    private Texture2D? _lock;

    /// <summary>
    /// The sixth move slot (phase 3.6): a run holds five moves; the sixth will be bought with the meta-progression, so its
    /// slot shows darkened with a small padlock.
    /// </summary>
    public void DrawLockedSlot(SpriteBatch batch, Rectangle slot)
    {
        var p = _services.Primitives;
        UiDraw.Frame(batch, p, slot, _services.Style.Theme);
        p.Rect(batch, new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4), Ink * 0.55f);
        _lock ??= PixelArt.FromPatternOutlined(_services.Device,
        [
            ".ggg.",
            "g...g",
            "g...g",
            "ggggg",
            "gg.gg",
            "ggggg",
        ], new Dictionary<char, Color> { ['g'] = new(150, 142, 176) }, Ink);
        batch.Draw(_lock, new Vector2(slot.Center.X - _lock.Width / 2, slot.Center.Y - _lock.Height / 2), Color.White);
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
