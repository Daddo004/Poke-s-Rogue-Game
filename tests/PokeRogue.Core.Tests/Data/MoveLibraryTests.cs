using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Tests.Data;

public class MoveLibraryTests
{
    /// <summary>Moves whose animation is still waiting for approval; every other move is offered.</summary>
    private static readonly string[] AwaitingApproval = [];

    [Fact]
    public void Every_type_of_generations_one_to_four_has_at_least_two_moves()
    {
        var byType = TestContent.Moves.Value.All.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(17, byType.Count);
        Assert.All(byType, entry => Assert.True(entry.Value >= 2, $"{entry.Key} has {entry.Value} moves"));
    }

    [Fact]
    public void Loads_every_move_with_eight_levels()
    {
        var library = TestContent.Moves.Value;
        var baseMoves = library.All.Where(m => !m.IsEvolution).ToList();

        Assert.Equal(35, baseMoves.Count);
        Assert.All(baseMoves, m => Assert.Equal(MoveLibrary.MaxLevels, m.MaxLevel));
        Assert.Equal(Enum.GetValues<MoveArchetype>().Length, baseMoves.Select(m => m.Archetype).Distinct().Count());
    }

    [Fact]
    public void Each_of_the_first_twelve_moves_evolves_with_its_own_item()
    {
        var library = TestContent.Moves.Value;
        var evolutions = library.All.Where(m => m.IsEvolution).ToList();

        Assert.Equal(12, evolutions.Count);
        Assert.All(evolutions, e =>
        {
            Assert.Equal(1, e.MaxLevel);
            Assert.False(library[e.EvolvesFrom!].IsEvolution);
            Assert.NotNull(TestContent.Item(e.EvolveItem!));
        });
        Assert.Equal(12, evolutions.Select(e => e.EvolveItem).Distinct().Count());

        // Approved in phase 2.6: offered as evolutions, never as new moves.
        Assert.Equal(12, library.Evolutions.Count);
        Assert.DoesNotContain(library.Enabled, m => m.IsEvolution);
    }

    [Fact]
    public void Name_type_and_category_come_from_the_catalogue()
    {
        var ember = TestContent.Move("ember");

        Assert.Equal("Braciere", ember.NameIt);
        Assert.Equal(PokemonType.Fire, ember.Type);
        Assert.Equal(MoveCategory.Special, ember.Category);
        Assert.Equal(StatusKind.Burn, ember.Status);
    }

    [Fact]
    public void Thundershock_keeps_the_levels_approved_in_phase_1_4()
    {
        var levels = TestContent.Move("thundershock").Levels;

        (float Power, float Cooldown, float Range, float Knockback, int Chains)[] expected =
        [
            (10, 1.0f, 150, 40, 0), (12, 0.95f, 160, 40, 1), (14, 0.9f, 170, 45, 1), (16, 0.85f, 180, 45, 2),
            (18, 0.8f, 190, 50, 2), (21, 0.75f, 200, 50, 3), (24, 0.7f, 210, 55, 3), (28, 0.6f, 220, 60, 4),
        ];

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Power, levels[i].Power);
            Assert.Equal(expected[i].Cooldown, levels[i].Cooldown, 3);
            Assert.Equal(expected[i].Range, levels[i].Range);
            Assert.Equal(expected[i].Knockback, levels[i].Knockback);
            Assert.Equal(expected[i].Chains, levels[i].Chains);
        }
    }

    [Fact]
    public void Fields_missing_from_the_json_keep_their_documented_defaults()
    {
        // Regression: the JSON source generator used to reset init-only properties to 0/false when absent.
        var thundershock = TestContent.Move("thundershock");
        var level = thundershock.Levels[0];

        Assert.True(thundershock.Enabled);
        Assert.Equal(1, level.Amount);
        Assert.Equal(220f, level.Speed);
        Assert.Equal(5f, level.Radius);
        Assert.Equal(1, level.Pierce);
        Assert.Equal(0.5f, level.HitInterval);
        Assert.Equal(270f, level.TurnRate);
    }

    [Fact]
    public void Only_moves_with_an_approved_animation_are_offered()
    {
        var disabled = TestContent.Moves.Value.All.Where(m => !m.Enabled).Select(m => m.Id).Order();

        Assert.Equal(AwaitingApproval.Order(), disabled);
    }

    [Fact]
    public void Status_moves_become_weak_damaging_moves_with_their_status()
    {
        var thunderWave = TestContent.Move("thunder-wave");

        Assert.Equal("Tuononda", thunderWave.NameIt);
        Assert.Equal(MoveCategory.Special, thunderWave.Category);
        Assert.Equal(StatusKind.Paralysis, thunderWave.Status);
        Assert.InRange(thunderWave.Levels[0].Power, 1f, 2f);
    }

    [Fact]
    public void Unknown_moves_need_explicit_name_type_and_category()
    {
        var file = new MoveFile(1, [new MoveSpec { Id = "not-a-move", Archetype = MoveArchetype.Projectile, Base = new MoveLevel { Power = 5 } }]);

        Assert.Throws<InvalidDataException>(() => MoveLibrary.From(file, GameDataFiles.LoadMoveCatalog(TestContent.Root)));
    }
}
