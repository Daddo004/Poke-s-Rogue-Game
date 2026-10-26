using System.Numerics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Items;

namespace PokeRogue.Core.Tests.Data;

public class CharacterLibraryTests
{
    private static CharacterLibrary Library => TestContent.Characters.Value;

    [Fact]
    public void Holds_the_users_roster()
    {
        // The roster of 2026-10-26: 12 starters, 7 fossils, 26 legendaries, 9 mythicals and 27 more lines.
        Assert.Equal(81, Library.All.Count);
        Assert.Equal(12, Library.All.Count(c => c.Group == CharacterGroup.Starter));
        Assert.Equal(7, Library.All.Count(c => c.Group == CharacterGroup.Fossil));
        Assert.Equal(26, Library.All.Count(c => c.Group == CharacterGroup.Legendary));
        Assert.Equal(9, Library.All.Count(c => c.Group == CharacterGroup.Mythical));
        Assert.Equal(153, Library.All.Sum(c => c.Stages.Count));

        var legendaries = TestContent.Pokedex.Value.Species.Where(s => (s.IsLegendary || s.IsMythical) && s.Generation <= 4).Select(s => s.Id);
        Assert.All(legendaries, id => Assert.NotNull(Library.WithSpecies(id)));
        Assert.All(Library.All.SelectMany(c => c.Stages), s => Assert.InRange(s.Species.Id, 1, 493));
    }

    [Fact]
    public void Pikachu_is_the_starting_character()
    {
        var pikachu = Library[CharacterLibrary.DefaultId];

        Assert.Equal(UnlockKind.Start, pikachu.Unlock.Kind);
        Assert.Equal("thundershock", pikachu.MainMove.Id);
        Assert.Equal("raichu", Assert.Single(pikachu.EvolutionsOf(pikachu.Stages[0])).Species.Key);
        Assert.Equal("thunder-stone", pikachu.Stages[1].Item);
        Assert.Equal(new[] { "pikachu" }, Library.All.Where(c => c.Unlock.Kind == UnlockKind.Start).Select(c => c.Id));
    }

    [Fact]
    public void Branching_lines_offer_a_choice()
    {
        var eevee = Library["eevee"];
        Assert.Equal(7, eevee.EvolutionsOf(eevee.Stages[0]).Count());
        Assert.All(eevee.EvolutionsOf(eevee.Stages[0]), s => Assert.Equal(20, s.Level));

        var ralts = Library["ralts"];
        var kirlia = ralts.Stages.Single(s => s.Species.Key == "kirlia");
        Assert.Equal(new[] { "gardevoir", "gallade" }, ralts.EvolutionsOf(kirlia).Select(s => s.Species.Key));
    }

    [Fact]
    public void A_stage_can_change_the_ability()
    {
        var magikarp = Library["magikarp"];

        Assert.Equal("swift-swim", magikarp.Stages[0].Ability.Id);
        Assert.Equal("intimidate", magikarp.Stages[1].Ability.Id);
        Assert.Equal("overgrow", Library["bulbasaur"].Stages[2].Ability.Id);
    }

    [Fact]
    public void Evolution_levels_rise_along_each_line()
    {
        foreach (var character in Library.All)
        {
            foreach (var stage in character.Stages.Where(s => s.FromIndex is not null && s.Level is not null))
                Assert.True(stage.Level > (character.Stages[stage.FromIndex!.Value].Level ?? 1), $"{character.Id}: {stage.Species.Key}");
        }
    }

    [Fact]
    public void Playing_a_character_applies_its_modifiers_and_main_move()
    {
        var snorlax = Library["snorlax"];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, snorlax.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360));
        var baseHp = run.Player.MaxHp;
        var baseSpeed = run.Player.MoveSpeed;

        run.UseCharacter(snorlax);

        Assert.Same(snorlax, run.Character);
        Assert.Equal(MathF.Round(baseHp * 1.2f), run.Player.MaxHp);
        Assert.Equal(run.Player.MaxHp, run.Player.Hp);
        Assert.Equal(baseSpeed * 0.85f, run.Player.MoveSpeed, 3);
        Assert.Equal(0.9f, run.Player.Growth, 3);
        Assert.Equal("hyper-voice", Assert.Single(run.Player.Moves).Definition.Id);
    }

    [Fact]
    public void Item_bonuses_add_to_the_characters()
    {
        var snorlax = Library["snorlax"];
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, snorlax.First.Id, [new SpawnEntry(19, 1)], new Vector2(640, 360));
        run.UseCharacter(snorlax);
        var withCharacter = run.Player.MoveSpeed;

        run.Player.Items.Add(new ItemInstance(TestContent.Item("carbos")));
        run.Player.RecalculateStats();

        Assert.Equal(withCharacter / 0.85f * 0.95f, run.Player.MoveSpeed, 3);
    }

    [Fact]
    public void Rejects_broken_lines()
    {
        static CharacterSpec Line(params StageSpec[] stages) => new()
        {
            Id = "test",
            Species = stages,
            Ability = new AbilitySpec { Id = "static", NameIt = "Statico" },
            MainMove = "thundershock",
            SecondaryMove = "thunder-wave",
            Special = PokemonType.Electric,
            Unlock = new UnlockSpec { Kind = UnlockKind.Start },
        };

        CharacterLibrary Build(CharacterSpec spec) => CharacterLibrary.From(
            new CharacterFile(1, [spec], [new EvolutionItemSpec { Id = "thunder-stone", NameIt = "Pietratuono" }]), TestContent.Pokedex.Value.Species, TestContent.Moves.Value);

        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachoo" })));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }, new StageSpec { Key = "gengar", Level = 20 })));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }, new StageSpec { Key = "raichu" })));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu", Level = 5 })));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }) with { MainMove = "thunder" }));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }) with { SecondaryMove = "thundershock" }));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }) with { Unlock = new UnlockSpec { Kind = UnlockKind.Shop } }));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }, new StageSpec { Key = "raichu", Item = "moon-stone" })));
        Assert.Throws<InvalidDataException>(() => Build(Line(new StageSpec { Key = "pikachu" }, new StageSpec { Key = "raichu", Item = "thunder-stone", Level = 20 })));
        Assert.Equal("Pietratuono", Build(Line(new StageSpec { Key = "pikachu" }, new StageSpec { Key = "raichu", Item = "thunder-stone" }))["test"].Stages[1].ItemNameIt);
    }
}
