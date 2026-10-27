using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The settings as a list (phase 8.2), the same in the Opzioni screen and in the pause: the three volumes as ten pips,
/// damage numbers, how much the screen shakes, full screen and how the picture is scaled. Up and down choose, left and
/// right change; with the mouse, a row is clicked, or the arrows beside its value. Every change is saved at once.
/// </summary>
public sealed class OptionsPanel(GameServices services)
{
    public enum Option
    {
        Music,
        Effects,
        Cries,
        DamageNumbers,
        Shake,
        HudSize,
        FullScreen,
        Resolution,
        Scale,
    }

    /// <summary>On a phone the game always fills the screen: no window, no full screen, no resolution to choose.</summary>
    public static readonly Option[] All = Enum.GetValues<Option>()
        .Where(o => !OperatingSystem.IsAndroid() || o is not (Option.FullScreen or Option.Resolution))
        .ToArray();

    private const string Tag = "opzioni";
    private const int RowHeight = 20;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Off = new(60, 54, 88);

    private int _age;

    public int Cursor { get; set; }

    private Settings Settings => services.Settings;

    private SpriteFontBase Body => services.Fonts.Body(services.Style);

    private SpriteFontBase Small => services.Fonts.Small(services.Style);

    /// <summary>Reads the keys and the pointer; false when Esc asks to leave.</summary>
    public bool Update(InputState input)
    {
        _age++;
        if (input.HoverMoved(Tag) is { } hovered)
            Cursor = hovered / 3;
        if (input.Click(Tag) is { } clicked)
        {
            Cursor = clicked / 3;
            Change(clicked % 3 == 1 ? -1 : 1, wrap: clicked % 3 == 0);
        }

        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W) || input.Pressed(Buttons.DPadUp))
            Move(-1);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S) || input.Pressed(Buttons.DPadDown))
            Move(1);
        if (input.Pressed(Keys.Left) || input.Pressed(Keys.A) || input.Pressed(Buttons.DPadLeft))
            Change(-1, wrap: false);
        if (input.Pressed(Keys.Right) || input.Pressed(Keys.D) || input.Pressed(Buttons.DPadRight))
            Change(1, wrap: false);
        if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
            Change(1, wrap: true);

        return !(input.Pressed(Keys.Escape) || input.Pressed(Keys.Back) || input.Pressed(Buttons.B));
    }

    private void Move(int delta)
    {
        Cursor = (Cursor + delta + All.Length) % All.Length;
        services.Sounds.Play("menu-sposta");
    }

    /// <summary>One step of the selected setting; with <paramref name="wrap"/> past the end it starts again (a click on the row).</summary>
    public void Change(int delta, bool wrap)
    {
        var settings = Settings;
        switch (All[Cursor])
        {
            case Option.Music:
                settings.MusicVolume = Step(settings.MusicVolume, delta, Settings.MaxVolume, wrap);
                break;
            case Option.Effects:
                settings.EffectsVolume = Step(settings.EffectsVolume, delta, Settings.MaxVolume, wrap);
                break;
            case Option.Cries:
                settings.CriesVolume = Step(settings.CriesVolume, delta, Settings.MaxVolume, wrap);
                services.ApplyVolumes();
                services.Cries.Play(25);
                break;
            case Option.DamageNumbers:
                settings.ShowDamageNumbers = !settings.ShowDamageNumbers;
                break;
            case Option.Shake:
                settings.ShakePercent = Step(settings.ShakePercent / 25, delta, 4, wrap) * 25;
                break;
            case Option.HudSize:
                var sizes = Hud.Sizes(services.Screen.Zoom);
                var size = Array.IndexOf(sizes, Hud.EffectivePercent(settings.HudPercent, services.Screen.Zoom));
                settings.HudPercent = sizes[Step(size, delta, sizes.Length - 1, wrap)];
                break;
            case Option.FullScreen:
                services.SetFullScreen(!settings.FullScreen);
                break;
            case Option.Resolution:
                var resolutions = services.Resolutions();
                if (resolutions.Count > 0)
                    services.SetResolution(resolutions[Step(ResolutionIndex(resolutions), delta, resolutions.Count - 1, wrap)]);
                break;
            case Option.Scale:
                services.SetFillScreen(!settings.FillScreen);
                break;
        }

        settings.Save();
        services.ApplyVolumes();
        if (All[Cursor] != Option.Cries)
            services.Sounds.Play("menu-conferma");
    }

    /// <summary>Where the resolution in use stands in <paramref name="list"/>: itself, or the nearest by size (a window resized by hand).</summary>
    private int ResolutionIndex(IReadOnlyList<Point> list)
    {
        var current = services.Resolution();
        var best = 0;
        for (var i = 1; i < list.Count; i++)
        {
            if (Math.Abs(list[i].X * list[i].Y - current.X * current.Y) < Math.Abs(list[best].X * list[best].Y - current.X * current.Y))
                best = i;
        }

        return best;
    }

    private static int Step(int value, int delta, int max, bool wrap)
    {
        var next = value + delta;
        if (wrap)
            return next > max ? 0 : next < 0 ? max : next;
        return Math.Clamp(next, 0, max);
    }

    public static string Label(Option option) => option switch
    {
        Option.Music => "Musica",
        Option.Effects => "Effetti sonori",
        Option.Cries => "Versi dei Pokémon",
        Option.DamageNumbers => "Numeri dei danni",
        Option.Shake => "Scossa dello schermo",
        Option.HudSize => "Dimensione HUD",
        Option.FullScreen => "Schermo intero",
        Option.Resolution => "Risoluzione",
        _ => "Scala dell'immagine",
    };

    private static string Help(Option option) => option switch
    {
        Option.Music => "IL VOLUME DELLA MUSICA. METTI I TUOI FILE IN CONTENT/AUDIO PER CAMBIARLA.",
        Option.Effects => "COLPI, GEMME, MENU, FORZIERI E TUTTI GLI ALTRI SUONI.",
        Option.Cries => "I VERSI DEI BOSS, DELLE EVOLUZIONI E DEI POKÉMON DEGLI ALLENATORI.",
        Option.DamageNumbers => "I NUMERI CHE ESCONO DAI NEMICI COLPITI.",
        Option.Shake => "QUANTO TREMA LO SCHERMO PER ESPLOSIONI, BOSS E MOSSE FORTI.",
        Option.HudSize => "QUANTO SONO GRANDI MOSSE, STRUMENTI, TEMPO E MONETE IN PARTITA. OGNI MISURA RESTA NITIDA.",
        Option.FullScreen => "A TUTTO SCHERMO, ALLA RISOLUZIONE SCELTA SOTTO. ANCHE CON ALT+INVIO.",
        Option.Resolution => "IN FINESTRA: LA SUA GRANDEZZA. A SCHERMO INTERO: LA RISOLUZIONE DEL MONITOR (LA PRIMA È LA SUA).",
        _ => "NITIDA: PIXEL TUTTI UGUALI, CON BORDI. PIENA: RIEMPIE LO SCHERMO, SEMPRE NITIDA.",
    };

    /// <summary>The list inside <paramref name="area"/>, the selected setting's help under it.</summary>
    public void Draw(SpriteBatch batch, Rectangle area)
    {
        var p = services.Primitives;
        var input = services.Input;
        for (var i = 0; i < All.Length; i++)
        {
            var option = All[i];
            var row = new Rectangle(area.X, area.Y + i * (RowHeight + 3), area.Width, RowHeight);
            var selected = i == Cursor;
            input.Region(row, Tag, i * 3);
            if (selected)
            {
                p.Rect(batch, row, Color.White * 0.08f);
                p.Outline(batch, row, _age / 8 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.6f), 1);
            }

            UiDraw.Text(batch, Body, Label(option), new Vector2(row.X + 8, row.Y + 2), selected ? Paper : Dim);

            // The value on the right, between two arrows that can be clicked.
            var right = new Rectangle(row.Right - 16, row.Y + 3, 12, 14);
            var valueRight = right.X - 4;
            var valueWidth = DrawValue(batch, option, valueRight, row.Y, selected);
            var left = new Rectangle(valueRight - valueWidth - 16, row.Y + 3, 12, 14);
            DrawArrow(batch, left, -1, selected);
            DrawArrow(batch, right, 1, selected);
            input.Region(left, Tag, i * 3 + 1);
            input.Region(right, Tag, i * 3 + 2);
        }

        var help = UiDraw.Wrap(Small, Help(All[Cursor]), area.Width - 16);
        var y = area.Y + All.Length * (RowHeight + 3) + 6;
        for (var l = 0; l < Math.Min(2, help.Count); l++)
            UiDraw.Text(batch, Small, help[l], new Vector2(area.X + 8, y + l * 10), Dim);
    }

    /// <summary>Draws the value right-aligned at <paramref name="right"/>; returns its width.</summary>
    private int DrawValue(SpriteBatch batch, Option option, int right, int y, bool selected)
    {
        var settings = Settings;
        switch (option)
        {
            case Option.Music or Option.Effects or Option.Cries:
                var value = option == Option.Music ? settings.MusicVolume : option == Option.Effects ? settings.EffectsVolume : settings.CriesVolume;
                const int pip = 5, gap = 2;
                var width = Settings.MaxVolume * (pip + gap) - gap;
                for (var k = 0; k < Settings.MaxVolume; k++)
                {
                    var box = new Rectangle(right - width + k * (pip + gap), y + 5 + (k < value ? 0 : 2), pip, k < value ? 10 : 6);
                    services.Primitives.Rect(batch, new Rectangle(box.X - 1, box.Y - 1, box.Width + 2, box.Height + 2), Ink);
                    services.Primitives.Rect(batch, box, k < value ? (selected ? Gold : Color.Lerp(Gold, Dim, 0.4f)) : Off);
                }

                return width;
            default:
                var (text, on) = option switch
                {
                    Option.DamageNumbers => (settings.ShowDamageNumbers ? "SÌ" : "NO", settings.ShowDamageNumbers),
                    Option.FullScreen => (settings.FullScreen ? "SÌ" : "NO", settings.FullScreen),
                    Option.Shake => (settings.ShakePercent == 0 ? "NO" : $"{settings.ShakePercent}%", settings.ShakePercent > 0),
                    Option.HudSize => ($"{Hud.EffectivePercent(settings.HudPercent, services.Screen.Zoom)}%", true),
                    Option.Resolution => (services.Resolution() is var r ? $"{r.X}x{r.Y}" : "", true),
                    _ => (settings.FillScreen ? "PIENA" : "NITIDA", true),
                };
                var numeric = option is Option.HudSize or Option.Resolution || option == Option.Shake && settings.ShakePercent > 0;
                var font = numeric ? services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize) : Body;
                var size = UiDraw.Measure(font, text);
                UiDraw.Text(batch, font, text, new Vector2(right - size.X, y + (font == Body ? 2 : -2)), on ? Gold : Dim);
                return (int)size.X;
        }
    }

    private void DrawArrow(SpriteBatch batch, Rectangle box, int direction, bool selected)
    {
        var p = services.Primitives;
        var hovered = services.Input.PointerAwake && box.Contains(services.Input.Pointer);
        var color = hovered ? Paper : selected ? Gold : Dim * 0.7f;
        var cx = box.Center.X;
        var cy = box.Center.Y;
        for (var k = 0; k < 4; k++)
        {
            var x = direction < 0 ? cx - 2 + k : cx + 1 - k;
            p.Rect(batch, new Rectangle(x, cy - k, 1, k * 2 + 1), color);
        }
    }
}
