using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ZebraBear.Core;

/// <summary>
/// Loads Wavefront OBJ files into VertexPositionColor mesh data.
///
/// Supports:
///   v x y z              — vertex position
///   v x y z # r g b      — vertex position + colour (ZebraBear extended format)
///   f a b c              — triangle face (1-based indices)
///   f a/t b/t c/t        — face with texture coords (tex coords ignored)
///   f a/t/n b/t/n c/t/n  — face with tex + normal (both ignored)
///   f a//n b//n c//n     — face with normals (ignored)
///   o / g                — object/group names (ignored — all geo merged)
///   # comment            — skipped
///
/// If no vertex colour comment is present, the supplied fallback tint is used.
///
/// Usage:
///   var (verts, idx) = ObjLoader.Load("Data/Models/crate.obj", Color.White);
/// </summary>
public static class ObjLoader
{
    /// <summary>
    /// Load an OBJ file from a path relative to the executable directory.
    /// Returns the mesh as (VertexPositionColor[], short[]) ready for MeshEntity.
    /// </summary>
    public static (VertexPositionColor[] verts, short[] idx) Load(
        string relativePath,
        Color fallbackTint)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"[ObjLoader] OBJ file not found: {fullPath}");

        var positions = new List<Vector3>();
        var colours   = new List<Color>();       // parallel to positions
        var outVerts  = new List<VertexPositionColor>();
        var outIdx    = new List<short>();

        // OBJ indices are 1-based and reference the positions list.
        // We build a flat triangle list — no index reuse across faces
        // to keep it simple and avoid colour-per-vertex conflicts.

        foreach (var rawLine in File.ReadLines(fullPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Split on whitespace, ignore inline comments for most tokens
            var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            switch (tokens[0])
            {
                case "v":
                    ParseVertex(tokens, rawLine, positions, colours, fallbackTint);
                    break;

                case "f":
                    ParseFace(tokens, positions, colours, outVerts, outIdx);
                    break;

                // Ignored: vt, vn, o, g, s, usemtl, mtllib
            }
        }

        Console.WriteLine($"[ObjLoader] Loaded '{relativePath}' " +
                          $"({outVerts.Count} verts, {outIdx.Count / 3} tris)");

        return (outVerts.ToArray(), outIdx.ToArray());
    }

    // -----------------------------------------------------------------------
    // Vertex parsing
    // -----------------------------------------------------------------------

    private static void ParseVertex(
        string[] tokens, string rawLine,
        List<Vector3> positions, List<Color> colours,
        Color fallback)
    {
        if (tokens.Length < 4) return;

        float x = F(tokens[1]);
        float y = F(tokens[2]);
        float z = F(tokens[3]);
        positions.Add(new Vector3(x, y, z));

        // Look for inline colour comment:  v x y z # r g b
        var commentIdx = rawLine.IndexOf('#');
        if (commentIdx >= 0)
        {
            var comment = rawLine[(commentIdx + 1)..].Trim();
            var parts   = comment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                colours.Add(new Color(r, g, b));
                return;
            }
        }

        colours.Add(fallback);
    }

    // -----------------------------------------------------------------------
    // Face parsing
    // -----------------------------------------------------------------------

    private static void ParseFace(
        string[] tokens,
        List<Vector3> positions,
        List<Color> colours,
        List<VertexPositionColor> outVerts,
        List<short> outIdx)
    {
        // Collect position indices for this face (may be a polygon, triangulate via fan)
        var facePositionIndices = new List<int>();

        for (int t = 1; t < tokens.Length; t++)
        {
            // Token format: posIdx  or  posIdx/texIdx  or  posIdx/texIdx/normIdx  or  posIdx//normIdx
            var slash = tokens[t].IndexOf('/');
            var posToken = slash >= 0 ? tokens[t][..slash] : tokens[t];

            if (int.TryParse(posToken, out int posIdx))
            {
                // Convert 1-based OBJ index to 0-based; negative = relative from end
                int i = posIdx > 0 ? posIdx - 1 : positions.Count + posIdx;
                facePositionIndices.Add(i);
            }
        }

        if (facePositionIndices.Count < 3) return;

        // Fan triangulation: (0,1,2), (0,2,3), (0,3,4) ...
        for (int i = 1; i < facePositionIndices.Count - 1; i++)
        {
            short baseIdx = (short)outVerts.Count;

            int ia = facePositionIndices[0];
            int ib = facePositionIndices[i];
            int ic = facePositionIndices[i + 1];

            outVerts.Add(new VertexPositionColor(positions[ia], colours[ia]));
            outVerts.Add(new VertexPositionColor(positions[ib], colours[ib]));
            outVerts.Add(new VertexPositionColor(positions[ic], colours[ic]));

            outIdx.Add(baseIdx);
            outIdx.Add((short)(baseIdx + 1));
            outIdx.Add((short)(baseIdx + 2));
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static float F(string s) =>
        float.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>
    /// Compute a BoundingBox from a loaded vertex array.
    /// </summary>
    public static BoundingBox ComputeBounds(VertexPositionColor[] verts)
    {
        if (verts.Length == 0) return new BoundingBox();

        var min = verts[0].Position;
        var max = verts[0].Position;

        foreach (var v in verts)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }

        return new BoundingBox(min, max);
    }
}