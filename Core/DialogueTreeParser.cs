using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace ZebraBear.Core;

/// <summary>
/// Parses a "dialogueTree" JSON node into a DialogueNode graph.
///
/// Called by GameLoader when an entity has a "dialogueTree" field.
/// If no tree is present, GameLoader falls back to the flat "dialogue"
/// string array, converting it to a linear DialogueNode automatically.
/// </summary>
public static class DialogueTreeParser
{
    // -----------------------------------------------------------------------
    // Entry points
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parse a full tree from a "dialogueTree" JSON node.
    /// </summary>
    public static DialogueNode Parse(JsonNode node)
    {
        if (node == null) return null;
        return ParseNode(node);
    }

    /// <summary>
    /// Convert a flat string[] into a single-node linear DialogueNode.
    /// Allows legacy flat dialogue to go through the same runtime path.
    /// </summary>
    public static DialogueNode FromFlatLines(string[] lines)
    {
        if (lines == null || lines.Length == 0) return null;

        var node = new DialogueNode { Id = "root" };
        foreach (var l in lines)
            node.Lines.Add(new DialogueLine { Text = l });
        return node;
    }

    // -----------------------------------------------------------------------
    // Recursive node parser
    // -----------------------------------------------------------------------

    private static DialogueNode ParseNode(JsonNode json)
    {
        var node = new DialogueNode
        {
            Id = json["id"]?.GetValue<string>() ?? ""
        };

        // Parse lines (plain strings or conditional objects)
        var linesArr = json["lines"]?.AsArray();
        if (linesArr != null)
        {
            foreach (var lineNode in linesArr)
            {
                if (lineNode is JsonValue v)
                {
                    // Plain string
                    node.Lines.Add(new DialogueLine { Text = v.GetValue<string>() });
                }
                else
                {
                    // Conditional object: { "text": "...", "ifFlag": "...", "ifNotFlag": "..." }
                    node.Lines.Add(new DialogueLine
                    {
                        Text       = lineNode["text"]?.GetValue<string>() ?? "",
                        IfFlag     = lineNode["ifFlag"]?.GetValue<string>(),
                        IfNotFlag  = lineNode["ifNotFlag"]?.GetValue<string>()
                    });
                }
            }
        }

        // Parse onEnter actions
        var enterArr = json["onEnterActions"]?.AsArray();
        if (enterArr != null)
            foreach (var a in enterArr)
            {
                var cb = InteractCallbackBuilder.Build(a);
                if (cb != null) node.OnEnterActions.Add(cb);
            }

        // Parse choices
        var choicesArr = json["choices"]?.AsArray();
        if (choicesArr != null)
        {
            foreach (var choiceNode in choicesArr)
            {
                var choice = new DialogueChoice
                {
                    Label = choiceNode!["label"]?.GetValue<string>() ?? "?"
                };

                // Recurse into the next node if present
                var nextNode = choiceNode["next"];
                if (nextNode != null)
                    choice.Next = ParseNode(nextNode);

                // onSelect actions
                var selectArr = choiceNode["onSelectActions"]?.AsArray();
                if (selectArr != null)
                    foreach (var a in selectArr)
                    {
                        var cb = InteractCallbackBuilder.Build(a);
                        if (cb != null) choice.OnSelectActions.Add(cb);
                    }

                // Convenience shorthand: "onInteract" at choice level
                var interact = choiceNode["onInteract"];
                if (interact != null)
                {
                    var cb = InteractCallbackBuilder.Build(interact);
                    if (cb != null) choice.OnSelectActions.Add(cb);
                }

                node.Choices.Add(choice);
            }
        }

        return node;
    }
}