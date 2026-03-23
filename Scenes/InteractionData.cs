//======== Scenes/InteractionData.cs ========
using System;
using System.Collections.Generic;
using ZebraBear.Core;

namespace ZebraBear.Scenes;

/// <summary>
/// A reusable interaction definition created in the editor.
/// Can be attached to any object or character.
///
/// Structure:
///   - An interaction has a unique Id and display Name
///   - It contains a root InteractionNode
///   - Each node has lines of text and optional choices
///   - Choices can lead to child nodes or trigger events (navigate)
/// </summary>
public class InteractionDef
{
    public string Id = "";
    public string Name = "";
    public InteractionNode Root = new();

    public InteractionDef()
    {
        Id = $"int_{Guid.NewGuid().ToString()[..8]}";
    }
}

/// <summary>
/// A single node in the interaction tree.
/// </summary>
public class InteractionNode
{
    public string Id = "";
    public List<string> Lines = new();
    public List<InteractionChoice> Choices = new();

    /// <summary>Navigate to this room when this node ends (leaf only).</summary>
    public string NavigateTarget = null;

    public InteractionNode()
    {
        Id = $"node_{Guid.NewGuid().ToString()[..8]}";
    }

    public bool IsLeaf => Choices.Count == 0;
}

/// <summary>
/// A choice the player can pick at the end of a node's lines.
/// </summary>
public class InteractionChoice
{
    public string Label = "Choice";
    public InteractionNode Next = null;

    /// <summary>Navigate to this room when this choice is picked.</summary>
    public string NavigateTarget = null;
}

/// <summary>
/// Stores all interactions for the current editor session.
/// </summary>
public static class InteractionStore
{
    public static List<InteractionDef> Interactions =>
        GameContext.Instance.Interactions;
 
    public static InteractionDef FindById(string id) =>
        GameContext.Instance.FindInteractionById(id);
 
    public static InteractionDef FindByName(string name) =>
        GameContext.Instance.FindInteractionByName(name);
 
    public static void Clear() =>
        GameContext.Instance.Interactions.Clear();
}