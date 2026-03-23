using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Text.Json.Nodes;
using ZebraBear.Core;
using ZebraBear.Entities;

namespace ZebraBear;

/// <summary>
/// Registers all entity types used by this game.
///
/// The engine (Core/) knows nothing about these types. This file is the
/// complete list of what the game's level format supports.
///
/// To add a new entity type:
///   1. Write a class implementing IEntityBuilder below.
///   2. Add EntityRegistry.Register(new MyBuilder()) in Register().
///   3. Use the TypeName as "type" in rooms.json.
/// </summary>
public static class ZebraBearEntities
{
    /// <summary>
    /// Set this before calling Register() so FbxEntityBuilder can load
    /// compiled models from the content pipeline.
    /// Assign in Game.LoadContent(): ZebraBearEntities.Content = Content;
    /// </summary>
    public static ContentManager Content { get; set; }

    public static void Register()
    {
        EntityRegistry.Register(new BillboardBuilder());
        EntityRegistry.Register(new OrientedBoxBuilder());
        EntityRegistry.Register(new BoxBuilder());
        EntityRegistry.Register(new TableBuilder());
        EntityRegistry.Register(new ChairBuilder());
        EntityRegistry.Register(new ShelfBuilder());
        EntityRegistry.Register(new PillarBuilder());
        EntityRegistry.Register(new ObjEntityBuilder());
        EntityRegistry.Register(new FbxEntityBuilder());
    }
}

// ---------------------------------------------------------------------------
// billboard
// ---------------------------------------------------------------------------

/// <summary>
/// Camera-facing quad — characters and decorative sprites.
///
/// JSON: position, width, height, tint, isCharacter
/// </summary>
file class BillboardBuilder : IEntityBuilder
{
    public string TypeName => "billboard";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var pos  = J.Vec3(node["position"]);
        var tint = J.Color(node["tint"]) ?? Color.White;
        float w  = node["width"]?.GetValue<float>()      ?? 2f;
        float h  = node["height"]?.GetValue<float>()     ?? 4f;
        bool isC = node["isCharacter"]?.GetValue<bool>() ?? false;

        Texture2D sprite = null;
        if (!string.IsNullOrEmpty(name))
        {
            var profile = CharacterData.Characters.Find(c => c.Id == name);
            if (profile?.Portrait != null) sprite = profile.Portrait;
        }

        return new BillboardEntity
        {
            Name        = name,
            Position    = pos,
            Width       = w,
            Height      = h,
            Tint        = tint,
            IsCharacter = isC,
            Sprite      = sprite,
            Dialogue    = dialogue
        };
    }
}

// ---------------------------------------------------------------------------
// orientedBox
// ---------------------------------------------------------------------------

/// <summary>
/// Box flush against a wall, facing a given normal.
///
/// JSON: centre, width, height, depth, normal, tint, solid, onInteract
/// </summary>
file class OrientedBoxBuilder : IEntityBuilder
{
    public string TypeName => "orientedBox";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var centre = J.Vec3(node["centre"]);
        float w    = node["width"]?.GetValue<float>()  ?? 1f;
        float h    = node["height"]?.GetValue<float>() ?? 1f;
        float d    = node["depth"]?.GetValue<float>()  ?? 0.3f;
        bool solid = node["solid"]?.GetValue<bool>()   ?? true;
        var tint   = J.Color(node["tint"]) ?? new Color(100, 100, 100);
        var normal = J.Normal(node["normal"]?.GetValue<string>());

        var entity = MeshEntity.CreateOrientedBox(name, dialogue, centre, w, h, normal, tint, d);
        entity.Solid = solid;

        var interact = node["onInteract"];
        if (interact != null)
            entity.OnInteract = InteractCallbackBuilder.Build(interact);

        return entity;
    }
}

// ---------------------------------------------------------------------------
// box
// ---------------------------------------------------------------------------

/// <summary>
/// Axis-aligned box from min to max corner.
///
/// JSON: min, max, top, bottom, side
/// </summary>
file class BoxBuilder : IEntityBuilder
{
    public string TypeName => "box";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var min    = J.Vec3(node["min"]);
        var max    = J.Vec3(node["max"]);
        var top    = J.Color(node["top"])    ?? new Color(180, 140, 100);
        var bottom = J.Color(node["bottom"]) ?? new Color(80,  60,  40);
        var side   = J.Color(node["side"])   ?? new Color(130, 100, 70);

        return MeshEntity.CreateBox(name, dialogue, min, max, top, bottom, side);
    }
}

// ---------------------------------------------------------------------------
// table
// ---------------------------------------------------------------------------

/// <summary>
/// Freestanding table with top slab and four legs.
///
/// JSON: position, width, depth, height, tint
/// </summary>
file class TableBuilder : IEntityBuilder
{
    public string TypeName => "table";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var pos  = J.Vec3(node["position"]);
        float w  = node["width"]?.GetValue<float>()  ?? 2f;
        float d  = node["depth"]?.GetValue<float>()  ?? 1f;
        float h  = node["height"]?.GetValue<float>() ?? 1f;
        var tint = J.Color(node["tint"]) ?? new Color(120, 85, 55);

        return MeshEntity.CreateTable(name, dialogue, pos, w, d, h, tint);
    }
}

