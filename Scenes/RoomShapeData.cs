//======== Scenes/RoomShapeData.cs ========
using System.Collections.Generic;

namespace ZebraBear.Scenes;

public class RoomShapeData
{
    public const int MaxSize = 20;

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

    public WallEdges GetWalls(int col, int row)
    {
        if (!IsFilled(col, row)) return WallEdges.None;
        var w = WallEdges.None;
        if (!IsFilled(col, row - 1)) w |= WallEdges.North;
        if (!IsFilled(col, row + 1)) w |= WallEdges.South;
        if (!IsFilled(col - 1, row)) w |= WallEdges.West;
        if (!IsFilled(col + 1, row)) w |= WallEdges.East;
        return w;
    }

    public void FillDefault()
    {
        _tiles.Clear();
        int offset = (MaxSize - 10) / 2;
        for (int r = offset; r < offset + 10; r++)
            for (int c = offset; c < offset + 10; c++)
                _tiles.Add((c, r));
    }

    public string Serialise()
    {
        var parts = new List<string>();
        foreach (var (c, r) in _tiles) parts.Add($"{c},{r}");
        return string.Join(";", parts);
    }

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

    private readonly List<PlacedObject> _objects = new();
    public IReadOnlyList<PlacedObject> Objects => _objects;
    public void AddObject(PlacedObject obj) => _objects.Add(obj);
    public void RemoveObject(PlacedObject obj) => _objects.Remove(obj);
    public PlacedObject ObjectAt(int col, int row)
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i].Col == col && _objects[i].Row == row) return _objects[i];
        return null;
    }

    private readonly List<PlacedCharacter> _characters = new();
    public IReadOnlyList<PlacedCharacter> Characters => _characters;
    public void AddCharacter(PlacedCharacter ch) => _characters.Add(ch);
    public void RemoveCharacter(PlacedCharacter ch) => _characters.Remove(ch);
    public PlacedCharacter CharacterAt(int col, int row)
    {
        for (int i = _characters.Count - 1; i >= 0; i--)
            if (_characters[i].Col == col && _characters[i].Row == row) return _characters[i];
        return null;
    }
}

public class PlacedObject
{
    public string Type;
    public int Col;
    public int Row;
    public string InteractionId = null;
}

public class PlacedCharacter
{
    public string Name = "";
    public int Col;
    public int Row;
    public int TintR = 180, TintG = 160, TintB = 220;
    public string InteractionId = null;
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