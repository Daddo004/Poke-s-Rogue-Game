using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Tests.Data;

public class ItemLibraryTests
{
    [Fact]
    public void Loads_every_item_of_the_plan()
    {
        string[] plan =
        [
            "muscle-band", "wise-glasses", "iron", "zinc", "hp-up", "leftovers", "quick-claw", "wide-lens", "light-clay",
            "carbos", "magnet", "lucky-egg", "amulet-coin", "scope-lens", "focus-sash", "metronome", "shell-bell",
        ];

        Assert.Equal(plan.Order(), TestContent.Items.Value.All.Select(i => i.Id).Order());
        Assert.All(TestContent.Items.Value.All, i => Assert.Equal(i.Id, i.IconKey));
    }

    [Fact]
    public void Item_effects_read_as_italian_sentences()
    {
        Assert.Equal("Danni dai nemici fisici -6%.", LevelUp.Describe(TestContent.Item("iron").PerLevel));
        Assert.Equal("+1 rinascita.", LevelUp.Describe(TestContent.Item("focus-sash").PerLevel));
        Assert.Equal("Recuperi il 1% dei danni inflitti.", LevelUp.Describe(TestContent.Item("shell-bell").PerLevel));
    }

    [Fact]
    public void Items_need_an_effect_and_a_sensible_number_of_levels()
    {
        var useless = new ItemFile(1, [new ItemSpec { Id = "rock", NameIt = "Sasso" }]);
        var endless = new ItemFile(1, [new ItemSpec { Id = "rock", NameIt = "Sasso", MaxLevel = 20, PerLevel = new ItemBonus(MaxHpPercent: 0.1f) }]);

        Assert.Throws<InvalidDataException>(() => ItemLibrary.From(useless));
        Assert.Throws<InvalidDataException>(() => ItemLibrary.From(endless));
    }
}
