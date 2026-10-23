using System.Numerics;

namespace PokeRogue.Core.Run;

/// <summary>
/// Uniform grid over a square window centred on the player, rebuilt every tick with a counting sort.
/// Positions outside the window are clamped into the border cells, so queries stay correct, just less selective.
/// </summary>
public sealed class SpatialGrid
{
    private readonly int _cellsPerSide;
    private readonly float _cellSize;
    private readonly int[] _cellStart;
    private readonly int[] _cursor;
    private int[] _entries = new int[256];
    private int[] _cellOf = new int[256];
    private Vector2 _origin;

    public SpatialGrid(float cellSize, int cellsPerSide)
    {
        _cellSize = cellSize;
        _cellsPerSide = cellsPerSide;
        _cellStart = new int[cellsPerSide * cellsPerSide + 1];
        _cursor = new int[_cellStart.Length];
    }

    public float CellSize => _cellSize;

    /// <summary>Rebuilds the grid from the alive enemies of <paramref name="pool"/>.</summary>
    public void Rebuild(EnemyPool pool, Vector2 center)
    {
        _origin = center - new Vector2(_cellsPerSide * _cellSize / 2f);
        var items = pool.Items;
        if (_cellOf.Length < items.Length)
        {
            _cellOf = new int[items.Length * 2];
            _entries = new int[items.Length * 2];
        }

        Array.Clear(_cellStart);
        for (var i = 0; i < items.Length; i++)
        {
            if (!items[i].IsAlive)
            {
                _cellOf[i] = -1;
                continue;
            }

            var cell = CellIndex(items[i].Position);
            _cellOf[i] = cell;
            _cellStart[cell + 1]++;
        }

        for (var c = 1; c < _cellStart.Length; c++)
            _cellStart[c] += _cellStart[c - 1];

        _cellStart.AsSpan().CopyTo(_cursor);
        for (var i = 0; i < items.Length; i++)
        {
            if (_cellOf[i] >= 0)
                _entries[_cursor[_cellOf[i]]++] = i;
        }
    }

    /// <summary>Calls <paramref name="visit"/> for every enemy index in cells overlapping the circle. Callers still test exact distance.</summary>
    public void Query<TState>(Vector2 center, float radius, ref TState state, QueryVisitor<TState> visit)
        where TState : allows ref struct
    {
        var (x0, y0) = Cell(center - new Vector2(radius));
        var (x1, y1) = Cell(center + new Vector2(radius));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var cell = y * _cellsPerSide + x;
                for (var k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    visit(_entries[k], ref state);
            }
        }
    }

    private int CellIndex(Vector2 position)
    {
        var (x, y) = Cell(position);
        return y * _cellsPerSide + x;
    }

    private (int X, int Y) Cell(Vector2 position)
    {
        var x = (int)MathF.Floor((position.X - _origin.X) / _cellSize);
        var y = (int)MathF.Floor((position.Y - _origin.Y) / _cellSize);
        return (Math.Clamp(x, 0, _cellsPerSide - 1), Math.Clamp(y, 0, _cellsPerSide - 1));
    }
}

public delegate void QueryVisitor<TState>(int index, ref TState state)
    where TState : allows ref struct;
