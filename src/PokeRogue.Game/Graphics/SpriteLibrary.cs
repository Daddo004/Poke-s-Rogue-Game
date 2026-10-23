using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;

namespace PokeRogue.Game.Graphics;

/// <summary>Lazily loads and caches Pokémon sprite sets from content/sprites.</summary>
public sealed class SpriteLibrary(GraphicsDevice device, string contentRoot)
{
    private readonly Dictionary<int, PokemonSpriteSet?> _cache = [];
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);

    public PokemonSpriteSet? Get(int speciesId)
    {
        if (!_cache.TryGetValue(speciesId, out var set))
            _cache[speciesId] = set = Load(speciesId);
        return set;
    }

    /// <summary>Any loose PNG under content/ (portraits, trainers, fx), cached by relative path.</summary>
    public Texture2D? Texture(string relativePath)
    {
        if (!_textures.TryGetValue(relativePath, out var texture))
        {
            var path = Path.Combine(contentRoot, relativePath);
            _textures[relativePath] = texture = File.Exists(path) ? TextureLoader.Load(device, path) : null;
        }

        return texture;
    }

    public Texture2D? Portrait(int speciesId) => Texture(Path.Combine("portraits", $"{speciesId:D4}.png"));

    private PokemonSpriteSet? Load(int speciesId)
    {
        var folder = ContentPaths.SpriteFolder(contentRoot, speciesId);
        var animDataPath = Path.Combine(folder, PmdAnimations.AnimDataFile);
        if (File.Exists(animDataPath))
        {
            var data = PmdAnimData.Load(animDataPath);
            var sheets = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var sheetName in data.Anims.Values.Select(a => a.SheetName).Distinct())
            {
                var path = Path.Combine(folder, PmdAnimations.SheetFile(sheetName));
                if (File.Exists(path))
                    sheets[sheetName] = TextureLoader.Load(device, path);
            }

            if (sheets.Count > 0)
                return new PokemonSpriteSet(device, speciesId, data, sheets);
        }

        return LoadFallback(speciesId);
    }

    /// <summary>A static HGSS sprite wrapped as a one-frame, one-direction "Idle" animation.</summary>
    private PokemonSpriteSet? LoadFallback(int speciesId)
    {
        var path = Path.Combine(contentRoot, "sprites", "fallback", $"{speciesId:D4}.png");
        if (!File.Exists(path))
            return null;

        var texture = TextureLoader.Load(device, path);
        var anim = new PmdAnim(PmdAnimations.Idle, PmdAnimations.Idle, texture.Width, texture.Height, [60], null, null, null);
        var data = new PmdAnimData(1, new Dictionary<string, PmdAnim> { [PmdAnimations.Idle] = anim });
        return new PokemonSpriteSet(device, speciesId, data, new() { [PmdAnimations.Idle] = texture }) { IsFallback = true };
    }
}
