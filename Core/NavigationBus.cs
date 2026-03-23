using ZebraBear.Core;

namespace ZebraBear.Core;

/// <summary>
/// SHIM — delegates to GameContext.Instance.
///
/// Kept for source compatibility during the GameContext migration.
/// Once all call-sites use GameContext directly, delete this file.
///
/// Original location: NavigationBus.cs (project root)
/// Replace the existing file contents with this.
/// </summary>
public static class NavigationBus
{
    public static bool   HasRequest         => GameContext.Instance.HasNavigationRequest;
    public static string PendingDestination => GameContext.Instance.PendingNavigation;

    public static void RequestNavigate(string roomId) =>
        GameContext.Instance.RequestNavigate(roomId);

    public static string Consume() =>
        GameContext.Instance.ConsumeNavigation();
}