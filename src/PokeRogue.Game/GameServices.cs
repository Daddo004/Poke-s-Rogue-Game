using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;
using PokeRogue.Game.Text;

namespace PokeRogue.Game;

/// <summary>Shared engine services handed to every scene.</summary>
public sealed class GameServices(GraphicsDevice device, string contentRoot, VirtualScreen screen)
{
    public GraphicsDevice Device { get; } = device;

    public string ContentRoot { get; } = contentRoot;

    public VirtualScreen Screen { get; } = screen;

    public InputState Input { get; } = new();

    public Primitives Primitives { get; } = new(device);

    public PickupArt Pickups { get; } = new(device);

    public MoveIcons MoveIcons { get; } = new(device);

    public StatusIcons StatusIcons { get; } = new(device);

    public BreakableArt Breakables { get; } = new(device);

    public SpriteLibrary Sprites { get; } = new(device, contentRoot);

    private ItemIcons? _itemIcons;

    /// <summary>Held item icons, with their palette swaps.</summary>
    public ItemIcons ItemIcons => _itemIcons ??= new ItemIcons(Device, Sprites);

    public Fonts Fonts { get; } = new(contentRoot);

    /// <summary>Pokémon cries (phase 3.4); muted for captures and with --muto.</summary>
    public Audio.Cries Cries { get; } = new(contentRoot);

    public UiStyle Style { get; } = UiStyle.Load(contentRoot);

    /// <summary>Closes the game (set by <see cref="PokeRogueGame"/>).</summary>
    public Action Exit { get; set; } = () => { };

    /// <summary>Player settings (phase 3.6). Captures and previews use the defaults and never save.</summary>
    public Settings Settings { get; set; } = new();

    /// <summary>Switches full screen on or off (set by <see cref="PokeRogueGame"/>).</summary>
    public Action<bool> SetFullScreen { get; set; } = _ => { };

    public Lazy<PokedexFile> Pokedex { get; } = new(() => GameDataFiles.LoadPokedex(contentRoot));

    public Lazy<TypeChart> TypeChart { get; } = new(() => new TypeChart(GameDataFiles.LoadTypeChart(contentRoot).Matchups));

    public Lazy<Core.Run.Moves.MoveLibrary> Moves { get; } = new(() => Core.Run.Moves.MoveLibrary.Load(contentRoot));

    public Lazy<Core.Run.Statuses.StatusLibrary> Statuses { get; } = new(() => Core.Run.Statuses.StatusLibrary.Load(contentRoot));

    public Lazy<Core.Run.Items.ItemLibrary> Items { get; } = new(() => Core.Run.Items.ItemLibrary.Load(contentRoot));

    public Lazy<Core.Run.Progression.LevelUpRules> LevelUpRules { get; } = new(() => Core.Run.Progression.LevelUpRules.Load(contentRoot));

    /// <summary>The special moves, one per type (phase 4.4).</summary>
    public Lazy<Core.Run.Specials.SpecialMoves> Specials { get; } = new(() => Core.Run.Specials.SpecialMoves.Load(contentRoot));

    private Core.Run.Characters.CharacterLibrary? _characters;

    /// <summary>The playable characters (phase 4.1).</summary>
    public Core.Run.Characters.CharacterLibrary Characters =>
        _characters ??= Core.Run.Characters.CharacterLibrary.Load(ContentRoot, Pokedex.Value.Species, Moves.Value);

    /// <summary>The trainers (phase 5.1).</summary>
    public Lazy<Core.Run.Trainers.TrainerLibrary> Trainers => _trainers ??= new(() =>
        Core.Run.Trainers.TrainerLibrary.Load(ContentRoot, Pokedex.Value.Species, Moves.Value));

    private Lazy<Core.Run.Trainers.TrainerLibrary>? _trainers;

    private TrainerPortraits? _trainerPortraits;

    /// <summary>The trainers' sprites and the head square of each for the HUD.</summary>
    public TrainerPortraits TrainerPortraits => _trainerPortraits ??= new TrainerPortraits(Sprites);

    public SpeciesData? Species(int id) => id >= 1 && id <= Pokedex.Value.Species.Count ? Pokedex.Value.Species[id - 1] : null;
}
