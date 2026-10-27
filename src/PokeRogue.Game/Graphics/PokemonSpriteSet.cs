using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;

namespace PokeRogue.Game.Graphics;

/// <summary>One Pokémon's PMD animations: a sheet per animation, 8 direction rows, frames left to right.</summary>
public sealed class PokemonSpriteSet
{
    private readonly Dictionary<string, Texture2D> _sheets;
    private readonly Dictionary<string, Texture2D> _silhouettes = new(StringComparer.Ordinal);
    private readonly GraphicsDevice _device;

    public PokemonSpriteSet(GraphicsDevice device, int speciesId, PmdAnimData data, Dictionary<string, Texture2D> sheets)
    {
        _device = device;
        SpeciesId = speciesId;
        Data = data;
        _sheets = sheets;
        FootOffset = MeasureFootOffset();
        HeadHeight = MeasureHeadHeight();
    }

    public int SpeciesId { get; }

    public PmdAnimData Data { get; }

    /// <summary>Pixels from the frame centre down to the feet; frames are drawn so the feet sit on the entity position.</summary>
    public int FootOffset { get; }

    /// <summary>Pixels from the feet up to the top of the head when standing; status icons float above it.</summary>
    public int HeadHeight { get; }

    /// <summary>True when only a static fallback image exists (no PMD sprite yet).</summary>
    public bool IsFallback { get; init; }

    public bool Has(string anim) => Data.Anims.TryGetValue(anim, out var a) && _sheets.ContainsKey(a.SheetName);

    public PmdAnim? Anim(string name) =>
        Data.Anims.TryGetValue(name, out var a) && _sheets.ContainsKey(a.SheetName) ? a : null;

    /// <summary>Best available animation: the requested one, otherwise Idle, otherwise Walk.</summary>
    public PmdAnim Resolve(string name) =>
        Anim(name) ?? Anim(PmdAnimations.Idle) ?? Anim(PmdAnimations.Walk) ?? Data.Anims.Values.First(a => _sheets.ContainsKey(a.SheetName));

    public int DirectionRows(PmdAnim anim) => System.Math.Max(1, _sheets[anim.SheetName].Height / anim.FrameHeight);

    /// <summary>How far round the colour wheel a shiny's colours turn (phase 3.5): PMD has no shiny sheets downloaded.</summary>
    public const float ShinyHueDegrees = 150f;

    public void Draw(SpriteBatch batch, PmdAnim anim, int frame, Direction8 direction, Vector2 feet, Color tint, float scale = 1f, bool silhouette = false, bool shiny = false)
    {
        if (!_sheets.ContainsKey(anim.SheetName))
            return;

        var sheet = silhouette ? Silhouette(anim.SheetName) : shiny ? Shiny(anim.SheetName) : _sheets[anim.SheetName];
        var row = DirectionRows(anim) > 1 ? (int)direction : 0;
        var source = new Rectangle(frame * anim.FrameWidth, row * anim.FrameHeight, anim.FrameWidth, anim.FrameHeight);
        var origin = new Vector2(anim.FrameWidth / 2f, anim.FrameHeight / 2f + FootOffset);
        batch.Draw(sheet, Round(feet), source, tint, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private readonly Dictionary<string, Texture2D> _shinies = new(StringComparer.Ordinal);

    private Texture2D Shiny(string sheetName)
    {
        if (!_shinies.TryGetValue(sheetName, out var texture))
            _shinies[sheetName] = texture = TextureLoader.CreateHueShift(_device, _sheets[sheetName], ShinyHueDegrees);
        return texture;
    }

    private Texture2D Silhouette(string sheetName)
    {
        if (!_silhouettes.TryGetValue(sheetName, out var texture))
            _silhouettes[sheetName] = texture = TextureLoader.CreateSilhouette(_device, _sheets[sheetName]);
        return texture;
    }

    private int MeasureFootOffset()
    {
        var anim = Anim(PmdAnimations.Idle) ?? Anim(PmdAnimations.Walk);
        if (anim is null)
            return 0;

        // Lowest opaque pixel of the facing-down row, across all frames.
        var sheet = _sheets[anim.SheetName];
        var pixels = new Color[sheet.Width * anim.FrameHeight];
        sheet.GetData(0, new Rectangle(0, 0, sheet.Width, anim.FrameHeight), pixels, 0, pixels.Length);

        for (var y = anim.FrameHeight - 1; y >= 0; y--)
        {
            for (var x = 0; x < sheet.Width; x++)
            {
                if (pixels[y * sheet.Width + x].A > 32)
                    return y - anim.FrameHeight / 2;
            }
        }

        return 0;
    }

    private int MeasureHeadHeight()
    {
        var anim = Anim(PmdAnimations.Idle) ?? Anim(PmdAnimations.Walk);
        if (anim is null)
            return 16;

        // Highest opaque pixel of the facing-down row, across all frames, measured from the feet.
        var sheet = _sheets[anim.SheetName];
        var pixels = new Color[sheet.Width * anim.FrameHeight];
        sheet.GetData(0, new Rectangle(0, 0, sheet.Width, anim.FrameHeight), pixels, 0, pixels.Length);

        for (var y = 0; y < anim.FrameHeight; y++)
        {
            for (var x = 0; x < sheet.Width; x++)
            {
                if (pixels[y * sheet.Width + x].A > 32)
                    return anim.FrameHeight / 2 + FootOffset - y;
            }
        }

        return 16;
    }

    private static Vector2 Round(Vector2 v) => VirtualScreen.Snap(v);
}
