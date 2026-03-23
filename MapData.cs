using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ZebraBear;

public class MapRoom
{
    public string  Id;
    public string  Label;
    public Vector2 Position;
    public Vector2 Size;
    public bool    Discovered;
}

public class MapConnection
{
    public string FromId;
    public string ToId;
}

/// <summary>
/// Holds all map data for the game.
///
/// Rooms, connections, and the start room are now loaded from
/// Data/map.json by GameLoader.LoadMap(). Do not add entries here
/// directly — edit the JSON file instead.
///
/// Runtime helpers (SetDiscovered, CurrentRoomId) remain here.
/// </summary>
public static class MapData
{
    public static string CurrentRoomId = "MainHall";

    /// <summary>Populated by GameLoader.LoadMap().</summary>
    public static readonly List<MapRoom>       Rooms       = new();

    /// <summary>Populated by GameLoader.LoadMap().</summary>
    public static readonly List<MapConnection> Connections = new();

    /// <summary>
    /// Mark a room as discovered (e.g. when the player enters it).
    /// Reveals it on the pause-menu map.
    /// </summary>
    public static void SetDiscovered(string roomId)
    {
        var room = Rooms.Find(r => r.Id == roomId);
        if (room != null) room.Discovered = true;
    }
}