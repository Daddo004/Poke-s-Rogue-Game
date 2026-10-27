using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run.Characters;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// The collectible cards' pictures (phase 10). A card's face is the TCG card's own picture from the TCGdex API
/// (content/cards/tcg, 600×825): loaded on demand with rounded corners and mipmaps, so it stays smooth both small in the
/// album and large in the viewer. A picture not downloaded yet is fetched from the API in the background and shows up a
/// moment later. The back and the packs are drawn by <see cref="CardFace"/>, in card units of <see cref="Width"/>×<see cref="Height"/>.
/// </summary>
public sealed class CardArt(GameServices services)
{
    public const int Width = 100;

    /// <summary>A TCG card is 63×88 mm: 100×138 units.</summary>
    public const int Height = 138;

    /// <summary>The back and the composed viewer card are drawn at 6 pixels a unit (600×828, the size of the API's pictures).</summary>
    public const int LargeScale = 6;

    private const int MaxFaces = 48;

    /// <summary>Multiplies what is below by the source's alpha: cuts a composed card to its rounded corners.</summary>
    public static readonly BlendState Mask = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.SourceAlpha,
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly Dictionary<string, Texture2D> _faces = [];
    private readonly LinkedList<string> _faceOrder = [];
    private readonly HashSet<string> _fetching = [];
    private readonly HashSet<string> _failed = [];
    private readonly Dictionary<(int Species, string Kind), Texture2D?> _pictures = [];
    private readonly Dictionary<int, Texture2D> _rounded = [];
    private readonly Dictionary<int, RenderTarget2D> _backs = [];
    private Texture2D? _glow;
    private Texture2D? _disc;
    private Texture2D? _foil;
    private Texture2D? _sparkles;
    private Texture2D? _stripes;
    private Dictionary<CardRarity, Texture2D>? _symbols;
    private CardMesh? _mesh;

    public GameServices Services { get; } = services;

    /// <summary>Draws cards and packs turned in 3D.</summary>
    public CardMesh Mesh => _mesh ??= new CardMesh(Services.Device);

    /// <summary>The card's face if it is loaded; null while it loads or downloads (<see cref="LoadFaces"/>).</summary>
    public Texture2D? Face(CardDefinition card)
    {
        if (!_faces.TryGetValue(card.Id, out var face))
            return null;
        _faceOrder.Remove(card.Id);
        _faceOrder.AddFirst(card.Id);
        return face;
    }

    /// <summary>The picture could not be had from the API (offline, or gone): the card shows its back.</summary>
    public bool Unavailable(CardDefinition card)
    {
        lock (_failed)
            return _failed.Contains(card.Id);
    }

    /// <summary>
    /// Loads up to <paramref name="budget"/> faces among <paramref name="wanted"/> that are on disk, and starts downloading
    /// from the API those that are not.
    /// </summary>
    public void LoadFaces(IEnumerable<CardDefinition> wanted, int budget = 4)
    {
        foreach (var card in wanted)
        {
            if (budget <= 0)
                break;
            if (_faces.ContainsKey(card.Id) || Unavailable(card))
                continue;

            var path = ContentPaths.TcgCard(Services.ContentRoot, card.Id);
            if (!File.Exists(path))
            {
                Fetch(card, path);
                continue;
            }

            try
            {
                _faces[card.Id] = LoadCard(Services.Device, path);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                lock (_failed)
                    _failed.Add(card.Id);
                continue;
            }

            _faceOrder.AddFirst(card.Id);
            budget--;
            if (_faces.Count > MaxFaces && _faceOrder.Last is { } oldest)
            {
                _faces[oldest.Value].Dispose();
                _faces.Remove(oldest.Value);
                _faceOrder.RemoveLast();
            }
        }
    }

