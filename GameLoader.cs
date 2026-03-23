using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using ZebraBear.Entities;
using ZebraBear.Scenes;

namespace ZebraBear.Core;

/// <summary>
/// Loads all game data from JSON files in the Data/ directory.
///
/// Call order in Game.LoadContent():
///   EntityRegistry.RegisterDefaults();             // register built-in entity types
///   Assets.Load(Content, GraphicsDevice);          // fonts, textures
///   GameLoader.LoadCharacters(Content);            // populates CharacterData
///   GameLoader.LoadMap();                          // populates MapData
///   GameLoader.LoadRoom("MainHall", room);         // populates a Room with entities
///
/// Adding a new entity type:
///   1. Implement IEntityBuilder with a unique TypeName.
///   2. Call EntityRegistry.Register(new MyBuilder()) before LoadRoom().
///   3. Use the TypeName as the "type" field in rooms.json.
///   No changes to GameLoader are needed.
///
/// Navigation is now fully JSON-driven via onInteract nodes — no
/// onInteractOverrides dictionary. See InteractCallbackBuilder for supported types.
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
            var portrait = node["portrait"]?.GetValue<string>() ?? "";
 
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
                Id          = id,
                Name        = name,
                Title       = title,
                Bio         = bio.ToArray(),
                Portrait    = tex,
                PortraitPath = portrait,   // ← store the path for later use
                Met         = met
            });
        }
 
        Console.WriteLine($"[GameLoader] Loaded {CharacterData.Characters.Count} character(s).");
    }
    // -----------------------------------------------------------------------
    // Map
    // -----------------------------------------------------------------------

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
                    SceneType  = node["sceneType"]?.GetValue<string>() ?? "box",
                    Position   = new Vector2(pos[0]!.GetValue<float>(), pos[1]!.GetValue<float>()),
                    Size       = new Vector2(size[0]!.GetValue<float>(), size[1]!.GetValue<float>()),
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
    /// Entity types and navigation are now entirely data-driven:
    ///   - Add entity types by registering IEntityBuilder instances.
    ///   - Wire navigation via onInteract JSON nodes (no overrides dict).
    /// </summary>
    public static void LoadRoom(string roomId, Room room)
    {
        var path = LevelData._levelRoomsOverride != null && File.Exists(LevelData._levelRoomsOverride)
            ? LevelData._levelRoomsOverride
            : DataPath("rooms.json");
 
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameLoader] WARNING: {path} not found.");
            return;
        }
 
        var root = JsonNode.Parse(File.ReadAllText(path));
 
        // ---- Populate InteractionStore from the level's interactions array ----
        // This must happen before BuildEntity runs, since entities reference
        // interactions by ID and we need the store to be populated to resolve them.
        var interactionsArr = root!["interactions"]?.AsArray();
        if (interactionsArr != null)
        {
            foreach (var node in interactionsArr)
            {
                var def = DeserialiseInteractionDef(node!);
                // Only add if not already in the store (LoadRoom may be called
                // multiple times for different rooms in the same level).
                if (InteractionStore.FindById(def.Id) == null)
                    InteractionStore.Interactions.Add(def);
            }
        }
 
        // ---- Find the matching room ----
        var roomsArr = root["rooms"]!.AsArray();
        JsonNode roomNode = null;
        foreach (var r in roomsArr)
            if (r!["id"]!.GetValue<string>() == roomId) { roomNode = r; break; }
 
        if (roomNode == null)
        {
            Console.WriteLine($"[GameLoader] WARNING: room '{roomId}' not found.");
            return;
        }
 
        var entitiesArr = roomNode["entities"]?.AsArray();
        if (entitiesArr == null)
        {
            Console.WriteLine($"[GameLoader] Room '{roomId}' has no entities array — room will be empty.");
            return;
        }
 
        int count = 0;
        foreach (var node in entitiesArr)
        {
            var entity = BuildEntity(node!);
            if (entity != null) { room.Add(entity); count++; }
        }
 
        Console.WriteLine($"[GameLoader] Loaded {count} entity(s) into room '{roomId}'.");
    }
 
    // -----------------------------------------------------------------------
    // Interaction deserialisation
    // -----------------------------------------------------------------------
    private static InteractionDef DeserialiseInteractionDef(JsonNode node)
    {
        var def  = new InteractionDef();
        def.Id   = node["id"]?.GetValue<string>()   ?? def.Id;
        def.Name = node["name"]?.GetValue<string>() ?? "";
        def.Root = node["root"] != null
            ? DeserialiseInteractionNode(node["root"]!)
            : new InteractionNode();
        return def;
    }
 
    private static InteractionNode DeserialiseInteractionNode(JsonNode node)
    {
        var n  = new InteractionNode();
        n.Id   = node["id"]?.GetValue<string>() ?? n.Id;
        n.NavigateTarget = node["navigateTarget"]?.GetValue<string>();
 
        var linesArr = node["lines"]?.AsArray();
        if (linesArr != null)
            foreach (var line in linesArr)
                n.Lines.Add(line!.GetValue<string>());
 
        var choicesArr = node["choices"]?.AsArray();
        if (choicesArr != null)
        {
            foreach (var choiceNode in choicesArr)
            {
                var choice = new InteractionChoice
                {
                    Label          = choiceNode!["label"]?.GetValue<string>() ?? "?",
                    NavigateTarget = choiceNode["navigateTarget"]?.GetValue<string>()
                };
                if (choiceNode["next"] != null)
                    choice.Next = DeserialiseInteractionNode(choiceNode["next"]!);
                n.Choices.Add(choice);
            }
        }
 
        return n;
    }

    // -----------------------------------------------------------------------
    // Room colour helper
    // -----------------------------------------------------------------------

    public static (Color? wall, Color? floor, Color? ceil, string label)
        ReadRoomColors(string roomId)
    {
        var path = LevelData._levelRoomsOverride != null && File.Exists(LevelData._levelRoomsOverride)
            ? LevelData._levelRoomsOverride
            : DataPath("rooms.json");

        if (!File.Exists(path)) return (null, null, null, roomId);

        var root     = JsonNode.Parse(File.ReadAllText(path));
        var roomsArr = root!["rooms"]!.AsArray();

        foreach (var r in roomsArr)
        {
            if (r!["id"]!.GetValue<string>() != roomId) continue;
            return (
                ReadColor(r["wallColor"]),
                ReadColor(r["floorColor"]),
                ReadColor(r["ceilColor"]),
                r["label"]?.GetValue<string>() ?? roomId);
        }

        return (null, null, null, roomId);
    }

    // -----------------------------------------------------------------------
    // Entity builder  (delegates to EntityRegistry)
    // -----------------------------------------------------------------------

    private static Entity BuildEntity(JsonNode node)
        {
            var name = node["name"]?.GetValue<string>() ?? "";
    
            // Dispatch to the registered builder for this entity's type.
            var entity = EntityRegistry.Build(node, name);
            if (entity == null) return null;
    
            // ---- Interaction wiring ----
            // Entities reference an InteractionDef by ID. The InteractionStore is
            // populated when the level file is loaded (LoadMap / LoadLevel).
            // We look up the def here and assign it directly — no JSON parsing,
            // no delegate compilation.
            var interactionId = node["interactionId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(interactionId))
            {
                var interaction = InteractionStore.FindById(interactionId);
                if (interaction != null)
                    entity.Interaction = interaction;
                else
                    Console.WriteLine($"[GameLoader] Warning: entity '{name}' references unknown interactionId '{interactionId}'.");
            }
    
            return entity;
        }
 
    // -----------------------------------------------------------------------
    // JSON helpers
    // -----------------------------------------------------------------------

    private static Color? ReadColor(JsonNode node)
    {
        if (node == null) return null;
        var arr = node.AsArray();
        return new Color(arr[0]!.GetValue<int>(), arr[1]!.GetValue<int>(), arr[2]!.GetValue<int>());
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
}