using System;
using System.Text.Json.Nodes;

namespace ZebraBear.Core;

/// <summary>
/// Builds Action&lt;int&gt; callbacks from JSON "onInteract" nodes.
///
/// This is the single place to extend JSON-driven interactions.
/// Scenes no longer need onInteractOverrides dictionaries — everything
/// that can be expressed as a rule (navigate, setFlag, giveItem, etc.)
/// lives here and is wired automatically by the entity builders.
///
/// Supported types
/// ---------------
///
/// Navigate to another room:
///   { "type": "navigate", "target": "RoomNorth", "choiceIndex": 0 }
///   choiceIndex = which choice result triggers navigation (0 = Yes / first option).
///
/// Set a game flag:
///   { "type": "setFlag", "flag": "foundNote", "choiceIndex": 0 }
///
/// Multi-action (execute several actions in sequence):
///   { "type": "multi", "actions": [ { ... }, { ... } ] }
///
/// Return null if the type is unrecognised — the entity will have no callback.
/// </summary>
public static class InteractCallbackBuilder
{
    public static Action<int> Build(JsonNode node)
    {
        if (node == null) return null;

        var type = node["type"]?.GetValue<string>();
        return type switch
        {
            "navigate" => BuildNavigate(node),
            "setFlag"  => BuildSetFlag(node),
            "multi"    => BuildMulti(node),
            _ => LogUnknown(type)
        };
    }

    // -----------------------------------------------------------------------
    // navigate
    // -----------------------------------------------------------------------

    private static Action<int> BuildNavigate(JsonNode node)
    {
        var target      = node["target"]?.GetValue<string>() ?? "";
        int choiceIndex = node["choiceIndex"]?.GetValue<int>() ?? 0;

        return result =>
        {
            if (result == choiceIndex)
                NavigationBus.RequestNavigate(target);
        };
    }

    // -----------------------------------------------------------------------
    // setFlag
    // -----------------------------------------------------------------------

    private static Action<int> BuildSetFlag(JsonNode node)
    {
        var flag        = node["flag"]?.GetValue<string>() ?? "";
        int choiceIndex = node["choiceIndex"]?.GetValue<int>() ?? 0;

        return result =>
        {
            if (result == choiceIndex)
                GameFlags.Set(flag);
        };
    }

    // -----------------------------------------------------------------------
    // multi  — compose several actions
    // -----------------------------------------------------------------------

    private static Action<int> BuildMulti(JsonNode node)
    {
        var actionsArr = node["actions"]?.AsArray();
        if (actionsArr == null) return null;

        var actions = new System.Collections.Generic.List<Action<int>>();
        foreach (var a in actionsArr)
        {
            var cb = Build(a);
            if (cb != null) actions.Add(cb);
        }

        return result =>
        {
            foreach (var a in actions) a(result);
        };
    }

    // -----------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------

    private static Action<int> LogUnknown(string type)
    {
        Console.WriteLine($"[InteractCallbackBuilder] Unknown onInteract type: '{type}'");
        return null;
    }
}