    /// <summary>Downloads the card's picture from the TCGdex API in the background; the next <see cref="LoadFaces"/> finds it.</summary>
    private void Fetch(CardDefinition card, string path)
    {
        if (!_fetching.Add(card.Id))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(card.Tcg.ImageUrl);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path + ".part", bytes);
                File.Move(path + ".part", path, overwrite: true);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
            {
                lock (_failed)
                    _failed.Add(card.Id);
            }
        });
    }

    /// <summary>The cards' back, the same for every card (no batch open the first time).</summary>
    public Texture2D Back(SpriteBatch batch)
    {
        const int scale = LargeScale;
        if (_backs.TryGetValue(scale, out var back))
            return back;

        back = new RenderTarget2D(Services.Device, Width * scale, Height * scale, true, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        Draw(back, scale, () => CardFace.DrawBack(batch, this, scale));
        _backs[scale] = back;
        return back;
    }

    /// <summary>A pack of <paramref name="line"/>'s cards, drawn at <paramref name="scale"/> into a new target the caller keeps.</summary>
    public RenderTarget2D RenderPack(SpriteBatch batch, CharacterDefinition line, int scale)
    {
        var target = new RenderTarget2D(Services.Device, Width * scale, CardFace.PackHeight * scale, true, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        Draw(target, scale, () => CardFace.DrawPack(batch, this, line, scale));
        return target;
    }

    /// <summary>One of the pictures PokeAPI gave (content/cards): "home", the Pokémon HOME render of the packs.</summary>
    public Texture2D? Picture(int speciesId, string kind)
    {
        if (_pictures.TryGetValue((speciesId, kind), out var picture))
            return picture;

        var path = ContentPaths.CardArt(Services.ContentRoot, speciesId, kind);
        picture = File.Exists(path) ? Load(Services.Device, path, rounded: false) : null;
        _pictures[(speciesId, kind)] = picture;
        return picture;
    }

    /// <summary>Draws into <paramref name="target"/> with the fonts rasterised at <paramref name="scale"/>, then goes back to the screen.</summary>
    private void Draw(RenderTarget2D target, int scale, Action draw)
    {
        var device = Services.Device;
        var zoom = Services.Fonts.Zoom;
        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);
        Services.Fonts.Zoom = scale;
        draw();
        Services.Fonts.Zoom = zoom;
        device.SetRenderTarget(Services.Screen.Target);
    }

    // ---- Generated textures ----

    /// <summary>A white card shape with rounded corners at <paramref name="scale"/>, smooth at the edge.</summary>
    public Texture2D Rounded(int scale)
    {
        if (_rounded.TryGetValue(scale, out var texture))
            return texture;

        int w = Width * scale, h = Height * scale;
        var pixels = new Color[w * h];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Color.White * CornerAlpha(i % w, i / w, w, h);

        texture = new Texture2D(Services.Device, w, h);
        texture.SetData(pixels);
        _rounded[scale] = texture;
        return texture;
    }

    /// <summary>How much of pixel (x, y) of a <paramref name="w"/>×<paramref name="h"/> card lies inside its rounded corners.</summary>
    private static float CornerAlpha(int x, int y, int w, int h)
    {
        var radius = w * 0.05f;
        var dx = MathF.Max(0f, MathF.Max(radius - (x + 0.5f), x + 0.5f - (w - radius)));
        var dy = MathF.Max(0f, MathF.Max(radius - (y + 0.5f), y + 0.5f - (h - radius)));
        return Math.Clamp(radius - MathF.Sqrt(dx * dx + dy * dy) + 0.5f, 0f, 1f);
    }

    /// <summary>A soft white spot, brightest at the centre: backgrounds and the glare.</summary>
    public Texture2D Glow => _glow ??= Generate(128, 128, (u, v) =>
    {
        var d = MathF.Min(1f, MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
        return Color.White * ((1f - d) * (1f - d));
    });

    /// <summary>A white disc with a smooth edge.</summary>
    public Texture2D Disc => _disc ??= Generate(256, 256, (u, v) =>
    {
        var d = MathF.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 256f;
        return Color.White * Math.Clamp(128f - d, 0f, 1f);
    });

    /// <summary>The holo foil: soft diagonal rainbow bands that tile, slid by the card's tilt.</summary>
    public Texture2D Foil => _foil ??= Generate(256, 256, (u, v) =>
    {
        var t = u + v;
        var band = 0.5f + 0.5f * MathF.Sin(t * MathF.Tau * 2f);
        var (r, g, b) = Rainbow(t % 1f);
        var a = 0.35f + 0.65f * band * band;
        return new Color(r * a, g * a, b * a, a);
    });

    /// <summary>Small four-pointed glints scattered on a tile, for the rarest cards.</summary>
    public Texture2D Sparkles => _sparkles ??= MakeSparkles();

    /// <summary>Gold diagonal stripes.</summary>
    public Texture2D Stripes => _stripes ??= Generate(64, 64, (u, v) =>
        (u + v) % 0.5f < 0.25f ? new Color(150, 112, 34) : new Color(176, 138, 50));

    /// <summary>The rarity mark: ◆ (common and rare, repeated), ★ (ultra rare and special, repeated), ♛ (secret).</summary>
    public Texture2D Symbol(CardRarity rarity)
    {
        _symbols ??= new Dictionary<CardRarity, Texture2D>
        {
            [CardRarity.Base] = PixelArt.FromPattern(Services.Device, ["...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..."], White),
            [CardRarity.FullArt] = PixelArt.FromPattern(Services.Device, ["...#...", "...#...", "..###..", "#######", ".#####.", "..###..", ".##.##.", "##...##"], White),
            [CardRarity.Crown] = PixelArt.FromPattern(Services.Device, ["#..#..#", "##.#.##", "#######", "#######", ".#####."], White),
        };
        return rarity switch
        {
            CardRarity.Base or CardRarity.Holo => _symbols[CardRarity.Base],
            CardRarity.FullArt or CardRarity.Shiny => _symbols[CardRarity.FullArt],
            _ => _symbols[CardRarity.Crown],
        };
    }

    /// <summary>How many marks a rarity shows.</summary>
    public static int SymbolCount(CardRarity rarity) => rarity switch
    {
        CardRarity.Holo => 3,
        CardRarity.Shiny => 2,
        _ => 1,
    };

    private static readonly Dictionary<char, Color> White = new() { ['#'] = Color.White };

    private Texture2D MakeSparkles()
    {
        const int size = 256;
        var pixels = new float[size * size];
        var random = new Random(7);
        for (var i = 0; i < 46; i++)
        {
            int cx = random.Next(size), cy = random.Next(size);
            var arm = 3 + random.Next(8);
            for (var d = -arm; d <= arm; d++)
            {
                var a = 1f - MathF.Abs(d) / (arm + 1f);
                Plot(cx + d, cy, a);
                Plot(cx, cy + d, a);
            }
        }

        var colors = new Color[size * size];
        for (var i = 0; i < colors.Length; i++)
            colors[i] = Color.White * Math.Min(1f, pixels[i]);
        return WithMips(Services.Device, size, size, colors);

        void Plot(int x, int y, float a)
        {
            x = (x % size + size) % size;
            y = (y % size + size) % size;
            pixels[y * size + x] = MathF.Max(pixels[y * size + x], a);
        }
    }

    private Texture2D Generate(int w, int h, Func<float, float, Color> pixel)
    {
        var colors = new Color[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
                colors[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
        }

        return WithMips(Services.Device, w, h, colors);
    }

    private static (float R, float G, float B) Rainbow(float hue)
    {
        var h = hue * 6f;
        var x = 1f - MathF.Abs(h % 2f - 1f);
        return (int)h switch
        {
            0 => (1f, x, 0.3f),
            1 => (x, 1f, 0.3f),
            2 => (0.3f, 1f, x),
            3 => (0.3f, x, 1f),
            4 => (x, 0.3f, 1f),
            _ => (1f, 0.3f, x),
        };
    }

    /// <summary>A card's picture: the JPEG's square corners cut round like the printed card.</summary>
    private static Texture2D LoadCard(GraphicsDevice device, string path) => Load(device, path, rounded: true);

    /// <summary>A picture as a premultiplied texture with its whole mip chain (box filtered), so it shrinks without shimmering.</summary>
    private static Texture2D Load(GraphicsDevice device, string path, bool rounded)
    {
        using var stream = File.OpenRead(path);
        using var raw = Texture2D.FromStream(device, stream);
        int w = raw.Width, h = raw.Height;
        var pixels = new Color[w * h];
        raw.GetData(pixels);
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            var a = p.A / 255f * (rounded ? CornerAlpha(i % w, i / w, w, h) : 1f);
            pixels[i] = new Color(p.R / 255f * a, p.G / 255f * a, p.B / 255f * a, a);
        }

        return WithMips(device, w, h, pixels);
    }

    private static Texture2D WithMips(GraphicsDevice device, int w, int h, Color[] pixels)
    {
        var texture = new Texture2D(device, w, h, true, SurfaceFormat.Color);
        texture.SetData(0, null, pixels, 0, pixels.Length);
        for (var level = 1; level < texture.LevelCount; level++)
        {
            int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
            var next = new Color[nw * nh];
            for (var y = 0; y < nh; y++)
            {
                for (var x = 0; x < nw; x++)
                {
                    int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1), y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
                    var a = pixels[y0 * w + x0].ToVector4() + pixels[y0 * w + x1].ToVector4() + pixels[y1 * w + x0].ToVector4() + pixels[y1 * w + x1].ToVector4();
                    next[y * nw + x] = new Color(a / 4f);
                }
            }

            texture.SetData(level, null, next, 0, next.Length);
            (pixels, w, h) = (next, nw, nh);
        }

        return texture;
    }
}
