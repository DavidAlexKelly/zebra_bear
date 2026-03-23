using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ZebraBear.Entities;

/// <summary>
/// A world entity whose appearance is a pre-built vertex colour mesh.
/// Covers wall-mounted objects, freestanding furniture, and anything else
/// with solid geometry — there is no separate type for each.
///
/// Don't construct directly — use the static factory methods:
///   MeshEntity.CreateBox(...)          — general axis-aligned box
///   MeshEntity.CreateOrientedBox(...)  — box facing any direction (walls, etc.)
///   MeshEntity.CreateTable(...)        — table with four legs
///
/// To add a new shape, add a factory here that calls MeshBuilder.
/// No other files need to change.
///
/// Usage — freestanding:
///   _room.Add(MeshEntity.CreateTable("Table", dialogue,
///       position: new Vector3(0, -3, 0),
///       w: 2.8f, d: 1.4f, h: 1.1f,
///       tint: new Color(120, 85, 55)));
///
/// Usage — wall mounted:
///   var door = MeshEntity.CreateOrientedBox("Door", dialogue,
///       centre:  new Vector3(13.9f, 0f, -4f),
///       w: 2.2f, h: 3.2f,
///       normal:  MeshBuilder.FaceEast,
///       tint:    new Color(90, 65, 45),
///       depth:   0.25f);
///   door.OnInteract = (result) => { if (result == 0) game.GoToRoom2(); };
///   _room.Add(door);
/// </summary>
public class MeshEntity : Entity
{
    private readonly VertexPositionColor[] _verts;
    private readonly short[]               _idx;
    private readonly int                   _highlightBoost;

    // -----------------------------------------------------------------------
    // Constructor — prefer the factory methods below
    // -----------------------------------------------------------------------

    public MeshEntity(string name, string[] dialogue,
        VertexPositionColor[] verts, short[] idx,
        BoundingBox bounds,
        int highlightBoost = 50)
    {
        Name            = name;
        Dialogue        = dialogue;
        _verts          = verts;
        _idx            = idx;
        _bounds         = bounds;
        _highlightBoost = highlightBoost;
        Solid           = true;  // mesh objects block the player by default
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public override void Draw(GraphicsDevice gd, BasicEffect effect, bool targeted)
    {
        var drawVerts = (VertexPositionColor[])_verts.Clone();

        if (targeted)
        {
            int rb = _highlightBoost;
            int gb = (int)(_highlightBoost * 0.7f);
            for (int i = 0; i < drawVerts.Length; i++)
            {
                var c = drawVerts[i].Color;
                drawVerts[i].Color = new Color(
                    Math.Min(255, c.R + rb),
                    Math.Min(255, c.G + gb),
                    Math.Min(255, c.B + rb));
            }
        }

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                drawVerts, 0, drawVerts.Length,
                _idx, 0, _idx.Length / 3);
        }
    }

    // -----------------------------------------------------------------------
    // Factories
    // -----------------------------------------------------------------------

    /// <summary>
    /// General axis-aligned box with explicit min/max corners.
    /// Use for crates, pillars, shelves, or anything box-shaped that
    /// sits flat in the world.
    /// </summary>
    public static MeshEntity CreateBox(string name, string[] dialogue,
        Vector3 min, Vector3 max,
        Color top, Color bottom, Color side)
    {
        var (verts, idx) = MeshBuilder.Box(min, max, top, bottom, side);
        return new MeshEntity(name, dialogue, verts, idx, new BoundingBox(min, max));
    }

    /// <summary>
    /// Box that faces a given direction — the standard building block for
    /// wall-mounted objects (doors, notice boards, windows, etc.).
    ///
    /// normal = direction the front face points, e.g. MeshBuilder.FaceEast
    /// for an object on the right wall. Any normalised Vector3 works for
    /// non-axis-aligned placements.
    /// </summary>
    public static MeshEntity CreateOrientedBox(string name, string[] dialogue,
        Vector3 centre, float w, float h, Vector3 normal,
        Color tint, float depth = 0.3f)
    {
        var (verts, idx) = MeshBuilder.OrientedBox(centre, w, h, normal, tint, depth);
        var bounds       = MeshBuilder.BoundsForOrientedBox(centre, w, h, normal, depth);
        return new MeshEntity(name, dialogue, verts, idx, bounds);
    }

    /// <summary>
    /// Freestanding table with a top slab and four legs.
    /// position = centre of the base at floor level.
    /// </summary>
    public static MeshEntity CreateTable(string name, string[] dialogue,
        Vector3 position, float w, float d, float h, Color tint)
    {
        var (verts, idx) = MeshBuilder.Table(position, w, d, h, tint);
        float hw = w / 2f, hd = d / 2f;
        var bounds = new BoundingBox(
            new Vector3(position.X - hw, position.Y,     position.Z - hd),
            new Vector3(position.X + hw, position.Y + h, position.Z + hd));
        return new MeshEntity(name, dialogue, verts, idx, bounds);
    }
}