using System.Numerics;
using PokeRogue.Core.Run;

namespace PokeRogue.Core.Tests.Run;

public class SpatialGridTests
{
    [Fact]
    public void Query_finds_every_enemy_a_brute_force_search_finds()
    {
        var pool = new EnemyPool(2000);
        var random = new Random(11);
        var species = TestContent.Species(19);
        for (var i = 0; i < 1500; i++)
            pool.Spawn(species, new Vector2(random.Next(-1500, 1500), random.Next(-1500, 1500)));

        var grid = new SpatialGrid(32f, 64);
        grid.Rebuild(pool, Vector2.Zero);

        for (var q = 0; q < 50; q++)
        {
            var center = new Vector2(random.Next(-1200, 1200), random.Next(-1200, 1200));
            const float radius = 40f;

            var expected = new HashSet<int>();
            for (var i = 0; i < pool.HighWater; i++)
            {
                if (Vector2.Distance(pool[i].Position, center) <= radius)
                    expected.Add(i);
            }

            var found = new HashSet<int>();
            grid.Query(center, radius, ref found, (int index, ref HashSet<int> set) => set.Add(index));

            Assert.Subset(found, expected);
        }
    }
}
