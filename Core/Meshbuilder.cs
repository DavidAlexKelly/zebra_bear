using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ZebraBear.Entities;

/// <summary>
/// Static factory for building vertex/index geometry.
/// Returns raw arrays ready to be owned by a MeshEntity.
///
/// Add new shape methods here — don't create new entity subclasses for shapes.
/// Every public method returns (VertexPositionColor[], short[]) so it can be
/// passed directly to a MeshEntity constructor.
/// </summary>
public static class MeshBuilder
{
    // -----------------------------------------------------------------------
    // Common normals
    // -----------------------------------------------------------------------

    /// <summary>Object faces toward +Z (protrudes from the back wall).</summary>
    public static readonly Vector3 FaceNorth = Vector3.Forward * -1; // (0, 0, 1)

    /// <summary>Object faces toward -Z (protrudes from the front wall).</summary>
    public static readonly Vector3 FaceSouth = Vector3.Forward;      // (0, 0, -1)

    /// <summary>Object faces toward -X (protrudes from the right wall).</summary>
    public static readonly Vector3 FaceEast  = Vector3.Left;         // (-1, 0, 0)

    /// <summary>Object faces toward +X (protrudes from the left wall).</summary>
    public static readonly Vector3 FaceWest  = Vector3.Right;        // (1, 0, 0)

    // -----------------------------------------------------------------------
    // Box
    // -----------------------------------------------------------------------

