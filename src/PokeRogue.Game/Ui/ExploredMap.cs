using System.Numerics;

namespace PokeRogue.Game.Ui;

/// <summary>The ground the player has walked this run (phase 3.6), in square cells, for the map in the pause.</summary>
public sealed class ExploredMap
{
    /// <summary>World pixels per cell side.</summary>
    public const float CellSize = 160f;

    /// <summary>How far around the player counts as seen: about the screen.</summary>
    private const int Reach = 2;

    private readonly HashSet<(int X, int Y)> _cells = [];

    public IReadOnlyCollection<(int X, int Y)> Cells => _cells;

    public void Clear() => _cells.Clear();

    public void Visit(Vector2 position)
    {
        var cx = (int)MathF.Floor(position.X / CellSize);
        var cy = (int)MathF.Floor(position.Y / CellSize);
        for (var dy = -Reach + 1; dy <= Reach - 1; dy++)
        {
            for (var dx = -Reach; dx <= Reach; dx++)
                _cells.Add((cx + dx, cy + dy));
        }
    }
}
