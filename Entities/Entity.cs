using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using ZebraBear.Core;

namespace ZebraBear.Entities;

/// <summary>
/// Base class for all interactive world objects.
///
/// Owns:
///   - Identity (Name, Dialogue / DialogueTree)
///   - Interaction callback (OnInteract) — for simple cases only
///   - Raycasting via BoundingBox
///
/// For branching dialogue, set DialogueTree instead of Dialogue.
/// The scene's StartDialogue() method checks DialogueTree first.
/// Legacy flat Dialogue arrays are automatically promoted to a single-node
/// DialogueTree by GameLoader, so scenes always use DialogueTree at runtime.
///
/// Rendering is delegated to subclasses.
/// </summary>
public abstract class Entity
{
    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Display name shown in the HUD interact prompt and dialogue name tag.
    /// Empty = not interactable (decorative only).
    /// </summary>
    public string Name;

    // -----------------------------------------------------------------------
    // Dialogue
    // -----------------------------------------------------------------------

    /// <summary>
    /// Legacy flat dialogue array. Kept for compatibility with code that sets
    /// it directly. At load time, GameLoader promotes this to a DialogueTree
    /// automatically so that scenes always use the tree path.
    /// </summary>
    public string[] Dialogue;

    /// <summary>
    /// Branching dialogue tree. Set directly (from dialogueTree JSON) or
    /// promoted from flat Dialogue by GameLoader.
    /// Scenes should use this field; fall back to Dialogue only as a last resort.
    /// </summary>
    public DialogueNode DialogueTree;

    // -----------------------------------------------------------------------
    // Interaction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Optional callback fired when a top-level dialogue choice is confirmed.
    /// Receives the choice index (0 = first option, 1 = second, etc.).
    ///
    /// For simple binary Yes/No interactions where a dialogue tree is overkill,
    /// set this directly. For everything else, use DialogueTree with
    /// onSelectActions on each choice instead.
    ///
    /// Cleared automatically by DialogueBox after firing.
    /// </summary>
    public Action<int> OnInteract;

    /// <summary>
    /// True when this entity's dialogue will present a choice to the player.
    /// Derived from the DialogueTree if present, else from OnInteract.
    /// </summary>
    public bool HasChoice =>
        DialogueTree != null
            ? !DialogueTree.IsLeaf
            : OnInteract != null;

    // -----------------------------------------------------------------------
    // Collision
    // -----------------------------------------------------------------------

    /// <summary>
    /// When true, the player cannot walk through this entity.
    /// MeshEntity defaults to true. BillboardEntity defaults to false.
    /// </summary>
    public bool Solid = false;

    // -----------------------------------------------------------------------
    // Raycasting
    // -----------------------------------------------------------------------

    protected BoundingBox _bounds;

    public BoundingBox Bounds => _bounds;

    public virtual bool Intersects(Ray ray, out float distance)
    {
        var result = ray.Intersects(_bounds);
        distance = result ?? float.MaxValue;
        return result.HasValue;
    }

    // -----------------------------------------------------------------------
    // Rendering (delegated to subclasses)
    // -----------------------------------------------------------------------

    public abstract void Draw(
        Microsoft.Xna.Framework.Graphics.GraphicsDevice gd,
        BasicEffect fx,
        bool targeted);
}