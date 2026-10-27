using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Preview.Artboard;

public sealed partial class ArtboardScene
{
    private static readonly int[] RosterGrid =
    [
        1, 4, 7, 25, 133, 152,
        155, 158, 252, 255, 258, 387,
        390, 393, 447, 92, 63, 66,
        147, 246, 374, 443, 280, 143,
    ];

    private static readonly HashSet<int> Unlocked = [1, 4, 7, 25, 133, 152, 155, 158, 92, 63];

    private void DrawMenu(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var theme = UiTheme.Dark;
        Services.Device.Clear(new Color(22, 18, 36));

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);

        // Subtle diagonal stripes
        for (var x = -360; x < 640; x += 16)
            UiDraw.Line(batch, p, new Vector2(x, 360), new Vector2(x + 360, 0), Color.White * 0.03f, 6f);

        UiDraw.TextOutlined(batch, Body, "SCEGLI IL TUO POKÉMON", new Vector2(16, 10), Paper, Ink);
        batch.Draw(_art.Coin, new Vector2(560, 15), Color.White);
        UiDraw.TextOutlined(batch, Body, "2.340", new Vector2(570, 10), Gold, Ink);

        // Roster grid
        var selected = Array.IndexOf(RosterGrid, 25);
        for (var i = 0; i < RosterGrid.Length; i++)
        {
            var cell = new Rectangle(16 + i % 6 * 48, 40 + i / 6 * 48, 44, 44);
            var unlocked = Unlocked.Contains(RosterGrid[i]);
            UiDraw.Frame(batch, p, cell, theme);
            if (Services.Sprites.Portrait(RosterGrid[i]) is { } portrait)
                batch.Draw(portrait, new Rectangle(cell.X + 2, cell.Y + 2, 40, 40), unlocked ? Color.White : new Color(36, 32, 56));

            if (!unlocked)
            {
                var q = UiDraw.Measure(Body, "?");
                UiDraw.TextOutlined(batch, Body, "?", new Vector2(cell.Center.X - q.X / 2, cell.Center.Y - q.Y / 2), theme.TextDim, Ink);
            }

            if (i == selected)
            {
                var blink = _tick / 20 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.5f);
                p.Outline(batch, new Rectangle(cell.X - 2, cell.Y - 2, cell.Width + 4, cell.Height + 4), blink, 2);
            }
        }

        var hint = new Rectangle(16, 238, 284, 42);
        UiDraw.Frame(batch, p, hint, theme);
        UiDraw.Text(batch, Small, "BLOCCATO  Treecko", new Vector2(24, 244), theme.Text);
        UiDraw.Text(batch, Small, "Missione: completa Bosco Smeraldo (Difficile)", new Vector2(24, 256), theme.TextDim);
        UiDraw.Text(batch, Small, "oppure acquista per 800 monete", new Vector2(24, 266), theme.TextDim);

        DrawDetailPanel(batch, theme);

        UiDraw.Text(batch, Small, "[INVIO] Seleziona     [ESC] Indietro     [TAB] Allenatore     [F] Potenzia", new Vector2(16, 344), theme.TextDim);
        batch.End();
    }

    private void DrawDetailPanel(SpriteBatch batch, UiTheme theme)
    {
        var p = Services.Primitives;
        var panel = new Rectangle(316, 40, 308, 296);
        UiDraw.Frame(batch, p, panel, theme);

        // Stage with the animated sprite
        p.Ellipse(batch, new Vector2(470, 152), 90, 22, Color.White * 0.07f);
        p.Ellipse(batch, _hero.Position + new Vector2(0, 1), 30, 10, new Color(0, 0, 0, 90));
        DrawMon(batch, _hero);

        UiDraw.Text(batch, Body, "Pikachu", new Vector2(326, 46), theme.Text);
        UiDraw.Text(batch, Small, "#025  Pokémon Topo", new Vector2(326, 64), theme.TextDim);
        DrawTypePill(batch, new Rectangle(326, 76, 48, 11), PokemonType.Electric);

        // Evolution line
        UiDraw.Text(batch, Small, "Linea", new Vector2(560, 46), theme.TextDim);
        int[] line = [172, 25, 26];
        for (var i = 0; i < line.Length; i++)
        {
            if (Services.Sprites.Portrait(line[i]) is { } portrait)
                batch.Draw(portrait, new Rectangle(540 + i * 26, 58, 24, 24), line[i] == 25 ? Color.White : new Color(150, 150, 170));
        }

        // Base stats
        var species = Services.Species(25)!;
        (string Label, int Value)[] stats =
        [
            ("PS", species.BaseStats.Hp), ("Att", species.BaseStats.Attack), ("Dif", species.BaseStats.Defense),
            ("AttSp", species.BaseStats.SpAttack), ("DifSp", species.BaseStats.SpDefense), ("Vel", species.BaseStats.Speed),
        ];
        for (var i = 0; i < stats.Length; i++)
        {
            var y = 172 + i * 11;
            UiDraw.Text(batch, Small, stats[i].Label, new Vector2(326, y), theme.TextDim);
            var ratio = stats[i].Value / 160f;
            var color = stats[i].Value >= 90 ? new Color(88, 208, 96) : stats[i].Value >= 60 ? new Color(248, 200, 40) : new Color(232, 120, 64);
            UiDraw.Bar(batch, p, new Rectangle(364, y + 1, 110, 6), ratio, color, new Color(24, 20, 40), Ink);
            UiDraw.Text(batch, Small, stats[i].Value.ToString(), new Vector2(480, y), theme.Text);
        }

        // Ability
        UiDraw.Text(batch, Small, "ABILITÀ", new Vector2(510, 172), Gold);
        UiDraw.Text(batch, Small, "Statico", new Vector2(510, 183), theme.Text);
        UiDraw.Text(batch, Small, "Chi ti tocca può", new Vector2(510, 194), theme.TextDim);
        UiDraw.Text(batch, Small, "restare paralizzato.", new Vector2(510, 204), theme.TextDim);

        // Moves
        (string Slot, PokemonType Type, string Name, int? Price)[] moves =
        [
            ("Principale", PokemonType.Electric, "Tuonoshock", null),
            ("Secondaria", PokemonType.Steel, "Codacciaio", 500),
            ("Speciale", PokemonType.Electric, "Fulmine", 1200),
        ];
        for (var i = 0; i < moves.Length; i++)
        {
            var y = 246 + i * 26;
            var row = new Rectangle(324, y, 292, 22);
            p.Rect(batch, row, Color.White * 0.05f);
            UiDraw.Text(batch, Small, moves[i].Slot.ToUpperInvariant(), new Vector2(330, y + 2), moves[i].Price is null ? Gold : theme.TextDim);
            DrawTypePill(batch, new Rectangle(330, y + 11, 44, 9), moves[i].Type);
            UiDraw.Text(batch, Body, moves[i].Name, new Vector2(382, y + 3), moves[i].Price is null ? theme.Text : theme.TextDim);
            if (moves[i].Price is { } price)
            {
                batch.Draw(_art.Coin, new Vector2(560, y + 8), Color.White);
                UiDraw.Text(batch, Small, price.ToString(), new Vector2(570, y + 7), Gold);
            }
            else
            {
                UiDraw.Text(batch, Small, "SBLOCCATA", new Vector2(552, y + 7), new Color(120, 220, 120));
            }
        }
    }

    private void DrawStyleBoard(SpriteBatch batch)
    {
        var p = Services.Primitives;
        Services.Device.Clear(new Color(22, 18, 36));
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);

        var label = Services.Fonts.Get("Silkscreen-Regular", 8);
        UiDraw.Text(batch, label, "TAVOLA DI STILE  -  FONT", new Vector2(8, 6), Gold);

        // Fonts at their crisp native sizes
        (string Name, float Size)[] fonts = [("Silkscreen-Regular", 8), ("Silkscreen-Regular", 16), ("PressStart2P-Regular", 8), ("PixelifySans", 16), ("VT323-Regular", 16), ("Jersey10-Regular", 16)];
        var y = 18f;
        foreach (var (name, size) in fonts)
        {
            UiDraw.Text(batch, label, $"{name.Replace("-Regular", "")} {size}", new Vector2(8, y + 2), new Color(160, 152, 190));
            UiDraw.Text(batch, Services.Fonts.Get(name, size), "Pikachu usa Tuonoshock! È superefficace! 0123456789", new Vector2(120, y), Paper);
            y += size + 6;
        }

        // Type pills
        UiDraw.Text(batch, label, "COLORI DEI TIPI", new Vector2(8, 128), Gold);
        var types = PokemonTypes.All.ToList();
        for (var i = 0; i < types.Count; i++)
            DrawTypePill(batch, new Rectangle(8 + i % 9 * 58, 140 + i / 9 * 14, 54, 11), types[i]);

        // Frames
        UiDraw.Text(batch, label, "CORNICI  A: scura (stile VS)   B: chiara (stile DS)", new Vector2(8, 174), Gold);
        UiDraw.Frame(batch, p, new Rectangle(8, 186, 150, 46), UiTheme.Dark);
        UiDraw.Text(batch, Body, "Pannello A", new Vector2(16, 190), UiTheme.Dark.Text);
        UiDraw.Text(batch, Small, "Testo secondario", new Vector2(16, 212), UiTheme.Dark.TextDim);
        UiDraw.Frame(batch, p, new Rectangle(166, 186, 150, 46), UiTheme.Light);
        UiDraw.Text(batch, Body, "Pannello B", new Vector2(174, 190), UiTheme.Light.Text);
        UiDraw.Text(batch, Small, "Testo secondario", new Vector2(174, 212), UiTheme.Light.TextDim);

        // Bars
        UiDraw.Text(batch, label, "BARRE", new Vector2(8, 240), Gold);
        UiDraw.Bar(batch, p, new Rectangle(8, 252, 96, 6), 0.8f, UiDraw.HpColor(0.8f), new Color(40, 24, 32), Ink);
        UiDraw.Bar(batch, p, new Rectangle(112, 252, 96, 6), 0.4f, UiDraw.HpColor(0.4f), new Color(40, 24, 32), Ink);
        UiDraw.Bar(batch, p, new Rectangle(216, 252, 96, 6), 0.15f, UiDraw.HpColor(0.15f), new Color(40, 24, 32), Ink);
        UiDraw.Bar(batch, p, new Rectangle(8, 264, 200, 6), 0.62f, new Color(72, 152, 248), new Color(24, 20, 44), Ink);
        UiDraw.Bar(batch, p, new Rectangle(216, 264, 96, 6), 0.7f, Gold, new Color(40, 32, 20), Ink);
        UiDraw.Text(batch, label, "PS  >50% / <50% / <20%     ESP          SPECIALE", new Vector2(8, 274), new Color(160, 152, 190));

        // Pickups and chests
        UiDraw.Text(batch, label, "RACCOGLIBILI E FORZIERI", new Vector2(8, 290), Gold);
        batch.Draw(_art.GemSmall, new Vector2(12, 304), Color.White);
        batch.Draw(_art.GemMedium, new Vector2(24, 304), Color.White);
        batch.Draw(_art.GemLarge, new Vector2(36, 304), Color.White);
        batch.Draw(_art.Coin, new Vector2(50, 305), Color.White);
        string[] icons = ["poke-ball", "great-ball", "ultra-ball", "rare-candy", "potion", "max-repel", "exp-share", "leftovers", "fire-stone", "thunder-stone"];
        for (var i = 0; i < icons.Length; i++)
        {
            if (Services.Sprites.Texture(Path.Combine("icons", "items", icons[i] + ".png")) is { } icon)
                batch.Draw(icon, new Vector2(64 + i * 26, 296), new Rectangle(3, 3, 24, 24), Color.White);
        }

        UiDraw.Text(batch, label, "gemme Esp  moneta  Poké/Mega/Ultra Ball = forzieri 1/3/5   strumenti", new Vector2(8, 324), new Color(160, 152, 190));

        DrawFxComparison(batch);
        batch.End();
    }

    private void DrawFxComparison(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var label = Services.Fonts.Get("Silkscreen-Regular", 8);
        var panel = new Rectangle(330, 174, 302, 140);
        UiDraw.Frame(batch, p, panel, UiTheme.Dark);
        UiDraw.Text(batch, label, "EFFETTI MOSSE", new Vector2(338, 180), Gold);
        UiDraw.Text(batch, label, "A: pixel procedurali", new Vector2(338, 194), Paper);
        UiDraw.Text(batch, label, "B: sprite Showdown ridotti", new Vector2(488, 194), Paper);

        batch.End();
        batch.BeginVirtual(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        DrawBolt(batch, new Vector2(346, 240), new Vector2(460, 226), _tick / 4 * 13);
        DrawSpark(batch, new Vector2(460, 226), _tick);
        DrawPixelFlame(batch, new Vector2(370, 290));
        DrawPixelFlame(batch, new Vector2(410, 286));
        batch.End();

        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        string[] fx = ["electroball", "fireball", "leaf1", "shadowball", "iceball", "waterwisp"];
        for (var i = 0; i < fx.Length; i++)
        {
            if (Services.Sprites.Texture(Path.Combine("fx", fx[i] + ".png")) is { } texture)
            {
                var scale = 24f / System.Math.Max(texture.Width, texture.Height);
                batch.Draw(texture, new Vector2(500 + i % 3 * 40, 222 + i / 3 * 40), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        batch.End();
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
    }

    private void DrawPixelFlame(SpriteBatch batch, Vector2 at)
    {
        var p = Services.Primitives;
        var random = new Random((int)at.X);
        for (var i = 0; i < 46; i++)
        {
            // Each particle loops upward on its own phase so the flame flickers continuously.
            var phase = random.NextSingle();
            var t = (phase + _tick * (0.018f + random.NextSingle() * 0.01f)) % 1f;
            var sway = MathF.Sin((t * 6f) + phase * 10f) * 2f;
            var offset = new Vector2(random.Next(-7, 8) * (1 - t) * 0.9f + sway, -t * 30);
            var color = t < 0.25f
                ? Color.Lerp(new Color(255, 255, 220), new Color(255, 200, 64), t * 4)
                : Color.Lerp(new Color(255, 160, 40), new Color(200, 40, 16), (t - 0.25f) / 0.75f);
            var size = System.Math.Max(1, (int)(6 - t * 5));
            p.Rect(batch, new Rectangle((int)(at.X + offset.X - size / 2f), (int)(at.Y + offset.Y), size, size), color * (1 - t * 0.7f));
        }
    }
}
