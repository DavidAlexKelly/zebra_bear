//======== Scenes/LevelEditorScene.cs ========
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

public class LevelEditorScene : IScene
{
    public class EditorRoom
    {
        public string Id;
        public string Label;
        public Vector2 Position;
        public RoomShapeData Shape = new();
        public const int W = 120;
        public const int H = 70;
        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, W, H);
        public Vector2 Centre => new Vector2(Position.X + W / 2f, Position.Y + H / 2f);
    }

    private class EditorConnection
    {
        public EditorRoom A;
        public EditorRoom B;
    }

    private readonly Game _game;
    private readonly SpriteBatch _sb;
    private readonly RoomGeometryEditorScene _geoEditor;
    private readonly InteractionEditorScene _interactionEditor;

    private readonly VStack _sideStack = new() { Padding = 10, Spacing = 6 };
    private readonly VStack _publishStack = new() { Padding = 20, Spacing = 10 };
    private readonly VStack _loadStack = new() { Padding = 20, Spacing = 8 };
    private readonly VStack _loadListStack = new() { Padding = 0, Spacing = 4 };

    private int SideW => 210;
    private int CanvasX => SideW + 10;
    private int CanvasY => 60;
    private int CanvasW => _game.GraphicsDevice.Viewport.Width - SideW - 20;
    private int CanvasH => _game.GraphicsDevice.Viewport.Height - 100;

    private readonly List<EditorRoom> _rooms = new();
    private readonly List<EditorConnection> _connections = new();
    private int _nextId = 1;

    private EditorRoom _dragging;
    private Vector2 _dragOffset;
    private Vector2 _pressPos;
    private bool _didDrag;
    private const float DragThreshold = 6f;

    private EditorRoom _selected;
    private EditorRoom _connectFrom;
    private bool _connectMode;

    private EditorRoom _contextRoom;
    private Rectangle _popupBounds;
    private Rectangle _popupEditBtn;
    private Rectangle _popupConnBtn;

    private Rectangle _addRoomRect, _publishRect, _loadRect, _backRect;
    private Rectangle _addInteractionRect;
    private List<Rectangle> _interactionEntryRects = new();

    private bool _showPublishDialog = false;
    private string _publishName = "";
    private string _publishStatus = "";
    private double _publishStatusTimer = 0;
    private Rectangle _publishDialogRect;
    private Rectangle _publishNameRect;
    private Rectangle _publishConfirmRect;
    private Rectangle _publishCancelRect;
    private bool _editingPublishName = false;

    private bool _showLoadDialog = false;
    private List<LevelInfo> _loadLevels = new();
    private int _loadSelectedIndex = 0;
    private Rectangle _loadDialogRect;
    private Rectangle _loadConfirmRect;
    private Rectangle _loadCancelRect;
    private List<Rectangle> _loadEntryRects = new();

    private string _currentLevelFileName = null;
    private string _currentLevelName = null;

    private Keys[] _prevPressedKeys = Array.Empty<Keys>();
    private string _status = "Click 'Add Room' to start, or 'Load Level' to edit an existing level.";
    private double _statusTimer = 0;
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;

    public LevelEditorScene(Game game, SpriteBatch spriteBatch,
        RoomGeometryEditorScene geoEditor, InteractionEditorScene interactionEditor)
    {
        _game = game;
        _sb = spriteBatch;
        _geoEditor = geoEditor;
        _interactionEditor = interactionEditor;
    }

    public void Load() { }
    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        _showPublishDialog = false;
        _showLoadDialog = false;
    }
    public void OnExit() { _game.IsMouseVisible = false; }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime)
    {
        double dt = gameTime.ElapsedGameTime.TotalSeconds;
        var mouse = Mouse.GetState();
        var keys = Keyboard.GetState();
        var mp = mouse.Position.ToVector2();

        if (_statusTimer > 0) _statusTimer -= dt;
        if (_publishStatusTimer > 0) _publishStatusTimer -= dt;

        bool clicked = mouse.LeftButton == ButtonState.Released &&
                       _prevMouse.LeftButton == ButtonState.Pressed;

        if (_showPublishDialog) { UpdatePublishDialog(keys, clicked, mp); SaveInput(mouse, keys); return; }
        if (_showLoadDialog) { UpdateLoadDialog(keys, clicked, mp); SaveInput(mouse, keys); return; }

        bool lPressed = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        bool lReleased = mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed;
        if (lPressed) _pressPos = mp;
        bool lClick = lReleased && Vector2.Distance(mp, _pressPos) < DragThreshold;

        if (IsPressed(keys, _prevKeys, Keys.Delete) && _selected != null) RemoveRoom(_selected);

        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            if (_contextRoom != null) { _contextRoom = null; goto Done; }
            if (_connectMode) { _connectFrom = null; _connectMode = false; goto Done; }
            NavigationBus.RequestNavigate("MainMenu"); goto Done;
        }

        if (_contextRoom != null)
        {
            if (lClick)
            {
                if (_popupEditBtn.Contains(mp)) { var r = _contextRoom; _contextRoom = null; OpenGeoEditor(r); }
                else if (_popupConnBtn.Contains(mp)) { StartConnect(_contextRoom); _contextRoom = null; }
                else if (!_popupBounds.Contains(mp)) { _contextRoom = null; }
            }
            goto Done;
        }

        if (lClick && _addRoomRect.Contains(mp)) { AddRoom(); goto Done; }
        if (lClick && _loadRect.Contains(mp)) { OpenLoadDialog(); goto Done; }
        if (lClick && _publishRect.Contains(mp)) { OpenPublishDialog(); goto Done; }
        if (lClick && _backRect.Contains(mp)) { NavigationBus.RequestNavigate("MainMenu"); goto Done; }

        // Interaction clicks
        if (lClick && _addInteractionRect.Contains(mp))
        {
            var newInt = new InteractionDef { Name = $"Interaction {InteractionStore.Interactions.Count + 1}" };
            InteractionStore.Interactions.Add(newInt);
            OpenInteractionEditor(newInt);
            goto Done;
        }
        for (int i = 0; i < _interactionEntryRects.Count; i++)
        {
            if (lClick && i < InteractionStore.Interactions.Count && _interactionEntryRects[i].Contains(mp))
            {
                OpenInteractionEditor(InteractionStore.Interactions[i]);
                goto Done;
            }
        }

        bool inCanvas = new Rectangle(CanvasX, CanvasY, CanvasW, CanvasH).Contains(mp);

        if (_dragging != null)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                if (Vector2.Distance(mp, _pressPos) >= DragThreshold)
                { _dragging.Position = mp - _dragOffset; ClampToCanvas(_dragging); _didDrag = true; }
            }
            else
            {
                if (!_didDrag)
                {
                    if (_connectMode && _connectFrom != null && _connectFrom != _dragging)
                    { TryAddConnection(_connectFrom, _dragging); _connectFrom = null; _connectMode = false; }
                    else if (_connectMode && _connectFrom == _dragging)
                    { _connectFrom = null; _connectMode = false; SetStatus("Connection cancelled."); }
                    else OpenContextMenu(_dragging, mouse.Position);
                }
                _dragging = null; _didDrag = false;
            }
            goto Done;
        }

        if (lPressed && inCanvas)
        {
            var hit = HitTest(mp);
            if (hit != null) { _selected = hit; _dragging = hit; _dragOffset = mp - hit.Position; _didDrag = false; }
            else
            {
                if (_connectMode) { _connectFrom = null; _connectMode = false; SetStatus("Connection cancelled."); }
                _selected = null; _contextRoom = null;
            }
        }

        Done:
        SaveInput(mouse, keys);
    }

    private void SaveInput(MouseState mouse, KeyboardState keys)
    {
        _prevMouse = mouse; _prevKeys = keys;
        _prevPressedKeys = keys.GetPressedKeys();
    }

    // -----------------------------------------------------------------------
    // Interaction editor
    // -----------------------------------------------------------------------
    private void OpenInteractionEditor(InteractionDef interaction)
    {
        var context = new RoomEditorContext { CurrentRoomId = "" };
        var seen = new HashSet<string>();
        foreach (var r in _rooms)
            if (seen.Add(r.Id)) context.ConnectedRooms.Add((r.Id, r.Label));

        _interactionEditor.OpenInteraction(interaction, context, () => { });
        NavigationBus.RequestNavigate("InteractionEditor");
    }

    // -----------------------------------------------------------------------
    // Load dialog
    // -----------------------------------------------------------------------
    private void OpenLoadDialog()
    {
        _loadLevels = LevelData.ListLevels();
        _loadSelectedIndex = 0;
        _showLoadDialog = true;
    }

    private void UpdateLoadDialog(KeyboardState keys, bool clicked, Vector2 mp)
    {
        if (IsPressed(keys, _prevKeys, Keys.Escape)) { _showLoadDialog = false; return; }
        if (_loadLevels.Count > 0)
        {
            if (IsPressed(keys, _prevKeys, Keys.Down) || IsPressed(keys, _prevKeys, Keys.S))
                _loadSelectedIndex = (_loadSelectedIndex + 1) % _loadLevels.Count;
            if (IsPressed(keys, _prevKeys, Keys.Up) || IsPressed(keys, _prevKeys, Keys.W))
                _loadSelectedIndex = (_loadSelectedIndex - 1 + _loadLevels.Count) % _loadLevels.Count;
            if (IsPressed(keys, _prevKeys, Keys.Enter)) DoLoadLevel();
        }
        if (clicked)
        {
            for (int i = 0; i < _loadEntryRects.Count; i++)
                if (_loadEntryRects[i].Contains(mp)) _loadSelectedIndex = i;
            if (_loadConfirmRect.Contains(mp) && _loadLevels.Count > 0) DoLoadLevel();
            if (_loadCancelRect.Contains(mp)) _showLoadDialog = false;
        }
    }

    private void DoLoadLevel()
    {
        if (_loadSelectedIndex < 0 || _loadSelectedIndex >= _loadLevels.Count) return;
        var level = _loadLevels[_loadSelectedIndex];

        _rooms.Clear(); _connections.Clear(); _selected = null;
        _connectFrom = null; _connectMode = false; _contextRoom = null; _nextId = 1;
        InteractionStore.Clear();

        if (level.IsBuiltIn) LoadBuiltInIntoEditor();
        else LoadLevelFileIntoEditor(level.FileName);

        _currentLevelFileName = level.IsBuiltIn ? null : level.FileName;
        _currentLevelName = level.Name;
        _showLoadDialog = false;
        SetStatus($"Loaded '{level.Name}' ({_rooms.Count} rooms, {InteractionStore.Interactions.Count} interactions).");
    }

    private void LoadBuiltInIntoEditor()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        var mapPath = Path.Combine(dataDir, "map.json");
        if (!File.Exists(mapPath)) return;

        var mapRoot = JsonNode.Parse(File.ReadAllText(mapPath));
        var mapRooms = mapRoot?["rooms"]?.AsArray();
        var mapConns = mapRoot?["connections"]?.AsArray();

        if (mapRooms != null)
            foreach (var node in mapRooms)
            {
                var id = node!["id"]!.GetValue<string>();
                var label = node["label"]!.GetValue<string>();
                var pos = node["position"]!.AsArray();
                var room = new EditorRoom
                {
                    Id = id, Label = label,
                    Position = new Vector2(CanvasX + pos[0]!.GetValue<float>() * CanvasW,
                        CanvasY + pos[1]!.GetValue<float>() * CanvasH)
                };
                room.Shape.FillDefault();
                _rooms.Add(room);
                TrackNextId(id);
            }

        if (mapConns != null) LoadConnectionsFromJson(mapConns);

        var roomsPath = Path.Combine(dataDir, "rooms.json");
        if (File.Exists(roomsPath))
        {
            var root = JsonNode.Parse(File.ReadAllText(roomsPath));
            var arr = root?["rooms"]?.AsArray();
            if (arr != null) LoadRoomEntitiesFromArray(arr);
        }
    }

    private void LoadLevelFileIntoEditor(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Levels", fileName);
        if (!File.Exists(path)) return;
        var root = JsonNode.Parse(File.ReadAllText(path));

        var mapNode = root?["map"];
        var mapRooms = mapNode?["rooms"]?.AsArray();
        var mapConns = mapNode?["connections"]?.AsArray();

        if (mapRooms != null)
            foreach (var node in mapRooms)
            {
                var id = node!["id"]!.GetValue<string>();
                var label = node["label"]!.GetValue<string>();
                var pos = node["position"]!.AsArray();
                var room = new EditorRoom
                {
                    Id = id, Label = label,
                    Position = new Vector2(CanvasX + pos[0]!.GetValue<float>() * CanvasW,
                        CanvasY + pos[1]!.GetValue<float>() * CanvasH)
                };
                room.Shape.FillDefault();
                _rooms.Add(room);
                TrackNextId(id);
            }

        if (mapConns != null) LoadConnectionsFromJson(mapConns);

        var roomsArr = root?["rooms"]?.AsArray();
        if (roomsArr != null) LoadRoomEntitiesFromArray(roomsArr);
    }

    private void TrackNextId(string id)
    {
        if (id.StartsWith("Room") && int.TryParse(id.AsSpan(4), out int num))
            _nextId = Math.Max(_nextId, num + 1);
    }

    private void LoadConnectionsFromJson(JsonArray conns)
    {
        foreach (var node in conns)
        {
            var fromId = node!["fromId"]!.GetValue<string>();
            var toId = node["toId"]!.GetValue<string>();
            var fromRoom = _rooms.Find(r => r.Id == fromId);
            var toRoom = _rooms.Find(r => r.Id == toId);
            if (fromRoom != null && toRoom != null)
                _connections.Add(new EditorConnection { A = fromRoom, B = toRoom });
        }
    }

    private void LoadRoomEntitiesFromArray(JsonArray roomsArr)
    {
        foreach (var roomNode in roomsArr)
        {
            var roomId = roomNode!["id"]!.GetValue<string>();
            var editorRoom = _rooms.Find(r => r.Id == roomId);
            if (editorRoom == null) continue;

            var entities = roomNode["entities"]?.AsArray();
            if (entities == null) continue;

            foreach (var entity in entities)
            {
                var type = entity!["type"]?.GetValue<string>() ?? "";
                var name = entity["name"]?.GetValue<string>() ?? type;

                // Extract dialogue and nav info for creating an interaction
                var dialogueLines = new List<string>();
                string navTarget = null;
                bool isDoor = false;

                // Flat dialogue
                var dialogueArr = entity["dialogue"]?.AsArray();
                if (dialogueArr != null)
                    foreach (var line in dialogueArr)
                        dialogueLines.Add(line!.GetValue<string>());

                // Dialogue tree
                var dialogueTree = entity["dialogueTree"];
                InteractionNode importedRoot = null;
                if (dialogueTree != null)
                    importedRoot = ImportInteractionNode(dialogueTree, ref navTarget, ref isDoor);

                // Simple onInteract navigate
                var onInteract = entity["onInteract"];
                if (onInteract?["type"]?.GetValue<string>() == "navigate")
                {
                    navTarget = onInteract["target"]?.GetValue<string>();
                    isDoor = true;
                }

                // Skip pure auto-generated doors
                if (isDoor && importedRoot == null && dialogueLines.Count == 0)
                {
                    if (name.Contains("Door") || name.Contains("door") || name.StartsWith("Door to"))
                        continue;
                }

                // Map position to grid
                int col = 10, row = 10;
                var posNode = entity["position"];
                var centreNode = entity["centre"];
                if (posNode != null)
                {
                    var arr = posNode.AsArray();
                    col = (int)(arr[0]!.GetValue<float>() / 1.4f + 10);
                    row = (int)(arr[2]!.GetValue<float>() / 1.4f + 10);
                }
                else if (centreNode != null)
                {
                    var arr = centreNode.AsArray();
                    col = (int)(arr[0]!.GetValue<float>() / 1.4f + 10);
                    row = (int)(arr[2]!.GetValue<float>() / 1.4f + 10);
                }
                col = Math.Clamp(col, 0, RoomShapeData.MaxSize - 1);
                row = Math.Clamp(row, 0, RoomShapeData.MaxSize - 1);
                editorRoom.Shape.Fill(col, row);

                // Create interaction if entity has dialogue or nav
                string interactionId = null;
                bool hasContent = dialogueLines.Count > 0 || importedRoot != null;

                if (hasContent || !string.IsNullOrEmpty(navTarget))
                {
                    var interaction = new InteractionDef
                    {
                        Name = $"{name} ({roomId})"
                    };

                    if (importedRoot != null)
                    {
                        interaction.Root = importedRoot;
                    }
                    else if (dialogueLines.Count > 0)
                    {
                        interaction.Root.Lines.AddRange(dialogueLines);
                        if (!string.IsNullOrEmpty(navTarget))
                            interaction.Root.NavigateTarget = navTarget;
                    }
                    else if (!string.IsNullOrEmpty(navTarget))
                    {
                        interaction.Root.NavigateTarget = navTarget;
                    }

                    InteractionStore.Interactions.Add(interaction);
                    interactionId = interaction.Id;
                }

                // Create placed entity
                if (type == "billboard")
                {
                    var ch = new PlacedCharacter
                    {
                        Name = name, Col = col, Row = row,
                        InteractionId = interactionId
                    };
                    var tintNode = entity["tint"]?.AsArray();
                    if (tintNode != null && tintNode.Count >= 3)
                    {
                        ch.TintR = tintNode[0]!.GetValue<int>();
                        ch.TintG = tintNode[1]!.GetValue<int>();
                        ch.TintB = tintNode[2]!.GetValue<int>();
                    }
                    editorRoom.Shape.AddCharacter(ch);
                }
                else
                {
                    editorRoom.Shape.AddObject(new PlacedObject
                    {
                        Type = type, Col = col, Row = row,
                        InteractionId = interactionId
                    });
                }
            }
        }
    }

    /// <summary>
    /// Recursively import a dialogueTree JSON node into an InteractionNode.
    /// </summary>
    private InteractionNode ImportInteractionNode(JsonNode json, ref string navTarget, ref bool isDoor)
    {
        var interNode = new InteractionNode();
        interNode.Id = json["id"]?.GetValue<string>() ?? interNode.Id;

        var linesArr = json["lines"]?.AsArray();
        if (linesArr != null)
        {
            foreach (var line in linesArr)
            {
                string text = line is JsonValue v ? v.GetValue<string>()
                    : line?["text"]?.GetValue<string>() ?? "";
                if (!text.StartsWith("Go to "))
                    interNode.Lines.Add(text);
            }
        }

        var choicesArr = json["choices"]?.AsArray();
        if (choicesArr != null)
        {
            foreach (var choiceJson in choicesArr)
            {
                var choice = new InteractionChoice
                {
                    Label = choiceJson!["label"]?.GetValue<string>() ?? "?"
                };

                var interact = choiceJson["onInteract"];
                if (interact?["type"]?.GetValue<string>() == "navigate")
                {
                    choice.NavigateTarget = interact["target"]?.GetValue<string>();
                    navTarget = choice.NavigateTarget;
                    isDoor = true;
                }

                var nextJson = choiceJson["next"];
                if (nextJson != null)
                    choice.Next = ImportInteractionNode(nextJson, ref navTarget, ref isDoor);

                interNode.Choices.Add(choice);
            }
        }

        return interNode;
    }
    // -----------------------------------------------------------------------
    // Publish
    // -----------------------------------------------------------------------
    private void OpenPublishDialog()
    {
        if (_rooms.Count == 0) { SetStatus("Add at least one room before publishing."); return; }
        _showPublishDialog = true;
        _publishName = _currentLevelName ?? "";
        _editingPublishName = true;
        _publishStatus = "";
    }

    private void UpdatePublishDialog(KeyboardState keys, bool clicked, Vector2 mp)
    {
        if (IsPressed(keys, _prevKeys, Keys.Escape)) { _showPublishDialog = false; return; }
        if (_editingPublishName) HandlePublishTextInput(keys);
        if (clicked)
        {
            if (_publishNameRect.Contains(mp)) _editingPublishName = true;
            else _editingPublishName = false;
            if (_publishConfirmRect.Contains(mp)) DoPublish();
            if (_publishCancelRect.Contains(mp)) _showPublishDialog = false;
        }
        if (IsPressed(keys, _prevKeys, Keys.Enter) && _publishName.Length > 0) DoPublish();
    }

    private void DoPublish()
    {
        if (string.IsNullOrWhiteSpace(_publishName))
        { _publishStatus = "Please enter a level name."; _publishStatusTimer = 3; return; }

        var rooms = new List<PublishRoom>();
        foreach (var r in _rooms)
        {
            var pr = new PublishRoom
            {
                Id = r.Id, Label = r.Label,
                CanvasX = r.Position.X, CanvasY = r.Position.Y,
                CanvasW = EditorRoom.W, CanvasH = EditorRoom.H
            };
            foreach (var obj in r.Shape.Objects)
                pr.Objects.Add(new PublishObject
                {
                    Type = obj.Type, Col = obj.Col, Row = obj.Row,
                    InteractionId = obj.InteractionId
                });
            foreach (var ch in r.Shape.Characters)
                pr.Characters.Add(new PublishCharacter
                {
                    Name = ch.Name, Col = ch.Col, Row = ch.Row,
                    TintR = ch.TintR, TintG = ch.TintG, TintB = ch.TintB,
                    InteractionId = ch.InteractionId
                });
            rooms.Add(pr);
        }

        var connections = new List<PublishConnection>();
        foreach (var c in _connections)
            connections.Add(new PublishConnection { FromId = c.A.Id, ToId = c.B.Id });

        var fileName = LevelData.Publish(_publishName, rooms, connections,
            CanvasX, CanvasY, CanvasW, CanvasH);

        _currentLevelFileName = fileName;
        _currentLevelName = _publishName;
        _showPublishDialog = false;
        SetStatus($"Published '{_publishName}' as {fileName}");
    }

    private void HandlePublishTextInput(KeyboardState keys)
    {
        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back) { if (_publishName.Length > 0) _publishName = _publishName[..^1]; continue; }
            if (key == Keys.Enter) continue;
            char? c = KeyToChar(key, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
            if (c.HasValue && _publishName.Length < 40) _publishName += c.Value;
        }
    }

    // -----------------------------------------------------------------------
    // Open geometry editor
    // -----------------------------------------------------------------------
    private void OpenGeoEditor(EditorRoom room)
    {
        var context = new RoomEditorContext { CurrentRoomId = room.Id };
        foreach (var conn in _connections)
        {
            if (conn.A.Id == room.Id) context.ConnectedRooms.Add((conn.B.Id, conn.B.Label));
            else if (conn.B.Id == room.Id) context.ConnectedRooms.Add((conn.A.Id, conn.A.Label));
        }
        _geoEditor.OpenRoom(room.Id, room.Label, room.Shape,
            _ => SetStatus($"Geometry saved for '{room.Label}'."), context);
        NavigationBus.RequestNavigate("RoomGeoEditor");
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------
    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        var mp = Mouse.GetState().Position.ToVector2();

        _sb.Begin();
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, vp.Width, vp.Height), LayoutDraw.BgDark);

        // ── LEFT SIDEBAR ──────────────────────────────────────────────────
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, SideW, vp.Height), LayoutDraw.PanelBg);
        LayoutDraw.Rect(_sb, new Rectangle(SideW, 0, 1, vp.Height), LayoutDraw.Border);

        _sideStack.Begin(0, 0, SideW, vp.Height);

        var titleRect = _sideStack.Next(36);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "LEVEL EDITOR", titleRect, LayoutDraw.Accent, 6);

        if (!string.IsNullOrEmpty(_currentLevelName))
        {
            var nameRect = _sideStack.Next(20);
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, _currentLevelName, nameRect, new Color(120, 180, 220), 6);
        }

        _sideStack.Space(4);

        _addRoomRect = _sideStack.Next(44);
        LayoutDraw.Button(_sb, _addRoomRect, "Add Room", mp);
        _sideStack.Space(2);

        _loadRect = _sideStack.Next(44);
        bool hovLoad = _loadRect.Contains(mp);
        LayoutDraw.Rect(_sb, _loadRect, hovLoad ? new Color(50, 40, 20) : new Color(30, 25, 15));
        LayoutDraw.BorderRect(_sb, _loadRect, hovLoad ? new Color(200, 180, 80) : new Color(120, 100, 50));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Load Level", _loadRect, hovLoad ? Color.White : new Color(200, 180, 120));

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Pin bottom buttons
        _backRect = _sideStack.NextFromBottom(44);
        _publishRect = _sideStack.NextFromBottom(44);
        _sideStack.NextFromBottom(8);

        // Help
        var helpCol = new Color(55, 53, 78);
        var h4 = _sideStack.NextFromBottom(18);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Esc: back", h4, helpCol, 4);
        var h3 = _sideStack.NextFromBottom(18);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Del: remove", h3, helpCol, 4);
        var h2 = _sideStack.NextFromBottom(18);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Drag room: move", h2, helpCol, 4);
        var h1 = _sideStack.NextFromBottom(18);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click room: menu", h1, helpCol, 4);
        _sideStack.NextFromBottom(4);
        LayoutDraw.DividerLine(_sb, new Rectangle(_sideStack.X + 10, h1.Y - 6, SideW - 20, 1));

        // Rooms section
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(22), $"Rooms ({_rooms.Count}):");
        foreach (var r in _rooms)
        {
            if (_sideStack.IsFull) break;
            LayoutDraw.Selectable(_sb, _sideStack.Next(24), r.Label, r == _selected, mp);
        }

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Interactions section
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(22), $"Interactions ({InteractionStore.Interactions.Count}):");

        _addInteractionRect = _sideStack.Next(32);
        bool hovAddInt = _addInteractionRect.Contains(mp);
        LayoutDraw.Rect(_sb, _addInteractionRect, hovAddInt ? new Color(30, 20, 45) : new Color(20, 14, 32));
        LayoutDraw.BorderRect(_sb, _addInteractionRect, hovAddInt ? new Color(160, 100, 220) : new Color(90, 60, 130));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "+ New Interaction", _addInteractionRect,
            hovAddInt ? Color.White : new Color(160, 120, 210));

        _interactionEntryRects.Clear();
        foreach (var inter in InteractionStore.Interactions)
        {
            if (_sideStack.IsFull) break;
            var entryRect = _sideStack.Next(24);
            _interactionEntryRects.Add(entryRect);
            bool hov = entryRect.Contains(mp);
            if (hov) LayoutDraw.Rect(_sb, entryRect, new Color(24, 20, 36));
            LayoutDraw.Rect(_sb, new Rectangle(entryRect.X, entryRect.Y, 3, entryRect.Height), new Color(120, 80, 200));
            string intName = string.IsNullOrEmpty(inter.Name) ? "(unnamed)" : inter.Name;
            _sb.DrawString(Assets.MenuFont, intName, new Vector2(entryRect.X + 8, entryRect.Y + 2),
                hov ? Color.White : new Color(140, 130, 170));
        }

        // Bottom buttons
        bool hovPub = _publishRect.Contains(mp);
        LayoutDraw.Rect(_sb, _publishRect, hovPub ? new Color(40, 60, 80) : new Color(20, 35, 50));
        LayoutDraw.BorderRect(_sb, _publishRect, hovPub ? new Color(80, 160, 220) : new Color(50, 100, 140));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Publish Level", _publishRect, hovPub ? Color.White : new Color(120, 180, 220));
        LayoutDraw.Button(_sb, _backRect, "< Back", mp);

        // ── CANVAS ────────────────────────────────────────────────────────
        var canvasRect = new Rectangle(CanvasX, CanvasY, CanvasW, CanvasH);
        LayoutDraw.Rect(_sb, canvasRect, new Color(13, 12, 22));
        LayoutDraw.BorderRect(_sb, canvasRect, LayoutDraw.Border);
        DrawGrid();

        foreach (var conn in _connections) DrawLine(conn.A.Centre, conn.B.Centre, new Color(80, 75, 120));
        if (_connectMode && _connectFrom != null) DrawLine(_connectFrom.Centre, mp, new Color(232, 180, 60));
        foreach (var room in _rooms) DrawRoom(room, gameTime);

        _sb.DrawString(Assets.MenuFont, "MAP CANVAS", new Vector2(CanvasX + 8, CanvasY + 6), new Color(80, 75, 110));

        if (_contextRoom != null) DrawContextPopup(mp);

        LayoutDraw.Rect(_sb, new Rectangle(0, vp.Height - 28, vp.Width, 28), new Color(8, 7, 16));
        float sa = _statusTimer > 0 ? Math.Min(1f, (float)_statusTimer) : 0.4f;
        _sb.DrawString(Assets.MenuFont, _status, new Vector2(CanvasX + 4, vp.Height - 24), LayoutDraw.TextNormal * sa);

        if (_showPublishDialog) DrawPublishDialog(mp);
        if (_showLoadDialog) DrawLoadDialog(mp);

        _sb.End();
    }

    // -----------------------------------------------------------------------
    // Dialogs (Load / Publish / Context popup / Room drawing)
    // These are unchanged from previous version — included for completeness
    // -----------------------------------------------------------------------
    private void DrawLoadDialog(Vector2 mp)
    {
        var vp = _game.GraphicsDevice.Viewport;
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 180));
        int dw = 500, dh = 420;
        int dx = (vp.Width - dw) / 2, dy = (vp.Height - dh) / 2;
        _loadDialogRect = new Rectangle(dx, dy, dw, dh);
        LayoutDraw.Rect(_sb, _loadDialogRect, new Color(14, 12, 26, 250));
        LayoutDraw.BorderRect(_sb, _loadDialogRect, LayoutDraw.Border);
        LayoutDraw.AccentBar(_sb, _loadDialogRect);

        _loadStack.Begin(_loadDialogRect);
        LayoutDraw.TextLeft(_sb, Assets.TitleFont, "LOAD LEVEL", _loadStack.Next(36), LayoutDraw.Accent);
        LayoutDraw.DividerLine(_sb, _loadStack.Divider());
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"{_loadLevels.Count} level(s) available", _loadStack.Next(20), LayoutDraw.DimText);

        _loadCancelRect = _loadStack.NextFromBottom(40);
        _loadConfirmRect = _loadStack.NextFromBottom(44);
        _loadStack.NextFromBottom(8);

        if (_rooms.Count > 0)
        { LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Warning: replaces current content.", _loadStack.NextFromBottom(20), new Color(200, 140, 80)); _loadStack.NextFromBottom(4); }

        var listArea = _loadStack.ConsumeRemaining();
        LayoutDraw.Rect(_sb, listArea, new Color(10, 10, 20));
        LayoutDraw.BorderRect(_sb, listArea, LayoutDraw.Border);

        _loadListStack.Begin(new Rectangle(listArea.X + 4, listArea.Y + 4, listArea.Width - 8, listArea.Height - 8));
        _loadEntryRects.Clear();
        int maxVis = Math.Max(1, (listArea.Height - 8) / 58);
        int scroll = _loadSelectedIndex >= maxVis ? _loadSelectedIndex - maxVis + 1 : 0;

        for (int vi = 0; vi < Math.Min(_loadLevels.Count, maxVis); vi++)
        {
            int i = vi + scroll;
            if (i >= _loadLevels.Count || _loadListStack.IsFull) break;
            var level = _loadLevels[i];
            bool sel = i == _loadSelectedIndex;
            var er = _loadListStack.Next(52);
            _loadEntryRects.Add(er);
            LayoutDraw.Rect(_sb, er, sel ? new Color(28, 14, 32) : new Color(16, 14, 26));
            LayoutDraw.Rect(_sb, new Rectangle(er.X, er.Y, sel ? 3 : 1, er.Height), sel ? LayoutDraw.Accent : LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, level.Name, new Vector2(er.X + 12, er.Y + 6), sel ? Color.White : new Color(160, 155, 190));
            string sub = level.IsBuiltIn ? $"Built-in - {level.RoomCount} room(s)" : $"{level.Author} - {level.RoomCount} room(s)";
            _sb.DrawString(Assets.MenuFont, sub, new Vector2(er.X + 12, er.Y + 28), new Color(90, 85, 120));
        }

        if (_loadLevels.Count == 0) LayoutDraw.TextCentre(_sb, Assets.MenuFont, "No levels found.", listArea, LayoutDraw.DimText);

        bool hovConf = _loadConfirmRect.Contains(mp);
        if (_loadLevels.Count > 0)
        { LayoutDraw.Rect(_sb, _loadConfirmRect, hovConf ? new Color(50, 40, 20) : new Color(30, 25, 15)); LayoutDraw.BorderRect(_sb, _loadConfirmRect, hovConf ? new Color(200, 180, 80) : new Color(120, 100, 50)); LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Load Selected", _loadConfirmRect, hovConf ? Color.White : new Color(200, 180, 120)); }
        else { LayoutDraw.Rect(_sb, _loadConfirmRect, new Color(20, 18, 30)); LayoutDraw.BorderRect(_sb, _loadConfirmRect, LayoutDraw.Border); LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Load Selected", _loadConfirmRect, LayoutDraw.DimText); }
        LayoutDraw.Button(_sb, _loadCancelRect, "Cancel", mp);
    }

    private void DrawPublishDialog(Vector2 mp)
    {
        var vp = _game.GraphicsDevice.Viewport;
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 180));
        int dw = 420, dh = 260;
        int dx = (vp.Width - dw) / 2, dy = (vp.Height - dh) / 2;
        _publishDialogRect = new Rectangle(dx, dy, dw, dh);
        LayoutDraw.Rect(_sb, _publishDialogRect, new Color(14, 12, 26, 250));
        LayoutDraw.BorderRect(_sb, _publishDialogRect, LayoutDraw.Border);
        LayoutDraw.AccentBar(_sb, _publishDialogRect);

        _publishStack.Begin(_publishDialogRect);
        LayoutDraw.TextLeft(_sb, Assets.TitleFont, "PUBLISH LEVEL", _publishStack.Next(36), LayoutDraw.Accent);
        LayoutDraw.DividerLine(_sb, _publishStack.Divider());
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Level name:", _publishStack.Next(22), new Color(120, 115, 160));
        _publishNameRect = _publishStack.Next(40);
        LayoutDraw.TextArea(_sb, _publishNameRect, _publishName, _editingPublishName, mp, "Enter level name...");
        _publishStack.Space(4);
        if (!string.IsNullOrEmpty(_publishStatus) && _publishStatusTimer > 0)
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, _publishStatus, _publishStack.Next(22), LayoutDraw.Accent);
        else _publishStack.Space(22);
        _publishStack.Space(4);

        var btnArea = _publishStack.Next(44);
        int btnW = (btnArea.Width - 12) / 2;
        _publishCancelRect = new Rectangle(btnArea.X, btnArea.Y, btnW, btnArea.Height);
        LayoutDraw.Button(_sb, _publishCancelRect, "Cancel", mp);
        _publishConfirmRect = new Rectangle(btnArea.X + btnW + 12, btnArea.Y, btnW, btnArea.Height);
        bool canConf = _publishName.Length > 0;
        bool hovPubConf = _publishConfirmRect.Contains(mp);
        if (canConf) { LayoutDraw.Rect(_sb, _publishConfirmRect, hovPubConf ? new Color(40, 80, 40) : new Color(20, 50, 20)); LayoutDraw.BorderRect(_sb, _publishConfirmRect, hovPubConf ? new Color(80, 200, 80) : new Color(50, 120, 50)); LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Publish", _publishConfirmRect, hovPubConf ? Color.White : new Color(120, 200, 120)); }
        else { LayoutDraw.Rect(_sb, _publishConfirmRect, new Color(20, 18, 30)); LayoutDraw.BorderRect(_sb, _publishConfirmRect, LayoutDraw.Border); LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Publish", _publishConfirmRect, LayoutDraw.DimText); }
    }

    private void OpenContextMenu(EditorRoom room, Point mousePos)
    {
        _contextRoom = room; _selected = room;
        const int pw = 160, btnH = 44; int ph = btnH * 2 + 2;
        int px = mousePos.X + 6, py = mousePos.Y + 6;
        var vp = _game.GraphicsDevice.Viewport;
        if (px + pw > vp.Width) px = mousePos.X - pw - 6;
        if (py + ph > vp.Height) py = mousePos.Y - ph - 6;
        _popupBounds = new Rectangle(px, py, pw, ph);
        _popupEditBtn = new Rectangle(px, py, pw, btnH);
        _popupConnBtn = new Rectangle(px, py + btnH + 1, pw, btnH);
    }

    private void DrawContextPopup(Vector2 mp)
    {
        LayoutDraw.Rect(_sb, new Rectangle(_popupBounds.X + 4, _popupBounds.Y + 4, _popupBounds.Width, _popupBounds.Height), new Color(0, 0, 0, 140));
        LayoutDraw.Rect(_sb, _popupBounds, new Color(24, 22, 40));
        LayoutDraw.BorderRect(_sb, _popupBounds, LayoutDraw.Accent);
        var nameSz = Assets.MenuFont.MeasureString(_contextRoom.Label);
        _sb.DrawString(Assets.MenuFont, _contextRoom.Label, new Vector2(_popupBounds.X + 6, _popupBounds.Y - nameSz.Y - 4), new Color(130, 125, 165));
        DrawPopupButton(_popupEditBtn, "Edit Room", mp);
        LayoutDraw.Rect(_sb, new Rectangle(_popupBounds.X, _popupEditBtn.Bottom, _popupBounds.Width, 1), new Color(40, 38, 65));
        DrawPopupButton(_popupConnBtn, _connectMode && _connectFrom == _contextRoom ? "Cancel Connect" : "Connect", mp);
    }

    private void DrawPopupButton(Rectangle r, string label, Vector2 mp)
    {
        bool hov = r.Contains(mp);
        LayoutDraw.Rect(_sb, r, hov ? new Color(50, 18, 30) : new Color(24, 22, 40));
        LayoutDraw.AccentBar(_sb, r);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, label, r, hov ? Color.White : new Color(200, 195, 225), 14);
    }

    private void DrawRoom(EditorRoom room, GameTime gt)
    {
        bool isSel = room == _selected; bool isConn = room == _connectFrom;
        var bg = isSel ? new Color(40, 18, 48) : new Color(22, 20, 38);
        var border = isConn ? new Color(232, 180, 60) : isSel ? LayoutDraw.Accent : LayoutDraw.Border;
        var b = room.Bounds;
        LayoutDraw.Rect(_sb, b, bg); DrawMiniShape(room, b); LayoutDraw.BorderRect(_sb, b, border);
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, room.Label, new Rectangle(b.X, b.Y + 4, b.Width, 24), Color.White);
        var subSz = Assets.MenuFont.MeasureString($"{room.Shape.TileCount} tiles");
        _sb.DrawString(Assets.MenuFont, $"{room.Shape.TileCount} tiles", new Vector2(b.X + (b.Width - subSz.X) / 2f, b.Y + 28), new Color(80, 75, 110));
        if (isSel) { float pulse = (float)Math.Sin(gt.TotalGameTime.TotalSeconds * 4f) * 0.5f + 0.5f; LayoutDraw.Rect(_sb, new Rectangle(b.X, b.Y, b.Width, 3), Color.Lerp(LayoutDraw.Accent, new Color(255, 140, 160), pulse)); }
    }

    private void DrawMiniShape(EditorRoom room, Rectangle bounds)
    {
        const int maxS = RoomShapeData.MaxSize;
        float cw = (bounds.Width - 4f) / maxS, ch = (bounds.Height - 30f) / maxS;
        int bx = bounds.X + 2, by = bounds.Y + bounds.Height - (int)(maxS * ch) - 2;
        foreach (var (col, row) in room.Shape.Tiles)
            LayoutDraw.Rect(_sb, new Rectangle(bx + (int)(col * cw), by + (int)(row * ch), Math.Max(1, (int)cw), Math.Max(1, (int)ch)), new Color(55, 50, 85));
    }

    private void DrawGrid()
    {
        const int Step = 40; var col = new Color(20, 18, 32);
        for (int x = CanvasX; x < CanvasX + CanvasW; x += Step) LayoutDraw.Rect(_sb, new Rectangle(x, CanvasY, 1, CanvasH), col);
        for (int y = CanvasY; y < CanvasY + CanvasH; y += Step) LayoutDraw.Rect(_sb, new Rectangle(CanvasX, y, CanvasW, 1), col);
    }

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var dir = to - from; float len = dir.Length(); if (len < 1f) return; dir.Normalize();
        for (float t = 0; t < len; t += 3f) LayoutDraw.Rect(_sb, new Rectangle((int)(from.X + dir.X * t), (int)(from.Y + dir.Y * t), 2, 2), color);
    }

    // -----------------------------------------------------------------------
    // Room management
    // -----------------------------------------------------------------------
    private void AddRoom()
    {
        int col = _rooms.Count % 4, row = _rooms.Count / 4;
        var room = new EditorRoom { Id = $"Room{_nextId}", Label = $"Room {_nextId}",
            Position = new Vector2(CanvasX + 30 + col * (EditorRoom.W + 20), CanvasY + 30 + row * (EditorRoom.H + 30)) };
        room.Shape.FillDefault(); _nextId++; _rooms.Add(room); _selected = room;
        SetStatus($"Added '{room.Label}'.");
    }

    private void RemoveRoom(EditorRoom room)
    {
        _rooms.Remove(room); _connections.RemoveAll(c => c.A == room || c.B == room);
        if (_selected == room) _selected = null;
        if (_connectFrom == room) { _connectFrom = null; _connectMode = false; }
        if (_contextRoom == room) _contextRoom = null;
        SetStatus($"Removed '{room.Label}'.");
    }

    private void TryAddConnection(EditorRoom a, EditorRoom b)
    {
        foreach (var c in _connections) if ((c.A == a && c.B == b) || (c.A == b && c.B == a)) { SetStatus("Already connected."); return; }
        _connections.Add(new EditorConnection { A = a, B = b });
        SetStatus($"Connected '{a.Label}' <-> '{b.Label}'.");
    }

    private void StartConnect(EditorRoom room)
    { _connectFrom = room; _connectMode = true; _selected = room; SetStatus($"Connecting from '{room.Label}' - click another room."); }

    private EditorRoom HitTest(Vector2 point)
    { for (int i = _rooms.Count - 1; i >= 0; i--) if (_rooms[i].Bounds.Contains(point)) return _rooms[i]; return null; }

    private void ClampToCanvas(EditorRoom room) => room.Position = new Vector2(
        Math.Clamp(room.Position.X, CanvasX, CanvasX + CanvasW - EditorRoom.W),
        Math.Clamp(room.Position.Y, CanvasY, CanvasY + CanvasH - EditorRoom.H));

    private void SetStatus(string msg, double dur = 4.0) { _status = msg; _statusTimer = dur; }
    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key) => cur.IsKeyDown(key) && prev.IsKeyUp(key);

    private List<Keys> NewKeys(KeyboardState keys)
    {
        var result = new List<Keys>();
        foreach (var key in keys.GetPressedKeys()) { bool was = false; foreach (var p in _prevPressedKeys) if (p == key) { was = true; break; } if (!was) result.Add(key); }
        return result;
    }

    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z) { char c = (char)('a' + (key - Keys.A)); return shift ? char.ToUpper(c) : c; }
        if (key >= Keys.D0 && key <= Keys.D9) { if (!shift) return (char)('0' + (key - Keys.D0)); return (key - Keys.D0) switch { 1=>'!',2=>'@',3=>'#',4=>'$',5=>'%',6=>'^',7=>'&',8=>'*',9=>'(',0=>')',_=>null }; }
        return key switch { Keys.Space => ' ', Keys.OemMinus => shift ? '_' : '-', Keys.OemPeriod => '.', Keys.OemComma => ',', _ => null };
    }
}