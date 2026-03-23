//======== Core/LevelData.cs ========
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZebraBear.Scenes;

namespace ZebraBear.Core;

public class LevelInfo
{
    public string FileName;
    public string Name;
    public string Author;
    public string CreatedAt;
    public int RoomCount;
    public bool IsBuiltIn;
}

public static class LevelData
{
    public static string CurrentLevelName = "";

    private static string LevelsDir => Path.Combine(AppContext.BaseDirectory, "Data", "Levels");
    private static string DataDir => Path.Combine(AppContext.BaseDirectory, "Data");

    public static List<LevelInfo> ListLevels()
    {
        var levels = new List<LevelInfo>();
        if (File.Exists(Path.Combine(DataDir, "rooms.json")))
        {
            int rc = 0;
            try { var r = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "rooms.json"))); rc = r?["rooms"]?.AsArray().Count ?? 0; } catch { }
            levels.Add(new LevelInfo { FileName = "__builtin__", Name = "Main Story", Author = "Built-in", CreatedAt = "", RoomCount = rc, IsBuiltIn = true });
        }
        if (Directory.Exists(LevelsDir))
            foreach (var file in Directory.GetFiles(LevelsDir, "*.json"))
            {
                try
                {
                    var root = JsonNode.Parse(File.ReadAllText(file));
                    levels.Add(new LevelInfo
                    {
                        FileName = Path.GetFileName(file),
                        Name = root?["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(file),
                        Author = root?["author"]?.GetValue<string>() ?? "Unknown",
                        CreatedAt = root?["createdAt"]?.GetValue<string>() ?? "",
                        RoomCount = root?["rooms"]?.AsArray().Count ?? 0,
                        IsBuiltIn = false
                    });
                }
                catch (Exception ex) { Console.WriteLine($"[LevelData] Could not read '{file}': {ex.Message}"); }
            }
        return levels;
    }

    public static void LoadBuiltIn(Microsoft.Xna.Framework.Content.ContentManager content)
    {
        CurrentLevelName = "Main Story";
        _levelRoomsOverride = null;
        GameLoader.LoadCharacters(content);
        GameLoader.LoadMap();
    }

    public static void LoadLevel(string fileName, Microsoft.Xna.Framework.Content.ContentManager content)
    {
        var path = Path.Combine(LevelsDir, fileName);
        if (!File.Exists(path)) { Console.WriteLine($"[LevelData] Not found: {path}"); return; }
        var root = JsonNode.Parse(File.ReadAllText(path));
        CurrentLevelName = root?["name"]?.GetValue<string>() ?? fileName;

        CharacterData.Characters.Clear();
        var charsArr = root?["characters"]?.AsArray();
        if (charsArr != null)
            foreach (var node in charsArr)
            {
                var bio = new List<string>();
                var bioArr = node!["bio"]?.AsArray();
                if (bioArr != null) foreach (var b in bioArr) bio.Add(b!.GetValue<string>());
                Microsoft.Xna.Framework.Graphics.Texture2D tex = null;
                var portrait = node["portrait"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(portrait))
                    try { tex = content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(portrait); } catch { }
                CharacterData.Characters.Add(new CharacterProfile
                {
                    Id = node["id"]!.GetValue<string>(), Name = node["name"]!.GetValue<string>(),
                    Title = node["title"]!.GetValue<string>(), Bio = bio.ToArray(), Portrait = tex,
                    Met = node["met"]?.GetValue<bool>() ?? false
                });
            }

        MapData.Rooms.Clear(); MapData.Connections.Clear();
        var mapNode = root?["map"];
        if (mapNode != null)
        {
            MapData.CurrentRoomId = mapNode["startRoom"]?.GetValue<string>() ?? "";
            var roomsArr = mapNode["rooms"]?.AsArray();
            if (roomsArr != null)
                foreach (var node in roomsArr)
                {
                    var pos = node!["position"]!.AsArray(); var size = node["size"]!.AsArray();
                    MapData.Rooms.Add(new MapRoom
                    {
                        Id = node["id"]!.GetValue<string>(), Label = node["label"]!.GetValue<string>(),
                        SceneType = node["sceneType"]?.GetValue<string>() ?? "box",
                        Position = new Microsoft.Xna.Framework.Vector2(pos[0]!.GetValue<float>(), pos[1]!.GetValue<float>()),
                        Size = new Microsoft.Xna.Framework.Vector2(size[0]!.GetValue<float>(), size[1]!.GetValue<float>()),
                        Discovered = node["discovered"]?.GetValue<bool>() ?? false
                    });
                }
            var connArr = mapNode["connections"]?.AsArray();
            if (connArr != null)
                foreach (var node in connArr)
                    MapData.Connections.Add(new MapConnection { FromId = node!["fromId"]!.GetValue<string>(), ToId = node["toId"]!.GetValue<string>() });
        }

        var roomsNode = new JsonObject();
        roomsNode["rooms"] = root?["rooms"]?.DeepClone();
        var tempPath = Path.Combine(DataDir, "rooms.json.level_temp");
        File.WriteAllText(tempPath, roomsNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _levelRoomsOverride = tempPath;
    }

    internal static string _levelRoomsOverride = null;

    public static void ClearOverride()
    {
        if (_levelRoomsOverride != null && File.Exists(_levelRoomsOverride))
            try { File.Delete(_levelRoomsOverride); } catch { }
        _levelRoomsOverride = null;
    }

    // -----------------------------------------------------------------------
    // Publishing
    // -----------------------------------------------------------------------
    public static string Publish(string levelName, List<PublishRoom> rooms,
        List<PublishConnection> connections, int canvasX, int canvasY, int canvasW, int canvasH)
    {
        Directory.CreateDirectory(LevelsDir);
        var sanitized = SanitizeFileName(levelName);
        var fileName = sanitized + ".json";
        var path = Path.Combine(LevelsDir, fileName);

        var root = new JsonObject();
        root["name"] = levelName;
        root["author"] = "Player";
        root["createdAt"] = DateTime.Now.ToString("o");

        // Map
        var map = new JsonObject();
        map["startRoom"] = rooms.Count > 0 ? rooms[0].Id : "";
        var mapRooms = new JsonArray();
        foreach (var r in rooms)
        {
            var rn = new JsonObject();
            rn["id"] = r.Id; rn["label"] = r.Label; rn["sceneType"] = "box";
            rn["position"] = new JsonArray((r.CanvasX - canvasX) / (float)canvasW, (r.CanvasY - canvasY) / (float)canvasH);
            rn["size"] = new JsonArray(r.CanvasW / (float)canvasW, r.CanvasH / (float)canvasH);
            rn["discovered"] = false;
            mapRooms.Add(rn);
        }
        map["rooms"] = mapRooms;
        var mapConns = new JsonArray();
        foreach (var c in connections) { var cn = new JsonObject(); cn["fromId"] = c.FromId; cn["toId"] = c.ToId; mapConns.Add(cn); }
        map["connections"] = mapConns;
        root["map"] = map;

        // Rooms with entities
        var roomsArr = new JsonArray();
        foreach (var r in rooms)
        {
            var rn = new JsonObject();
            rn["id"] = r.Id; rn["label"] = r.Label;
            rn["wallColor"] = new JsonArray(30, 28, 45);
            rn["floorColor"] = new JsonArray(20, 18, 30);
            rn["ceilColor"] = new JsonArray(12, 10, 20);

            var entities = new JsonArray();

            // Objects
            foreach (var obj in r.Objects)
            {
                var entity = new JsonObject();
                entity["type"] = obj.Type;
                entity["name"] = obj.Type;
                float wx = (obj.Col - 10) * 1.4f, wz = (obj.Row - 10) * 1.4f;

                if (obj.Type == "orientedBox" || obj.Type == "shelf")
                { entity["centre"] = new JsonArray(wx, 0f, wz); entity["width"] = 2f; entity["height"] = 1.5f; entity["depth"] = 0.2f; entity["normal"] = "north"; entity["tint"] = new JsonArray(100, 80, 60); }
                else
                { entity["position"] = new JsonArray(wx, -3f, wz); entity["tint"] = new JsonArray(100, 80, 60); }

                var interaction = InteractionStore.FindById(obj.InteractionId);
                if (interaction != null)
                    entity["dialogueTree"] = BuildDialogueTreeJson(interaction.Root, rooms);

                entities.Add(entity);
            }

            // Characters
            foreach (var ch in r.Characters)
            {
                var entity = new JsonObject();
                entity["type"] = "billboard";
                entity["name"] = ch.Name;
                float wx = (ch.Col - 10) * 1.4f, wz = (ch.Row - 10) * 1.4f;
                entity["position"] = new JsonArray(wx, -0.75f, wz);
                entity["width"] = 2.2f; entity["height"] = 4.5f;
                entity["tint"] = new JsonArray(ch.TintR, ch.TintG, ch.TintB);
                entity["isCharacter"] = true;

                var interaction = InteractionStore.FindById(ch.InteractionId);
                if (interaction != null)
                    entity["dialogueTree"] = BuildDialogueTreeJson(interaction.Root, rooms);

                entities.Add(entity);
            }

            rn["entities"] = entities;
            roomsArr.Add(rn);
        }
        root["rooms"] = roomsArr;
        root["characters"] = new JsonArray();

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[LevelData] Published '{levelName}' -> {path}");
        return fileName;
    }

    private static JsonObject BuildDialogueTreeJson(InteractionNode node, List<PublishRoom> rooms)
    {
        var tree = new JsonObject();
        tree["id"] = node.Id;

        var lines = new JsonArray();
        foreach (var line in node.Lines) lines.Add(line);

        // If leaf node with navigate, add prompt
        if (!string.IsNullOrEmpty(node.NavigateTarget) && node.IsLeaf)
        {
            string targetLabel = node.NavigateTarget;
            foreach (var r in rooms) if (r.Id == node.NavigateTarget) { targetLabel = r.Label; break; }
            lines.Add($"Go to {targetLabel}?");
        }
        tree["lines"] = lines;

        if (node.Choices.Count > 0)
        {
            var choices = new JsonArray();
            foreach (var choice in node.Choices)
            {
                var cn = new JsonObject();
                cn["label"] = choice.Label;

                if (!string.IsNullOrEmpty(choice.NavigateTarget))
                {
                    var nav = new JsonObject();
                    nav["type"] = "navigate";
                    nav["target"] = choice.NavigateTarget;
                    nav["choiceIndex"] = 0;
                    cn["onInteract"] = nav;
                }

                if (choice.Next != null)
                    cn["next"] = BuildDialogueTreeJson(choice.Next, rooms);

                choices.Add(cn);
            }
            tree["choices"] = choices;
        }
        else if (!string.IsNullOrEmpty(node.NavigateTarget))
        {
            // Auto Yes/No for leaf navigate
            var choices = new JsonArray();
            var yes = new JsonObject(); yes["label"] = "Yes";
            var nav = new JsonObject(); nav["type"] = "navigate"; nav["target"] = node.NavigateTarget; nav["choiceIndex"] = 0;
            yes["onInteract"] = nav; choices.Add(yes);
            var no = new JsonObject(); no["label"] = "No"; choices.Add(no);
            tree["choices"] = choices;
        }

        return tree;
    }

    private static string SanitizeFileName(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        { if (char.IsLetterOrDigit(c)) sb.Append(c); else if (c == ' ' || c == '-') sb.Append('_'); }
        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "untitled" : result;
    }
}

public class PublishRoom
{
    public string Id;
    public string Label;
    public float CanvasX, CanvasY, CanvasW, CanvasH;
    public List<PublishObject> Objects = new();
    public List<PublishCharacter> Characters = new();
}

public class PublishObject
{
    public string Type;
    public int Col, Row;
    public string InteractionId;
}

public class PublishCharacter
{
    public string Name;
    public int Col, Row;
    public int TintR, TintG, TintB;
    public string InteractionId;
}

public class PublishConnection
{
    public string FromId;
    public string ToId;
}