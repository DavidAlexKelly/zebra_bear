using System;

namespace ZebraBear.Core;

/// <summary>
/// Decoupled navigation event channel.
///
/// JSON-driven interact callbacks can't hold a Game reference, so instead
/// they call NavigationBus.RequestNavigate(roomId).  Game.Update() polls
/// PendingDestination each frame and performs the actual transition.
///
/// Usage — in Game.Update():
///   if (NavigationBus.HasRequest)
///   {
///       var dest = NavigationBus.Consume();
///       switch (dest)
///       {
///           case "Room2": GoToRoom2(); break;
///           // add new rooms here
///       }
///   }
///
/// Usage — anywhere else (JSON callback, scene code, etc.):
///   NavigationBus.RequestNavigate("Room2");
/// </summary>
public static class NavigationBus
{
    private static string _pending = null;

    public static bool   HasRequest        => _pending != null;
    public static string PendingDestination => _pending;

    /// <summary>Queue a navigation request. Overwrites any previous unprocessed request.</summary>
    public static void RequestNavigate(string roomId) => _pending = roomId;

    /// <summary>Consume and return the pending destination, clearing the queue.</summary>
    public static string Consume()
    {
        var dest = _pending;
        _pending = null;
        return dest;
    }
}