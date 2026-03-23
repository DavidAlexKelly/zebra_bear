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

    /// <summary>
    /// Controls which scene geometry is used for this room.
    /// Values (case-insensitive):
    ///   "box"   — standard rectangular room (default)
    ///   "plus"  — plus-shaped hub room (PlusRoom3D geometry)
    ///
    /// Set in map.json:
    ///   { "id": "Hub", "sceneType": "plus", ... }
    /// </summary>
    public string SceneType = "box";
}

public class MapConnection
{
    public string FromId;
    public string ToId;
}

/// <summary>
/// Holds all map data for the game.
/// Populated by GameLoader.LoadMap() from Data/map.json.
/// </summary>
public static class MapData
{
    public static string CurrentRoomId = "MainHall";

    public static readonly List<MapRoom>       Rooms       = new();
    public static readonly List<MapConnection> Connections = new();

    public static void SetDiscovered(string roomId)
    {
        var room = Rooms.Find(r => r.Id == roomId);
        if (room != null) room.Discovered = true;
    }
}