using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZebraBear.Entities;

namespace ZebraBear.Core;

/// <summary>
/// Loads all game data from JSON files in the Data/ directory.
///
/// Call order in Game.LoadContent():
///   Assets.Load(Content, GraphicsDevice);          // fonts, textures
///   GameLoader.LoadCharacters(Content);            // populates CharacterData
///   GameLoader.LoadMap();                          // populates MapData
///   GameLoader.LoadRoom("MainHall", room, game);  // populates a Room with entities
///
/// JSON files live next to the built executable in Data/:
///   Data/characters.json
///   Data/map.json
///   Data/rooms.json
///
/// Add your .csproj copy targets so these files are always alongside the exe:
///   <None Update="Data\*.json">
///     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
///   </None>
/// </summary>
public static class GameLoader
{
    // -----------------------------------------------------------------------
    // Path helpers
    // -----------------------------------------------------------------------

    private static string DataDir =>
        Path.Combine(AppContext.BaseDirectory, "Data");

    private static string DataPath(string file) =>
        Path.Combine(DataDir, file);

    // -----------------------------------------------------------------------
    // Characters
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads Data/characters.json and rebuilds CharacterData.Characters.
    /// Must be called after Assets.Load() so portrait textures are available.
    /// </summary>
    public static void LoadCharacters(ContentManager content)
    {
        var path = DataPath("characters.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameLoader] WARNING: {path} not found — using empty character list.");
            return;
        }

        var root  = JsonNode.Parse(File.ReadAllText(path));
        var array = root!["characters"]!.AsArray();

        CharacterData.Characters.Clear();

        foreach (var node in array)
        {
            var id       = node!["id"]!.GetValue<string>();
            var name     = node["name"]!.GetValue<string>();
            var title    = node["title"]!.GetValue<string>();
            var met      = node["met"]?.GetValue<bool>() ?? false;
            var portrait = node["portrait"]?.GetValue<string>();

            var bioArray = node["bio"]?.AsArray();
            var bio      = new List<string>();
            if (bioArray != null)
                foreach (var b in bioArray) bio.Add(b!.GetValue<string>());

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(portrait))
            {
                try   { tex = content.Load<Texture2D>(portrait); }
                catch { Console.WriteLine($"[GameLoader] Could not load portrait: {portrait}"); }
            }

