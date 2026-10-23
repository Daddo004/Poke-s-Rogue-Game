using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>Debug: digit legibility of the candidate fonts at their crisp sizes (5 vs S, 0 vs O, 1 vs I).</summary>
public sealed class DigitsPreviewScene(GameServices services) : Scene(services)
{
    private const string Sample = "LIVELLO 5!  Lv 15  0123456789  S5 O0 I1  Tuonoshock 25!";

    public override void Update() { }

    public override void Draw(SpriteBatch batch)
    {
        Services.Device.Clear(new Color(22, 18, 36));
        batch.Begin(samplerState: SamplerState.PointClamp);
        (string Font, float Size)[] rows =
        [
            ("PixelifySans", 12), ("PixelifySans", 16), ("PixelifySans", 20), ("PixelifySans", 24),
            ("Silkscreen-Regular", 16), ("VT323-Regular", 16), ("VT323-Regular", 20), ("Jersey10-Regular", 16), ("Jersey10-Regular", 20),
        ];

        var label = Services.Fonts.Get("Silkscreen-Regular", 8);
        var y = 8f;
        foreach (var (font, size) in rows)
        {
            UiDraw.Text(batch, label, $"{font.Replace("-Regular", "")} {size}", new Vector2(8, y + 3), new Color(160, 152, 190));
            UiDraw.Text(batch, Services.Fonts.Get(font, size), Sample, new Vector2(150, y), new Color(244, 240, 228));
            y += size + 10;
        }

        batch.End();
    }
}
