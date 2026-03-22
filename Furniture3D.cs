using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ZebraBear;

public class Furniture3D
{
    public string   Name;
    public string[] Dialogue;

    private VertexPositionColor[] _verts;
    private short[]               _idx;
    private BoundingBox           _bounds;

    public Furniture3D(string name, string[] dialogue,
        Vector3 position, float w, float d, float h, Color tint)
    {
        Name     = name;
        Dialogue = dialogue;
        BuildTable(position, w, d, h, tint);
    }

    private void BuildTable(Vector3 pos, float w, float d, float h, Color tint)
    {
        // pos = centre of the table at floor level (Y = -3)
        float hw = w / 2f;
        float hd = d / 2f;

        float tableTop    = pos.Y + h;
        float topThick    = 0.12f;
        float legW        = 0.12f;
        float legInset    = 0.14f;

        Color topColor    = tint;
        Color sideColor   = Darken(tint, 0.7f);
        Color bottomColor = Darken(tint, 0.5f);
        Color legColor    = Darken(tint, 0.6f);
        Color legSide     = Darken(tint, 0.45f);

        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d2, Color col)
        {
            short i = (short)verts.Count;
            verts.Add(new VertexPositionColor(a, col));
            verts.Add(new VertexPositionColor(b, col));
            verts.Add(new VertexPositionColor(c, col));
            verts.Add(new VertexPositionColor(d2, col));
            idx.AddRange(new short[] {
                i, (short)(i+1), (short)(i+2),
                i, (short)(i+2), (short)(i+3) });
        }

        void AddBox(Vector3 min, Vector3 max,
            Color top, Color bottom, Color side)
        {
            float x0 = min.X, x1 = max.X;
            float y0 = min.Y, y1 = max.Y;
            float z0 = min.Z, z1 = max.Z;

            // Top
            AddQuad(
                new Vector3(x0, y1, z0), new Vector3(x1, y1, z0),
                new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), top);
            // Bottom
            AddQuad(
                new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),
                new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), bottom);
            // Front
            AddQuad(
                new Vector3(x0, y1, z1), new Vector3(x1, y1, z1),
                new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), side);
            // Back
            AddQuad(
                new Vector3(x1, y1, z0), new Vector3(x0, y1, z0),
                new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), side);
            // Left
            AddQuad(
                new Vector3(x0, y1, z0), new Vector3(x0, y1, z1),
                new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), side);
            // Right
            AddQuad(
                new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), side);
        }

        // Table top slab
        AddBox(
            new Vector3(pos.X - hw, tableTop - topThick, pos.Z - hd),
            new Vector3(pos.X + hw, tableTop,            pos.Z + hd),
            topColor, bottomColor, sideColor);

        // Four legs
        float legH    = h - topThick;
        float legTop  = tableTop - topThick;
        float legBot  = pos.Y;

        Vector3[] legCentres = new[]
        {
            new Vector3(pos.X - hw + legInset, 0, pos.Z - hd + legInset), // front left
            new Vector3(pos.X + hw - legInset, 0, pos.Z - hd + legInset), // front right
            new Vector3(pos.X - hw + legInset, 0, pos.Z + hd - legInset), // back left
            new Vector3(pos.X + hw - legInset, 0, pos.Z + hd - legInset), // back right
        };

        foreach (var lc in legCentres)
        {
            AddBox(
                new Vector3(lc.X - legW/2f, legBot, lc.Z - legW/2f),
                new Vector3(lc.X + legW/2f, legTop, lc.Z + legW/2f),
                legColor, bottomColor, legSide);
        }

        _verts = verts.ToArray();
        _idx   = idx.ToArray();

        // Bounding box for interaction
        _bounds = new BoundingBox(
            new Vector3(pos.X - hw, pos.Y,      pos.Z - hd),
            new Vector3(pos.X + hw, tableTop,   pos.Z + hd));
    }

    public bool Raycast(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        var hit = ray.Intersects(_bounds);
        if (hit.HasValue) { distance = hit.Value; return true; }
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
                    Math.Min(255, c.R + 50),
                    Math.Min(255, c.G + 35),
                    Math.Min(255, c.B + 50));
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

    private static Color Darken(Color c, float factor) => new Color(
        (int)(c.R * factor),
        (int)(c.G * factor),
        (int)(c.B * factor));
}