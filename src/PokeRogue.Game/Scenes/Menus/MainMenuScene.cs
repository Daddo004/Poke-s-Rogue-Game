using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The main menu (Block 6, after the title screen since phase 8.2): play, the shop's upgrades, the collection, the
/// options, the credits, quit. The last character and trainer played stand on the right; the save's totals are at the
/// bottom.
/// </summary>
public sealed class MainMenuScene : MenuScene
{
    private static readonly string[] Rows = ["Gioca", "Potenziamenti", "Collezione", "Opzioni", "Crediti", "Esci"];

    public const int OptionsRow = 3;
    public const int CreditsRow = 4;

    private readonly MenuPokemon _pokemon;
    private int _cursor;

    public MainMenuScene(GameServices services, int cursor = 0) : base(services)
    {
        _pokemon = new MenuPokemon(services);
        _cursor = cursor;
    }

    /// <summary>A run from the menus on a level at a difficulty (Block 7): played with the meta-progression, and back to this menu at the end.</summary>
    public static GameplayScene Run(GameServices services, CharacterDefinition character, TrainerDefinition trainer, string levelId,
        Core.Meta.Difficulty difficulty, TrainerDefinition? challenge = null) =>
        new(services, character.Id, trainerId: trainer.Id)
        {
            Meta = services.Meta,
            LevelId = levelId,
            Difficulty = difficulty,
            ChallengeId = challenge?.Id,
            BackToMenu = () => new MainMenuScene(services),
        };

    public override void Enter()
    {
        var character = Services.Characters.Find(Meta.Data.LastCharacter ?? "") ?? Services.Characters[CharacterLibrary.DefaultId];
        _pokemon.Show(character.First.Id);
    }

    protected override void UpdateMenu()
    {
        _pokemon.Update();
        if (Up())
            _cursor = (_cursor + Rows.Length - 1) % Rows.Length;
        if (Down())
            _cursor = (_cursor + 1) % Rows.Length;
        if (Pointed("menu") is { } pointed)
            _cursor = pointed;
        if (Back())
            Services.Switch(new TitleScene(Services));

        if (ClickedOn("menu") is { } clicked)
            _cursor = clicked;
        else if (!Confirm())
            return;

        switch (_cursor)
        {
            case 0:
                Services.Switch(new PokemonSelectScene(Services));
                break;
            case 1:
                Services.Switch(new UpgradesScene(Services));
                break;
            case 2:
                Services.Switch(new CollectionScene(Services));
                break;
            case OptionsRow:
                Services.Switch(new OptionsScene(Services));
                break;
            case CreditsRow:
                Services.Switch(new CreditsScene(Services));
                break;
            default:
                Services.Exit();
                break;
        }
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        DrawCoins(batch, Services.Screen.Width - 16, 10);

        UiDraw.TextOutlined(batch, Services.Fonts.Title(Services.Style), "POKÉROGUE", new Vector2(32, 34), Gold, Ink);
        UiDraw.Text(batch, Small, "UN ROGUELITE CON I POKÉMON DI KANTO, JOHTO, HOENN E SINNOH", new Vector2(34, 70), Dim);

        for (var i = 0; i < Rows.Length; i++)
        {
            var row = new Rectangle(32, 100 + i * 29, 200, 24);
            var selected = i == _cursor;
            Clickable(row, "menu", i);
            UiDraw.Frame(batch, p, row, selected ? new UiTheme(new Color(60, 48, 24, 235), Gold, new Color(140, 110, 40), Ink, Paper, Dim) : Services.Style.Theme);
            if (selected)
                p.Outline(batch, new Rectangle(row.X - 1, row.Y - 1, row.Width + 2, row.Height + 2), Age / 20 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.5f), 1);
            UiDraw.Text(batch, Body, Rows[i], new Vector2(row.X + 12, row.Y + 4), selected ? Paper : Dim);
        }

        // The last team played, on a stage.
        p.Ellipse(batch, new Vector2(452, 236), 120, 26, Color.White * 0.06f);
        var trainer = Services.Trainers.Value.Find(Meta.Data.LastTrainer ?? "") ?? Services.Trainers.Value[TrainerLibrary.DefaultId];
        if (Services.TrainerPortraits.Sprite(trainer) is { } sprite)
        {
            p.Ellipse(batch, new Vector2(400, 236), 26, 8, new Color(0, 0, 0, 90));
            batch.Draw(sprite, new Rectangle(400 - sprite.Width, 236 - sprite.Height * 2 + 6, sprite.Width * 2, sprite.Height * 2), Color.White);
        }

        _pokemon.Draw(batch, new Vector2(500, 238), 3f, Color.White);

        // The save's totals.
        var data = Meta.Data;
        var seen = data.Pokedex.Count(e => e.Value.Seen > 0);
        var line = $"PARTITE {data.Totals.Runs}     VITTORIE {data.Totals.Victories}     MISSIONI {data.Missions.Count}/{Meta.Missions.All.Count}     " +
                   $"POKÉDEX {seen}/{Meta.Pokedex.Count}     MEDAGLIE {Meta.Medals}";
        var panel = new Rectangle(16, 286, Services.Screen.Width - 32, 26);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        var width = UiDraw.Measure(Small, line).X;
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, line, new Vector2(panel.Center.X - width / 2f, panel.Y + 9), Paper);

        DrawFooter(batch, "[SU/GIÙ] SCEGLI     [INVIO] CONFERMA     [ESC] TITOLO");
    }
}
