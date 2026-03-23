using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ZebraBear.Core;

namespace ZebraBear;

// MapRoom and MapConnection are plain data — no changes needed.

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
    /// </summary>
    public string SceneType = "box";
}

public class MapConnection
{
    public string FromId;
    public string ToId;
}

/// <summary>
/// SHIM — delegates to GameContext.Instance.
///
/// Kept for source compatibility during the GameContext migration.
/// Once all call-sites reference GameContext directly, delete this class.
/// </summary>
public static class MapData
{
    public static string CurrentRoomId
    {
        get => GameContext.Instance.CurrentRoomId;
        set => GameContext.Instance.CurrentRoomId = value;
    }

    public static List<MapRoom>       Rooms       => GameContext.Instance.Rooms;
    public static List<MapConnection> Connections => GameContext.Instance.Connections;

    public static void SetDiscovered(string roomId) =>
        GameContext.Instance.SetDiscovered(roomId);
}