using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Scenes.Menus;

namespace PokeRogue.Game.Preview;

/// <summary>
/// The collectible cards (phase 10) on the demo save, with some cards owned and packs waiting. Variants: menu (the main
/// menu's badge), album, visore (one card, <c>--carta id</c>, leaning towards <c>--mouse x,y</c>), rarita (one card of
/// each rarity side by side), and the pack's opening: bustina, strappo, uscita, carta, rara (a rare card glowing face
/// down), girata (that card just turned), riepilogo, and sequenza (the whole opening by itself, for a GIF).
/// </summary>
public sealed class CardsPreviewScene : Scene
{
    private readonly string _variant;
    private readonly string? _card;
    private readonly Vector2? _mouse;
    private Scene _inner = null!;
    private CardsScene? _cards;
    private int _tick;

    private readonly List<CardView> _views = [];
    private readonly List<Texture2D> _composed = [];

    public CardsPreviewScene(GameServices services, string variant, string? card, string? mouse) : base(services)
    {
        _variant = variant;
        _card = card;
        if (mouse?.Split(',') is [var x, var y])
            _mouse = new Vector2(float.Parse(x), float.Parse(y));
        services.Meta = Demo(services);
    }

    public override bool HandlesEscape => true;

    public override void Enter()
    {
        if (_variant == "menu")
        {
            _inner = new MainMenuScene(Services, cursor: MainMenuScene.CardsRow);
            _inner.Enter();
            return;
        }

        if (_variant == "rarita")
        {
            _inner = this;
            return;
        }

        _cards = new CardsScene(Services, "pikachu") { PackRandom = new Random(4) };
        _inner = _cards;
        _inner.Enter();
        if (_variant == "album")
            _cards.Focus(1);
        if (_variant == "visore")
            _cards.Show(Services.Cards.Find(_card ?? "") ?? First(Services, CardRarity.Holo) ?? Services.Cards.All[0]);
        if (_variant is "bustina" or "strappo" or "uscita" or "carta" or "rara" or "girata" or "riepilogo" or "sequenza")
            _cards.OpenPack();
    }

    public override void Update()
    {
        _tick++;
        if (_mouse is { } at)
            Services.Input.SimulatedPointer = at;
        if (_inner == this)
        {
            for (var i = 0; i < _views.Count; i++)
                _views[i].Update(null, Vector2.Zero, Vector2.One, 40 + i * 37 + _tick);
            return;
        }

        if (_tick == 1 && _cards is not null)
        {
            switch (_variant)
            {
                case "strappo":
                    _cards.ShowPack(1, 22);
                    break;
                case "uscita":
                    _cards.ShowPack(2, 26);
                    break;
                case "carta":
                    _cards.ShowPack(3, 30, 0);
                    break;
                case "rara":
                    _cards.ShowPack(3, 36, 2);
                    break;
                case "girata":
                    _cards.ShowPack(3, 0, 2);
                    break;
                case "riepilogo":
                    _cards.ShowPack(4, 30);
                    break;
            }
        }

        // The whole opening by itself: a press every so often.
        if (_variant == "sequenza" && _tick % 45 == 30)
            _cards!.AdvancePack();

        _inner.Update();
    }

    public override void Draw(SpriteBatch batch)
    {
        if (_inner != this)
        {
            _inner.Draw(batch);
            return;
        }

        DrawRarities(batch);
    }

    /// <summary>Pikachu's line in every rarity, each leaning a little, side by side.</summary>
    private void DrawRarities(SpriteBatch batch)
    {
        var art = Services.CardArt;
        var cards = Enum.GetValues<CardRarity>().Select(r => First(Services, r)).OfType<CardDefinition>().ToList();
        while (_views.Count < cards.Count)
            _views.Add(new CardView(art));

        art.LoadFaces(cards, cards.Count);
        var back = art.Back(batch);
        _composed.Clear();
        for (var i = 0; i < cards.Count; i++)
            _composed.Add(art.Face(cards[i]) is { } face ? _views[i].Compose(batch, face, cards[i].Rarity, 60 + i * 23 + _tick) : back);

        Services.Device.Clear(new Color(22, 18, 36));
        for (var i = 0; i < cards.Count; i++)
            _views[i].Draw(batch, _composed[i], back, new Vector2(70 + i * 125, 172), new Vector2(114, 157));

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        var small = Services.Fonts.Small(Services.Style);
        for (var i = 0; i < cards.Count; i++)
            UiDraw.TextCentered(batch, small, cards[i].Spec.NameIt.ToUpperInvariant(), 70 + i * 125, 262, new Color(248, 208, 72));
        batch.End();
    }

    /// <summary>The demo save, plus a Pikachu line played for a while (won at Normale and Difficile) and a few Charmander cards.</summary>
    private static MetaProgress Demo(GameServices services)
    {
        var meta = MetaPreviewScene.Demo(services);
        var data = meta.Data;
        var pikachu = data.CardLineOf("pikachu");
        pikachu.Bond = 128;
        pikachu.Level = services.Cards.BondLevel(128);
        pikachu.Packs = 2;
        pikachu.Reached = ["pikachu", "raichu"];
        pikachu.Won = [Difficulty.Normal, Difficulty.Hard];
        pikachu.Runs = 14;
        pikachu.Kills = 18_430;
        pikachu.BestLevel = 61;
        pikachu.BestTicks = RunClock.TicksPerClockMinute * 15 + 40;

        var charmander = data.CardLineOf("charmander");
        charmander.Bond = 34;
        charmander.Level = services.Cards.BondLevel(34);
        charmander.Packs = 1;
        charmander.Reached = ["charmander", "charmeleon"];
        charmander.Runs = 4;

        // Owned: two common Pikachu line cards, a rare, an ultra rare (new) and a secret one; Charmander's first common.
        var set = services.Cards.Of(services.Characters["pikachu"]);
        var owned = set.Where(c => c.Rarity == CardRarity.Base).Take(2)
            .Concat(new[] { CardRarity.Holo, CardRarity.FullArt, CardRarity.Crown }.Select(r => First(services, r)).OfType<CardDefinition>())
            .Concat(services.Cards.Of(services.Characters["charmander"]).Take(1));
        data.Cards = owned.Select(c => c.Id).ToList();
        data.NewCards = First(services, CardRarity.FullArt) is { } fresh ? [fresh.Id] : [];
        return meta;
    }

    /// <summary>The first card of the Pikachu line at <paramref name="rarity"/>.</summary>
    private static CardDefinition? First(GameServices services, CardRarity rarity) =>
        services.Cards.Of(services.Characters["pikachu"]).FirstOrDefault(c => c.Rarity == rarity);
}
