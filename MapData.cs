using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ZebraBear;

/// <summary>
/// A room node on the 2D map.
/// Position is in map-space (0-1 normalised), scaled to fit the map panel at draw time.
/// </summary>
public class MapRoom
{
    public string  Id;           // matches Scene identifier, e.g. "MainHall"
    public string  Label;        // display name shown on the map
    public Vector2 Position;     // normalised 0-1 position within the map canvas
    public Vector2 Size;         // normalised size
    public bool    Discovered;   // greyed out if false
}

/// <summary>
/// A connection between two rooms on the map.
/// Drawn as a line between the centres of the two rooms.
/// </summary>
public class MapConnection
{
    public string FromId;
    public string ToId;
}

/// <summary>
/// Holds all map data for the game.
/// Add rooms and connections here as you build new areas.
/// The PauseMenu reads from this to draw the map tab.
///
/// Usage — add a room:
///   Rooms.Add(new MapRoom { Id = "Library", Label = "Library",
///       Position = new Vector2(0.6f, 0.3f), Size = new Vector2(0.18f, 0.12f) });
///
/// Usage — connect two rooms:
///   Connections.Add(new MapConnection { FromId = "MainHall", ToId = "Library" });
///
/// Usage — mark a room discovered when the player enters it:
///   MapData.SetDiscovered("Library");
///
/// Usage — set current room:
///   MapData.CurrentRoomId = "Library";
/// </summary>
public static class MapData
{
    public static string CurrentRoomId = "MainHall";

    public static readonly List<MapRoom> Rooms = new()
    {
        new MapRoom
        {
            Id         = "MainHall",
            Label      = "Main Hall",
            Position   = new Vector2(0.25f, 0.4f),
            Size       = new Vector2(0.22f, 0.14f),
            Discovered = true
        },
        new MapRoom
        {
            Id         = "Room2",
            Label      = "???",
            Position   = new Vector2(0.65f, 0.4f),
            Size       = new Vector2(0.22f, 0.14f),
            Discovered = false
        },
    };

    public static readonly List<MapConnection> Connections = new()
    {
        new MapConnection { FromId = "MainHall", ToId = "Room2" },
    };

    public static void SetDiscovered(string roomId)
    {
        var room = Rooms.Find(r => r.Id == roomId);
        if (room != null)
        {
            room.Discovered = true;
            // Once discovered, show the real name
            if (roomId == "Room2") room.Label = "???";  // update to real name when known
        }
    }
}