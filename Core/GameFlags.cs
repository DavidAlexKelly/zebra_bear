using ZebraBear.Core;

namespace ZebraBear.Core;

/// <summary>
/// SHIM — delegates to GameContext.Instance.
///
/// Kept for source compatibility during the GameContext migration.
/// Once all call-sites reference GameContext directly, delete this class.
/// </summary>
public static class GameFlags
{
    public static void Set(string flag)        => GameContext.Instance.SetFlag(flag);
    public static void Clear(string flag)      => GameContext.Instance.ClearFlag(flag);
    public static bool IsSet(string flag)      => GameContext.Instance.IsFlagSet(flag);
    public static void Reset()                 => GameContext.Instance.ResetFlags();
}