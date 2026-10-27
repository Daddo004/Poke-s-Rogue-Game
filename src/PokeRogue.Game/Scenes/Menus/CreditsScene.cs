using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The credits (phase 8.4), rolling up the screen: who made what the game is built from. The sprite artists come from
/// content/generated/CREDITS.md, written by the DataTool from PMD Sprite Collab's own credits; Discord ids with no name
/// are counted, not listed. Up and down (or the wheel) scroll by hand; [ESC] goes back.
/// </summary>
public sealed class CreditsScene : MenuScene
{
    private const float RollSpeed = 0.35f;
    private const int LinePitch = 12;

    private readonly List<(string Text, Kind Kind)> _lines = [];
    private float _scroll;

    private enum Kind
    {
        Title,
        Heading,
        Text,
        Names,
        Space,
    }

    public CreditsScene(GameServices services) : base(services)
    {
        Add("POKÉROGUE", Kind.Title);
        Add("UN ROGUELITE CON I POKÉMON, FATTO PER GIOCO", Kind.Text);
        Add("PROGETTO PERSONALE, NON COMMERCIALE E NON DISTRIBUITO", Kind.Text);
        Add("", Kind.Space);
        Add("Pokémon", Kind.Heading);
        Add("POKÉMON © NINTENDO, CREATURES INC., GAME FREAK INC.", Kind.Text);
        Add("", Kind.Space);
        Add("Musica ed effetti sonori", Kind.Heading);
        Add("COMPOSTI PER QUESTO GIOCO E SUONATI DAL SUO CHIP SONORO", Kind.Text);
        Add("I TUOI FILE IN CONTENT/AUDIO PRENDONO IL LORO POSTO", Kind.Text);
        Add("", Kind.Space);
        AddSpriteCredits(Path.Combine(services.ContentRoot, "generated", "CREDITS.md"));
        Add("", Kind.Space);
        Add("Dati e versi", Kind.Heading);
        Add("POKEAPI E POKEAPI/CRIES", Kind.Text);
        Add("", Kind.Space);
        Add("Effetti e allenatori", Kind.Heading);
        Add("POKÉMON SHOWDOWN CLIENT", Kind.Text);
        Add("", Kind.Space);
        Add("Font (SIL Open Font License)", Kind.Heading);
        Add("PIXELIFY SANS,SILKSCREEN, JERSEY 10, PRESS START 2P, VT323", Kind.Text);
        Add("", Kind.Space);
        Add("Librerie", Kind.Heading);
        Add("MONOGAME, FONTSTASHSHARP, NVORBIS, .NET", Kind.Text);
        Add("", Kind.Space);
        Add("Grazie per aver giocato!", Kind.Heading);
    }

    private void Add(string text, Kind kind) => _lines.Add((text, kind));

    /// <summary>The sprite artists, several to a line; the Discord ids with no name only counted.</summary>
    private void AddSpriteCredits(string path)
    {
        Add("Sprite e ritratti", Kind.Heading);
        Add("PMD SPRITE COLLAB (CC BY-NC 4.0)", Kind.Text);
        if (!File.Exists(path))
            return;

        var names = new List<string>();
        var anonymous = 0;
        var inSprites = false;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("## "))
            {
                inSprites = line.Contains("Sprite", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSprites || !line.StartsWith("- "))
                continue;

            var name = line[2..];
            var count = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (count > 0)
                name = name[..count];
            name = name.Replace("\\", "");
            if (name.StartsWith("<@"))
                anonymous++;
            else
                names.Add(name.Trim('<', '>'));
        }

        // Four names to a line.
        for (var i = 0; i < names.Count; i += 4)
            Add(string.Join("   ", names.Skip(i).Take(4)), Kind.Names);
        if (anonymous > 0)
            Add($"E ALTRI {anonymous} ARTISTI DI DISCORD", Kind.Text);
    }

    private int Height => _lines.Sum(l => Pitch(l.Kind));

    private static int Pitch(Kind kind) => kind switch { Kind.Title => 40, Kind.Heading => 20, Kind.Space => 14, _ => LinePitch };

    /// <summary>Previews: the credits rolled this far.</summary>
    public void RollTo(float scroll) => _scroll = scroll;

    protected override void UpdateMenu()
    {
        if (Up())
            _scroll -= 36f;
        else if (Down())
            _scroll += 36f;
        else
            _scroll += RollSpeed;

        // Round again once everything has gone by.
        if (_scroll > Height + 40)
            _scroll = -Services.Screen.Height + 60;
        if (_scroll < -Services.Screen.Height + 60)
            _scroll = -Services.Screen.Height + 60;

        if (Back() || Confirm())
            Services.Switch(new MainMenuScene(Services, cursor: MainMenuScene.CreditsRow));
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var width = Services.Screen.Width;
        var title = Services.Fonts.Title(Services.Style);
        var y = 40f - _scroll + Services.Screen.Height - 100;
        foreach (var (text, kind) in _lines)
        {
            if (y > -40 && y < Services.Screen.Height)
            {
                switch (kind)
                {
                    case Kind.Title:
                        UiDraw.TextCentered(batch, title, text, width / 2f, y, Gold, Ink);
                        break;
                    case Kind.Heading:
                        UiDraw.TextCentered(batch, Body, text, width / 2f, y + 2, Gold, Ink);
                        break;
                    case Kind.Names:
                        UiDraw.TextCentered(batch, Small, text, width / 2f, y, Paper);
                        break;
                    case Kind.Text:
                        UiDraw.TextCentered(batch, Small, text, width / 2f, y, Dim);
                        break;
                }
            }

            y += Pitch(kind);
        }

        // Fades at the top and bottom, so lines come and go softly.
        for (var i = 0; i < 24; i++)
        {
            var alpha = 1f - i / 24f;
            p.Rect(batch, new Rectangle(0, i, width, 1), Background * alpha);
            p.Rect(batch, new Rectangle(0, Services.Screen.Height - 28 - i, width, 1), Background * alpha);
        }

        p.Rect(batch, new Rectangle(0, Services.Screen.Height - 28, width, 28), Background);
        DrawHeader(batch, "CREDITI");
        DrawFooter(batch, "[SU/GIÙ] SCORRI     [ESC] INDIETRO");
    }
}
