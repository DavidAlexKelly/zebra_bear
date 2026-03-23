using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ZebraBear.Entities;

/// <summary>
/// Base class for all interactive world objects.
///
/// Owns:
///   - Identity (Name, Dialogue)
///   - Interaction callback (OnInteract)
///   - Raycasting via BoundingBox
///
/// Rendering is delegated to subclasses — Entity itself knows nothing
/// about how it looks.
/// </summary>
public abstract class Entity
{
    // -----------------------------------------------------------------------
    // Identity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Display name shown in the HUD interact prompt and dialogue name tag.
    /// Null or empty = not interactable (decorative only).
    /// </summary>
    public string Name;

    /// <summary>Lines of dialogue shown when the player interacts.</summary>
    public string[] Dialogue;

    // -----------------------------------------------------------------------
    // Interaction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Optional callback fired when a dialogue choice is confirmed.
    /// Receives the choice index (0 = Yes / first option, 1 = No / second, etc.).
    /// Set to null for simple non-branching dialogue.
    /// Cleared automatically by DialogueBox after firing.
    /// </summary>
    public Action<int> OnInteract;

    /// <summary>
    /// Convenience: returns true if this entity has a branching choice.
    /// DialogueBox uses this to decide whether to show the choice UI.
    /// </summary>
    public bool HasChoice => OnInteract != null;

    // -----------------------------------------------------------------------
    // Collision
    // -----------------------------------------------------------------------

    /// <summary>
    /// When true, the player cannot walk through this entity.
    /// MeshEntity defaults to true. BillboardEntity defaults to false.
    /// Override per-instance after construction if needed.
    /// </summary>
    public bool Solid = false;

    // -----------------------------------------------------------------------
    // Raycasting
    // -----------------------------------------------------------------------

    protected BoundingBox _bounds;

    /// <summary>Exposes the bounding box for collision resolution.</summary>
    public BoundingBox Bounds => _bounds;

    /// <summary>
    /// Returns true if the ray intersects this entity's bounding box.
    /// distance is set to the hit distance, or float.MaxValue on miss.
    /// </summary>
    public bool Raycast(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        var hit  = ray.Intersects(_bounds);
        if (hit.HasValue)
        {
            distance = hit.Value;
            return true;
        }
        return false;
    }

    // -----------------------------------------------------------------------
    // Rendering — implemented by subclasses
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draw this entity using the supplied effect.
    /// The caller (Room) is responsible for setting effect.View/Projection/World
    /// before iterating entities.
    /// targeted = true when the player's crosshair is over this entity.
    /// </summary>
    public abstract void Draw(GraphicsDevice gd, BasicEffect effect, bool targeted);
}