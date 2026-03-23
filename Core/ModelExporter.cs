using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace ZebraBear.Core;

/// <summary>
/// Exports all canonical MeshBuilder shapes to Data/Models/ as OBJ files.
///
/// Writes to the PROJECT SOURCE Data/Models/ folder (not the build output)
/// so exported files can be committed alongside rooms.json and edited freely.
///
/// Call ModelExporter.ExportAll() once from Game.LoadContent().
/// Files are skipped if they already exist — safe to leave enabled permanently.
/// Pass forceOverwrite: true to regenerate everything.
///
/// After export, replace hard-coded entities in rooms.json with:
///   { "type": "obj", "model": "Data/Models/table.obj", ... }
/// </summary>
public static class ModelExporter
{
    // -----------------------------------------------------------------------
    // Path resolution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the project-source Data/Models/ directory by walking up from
    /// the build output until we find a .csproj file, then stepping into Data/Models/.
    /// Falls back to AppContext.BaseDirectory/Data/Models/ if not found.
    /// </summary>
    private static string ResolveOutputDir()
    {
        // Walk up from bin/Debug/net8.0/ looking for the .csproj
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                var target = Path.Combine(dir.FullName, "Data", "Models");
                Console.WriteLine($"[ModelExporter] Project root found: {dir.FullName}");
                return target;
            }
            dir = dir.Parent;
        }

        // Fallback — write next to the executable
        var fallback = Path.Combine(AppContext.BaseDirectory, "Data", "Models");
        Console.WriteLine($"[ModelExporter] WARNING: Could not find .csproj — " +
                          $"writing to build output: {fallback}");
        return fallback;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Export all canonical shapes. Skips files that already exist unless
    /// forceOverwrite is true.
    /// </summary>
    public static void ExportAll(bool forceOverwrite = false)
    {
        var outputDir = ResolveOutputDir();
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"[ModelExporter] Output directory: {outputDir}");

        ExportShape(outputDir, "box_crate", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.Box(
                new Vector3(-0.5f, 0f,   -0.5f),
                new Vector3( 0.5f, 1.0f,  0.5f),
                top:    new Color(180, 140, 100),
                bottom: new Color( 80,  60,  40),
                side:   new Color(130, 100,  70)));

        ExportShape(outputDir, "table", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.Table(
                Vector3.Zero, w: 2.8f, d: 1.4f, h: 1.1f,
                tint: new Color(120, 85, 55)));

        ExportShape(outputDir, "chair", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.Chair(
                Vector3.Zero, w: 0.9f, d: 0.9f, h: 0.9f,
                tint: new Color(100, 70, 45), backrest: true));

        ExportShape(outputDir, "door", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.OrientedBox(
                Vector3.Zero, w: 2.2f, h: 3.2f,
                normal: ZebraBear.Entities.MeshBuilder.FaceNorth,
                tint: new Color(90, 65, 45), depth: 0.25f));

        ExportShape(outputDir, "notice_board", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.OrientedBox(
                Vector3.Zero, w: 2.4f, h: 1.6f,
                normal: ZebraBear.Entities.MeshBuilder.FaceNorth,
                tint: new Color(160, 130, 85), depth: 0.18f));

        ExportShape(outputDir, "shelf", forceOverwrite, () =>
            ZebraBear.Entities.MeshBuilder.Shelf(
                Vector3.Zero, width: 2.5f, depth: 0.35f,
                normal: ZebraBear.Entities.MeshBuilder.FaceNorth,
                tint: new Color(120, 95, 65)));

        ExportShape(outputDir, "pillar", forceOverwrite, () =>
        {
            var tint   = new Color(90, 88, 110);
            var min    = new Vector3(-0.3f, 0f,    -0.3f);
            var max    = new Vector3( 0.3f, 5.0f,   0.3f);
            var top    = new Color((int)Math.Min(255, tint.R * 1.15f),
                                   (int)Math.Min(255, tint.G * 1.15f),
                                   (int)Math.Min(255, tint.B * 1.15f));
            var bottom = new Color((int)(tint.R * 0.6f),
                                   (int)(tint.G * 0.6f),
                                   (int)(tint.B * 0.6f));
            return ZebraBear.Entities.MeshBuilder.Box(min, max, top, bottom, tint);
        });

        Console.WriteLine($"[ModelExporter] All shapes exported to {outputDir}");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void ExportShape(
        string outputDir,
        string name,
        bool forceOverwrite,
        Func<(Microsoft.Xna.Framework.Graphics.VertexPositionColor[], short[])> build)
    {
        var path = Path.Combine(outputDir, name + ".obj");

        if (!forceOverwrite && File.Exists(path))
        {
            Console.WriteLine($"[ModelExporter] Skipping '{name}.obj' (already exists).");
            return;
        }

        try
        {
            var (verts, idx) = build();

            // Write directly to the resolved absolute path
            var fullPath = path;
            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Exported by ZebraBear ModelExporter");
            sb.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"o {name}");
            sb.AppendLine();

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
                sb.AppendLine($"f {idx[i]+1} {idx[i+1]+1} {idx[i+2]+1}");

            File.WriteAllText(fullPath, sb.ToString());
            Console.WriteLine($"[ModelExporter] ✓ {name}.obj  ({verts.Length} verts, {idx.Length/3} tris)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModelExporter] ERROR exporting '{name}': {ex.Message}");
        }
    }

    private static string F(float v) =>
        v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
}