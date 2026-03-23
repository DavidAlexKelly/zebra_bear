using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZebraBear.Scenes; // InteractionDef lives here

namespace ZebraBear.Entities;

/// <summary>
/// Base class for all interactive world objects.
///
/// Owns:
///   - Identity (Name)
///   - Interaction — the InteractionDef authored in the editor, or null for decorative objects
///   - Raycasting via BoundingBox
///
/// Rendering is delegated to subclasses.
///
/// To make an entity interactable, assign an InteractionDef from InteractionStore.
/// Everything else — dialogue lines, choices, navigation — is expressed in the
/// InteractionDef and driven by DialogueBox at runtime.
/// </summary>
public abstract class Entity
{
    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Display name shown in the HUD interact prompt and dialogue name tag.
    /// Empty string = not interactable (decorative only).
    /// </summary>
    public string Name = "";

    // -----------------------------------------------------------------------
    // Interaction
    // -----------------------------------------------------------------------

    /// <summary>
    /// The interaction authored in the editor and resolved via InteractionStore.
    /// Null = no dialogue; the entity is decorative or not yet wired up.
    /// </summary>
    public InteractionDef Interaction = null;

    /// <summary>
    /// True when this entity will present a choice prompt to the player.
    /// Derived from the interaction's root node.
    /// </summary>
    public bool HasChoice => Interaction != null && !Interaction.Root.IsLeaf;

    /// <summary>
    /// True when this entity can be interacted with at all.
    /// </summary>
    public bool IsInteractable => Interaction != null && !string.IsNullOrEmpty(Name);

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