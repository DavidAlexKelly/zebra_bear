using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZebraBear.Core;

/// <summary>
/// Exports VertexPositionColor mesh data to Wavefront OBJ files.
///
/// Colours are stored as vertex colour comments in a format Blender
/// can import via the "Import Vertex Colors" addon. Each vertex line
/// is followed by a comment: # r g b (0.0–1.0 range).
///
/// Usage:
///   ObjExporter.Export("Data/Models/table.obj", verts, idx);
///
/// Or export all registered MeshBuilder shapes at once:
///   ModelExporter.ExportAll("Data/Models/");
/// </summary>
public static class ObjExporter
{
    /// <summary>
    /// Export a single mesh to an OBJ file.
    /// outputPath is relative to the executable directory.
    /// objectName is used as the OBJ object name (defaults to filename).
    /// </summary>
    public static void Export(
        string outputPath,
        VertexPositionColor[] verts,
        short[] idx,
        string objectName = null)
    {
        // outputPath can be absolute or relative to BaseDirectory
        var fullPath = Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(AppContext.BaseDirectory, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        objectName ??= Path.GetFileNameWithoutExtension(outputPath);

        var sb = new StringBuilder();
        sb.AppendLine($"# Exported by ZebraBear ObjExporter");
        sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Vertices: {verts.Length}  Triangles: {idx.Length / 3}");
        sb.AppendLine();
        sb.AppendLine($"o {objectName}");
        sb.AppendLine();

        // Vertices — write position, then colour as a comment on the same line
        // so round-tripping through ObjLoader can recover the colours.
        foreach (var v in verts)
        {
            float r = v.Color.R / 255f;
            float g = v.Color.G / 255f;
            float b = v.Color.B / 255f;

            // OBJ uses right-handed coords; MonoGame is right-handed so no flip needed.
            sb.AppendLine(
                $"v {F(v.Position.X)} {F(v.Position.Y)} {F(v.Position.Z)}" +
                $" # {F(r)} {F(g)} {F(b)}");
        }

        sb.AppendLine();

        // Faces — OBJ indices are 1-based
        for (int i = 0; i < idx.Length; i += 3)
        {
            int a = idx[i]     + 1;
            int b = idx[i + 1] + 1;
            int c = idx[i + 2] + 1;
            sb.AppendLine($"f {a} {b} {c}");
        }

        File.WriteAllText(fullPath, sb.ToString());
        Console.WriteLine($"[ObjExporter] Exported '{objectName}' → {outputPath}");
    }

    /// <summary>
    /// Export multiple named meshes into a single OBJ file as separate objects.
    /// Useful for exporting a room's worth of geometry in one file.
    /// </summary>
    public static void ExportMulti(
        string outputPath,
        IEnumerable<(string name, VertexPositionColor[] verts, short[] idx)> meshes)
    {
        // outputPath can be absolute or relative to BaseDirectory
        var fullPath = Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(AppContext.BaseDirectory, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var sb = new StringBuilder();
        sb.AppendLine($"# Exported by ZebraBear ObjExporter");
        sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        int vertOffset = 0;

        foreach (var (name, verts, idx) in meshes)
        {
            sb.AppendLine($"o {name}");

            foreach (var v in verts)
            {
                float r = v.Color.R / 255f;
                float g = v.Color.G / 255f;
                float b = v.Color.B / 255f;
                sb.AppendLine(
                    $"v {F(v.Position.X)} {F(v.Position.Y)} {F(v.Position.Z)}" +
                    $" # {F(r)} {F(g)} {F(b)}");
            }

            sb.AppendLine();

            for (int i = 0; i < idx.Length; i += 3)
            {
                int a = idx[i]     + 1 + vertOffset;
                int b = idx[i + 1] + 1 + vertOffset;
                int c = idx[i + 2] + 1 + vertOffset;
                sb.AppendLine($"f {a} {b} {c}");
            }

            sb.AppendLine();
            vertOffset += verts.Length;
        }

        File.WriteAllText(fullPath, sb.ToString());
        Console.WriteLine($"[ObjExporter] Exported multi-mesh → {outputPath}");
    }

    private static string F(float v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
}