    /// <summary>
    /// Axis-aligned box from min to max corner.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) Box(
        Vector3 min, Vector3 max,
        Color top, Color bottom, Color side)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        float x0 = min.X, x1 = max.X;
        float y0 = min.Y, y1 = max.Y;
        float z0 = min.Z, z1 = max.Z;

        AddQuad(verts, idx,
            new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
            new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), top);       // Top
        AddQuad(verts, idx,
            new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
            new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), bottom);    // Bottom
        AddQuad(verts, idx,
            new Vector3(x0, y1, z1), new Vector3(x1, y1, z1),
            new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), side);      // Front
        AddQuad(verts, idx,
            new Vector3(x1, y1, z0), new Vector3(x0, y1, z0),
            new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), side);      // Back
        AddQuad(verts, idx,
            new Vector3(x0, y1, z0), new Vector3(x0, y1, z1),
            new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), side);      // Left
        AddQuad(verts, idx,
            new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
            new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), side);      // Right

        return (verts.ToArray(), idx.ToArray());
    }

    // -----------------------------------------------------------------------
    // Table
    // -----------------------------------------------------------------------

    /// <summary>
    /// Table: a top slab and four legs.
    /// pos = centre of base at floor level.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) Table(
        Vector3 pos, float w, float d, float h, Color tint)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        float hw       = w / 2f;
        float hd       = d / 2f;
        float tableTop = pos.Y + h;
        float topThick = 0.12f;
        float legW     = 0.12f;
        float legInset = 0.14f;

        Color topColor    = tint;
        Color sideColor   = Darken(tint, 0.7f);
        Color bottomColor = Darken(tint, 0.5f);
        Color legColor    = Darken(tint, 0.6f);
        Color legSide     = Darken(tint, 0.45f);

        // Top slab
        Merge(verts, idx, Box(
            new Vector3(pos.X - hw, tableTop - topThick, pos.Z - hd),
            new Vector3(pos.X + hw, tableTop,            pos.Z + hd),
            topColor, bottomColor, sideColor));

        // Four legs
        float legTop = tableTop - topThick;
        float legBot = pos.Y;

        Vector3[] legCentres =
        {
            new Vector3(pos.X - hw + legInset, 0, pos.Z - hd + legInset),
            new Vector3(pos.X + hw - legInset, 0, pos.Z - hd + legInset),
            new Vector3(pos.X - hw + legInset, 0, pos.Z + hd - legInset),
            new Vector3(pos.X + hw - legInset, 0, pos.Z + hd - legInset),
        };

        foreach (var lc in legCentres)
        {
            Merge(verts, idx, Box(
                new Vector3(lc.X - legW / 2f, legBot, lc.Z - legW / 2f),
                new Vector3(lc.X + legW / 2f, legTop, lc.Z + legW / 2f),
                legColor, bottomColor, legSide));
        }

        return (verts.ToArray(), idx.ToArray());
    }

    // -----------------------------------------------------------------------
    // Chair
    // -----------------------------------------------------------------------

    /// <summary>
    /// Chair: seat slab, four legs, optional backrest panel.
    /// pos = centre of base at floor level.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) Chair(
        Vector3 pos, float w, float d, float h, Color tint, bool backrest = true)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        float hw      = w / 2f;
        float hd      = d / 2f;
        float seatTop = pos.Y + h;
        float seatThk = 0.1f;
        float legW    = 0.08f;
        float legInset = 0.1f;

        Color topColor    = tint;
        Color sideColor   = Darken(tint, 0.70f);
        Color bottomColor = Darken(tint, 0.50f);
        Color legColor    = Darken(tint, 0.60f);
        Color legSide     = Darken(tint, 0.45f);

        // Seat slab
        Merge(verts, idx, Box(
            new Vector3(pos.X - hw, seatTop - seatThk, pos.Z - hd),
            new Vector3(pos.X + hw, seatTop,            pos.Z + hd),
            topColor, bottomColor, sideColor));

        // Four legs
        Vector3[] legCentres =
        {
            new Vector3(pos.X - hw + legInset, 0, pos.Z - hd + legInset),
            new Vector3(pos.X + hw - legInset, 0, pos.Z - hd + legInset),
            new Vector3(pos.X - hw + legInset, 0, pos.Z + hd - legInset),
            new Vector3(pos.X + hw - legInset, 0, pos.Z + hd - legInset),
        };

        foreach (var lc in legCentres)
        {
            Merge(verts, idx, Box(
                new Vector3(lc.X - legW / 2f, pos.Y,              lc.Z - legW / 2f),
                new Vector3(lc.X + legW / 2f, seatTop - seatThk,  lc.Z + legW / 2f),
                legColor, bottomColor, legSide));
        }

        // Backrest panel
        if (backrest)
        {
            float backH   = 0.55f;
            float backThk = 0.07f;
            float backZ   = pos.Z - hd + backThk / 2f;

            Merge(verts, idx, Box(
                new Vector3(pos.X - hw, seatTop,          backZ - backThk / 2f),
                new Vector3(pos.X + hw, seatTop + backH,  backZ + backThk / 2f),
                Darken(tint, 0.75f), bottomColor, Darken(tint, 0.55f)));
        }

        return (verts.ToArray(), idx.ToArray());
    }

    // -----------------------------------------------------------------------
    // Shelf
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wall-mounted shelf: a horizontal slab with two side brackets.
    /// centre = world-space centre of the shelf slab.
    /// normal = direction the shelf faces (away from the wall it sits on).
    /// depth  = how far it protrudes from the wall.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) Shelf(
        Vector3 centre, float width, float depth, Vector3 normal, Color tint)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        float slabThk    = 0.06f;
        float brackW     = 0.05f;
        float brackH     = depth * 0.85f;
        float hw         = width / 2f;
        Color brackColor = Darken(tint, 0.65f);

        // Slab
        Merge(verts, idx, OrientedBox(centre, width, slabThk, normal, tint, depth));

        // Derive right vector so we can place brackets at each end
        var n     = Vector3.Normalize(normal);
        var up    = Vector3.Up;
        var right = Vector3.Normalize(Vector3.Cross(up, n));
        if (right.LengthSquared() < 0.001f)
        {
            up    = Vector3.Forward;
            right = Vector3.Normalize(Vector3.Cross(up, n));
        }

        float inset = 0.08f;
        var bracketOffset = Vector3.Up * (brackH / 2f + slabThk / 2f);

        Vector3[] bCentres =
        {
            centre - right * (hw - inset) - bracketOffset,
            centre + right * (hw - inset) - bracketOffset,
        };

        foreach (var bc in bCentres)
            Merge(verts, idx, OrientedBox(bc, brackW, brackH, n, brackColor, depth * 0.6f));

        return (verts.ToArray(), idx.ToArray());
    }

    // -----------------------------------------------------------------------
    // OrientedBox
    // -----------------------------------------------------------------------

    /// <summary>
    /// Box facing a given world-space normal direction — used for wall-mounted
    /// objects (doors, notice boards, windows, etc.) and anything that isn't
    /// axis-aligned.
    ///
    /// centre = world-space centre of the object.
    /// normal = direction the front face points (away from the wall).
    /// depth  = total thickness along the normal axis.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) OrientedBox(
        Vector3 centre, float w, float h, Vector3 normal,
        Color tint, float depth = 0.3f)
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        normal = Vector3.Normalize(normal);

        var up    = Vector3.Up;
        var right = Vector3.Normalize(Vector3.Cross(up, normal));

        if (right.LengthSquared() < 0.001f)
        {
            up    = Vector3.Forward;
            right = Vector3.Normalize(Vector3.Cross(up, normal));
        }

        float hw = w     / 2f;
        float hh = h     / 2f;
        float hd = depth / 2f;

        Vector3 backCentre  = centre - normal * hd;
        Vector3 frontCentre = centre + normal * hd;

        Vector3 bTL = backCentre  - right * hw + up * hh;
        Vector3 bTR = backCentre  + right * hw + up * hh;
        Vector3 bBL = backCentre  - right * hw - up * hh;
        Vector3 bBR = backCentre  + right * hw - up * hh;
        Vector3 fTL = frontCentre - right * hw + up * hh;
        Vector3 fTR = frontCentre + right * hw + up * hh;
        Vector3 fBL = frontCentre - right * hw - up * hh;
        Vector3 fBR = frontCentre + right * hw - up * hh;

        Color frontCol  = tint;
        Color sideCol   = Darken(tint, 0.7f);
        Color topCol    = Darken(tint, 0.85f);
        Color bottomCol = Darken(tint, 0.5f);

        AddQuad(verts, idx, fTL, fTR, fBR, fBL, frontCol);  // Front
        AddQuad(verts, idx, bTR, bTL, bBL, bBR, sideCol);   // Back
        AddQuad(verts, idx, bTL, bTR, fTR, fTL, topCol);    // Top
        AddQuad(verts, idx, fBL, fBR, bBR, bBL, bottomCol); // Bottom
        AddQuad(verts, idx, bTL, fTL, fBL, bBL, sideCol);   // Left
        AddQuad(verts, idx, fTR, bTR, bBR, fBR, sideCol);   // Right

        return (verts.ToArray(), idx.ToArray());
    }

    /// <summary>
    /// Computes a tight BoundingBox for an OrientedBox — use this when
    /// building the MeshEntity so raycasting matches the geometry.
    /// </summary>
    public static BoundingBox BoundsForOrientedBox(
        Vector3 centre, float w, float h, Vector3 normal, float depth = 0.3f)
    {
        normal = Vector3.Normalize(normal);

        var up    = Vector3.Up;
        var right = Vector3.Normalize(Vector3.Cross(up, normal));
        if (right.LengthSquared() < 0.001f)
        {
            up    = Vector3.Forward;
            right = Vector3.Normalize(Vector3.Cross(up, normal));
        }

        float hw = w     / 2f;
        float hh = h     / 2f;
        float hd = depth / 2f;

        var backCentre  = centre - normal * hd;
        var frontCentre = centre + normal * hd;

        Vector3[] corners =
        {
            backCentre  - right * hw + up * hh,
            backCentre  + right * hw + up * hh,
            backCentre  - right * hw - up * hh,
            backCentre  + right * hw - up * hh,
            frontCentre - right * hw + up * hh,
            frontCentre + right * hw + up * hh,
            frontCentre - right * hw - up * hh,
            frontCentre + right * hw - up * hh,
        };

        var min = corners[0];
        var max = corners[0];
        foreach (var p in corners)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    internal static void AddQuad(
        List<VertexPositionColor> verts, List<short> idx,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        short i = (short)verts.Count;
        verts.Add(new VertexPositionColor(a, col));
        verts.Add(new VertexPositionColor(b, col));
        verts.Add(new VertexPositionColor(c, col));
        verts.Add(new VertexPositionColor(d, col));
        idx.AddRange(new short[] { i, (short)(i+1), (short)(i+2), i, (short)(i+2), (short)(i+3) });
    }

    private static void Merge(
        List<VertexPositionColor> verts, List<short> idx,
        (VertexPositionColor[] v, short[] i) sub)
    {
        short offset = (short)verts.Count;
        verts.AddRange(sub.v);
        foreach (var i in sub.i) idx.Add((short)(i + offset));
    }

    public static Color Darken(Color c, float f) =>
        new Color((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
}