using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The title screen the game opens on (phase 8.2): a night sky over rolling hills, the name of the game, and a parade of
/// Pokémon from the four regions walking along the bottom. [INVIO] or a click goes on to the main menu, [ESC] quits.
/// </summary>
public sealed class TitleScene : MenuScene
{
    /// <summary>Those who march across the title: the starters of the four regions, and some friends.</summary>
    private static readonly int[] Parade = [25, 1, 4, 7, 133, 152, 155, 158, 252, 255, 258, 387, 390, 393, 447, 443, 147, 175, 280, 143];

    private const int Spacing = 46;
    private const float WalkSpeed = 0.45f;
    private const int GroundY = 306;
    private const int LeaveTicks = 24;

    private static readonly Color SkyTop = new(14, 10, 34);
    private static readonly Color SkyBottom = new(58, 40, 96);
    private static readonly Color[] HillColors = [new(44, 34, 84), new(30, 46, 72), new(22, 52, 48)];
    private static readonly Color Ground = new(18, 40, 32);

    private readonly List<(SpriteAnimator Animator, int SpeciesId)> _walkers = [];
    private int _leaving = -1;

    public TitleScene(GameServices services) : base(services)
    {
        foreach (var id in Parade)
        {
            var animator = new SpriteAnimator();
            if (services.Sprites.Get(id) is { } set)
                animator.Play(set.Resolve(PmdAnimations.Walk));
            _walkers.Add((animator, id));
        }
    }

    /// <summary>Previews: the screen as it leaves for the menu, a few ticks in.</summary>
    public void Leave() => _leaving = 0;

    protected override void UpdateMenu()
    {
        for (var i = 0; i < _walkers.Count; i++)
        {
            var (animator, id) = _walkers[i];
            animator.Update();
            _walkers[i] = (animator, id);
        }

        if (_leaving >= 0)
        {
            if (++_leaving >= LeaveTicks)
                Services.Switch(new MainMenuScene(Services));
            return;
        }

        if (Age > 20 && (Confirm() || ClickedOn("titolo") is not null))
        {
            Services.Sounds.Play("titolo-via");
            _leaving = 0;
        }
        else if (Back() && !Services.Input.RightClicked)
        {
            Services.Exit();
        }
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var width = Services.Screen.Width;
        var height = Services.Screen.Height;
        Clickable(new Rectangle(0, 0, width, height), "titolo");

        // The sky, in bands from deep night to violet.
        const int bands = 24;
        for (var b = 0; b < bands; b++)
            p.Rect(batch, new Rectangle(0, b * GroundY / bands, width, GroundY / bands + 1), Color.Lerp(SkyTop, SkyBottom, b / (float)(bands - 1)));

        DrawStars(batch);
        for (var layer = 0; layer < HillColors.Length; layer++)
            DrawHills(batch, layer);
        p.Rect(batch, new Rectangle(0, GroundY, width, height - GroundY), Ground);
        p.Rect(batch, new Rectangle(0, GroundY, width, 1), Color.Lerp(Ground, Color.White, 0.18f));

        DrawParade(batch);
        DrawLogo(batch);

        if (_leaving < 0 && Age / 30 % 2 == 0 && Age > 20)
            UiDraw.TextCentered(batch, Body, OperatingSystem.IsAndroid() ? "TOCCA LO SCHERMO" : "PREMI INVIO O CLICCA", width / 2f, 214, Paper, Ink);

        UiDraw.TextCentered(batch, Small, "UN GIOCO PERSONALE, NON COMMERCIALE.  POKÉMON © NINTENDO, CREATURES, GAME FREAK.", width / 2f, height - 14, Dim * 0.8f);

        // Leaving: a white flash fading to the menu's dark.
        if (_leaving >= 0)
        {
            var t = _leaving / (float)LeaveTicks;
            p.Rect(batch, new Rectangle(0, 0, width, height), Color.Lerp(Color.White, Background, t) * MathF.Min(1f, t * 3f));
        }
    }

    private void DrawStars(SpriteBatch batch)
    {
        var p = Services.Primitives;
        for (var i = 0; i < 70; i++)
        {
            var x = (i * 97 + 31) % Services.Screen.Width;
            var y = (i * 53 + 11) % 170;
            var twinkle = (Age + i * 13) % 90 < 8;
            var bright = i % 5 == 0;
            p.Rect(batch, new Rectangle(x, y, 1, 1), Color.White * (twinkle ? 1f : bright ? 0.75f : 0.4f));
            if (twinkle && bright)
            {
                p.Rect(batch, new Rectangle(x - 1, y, 3, 1), Color.White * 0.6f);
                p.Rect(batch, new Rectangle(x, y - 1, 1, 3), Color.White * 0.6f);
            }
        }
    }

    /// <summary>A range of hills drifting slowly left, nearer ones faster and lower.</summary>
    private void DrawHills(SpriteBatch batch, int layer)
    {
        var p = Services.Primitives;
        var speed = 0.1f + layer * 0.15f;
        var baseline = 220 + layer * 30;
        var offset = Age * speed;
        for (var x = 0; x < Services.Screen.Width; x += 2)
        {
            var u = (x + offset) * (0.012f + layer * 0.004f);
            var top = baseline - (int)(18f * MathF.Sin(u + layer * 1.7f) + 10f * MathF.Sin(u * 2.3f + layer) + 6f * MathF.Sin(u * 5.1f));
            p.Rect(batch, new Rectangle(x, top, 2, GroundY - top), HillColors[layer]);
        }
    }

    /// <summary>The Pokémon walking right in a line, wrapping round from the left edge.</summary>
    private void DrawParade(SpriteBatch batch)
    {
        var total = _walkers.Count * Spacing;
        for (var i = 0; i < _walkers.Count; i++)
        {
            var (animator, id) = _walkers[i];
            if (Services.Sprites.Get(id) is not { } set || animator.Anim is not { } anim)
                continue;

            var x = (i * Spacing + Age * WalkSpeed) % total - 40;
            if (x > Services.Screen.Width + 40)
                continue;

            var feet = new Vector2(x, GroundY + 22);
            Services.Primitives.Ellipse(batch, feet + new Vector2(0, 1), 14, 5, new Color(0, 0, 0, 90));
            set.Draw(batch, anim, animator.Frame, Direction8.Right, feet, Color.White, 2f);
        }
    }

    private void DrawLogo(SpriteBatch batch)
    {
        var font = Services.Fonts.Get(Services.Style.BodyFont, Services.Style.BodyFontSize * 3);
        var centre = Services.Screen.Width / 2f;
        var y = 60 + (Age / 40 % 2 == 0 ? 0 : 1);
        var size = UiDraw.Measure(font, "POKÉROGUE");
        var at = new Vector2(centre - size.X / 2f, y);

        // A thick dark rim and a drop shadow, then the gold letters with a lighter top.
        UiDraw.Text(batch, font, "POKÉROGUE", at + new Vector2(0, 5), Ink * 0.7f);
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                if (dx != 0 || dy != 0)
                    UiDraw.Text(batch, font, "POKÉROGUE", at + new Vector2(dx, dy), Ink);
            }
        }

        UiDraw.Text(batch, font, "POKÉROGUE", at, Gold);
        UiDraw.Text(batch, Body, "UN ROGUELITE CON I POKÉMON DI KANTO, JOHTO, HOENN E SINNOH", new Vector2(centre - UiDraw.Measure(Body, "UN ROGUELITE CON I POKÉMON DI KANTO, JOHTO, HOENN E SINNOH").X / 2f, y + size.Y + 8), Paper);
    }
}