// ---------------------------------------------------------------------------
// chair
// ---------------------------------------------------------------------------

/// <summary>
/// Freestanding chair with seat, legs, and optional backrest.
///
/// JSON: position, width, depth, height, tint, backrest
/// </summary>
file class ChairBuilder : IEntityBuilder
{
    public string TypeName => "chair";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var pos       = J.Vec3(node["position"]);
        float w       = node["width"]?.GetValue<float>()   ?? 0.9f;
        float d       = node["depth"]?.GetValue<float>()   ?? 0.9f;
        float h       = node["height"]?.GetValue<float>()  ?? 0.9f;
        bool backrest = node["backrest"]?.GetValue<bool>() ?? true;
        var tint      = J.Color(node["tint"]) ?? new Color(100, 70, 45);

        (VertexPositionColor[] verts, short[] idx) = MeshBuilder.Chair(pos, w, d, h, tint, backrest);

        float topH = backrest ? h + 0.6f : h;
        var bounds = new BoundingBox(
            new Vector3(pos.X - w / 2f, pos.Y,        pos.Z - d / 2f),
            new Vector3(pos.X + w / 2f, pos.Y + topH, pos.Z + d / 2f));

        return new MeshEntity(name, dialogue, verts, idx, bounds);
    }
}

// ---------------------------------------------------------------------------
// shelf
// ---------------------------------------------------------------------------

/// <summary>
/// Wall-mounted shelf with slab and two side brackets.
///
/// JSON: centre, width, depth, normal, tint
/// </summary>
file class ShelfBuilder : IEntityBuilder
{
    public string TypeName => "shelf";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var centre = J.Vec3(node["centre"]);
        float w    = node["width"]?.GetValue<float>()  ?? 2f;
        float d    = node["depth"]?.GetValue<float>()  ?? 0.4f;
        var tint   = J.Color(node["tint"]) ?? new Color(140, 110, 80);
        var normal = J.Normal(node["normal"]?.GetValue<string>());

        (VertexPositionColor[] verts, short[] idx) = MeshBuilder.Shelf(centre, w, d, normal, tint);
        var bounds = MeshBuilder.BoundsForOrientedBox(centre, w, 0.12f, normal, d);

        return new MeshEntity(name, dialogue, verts, idx, bounds);
    }
}

// ---------------------------------------------------------------------------
// pillar
// ---------------------------------------------------------------------------

/// <summary>
/// Freestanding vertical pillar.
///
/// JSON: position, width, depth, height, tint
/// </summary>
file class PillarBuilder : IEntityBuilder
{
    public string TypeName => "pillar";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var pos  = J.Vec3(node["position"]);
        float w  = node["width"]?.GetValue<float>()  ?? 0.6f;
        float d  = node["depth"]?.GetValue<float>()  ?? 0.6f;
        float h  = node["height"]?.GetValue<float>() ?? 5f;
        var tint = J.Color(node["tint"]) ?? new Color(90, 88, 110);

        var min = new Vector3(pos.X - w / 2f, pos.Y,     pos.Z - d / 2f);
        var max = new Vector3(pos.X + w / 2f, pos.Y + h, pos.Z + d / 2f);

        var top = new Color(
            (int)Math.Min(255, tint.R * 1.15f),
            (int)Math.Min(255, tint.G * 1.15f),
            (int)Math.Min(255, tint.B * 1.15f));
        var bottom = new Color(
            (int)(tint.R * 0.6f),
            (int)(tint.G * 0.6f),
            (int)(tint.B * 0.6f));

        (VertexPositionColor[] verts, short[] idx) = MeshBuilder.Box(min, max, top, bottom, tint);
        var entity = new MeshEntity(name, dialogue, verts, idx, new BoundingBox(min, max));
        entity.Solid = true;
        return entity;
    }
}

// ---------------------------------------------------------------------------
// obj  —  runtime OBJ file loading
// ---------------------------------------------------------------------------