            CharacterData.Characters.Add(new CharacterProfile
            {
                Id      = id,
                Name    = name,
                Title   = title,
                Bio     = bio.ToArray(),
                Portrait = tex,
                Met     = met
            });
        }

        Console.WriteLine($"[GameLoader] Loaded {CharacterData.Characters.Count} character(s).");
    }

    // -----------------------------------------------------------------------
    // Map
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads Data/map.json and rebuilds MapData.Rooms, Connections, and
    /// CurrentRoomId.
    /// </summary>
    public static void LoadMap()
    {
        var path = DataPath("map.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameLoader] WARNING: {path} not found — using empty map.");
            return;
        }

        var root = JsonNode.Parse(File.ReadAllText(path));

        MapData.CurrentRoomId = root!["startRoom"]?.GetValue<string>() ?? "MainHall";

        MapData.Rooms.Clear();
        var roomsArray = root["rooms"]?.AsArray();
        if (roomsArray != null)
        {
            foreach (var node in roomsArray)
            {
                var pos  = node!["position"]!.AsArray();
                var size = node["size"]!.AsArray();

                MapData.Rooms.Add(new MapRoom
                {
                    Id         = node["id"]!.GetValue<string>(),
                    Label      = node["label"]!.GetValue<string>(),
                    Position   = new Vector2(
                        pos[0]!.GetValue<float>(),
                        pos[1]!.GetValue<float>()),
                    Size       = new Vector2(
                        size[0]!.GetValue<float>(),
                        size[1]!.GetValue<float>()),
                    Discovered = node["discovered"]?.GetValue<bool>() ?? false
                });
            }
        }

        MapData.Connections.Clear();
        var connArray = root["connections"]?.AsArray();
        if (connArray != null)
        {
            foreach (var node in connArray)
            {
                MapData.Connections.Add(new MapConnection
                {
                    FromId = node!["fromId"]!.GetValue<string>(),
                    ToId   = node["toId"]!.GetValue<string>()
                });
            }
        }

        Console.WriteLine($"[GameLoader] Loaded {MapData.Rooms.Count} room(s) on the map.");
    }

    // -----------------------------------------------------------------------
    // Room entities
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads Data/rooms.json, finds the entry matching roomId, and populates
    /// the supplied Room with entities.
    ///
    /// onInteractOverrides: optional dictionary that maps entity names to
    /// Action&lt;int&gt; callbacks — use this to wire up navigation and other
    /// game-logic callbacks that can't be expressed in pure JSON.
    ///
    ///   GameLoader.LoadRoom("MainHall", _room, game,
    ///       new Dictionary&lt;string, Action&lt;int&gt;&gt;
    ///       {
    ///           ["Locked Door"] = i => { if (i == 0) game.GoToRoom2(); }
    ///       });
    /// </summary>
    public static void LoadRoom(
        string roomId,
        Room room,
        Dictionary<string, Action<int>> onInteractOverrides = null)
    {
        var path = DataPath("rooms.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameLoader] WARNING: {path} not found.");
            return;
        }

        var root      = JsonNode.Parse(File.ReadAllText(path));
        var roomsArr  = root!["rooms"]!.AsArray();
        JsonNode roomNode = null;

        foreach (var r in roomsArr)
        {
            if (r!["id"]!.GetValue<string>() == roomId)
            {
                roomNode = r;
                break;
            }
        }

        if (roomNode == null)
        {
            Console.WriteLine($"[GameLoader] WARNING: room '{roomId}' not found in rooms.json.");
            return;
        }

        var entitiesArr = roomNode["entities"]?.AsArray();
        if (entitiesArr == null)
        {
            Console.WriteLine($"[GameLoader] Room '{roomId}' has no entities array.");
            return;
        }

        int count = 0;
        foreach (var node in entitiesArr)
        {
            var entity = BuildEntity(node!, onInteractOverrides);
            if (entity != null)
            {
                room.Add(entity);
                count++;
            }
        }

        Console.WriteLine($"[GameLoader] Loaded {count} entit(ies) into room '{roomId}'.");
    }

    // -----------------------------------------------------------------------
    // Room colour helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the wall/floor/ceil colours for a room from rooms.json.
    /// Returns nulls if the room or the colour fields are absent (caller uses defaults).
    /// </summary>
    public static (Color? wall, Color? floor, Color? ceil, string label)
        ReadRoomColors(string roomId)
    {
        var path = DataPath("rooms.json");
        if (!File.Exists(path)) return (null, null, null, roomId);

        var root     = JsonNode.Parse(File.ReadAllText(path));
        var roomsArr = root!["rooms"]!.AsArray();

        foreach (var r in roomsArr)
        {
            if (r!["id"]!.GetValue<string>() != roomId) continue;

            Color? wall  = ReadColor(r["wallColor"]);
            Color? floor = ReadColor(r["floorColor"]);
            Color? ceil  = ReadColor(r["ceilColor"]);
            string label = r["label"]?.GetValue<string>() ?? roomId;
            return (wall, floor, ceil, label);
        }

        return (null, null, null, roomId);
    }

    // -----------------------------------------------------------------------
    // Entity builder
    // -----------------------------------------------------------------------

    private static Entity BuildEntity(
        JsonNode node,
        Dictionary<string, Action<int>> overrides)
    {
        var type = node["type"]!.GetValue<string>();
        var name = node["name"]?.GetValue<string>() ?? "";
        var dialogue = ReadStringArray(node["dialogue"]);

        switch (type)
        {
            case "billboard":
                return BuildBillboard(node, name, dialogue);

            case "orientedBox":
                return BuildOrientedBox(node, name, dialogue, overrides);

            case "table":
                return BuildTable(node, name, dialogue);

            case "box":
                return BuildBox(node, name, dialogue);

            default:
                Console.WriteLine($"[GameLoader] Unknown entity type: '{type}' (name='{name}')");
                return null;
        }
    }

    // -----------------------------------------------------------------------
    // Per-type builders
    // -----------------------------------------------------------------------

    private static Entity BuildBillboard(JsonNode node, string name, string[] dialogue)
    {
        var pos     = ReadVec3(node["position"]);
        var tint    = ReadColor(node["tint"]) ?? Color.White;
        float w     = node["width"]?.GetValue<float>()   ?? 2f;
        float h     = node["height"]?.GetValue<float>()  ?? 4f;
        bool isChar = node["isCharacter"]?.GetValue<bool>() ?? false;

        // Look up sprite from CharacterData by matching name
        Texture2D sprite = null;
        var spriteKey    = node["sprite"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(spriteKey))
        {
            // "sprite" can be a content path — but since portraits are already loaded
            // into CharacterData, we prefer to mirror them from there.
        }

        // Mirror portrait from CharacterData if available (avoids double-loading)
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
            IsCharacter = isChar,
            Sprite      = sprite,
            Dialogue    = dialogue
        };
    }

    private static Entity BuildOrientedBox(
        JsonNode node, string name, string[] dialogue,
        Dictionary<string, Action<int>> overrides)
    {
        var centre = ReadVec3(node["centre"]);
        float w    = node["width"]?.GetValue<float>()  ?? 1f;
        float h    = node["height"]?.GetValue<float>() ?? 1f;
        float d    = node["depth"]?.GetValue<float>()  ?? 0.3f;
        bool solid = node["solid"]?.GetValue<bool>()   ?? true;
        var tint   = ReadColor(node["tint"]) ?? new Color(100, 100, 100);
        var normal = ReadNormal(node["normal"]?.GetValue<string>());

        var entity = MeshEntity.CreateOrientedBox(name, dialogue, centre, w, h, normal, tint, d);
        entity.Solid = solid;

        // Wire interaction callback — override dictionary takes priority over JSON
        if (!string.IsNullOrEmpty(name) && overrides != null &&
            overrides.TryGetValue(name, out var cb))
        {
            entity.OnInteract = cb;
        }
        else
        {
            // Fall back to JSON-declared interaction
            var interact = node["onInteract"];
            if (interact != null)
                entity.OnInteract = BuildInteractCallback(interact);
        }

        return entity;
    }

    private static Entity BuildTable(JsonNode node, string name, string[] dialogue)
    {
        var pos  = ReadVec3(node["position"]);
        float w  = node["width"]?.GetValue<float>()  ?? 2f;
        float d  = node["depth"]?.GetValue<float>()  ?? 1f;
        float h  = node["height"]?.GetValue<float>() ?? 1f;
        var tint = ReadColor(node["tint"]) ?? new Color(120, 85, 55);

        return MeshEntity.CreateTable(name, dialogue, pos, w, d, h, tint);
    }

    private static Entity BuildBox(JsonNode node, string name, string[] dialogue)
    {
        var min    = ReadVec3(node["min"]);
        var max    = ReadVec3(node["max"]);
        var top    = ReadColor(node["top"])    ?? new Color(180, 140, 100);
        var bottom = ReadColor(node["bottom"]) ?? new Color(80,  60,  40);
        var side   = ReadColor(node["side"])   ?? new Color(130, 100, 70);

        return MeshEntity.CreateBox(name, dialogue, min, max, top, bottom, side);
    }

    // -----------------------------------------------------------------------
    // Interaction callback builder
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a simple Action&lt;int&gt; from the "onInteract" JSON node.
    /// Supported types:
    ///   { "type": "navigate", "target": "Room2", "choiceIndex": 0 }
    ///
    /// For anything more complex (multiple outcomes, inventory checks, etc.)
    /// pass the callback via onInteractOverrides in LoadRoom().
    /// </summary>
    private static Action<int> BuildInteractCallback(JsonNode node)
    {
        var interactType = node["type"]?.GetValue<string>();
        if (interactType == "navigate")
        {
            var target      = node["target"]?.GetValue<string>() ?? "";
            int choiceIndex = node["choiceIndex"]?.GetValue<int>() ?? 0;

            // Navigation is resolved at runtime via NavigationBus
            return (result) =>
            {
                if (result == choiceIndex)
                    NavigationBus.RequestNavigate(target);
            };
        }

        Console.WriteLine($"[GameLoader] Unknown onInteract type: '{interactType}'");
        return null;
    }

    // -----------------------------------------------------------------------
    // JSON read helpers
    // -----------------------------------------------------------------------

    private static Vector3 ReadVec3(JsonNode node)
    {
        if (node == null) return Vector3.Zero;
        var arr = node.AsArray();
        return new Vector3(
            arr[0]!.GetValue<float>(),
            arr[1]!.GetValue<float>(),
            arr[2]!.GetValue<float>());
    }

    private static Color? ReadColor(JsonNode node)
    {
        if (node == null) return null;
        var arr = node.AsArray();
        return new Color(
            arr[0]!.GetValue<int>(),
            arr[1]!.GetValue<int>(),
            arr[2]!.GetValue<int>());
    }

    private static string[] ReadStringArray(JsonNode node)
    {
        if (node == null) return null;
        var arr    = node.AsArray();
        var result = new string[arr.Count];
        for (int i = 0; i < arr.Count; i++)
            result[i] = arr[i]!.GetValue<string>();
        return result;
    }

    private static Vector3 ReadNormal(string s) => s switch
    {
        "north" => MeshBuilder.FaceNorth,
        "south" => MeshBuilder.FaceSouth,
        "east"  => MeshBuilder.FaceEast,
        "west"  => MeshBuilder.FaceWest,
        _       => MeshBuilder.FaceNorth
    };
}