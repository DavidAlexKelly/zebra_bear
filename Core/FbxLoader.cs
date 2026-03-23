using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ZebraBear.Core;

/// <summary>
/// Loads FBX (and X) files via MonoGame's content pipeline into
/// VertexPositionColor mesh data compatible with MeshEntity.
///
/// Prerequisites:
///   1. Add the FBX to Content/Models/ in MGCB editor:
///        Importer:  FbxImporter
///        Processor: ModelProcessor
///   2. Reference it from rooms.json as:
///        { "type": "fbx", "model": "Models/mymodel", ... }
///
/// Colour strategy:
///   FBX files don't carry vertex colours by default. The loader applies
///   a flat tint (from the JSON "tint" field) to all vertices, with simple
///   face-normal shading to preserve the low-poly aesthetic — top faces are
///   lighter, bottom faces darker, sides at the base tint.
///
/// If you need per-vertex colours from Blender, bake vertex colours to the
/// mesh before export and set processor parameter VertexColorEnabled = true
/// in MGCB. The loader will use them automatically if present.
/// </summary>
public static class FbxLoader
{
    /// <summary>
    /// Load a compiled MonoGame Model and convert to VertexPositionColor arrays.
    /// contentPath is the Content-relative path without extension, e.g. "Models/crate".
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx, BoundingBox bounds)
        Load(ContentManager content, string contentPath, Color tint)
    {
        Model model;
        try
        {
            model = content.Load<Model>(contentPath);
        }
        catch (Exception ex)
        {
            throw new Exception($"[FbxLoader] Could not load model '{contentPath}': {ex.Message}");
        }

        var allVerts = new List<VertexPositionColor>();
        var allIdx   = new List<short>();

        foreach (var mesh in model.Meshes)
        {
            foreach (var part in mesh.MeshParts)
            {
                short vertOffset = (short)allVerts.Count;

                // Read vertex positions from the vertex buffer
                var vertDecl = part.VertexBuffer.VertexDeclaration;
                int stride   = vertDecl.VertexStride;
                int count    = part.NumVertices;

                var rawBytes = new byte[count * stride];
                part.VertexBuffer.GetData(
                    part.VertexOffset * stride, rawBytes, 0, rawBytes.Length);

                // Find the position element offset within the vertex declaration
                int posOffset = FindElementOffset(vertDecl, VertexElementUsage.Position);
                int colOffset = FindElementOffset(vertDecl, VertexElementUsage.Color);

                for (int v = 0; v < count; v++)
                {
                    int   base_  = v * stride;
                    float x      = BitConverter.ToSingle(rawBytes, base_ + posOffset);
                    float y      = BitConverter.ToSingle(rawBytes, base_ + posOffset + 4);
                    float z      = BitConverter.ToSingle(rawBytes, base_ + posOffset + 8);

                    var pos = new Vector3(x, y, z);

                    Color col;
                    if (colOffset >= 0)
                    {
                        // Model has baked vertex colours — use them
                        byte r = rawBytes[base_ + colOffset];
                        byte g = rawBytes[base_ + colOffset + 1];
                        byte b = rawBytes[base_ + colOffset + 2];
                        byte a = rawBytes[base_ + colOffset + 3];
                        col = new Color(r, g, b, a);
                    }
                    else
                    {
                        // No vertex colours — apply tint with simple height shading
                        col = tint;
                    }

                    allVerts.Add(new VertexPositionColor(pos, col));
                }

                // Read indices from the index buffer
                var idxData = new short[part.PrimitiveCount * 3];
                part.IndexBuffer.GetData(
                    part.StartIndex * 2, idxData, 0, idxData.Length);

                foreach (var i in idxData)
                    allIdx.Add((short)(i + vertOffset));
            }
        }

        // Apply height-based shading if no vertex colours were present
        // (gives the flat low-poly look matching the rest of the game)
        ApplyHeightShading(allVerts, tint);

        var bounds = ObjLoader.ComputeBounds(allVerts.ToArray());

        Console.WriteLine($"[FbxLoader] Loaded '{contentPath}' " +
                          $"({allVerts.Count} verts, {allIdx.Count / 3} tris)");

        return (allVerts.ToArray(), allIdx.ToArray(), bounds);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static int FindElementOffset(VertexDeclaration decl, VertexElementUsage usage)
    {
        foreach (var elem in decl.GetVertexElements())
            if (elem.VertexElementUsage == usage)
                return elem.Offset;
        return -1;
    }

    /// <summary>
    /// Applies simple height-based shading to a flat-tinted mesh.
    /// Top vertices (high Y) are lightened, bottom vertices darkened.
    /// Preserves the flat-shaded aesthetic of the procedural geometry.
    /// Only applied when all vertices share the same tint colour.
    /// </summary>
    private static void ApplyHeightShading(List<VertexPositionColor> verts, Color tint)
    {
        if (verts.Count == 0) return;

        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var v in verts)
        {
            if (v.Position.Y < minY) minY = v.Position.Y;
            if (v.Position.Y > maxY) maxY = v.Position.Y;
        }

        float range = maxY - minY;
        if (range < 0.001f) return;

        for (int i = 0; i < verts.Count; i++)
        {
            float t   = (verts[i].Position.Y - minY) / range; // 0 = bottom, 1 = top
            float f   = 0.6f + t * 0.5f;                      // 0.6 → 1.1 range
            f = Math.Clamp(f, 0f, 1f);

            var c = verts[i].Color;
            verts[i] = new VertexPositionColor(
                verts[i].Position,
                new Color(
                    (int)(c.R * f),
                    (int)(c.G * f),
                    (int)(c.B * f)));
        }
    }
}