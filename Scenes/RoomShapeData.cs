using System.Collections.Generic;

namespace ZebraBear.Scenes;

/// <summary>
/// Stores the floor-tile grid for a room being edited.
/// A tile at (col, row) is "filled" if it's in the set.
/// The grid is bounded to MaxSize x MaxSize.
/// Wall edges are derived on-the-fly from empty neighbours.
/// </summary>
public class RoomShapeData
{
    public const int MaxSize = 20;

    // Set of filled tile coordinates (col, row) — (0,0) is top-left
    private readonly HashSet<(int col, int row)> _tiles = new();

    public IReadOnlyCollection<(int col, int row)> Tiles => _tiles;

    public bool IsFilled(int col, int row) => _tiles.Contains((col, row));

    public void Fill(int col, int row)
    {
        if (col < 0 || row < 0 || col >= MaxSize || row >= MaxSize) return;
        _tiles.Add((col, row));
    }

    public void Clear(int col, int row) => _tiles.Remove((col, row));

    public int TileCount => _tiles.Count;

    /// <summary>
    /// Returns which of the four edges of tile (col,row) are wall edges —
    /// i.e. the neighbour in that direction is empty or out of bounds.
    /// </summary>
    public WallEdges GetWalls(int col, int row)
    {
        if (!IsFilled(col, row)) return WallEdges.None;
        var w = WallEdges.None;
        if (!IsFilled(col,     row - 1)) w |= WallEdges.North;
        if (!IsFilled(col,     row + 1)) w |= WallEdges.South;
        if (!IsFilled(col - 1, row    )) w |= WallEdges.West;
        if (!IsFilled(col + 1, row    )) w |= WallEdges.East;
        return w;
    }

    /// <summary>Seed with the default 10x10 square.</summary>
    public void FillDefault()
    {
        _tiles.Clear();
        int offset = (MaxSize - 10) / 2;
        for (int r = offset; r < offset + 10; r++)
            for (int c = offset; c < offset + 10; c++)
                _tiles.Add((c, r));
    }

    /// <summary>Serialise to a compact string for JSON storage (e.g. "3,4;3,5;4,4").</summary>
    public string Serialise()
    {
        var parts = new List<string>();
        foreach (var (c, r) in _tiles) parts.Add($"{c},{r}");
        return string.Join(";", parts);
    }

    /// <summary>Deserialise from a compact string.</summary>
    public void Deserialise(string data)
    {
        _tiles.Clear();
        if (string.IsNullOrWhiteSpace(data)) return;
        foreach (var part in data.Split(';'))
        {
            var kv = part.Split(',');
            if (kv.Length == 2 && int.TryParse(kv[0], out int c) && int.TryParse(kv[1], out int r))
                _tiles.Add((c, r));
        }
    }
}

[System.Flags]
public enum WallEdges
{
    None  = 0,
    North = 1,
    South = 2,
    West  = 4,
    East  = 8,
}