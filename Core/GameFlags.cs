using System.Collections.Generic;

namespace ZebraBear.Core;

/// <summary>
/// Lightweight runtime flag store.
///
/// Flags are plain strings. JSON interactions can set them via
/// { "type": "setFlag", "flag": "foundNote" } and dialogue nodes
/// can gate lines behind them.
///
/// Flags are not persisted between sessions yet — that's handled
/// by the save/load system when implemented.
/// </summary>
public static class GameFlags
{
    private static readonly HashSet<string> _flags =
        new(System.StringComparer.OrdinalIgnoreCase);

    public static void  Set(string flag)          => _flags.Add(flag);
    public static void  Clear(string flag)        => _flags.Remove(flag);
    public static bool  IsSet(string flag)        => _flags.Contains(flag);
    public static void  Reset()                   => _flags.Clear();
}