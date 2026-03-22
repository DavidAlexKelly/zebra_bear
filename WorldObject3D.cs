using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ZebraBear;

public enum WallFacing { North, South, East, West }

public class WorldObject3D
{
    public string   Name;
    public string[] Dialogue;
    public Color    Tint;

    private VertexPositionColor[] _verts;
    private short[]               _idx;
    private BoundingBox           _bounds;

    // depth = how far the object protrudes from the wall
    public WorldObject3D(string name, string[] dialogue, Color tint,
        Vector3 centre, float w, float h, WallFacing facing, float depth = 0.3f)
    {
        Name     = name;
        Dialogue = dialogue;
        Tint     = tint;

        BuildGeometry(centre, w, h, depth, facing, tint);
    }

    private void BuildGeometry(Vector3 centre, float w, float h,
        float depth, WallFacing facing, Color tint)
    {
        float hw = w     / 2f;
        float hh = h     / 2f;
        float hd = depth / 2f;

        // We build a box with 6 faces (12 triangles)
        // Orientation depends on which wall it's mounted on
        // 'front' face is the visible face away from the wall
        // 'back' face is against the wall

        Vector3 right, up, normal;

        switch (facing)
        {
            case WallFacing.North: // mounted on back wall, protrudes toward +Z
                right  = new Vector3(1,  0, 0);
                up     = new Vector3(0,  1, 0);
                normal = new Vector3(0,  0, 1);
                break;
            case WallFacing.South: // mounted on front wall, protrudes toward -Z
                right  = new Vector3(-1, 0, 0);
                up     = new Vector3( 0, 1, 0);
                normal = new Vector3( 0, 0, -1);
                break;
            case WallFacing.East: // mounted on right wall, protrudes toward -X
                right  = new Vector3(0, 0,  1);
                up     = new Vector3(0, 1,  0);
                normal = new Vector3(-1, 0, 0);
                break;
            case WallFacing.West: // mounted on left wall, protrudes toward +X
                right  = new Vector3(0,  0, -1);
                up     = new Vector3(0,  1,  0);
                normal = new Vector3(1,  0,  0);
                break;
            default:
                goto case WallFacing.North;
        }

        // 8 corners of the box
        // back face (against wall), front face (protruding)
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

        // Shade the faces slightly differently for depth
        Color frontCol = tint;
        Color sideCol  = new Color(
            (int)(tint.R * 0.7f),
            (int)(tint.G * 0.7f),
            (int)(tint.B * 0.7f));
        Color topCol = new Color(
            (int)(tint.R * 0.85f),
            (int)(tint.G * 0.85f),
            (int)(tint.B * 0.85f));
        Color bottomCol = new Color(
            (int)(tint.R * 0.5f),
            (int)(tint.G * 0.5f),
            (int)(tint.B * 0.5f));

        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            short i = (short)verts.Count;
            verts.Add(new VertexPositionColor(a, col));
            verts.Add(new VertexPositionColor(b, col));
            verts.Add(new VertexPositionColor(c, col));
            verts.Add(new VertexPositionColor(d, col));
            idx.AddRange(new short[] {
                i, (short)(i+1), (short)(i+2),
                i, (short)(i+2), (short)(i+3) });
        }

        // Front face
        AddQuad(fTL, fTR, fBR, fBL, frontCol);

        // Back face (against wall — usually hidden but included for completeness)
        AddQuad(bTR, bTL, bBL, bBR, sideCol);

        // Top face
        AddQuad(bTL, bTR, fTR, fTL, topCol);

        // Bottom face
        AddQuad(fBL, fBR, bBR, bBL, bottomCol);

        // Left side
        AddQuad(bTL, fTL, fBL, bBL, sideCol);

        // Right side
        AddQuad(fTR, bTR, bBR, fBR, sideCol);

        _verts = verts.ToArray();
        _idx   = idx.ToArray();

        // Bounding box — compute from all 8 corners
        var allPoints = new[] { bTL, bTR, bBL, bBR, fTL, fTR, fBL, fBR };
        var min = allPoints[0];
        var max = allPoints[0];
        foreach (var p in allPoints)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        _bounds = new BoundingBox(min, max);
    }

    public bool Raycast(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        var hit = ray.Intersects(_bounds);
        if (hit.HasValue)
        {
            distance = hit.Value;
            return true;
        }
        return false;
    }

    public void Draw(GraphicsDevice gd, BasicEffect effect, bool targeted)
    {
        var drawVerts = (VertexPositionColor[])_verts.Clone();

        if (targeted)
        {
            for (int i = 0; i < drawVerts.Length; i++)
            {
                var c = drawVerts[i].Color;
                drawVerts[i].Color = new Color(
                    Math.Min(255, c.R + 60),
                    Math.Min(255, c.G + 40),
                    Math.Min(255, c.B + 60));
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
}