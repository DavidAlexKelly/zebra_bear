using System;
using System.Collections.Generic;

namespace ZebraBear.Core;

/// <summary>
/// A single node in a branching dialogue tree.
///
/// Flat dialogue (the existing string[] Dialogue on Entity) maps to a linear
/// chain of DialogueNodes with no choices. Branching replaces it with a tree.
///
/// JSON shape (in rooms.json entities or characters.json):
///
/// Simple flat dialogue (unchanged from before):
///   "dialogue": ["Line one.", "Line two."]
///
/// Branching dialogue tree:
///   "dialogueTree": {
///     "id": "root",
///     "lines": ["Hello. Do you need something?"],
///     "choices": [
///       {
///         "label": "Yes",
///         "next": {
///           "id": "yes_branch",
///           "lines": ["Great! Let me help."],
///           "onChoiceActions": [
///             { "type": "navigate", "target": "RoomNorth", "choiceIndex": 0 }
///           ]
///         }
///       },
///       {
///         "label": "No",
///         "next": {
///           "id": "no_branch",
///           "lines": ["Okay, let me know if you change your mind."]
///         }
///       }
///     ]
///   }
///
/// Conditional lines:
///   "lines": [
///     "Always shown.",
///     { "text": "Only if flag is set.", "ifFlag": "metKei" },
///     { "text": "Only if flag is NOT set.", "ifNotFlag": "metKei" }
///   ]
/// </summary>
public class DialogueNode
{
    // -----------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------

    /// <summary>Optional identifier — useful for jumping to specific nodes.</summary>
    public string Id;

    /// <summary>
    /// Lines of text shown in sequence before presenting choices.
    /// Each entry is either a plain string or a conditional line.
    /// </summary>
    public List<DialogueLine> Lines = new();

    /// <summary>
    /// If non-empty, a choice prompt is shown after all lines are displayed.
    /// Each choice can lead to a child DialogueNode.
    /// </summary>
    public List<DialogueChoice> Choices = new();

    /// <summary>
    /// Actions fired when this node is ENTERED (before lines are shown).
    /// Useful for setting flags, giving items, etc.
    /// </summary>
    public List<Action<int>> OnEnterActions = new();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    public bool IsLeaf => Choices.Count == 0;

    /// <summary>
    /// Resolve the lines to display given the current flag state.
    /// Filters out conditional lines whose flag condition isn't met.
    /// </summary>
    public string[] ResolveLines()
    {
        var result = new List<string>();
        foreach (var line in Lines)
        {
            if (!string.IsNullOrEmpty(line.IfFlag) && !GameFlags.IsSet(line.IfFlag))
                continue;
            if (!string.IsNullOrEmpty(line.IfNotFlag) && GameFlags.IsSet(line.IfNotFlag))
                continue;
            result.Add(line.Text);
        }
        return result.ToArray();
    }
}

/// <summary>A line of dialogue, optionally conditional on a GameFlag.</summary>
public class DialogueLine
{
    public string Text;

    /// <summary>Only show this line if this flag IS set.</summary>
    public string IfFlag;

    /// <summary>Only show this line if this flag is NOT set.</summary>
    public string IfNotFlag;

    /// <summary>Convenience: wrap a plain string.</summary>
    public static implicit operator DialogueLine(string s) =>
        new DialogueLine { Text = s };
}

/// <summary>A choice presented to the player at the end of a DialogueNode.</summary>
public class DialogueChoice
{
    /// <summary>Label shown on the choice button.</summary>
    public string Label;

    /// <summary>
    /// The node to transition to when this choice is selected.
    /// Null = end dialogue.
    /// </summary>
    public DialogueNode Next;

    /// <summary>
    /// Actions fired immediately when this choice is selected,
    /// before the next node's lines are shown.
    /// </summary>
    public List<Action<int>> OnSelectActions = new();
}