/// <summary>
/// Loads a Wavefront OBJ file at runtime and creates a MeshEntity from it.
///
/// OBJ files exported by ZebraBear's ObjExporter/ModelExporter carry vertex
/// colour comments and render with the original colours. External OBJ files
/// (from Blender, Kenney, etc.) use the "tint" field as a flat colour.
///
/// JSON fields:
///   model     string       path relative to executable, e.g. "Data/Models/crate.obj"
///   position  [x, y, z]   world offset applied to all vertices (default zero)
///   scale     float        uniform scale (default 1.0)
///   tint      [r, g, b]   fallback colour if the OBJ has no vertex colours
///   solid     bool         default true
///   onInteract             standard interact node
///
/// Example:
///   {
///     "type": "obj",
///     "name": "Crate",
///     "model": "Data/Models/box_crate.obj",
///     "position": [2.0, 0.0, -5.0],
///     "scale": 1.0,
///     "tint": [180, 140, 100],
///     "dialogue": ["A wooden crate."]
///   }
/// </summary>
file class ObjEntityBuilder : IEntityBuilder
{
    public string TypeName => "obj";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        var modelPath = node["model"]?.GetValue<string>()
            ?? throw new Exception($"[ObjEntityBuilder] 'model' path is required (entity '{name}')");

        var tint    = J.Color(node["tint"]) ?? Color.White;
        var pos     = J.Vec3(node["position"]);
        float scale = node["scale"]?.GetValue<float>() ?? 1f;
        bool solid  = node["solid"]?.GetValue<bool>()  ?? true;

        (VertexPositionColor[] verts, short[] idx) = ObjLoader.Load(modelPath, tint);

        if (pos != Vector3.Zero || scale != 1f)
        {
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new VertexPositionColor(
                    verts[i].Position * scale + pos,
                    verts[i].Color);
        }

        var bounds = ObjLoader.ComputeBounds(verts);
        var entity = new MeshEntity(name, dialogue, verts, idx, bounds);
        entity.Solid = solid;

        var interact = node["onInteract"];
        if (interact != null)
            entity.OnInteract = InteractCallbackBuilder.Build(interact);

        return entity;
    }
}

// ---------------------------------------------------------------------------
// fbx  —  MonoGame content pipeline model loading
// ---------------------------------------------------------------------------

/// <summary>
/// Loads a compiled FBX or X model from the MonoGame content pipeline.
///
/// Prerequisites:
///   1. Add the model to Content.mgcb (Importer: FbxImporter, Processor: ModelProcessor).
///   2. Set ZebraBearEntities.Content = Content in Game.LoadContent().
///
/// JSON fields:
///   model     string       Content-relative path without extension, e.g. "Models/crate"
///   position  [x, y, z]   world offset (default zero)
///   scale     float        uniform scale (default 1.0)
///   tint      [r, g, b]   flat tint when the model has no vertex colours
///   solid     bool         default true
///   onInteract             standard interact node
///
/// Example:
///   {
///     "type": "fbx",
///     "name": "Barrel",
///     "model": "Models/barrel",
///     "position": [-3.0, 0.0, -8.0],
///     "tint": [120, 85, 55],
///     "dialogue": ["A wooden barrel."]
///   }
/// </summary>
file class FbxEntityBuilder : IEntityBuilder
{
    public string TypeName => "fbx";

    public Entity Build(JsonNode node, string name, string[] dialogue)
    {
        if (ZebraBearEntities.Content == null)
            throw new Exception(
                "[FbxEntityBuilder] ZebraBearEntities.Content is null. " +
                "Add 'ZebraBearEntities.Content = Content;' in Game.LoadContent() " +
                "before calling ZebraBearEntities.Register().");

        var modelPath = node["model"]?.GetValue<string>()
            ?? throw new Exception($"[FbxEntityBuilder] 'model' path is required (entity '{name}')");

        var tint    = J.Color(node["tint"]) ?? Color.White;
        var pos     = J.Vec3(node["position"]);
        float scale = node["scale"]?.GetValue<float>() ?? 1f;
        bool solid  = node["solid"]?.GetValue<bool>()  ?? true;

        var (verts, idx, bounds) = FbxLoader.Load(ZebraBearEntities.Content, modelPath, tint);

        if (pos != Vector3.Zero || scale != 1f)
        {
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new VertexPositionColor(
                    verts[i].Position * scale + pos,
                    verts[i].Color);

            bounds = ObjLoader.ComputeBounds(verts);
        }

        var entity = new MeshEntity(name, dialogue, verts, idx, bounds);
        entity.Solid = solid;

        var interact = node["onInteract"];
        if (interact != null)
            entity.OnInteract = InteractCallbackBuilder.Build(interact);

        return entity;
    }
}

// ---------------------------------------------------------------------------
// Shared JSON helpers — private to this file
// ---------------------------------------------------------------------------

file static class J
{
    public static Vector3 Vec3(JsonNode node)
    {
        if (node == null) return Vector3.Zero;
        var a = node.AsArray();
        return new Vector3(
            a[0]!.GetValue<float>(),
            a[1]!.GetValue<float>(),
            a[2]!.GetValue<float>());
    }

    public static Color? Color(JsonNode node)
    {
        if (node == null) return null;
        var a = node.AsArray();
        return new Color(
            a[0]!.GetValue<int>(),
            a[1]!.GetValue<int>(),
            a[2]!.GetValue<int>());
    }

    public static Vector3 Normal(string s) => s switch
    {
        "north" => MeshBuilder.FaceNorth,
        "south" => MeshBuilder.FaceSouth,
        "east"  => MeshBuilder.FaceEast,
        "west"  => MeshBuilder.FaceWest,
        _       => MeshBuilder.FaceNorth
    };
}