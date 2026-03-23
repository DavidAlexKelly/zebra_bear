using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;

namespace ZebraBear.Scenes;

/// <summary>
/// In-engine level editor.
///
/// Click a room to open the "Edit Room" popup.
/// Drag a room (move mouse beyond 6px while held) to reposition it.
/// Click an empty canvas spot while a room is selected to start a connection,
/// then click another room to finish it.
/// </summary>
public class LevelEditorScene : IScene
{
    // -----------------------------------------------------------------------
    // Nested types
    // -----------------------------------------------------------------------

    public class EditorRoom
    {
        public string        Id;
        public string        Label;
        public Vector2       Position;
        public RoomShapeData Shape = new();

        public const int W = 120;
        public const int H = 70;

        public Rectangle Bounds => new Rectangle((int)Position.X, (int)Position.Y, W, H);
        public Vector2   Centre => new Vector2(Position.X + W / 2f, Position.Y + H / 2f);
    }

    private class EditorConnection
    {
        public EditorRoom A;
        public EditorRoom B;
    }

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly Game                     _game;
    private readonly SpriteBatch              _sb;
    private readonly RoomGeometryEditorScene  _geoEditor;

    private const int CanvasX = 220;
    private const int CanvasY = 60;
    private const int CanvasW = 1040;
    private const int CanvasH = 630;

    private readonly List<EditorRoom>       _rooms       = new();
    private readonly List<EditorConnection> _connections = new();
    private int _nextId = 1;

    // Drag state
    private EditorRoom _dragging;
    private Vector2    _dragOffset;
    private Vector2    _pressPos;       // where left button went down
    private bool       _didDrag;        // did we move beyond threshold this press?

    private const float DragThreshold = 6f;

    // Selection / connection
    private EditorRoom _selected;
    private EditorRoom _connectFrom;
    private bool       _connectMode;

    // Context popup
    private EditorRoom _contextRoom;
    private Rectangle  _popupBounds;
    private Rectangle  _popupEditBtn;
    private Rectangle  _popupConnBtn;

    // Sidebar buttons
    private Rectangle _addRoomBtn;
    private Rectangle _exportBtn;
    private Rectangle _backBtn;

    // Status bar
    private string _status      = "Click 'Add Room' to start. Click a room to open its menu.";
    private double _statusTimer = 0;

    // Input
    private MouseState    _prevMouse;
    private KeyboardState _prevKeys;

    private readonly Color _accent     = new Color(232, 0, 61);
    private readonly Color _bg         = new Color(10, 10, 18);
    private readonly Color _panel      = new Color(16, 14, 28);
    private readonly Color _border     = new Color(55, 50, 85);
    private readonly Color _roomBg     = new Color(22, 20, 38);
    private readonly Color _roomBgSel  = new Color(40, 18, 48);
    private readonly Color _connColor  = new Color(80, 75, 120);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public LevelEditorScene(Game game, SpriteBatch spriteBatch,
                            RoomGeometryEditorScene geoEditor)
    {
        _game      = game;
        _sb        = spriteBatch;
        _geoEditor = geoEditor;

        _addRoomBtn = new Rectangle(10, 80,  190, 44);
        _exportBtn  = new Rectangle(10, 580, 190, 44);
        _backBtn    = new Rectangle(10, 634, 190, 44);
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------

    public void Load() { }
    public void OnEnter()  { _game.IsMouseVisible = true; }
    public void OnExit()   { _game.IsMouseVisible = false; }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    public void Update(GameTime gameTime)
    {
        double dt    = gameTime.ElapsedGameTime.TotalSeconds;
        var    mouse = Mouse.GetState();
        var    keys  = Keyboard.GetState();
        var    mp    = mouse.Position.ToVector2();

        if (_statusTimer > 0) _statusTimer -= dt;

        // Track press position for click-vs-drag detection
        bool lPressed  = mouse.LeftButton == ButtonState.Pressed  &&
                         _prevMouse.LeftButton == ButtonState.Released;
        bool lReleased = mouse.LeftButton == ButtonState.Released &&
                         _prevMouse.LeftButton == ButtonState.Pressed;

        if (lPressed) _pressPos = mp;

        // A "click" = release within threshold of press point
        bool lClick = lReleased && Vector2.Distance(mp, _pressPos) < DragThreshold;

        // Keyboard
        if (IsPressed(keys, _prevKeys, Keys.Delete) && _selected != null)
            RemoveRoom(_selected);

        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            if (_contextRoom != null) { _contextRoom = null; goto Done; }
            if (_connectMode)         { _connectFrom = null; _connectMode = false; goto Done; }
            NavigationBus.RequestNavigate("MainMenu");
            goto Done;
        }

        // ── Popup intercepts everything ────────────────────────────────────
        if (_contextRoom != null)
        {
            if (lClick)
            {
                if      (_popupEditBtn.Contains(mp)) { var r = _contextRoom; _contextRoom = null; OpenGeoEditor(r); }
                else if (_popupConnBtn.Contains(mp)) { StartConnect(_contextRoom); _contextRoom = null; }
                else if (!_popupBounds.Contains(mp)) { _contextRoom = null; }
            }
            goto Done;
        }

        // ── Sidebar buttons ────────────────────────────────────────────────
        if (lClick && _addRoomBtn.Contains(mp)) { AddRoom();                                   goto Done; }
        if (lClick && _exportBtn.Contains(mp))  { ExportToJson();                              goto Done; }
        if (lClick && _backBtn.Contains(mp))    { NavigationBus.RequestNavigate("MainMenu");   goto Done; }

        // ── Canvas interaction ─────────────────────────────────────────────
        bool inCanvas = new Rectangle(CanvasX, CanvasY, CanvasW, CanvasH).Contains(mp);

        // Dragging in progress
        if (_dragging != null)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                if (Vector2.Distance(mp, _pressPos) >= DragThreshold)
                {
                    _dragging.Position = mp - _dragOffset;
                    ClampToCanvas(_dragging);
                    _didDrag = true;
                }
            }
            else // released
            {
                if (!_didDrag)
                {
                    // Clean click on a room — open popup
                    if (_connectMode && _connectFrom != null && _connectFrom != _dragging)
                    {
                        TryAddConnection(_connectFrom, _dragging);
                        _connectFrom = null; _connectMode = false;
                    }
                    else if (_connectMode && _connectFrom == _dragging)
                    {
                        _connectFrom = null; _connectMode = false;
                        SetStatus("Connection cancelled.");
                    }
                    else
                    {
                        OpenContextMenu(_dragging, mouse.Position);
                    }
                }
                _dragging = null;
                _didDrag  = false;
            }
            goto Done;
        }

        // Start press on canvas
        if (lPressed && inCanvas)
        {
            var hit = HitTest(mp);
            if (hit != null)
            {
                _selected   = hit;
                _dragging   = hit;
                _dragOffset = mp - hit.Position;
                _didDrag    = false;
            }
            else
            {
                // Clicked empty canvas
                if (_connectMode)
                {
                    // Cancel connection
                    _connectFrom = null; _connectMode = false;
                    SetStatus("Connection cancelled.");
                }
                _selected    = null;
                _contextRoom = null;
            }
        }

        Done:
        _prevMouse = mouse;
        _prevKeys  = keys;
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public void Draw(GameTime gameTime)
    {
        _sb.Begin();
        var vp = _game.GraphicsDevice.Viewport;

        DrawRect(new Rectangle(0, 0, vp.Width, vp.Height), _bg);

        // Sidebar
        DrawRect(new Rectangle(0, 0, 210, vp.Height), _panel);
        DrawRect(new Rectangle(210, 0, 1, vp.Height), _border);
        _sb.DrawString(Assets.MenuFont, "LEVEL EDITOR", new Vector2(10, 16), _accent);
        DrawButton(_addRoomBtn, "Add Room");
        DrawButton(_exportBtn,  "Export JSON");
        DrawButton(_backBtn,    "< Back");

        int listY = 140;
        _sb.DrawString(Assets.MenuFont, "Rooms:", new Vector2(14, listY), _border);
        listY += 28;
        foreach (var r in _rooms)
        {
            bool isSel = r == _selected;
            if (isSel) DrawRect(new Rectangle(10, listY - 2, 190, 24), new Color(50, 18, 50));
            _sb.DrawString(Assets.MenuFont, $"- {r.Label}", new Vector2(16, listY),
                isSel ? Color.White : new Color(130, 125, 160));
            listY += 26;
        }

        _sb.DrawString(Assets.MenuFont, "Click room: menu",         new Vector2(12, 460), new Color(60,58,85));
        _sb.DrawString(Assets.MenuFont, "Drag room: reposition",    new Vector2(12, 484), new Color(60,58,85));
        _sb.DrawString(Assets.MenuFont, "Del: remove selected",     new Vector2(12, 508), new Color(60,58,85));
        _sb.DrawString(Assets.MenuFont, "Esc: cancel / back",       new Vector2(12, 532), new Color(60,58,85));

        // Canvas
        DrawRect(new Rectangle(CanvasX, CanvasY, CanvasW, CanvasH), new Color(13, 12, 22));
        DrawRect(new Rectangle(CanvasX,           CanvasY,           CanvasW, 1), _border);
        DrawRect(new Rectangle(CanvasX,           CanvasY + CanvasH, CanvasW, 1), _border);
        DrawRect(new Rectangle(CanvasX + CanvasW, CanvasY,           1, CanvasH + 1), _border);
        DrawGrid();

        foreach (var conn in _connections)
            DrawLine(conn.A.Centre, conn.B.Centre, _connColor);

        if (_connectMode && _connectFrom != null)
            DrawLine(_connectFrom.Centre, Mouse.GetState().Position.ToVector2(),
                     new Color(232, 180, 60));

        foreach (var room in _rooms)
            DrawRoom(room, gameTime);

        _sb.DrawString(Assets.MenuFont, "MAP CANVAS  --  10x10 rooms",
            new Vector2(CanvasX + 8, CanvasY + 6), new Color(80, 75, 110));

        // Popup on top
        if (_contextRoom != null)
            DrawContextPopup();

        // Status bar
        DrawRect(new Rectangle(0, vp.Height - 28, vp.Width, 28), new Color(8, 7, 16));
        float sa = _statusTimer > 0 ? Math.Min(1f, (float)_statusTimer) : 0.4f;
        _sb.DrawString(Assets.MenuFont, _status,
            new Vector2(CanvasX + 4, vp.Height - 24), new Color(180, 170, 210) * sa);

        _sb.End();
    }

    // -----------------------------------------------------------------------
    // Context popup
    // -----------------------------------------------------------------------

    private void OpenContextMenu(EditorRoom room, Point mousePos)
    {
        _contextRoom = room;
        _selected    = room;

        const int pw = 160, btnH = 44;
        int ph = btnH * 2 + 2;   // two buttons
        int px = mousePos.X + 6;
        int py = mousePos.Y + 6;
        var vp = _game.GraphicsDevice.Viewport;
        if (px + pw > vp.Width)  px = mousePos.X - pw;
        if (py + ph > vp.Height) py = mousePos.Y - ph;

        _popupBounds  = new Rectangle(px, py, pw, ph);
        _popupEditBtn = new Rectangle(px, py,          pw, btnH);
        _popupConnBtn = new Rectangle(px, py + btnH + 1, pw, btnH);
    }

    private void DrawContextPopup()
    {
        var mp = Mouse.GetState().Position.ToVector2();

        // Drop shadow
        DrawRect(new Rectangle(_popupBounds.X + 4, _popupBounds.Y + 4,
                               _popupBounds.Width, _popupBounds.Height),
            new Color(0, 0, 0, 140));

        DrawRect(_popupBounds, new Color(24, 22, 40));
        DrawBorder(_popupBounds, _accent);

        // Room name label above popup
        var nameSz = Assets.MenuFont.MeasureString(_contextRoom.Label);
        _sb.DrawString(Assets.MenuFont, _contextRoom.Label,
            new Vector2(_popupBounds.X + 6, _popupBounds.Y - nameSz.Y - 2),
            new Color(130, 125, 165));

        DrawPopupButton(_popupEditBtn, "Edit Room", mp);

        // Divider between buttons
        DrawRect(new Rectangle(_popupBounds.X, _popupEditBtn.Bottom, _popupBounds.Width, 1),
            new Color(40, 38, 65));

        string connLabel = _connectMode && _connectFrom == _contextRoom
            ? "Cancel Connect" : "Connect";
        DrawPopupButton(_popupConnBtn, connLabel, mp);
    }

    private void DrawPopupButton(Rectangle r, string label, Vector2 mp)
    {
        bool hov = r.Contains(mp);
        DrawRect(r, hov ? new Color(50, 18, 30) : new Color(24, 22, 40));
        DrawRect(new Rectangle(r.X, r.Y, 4, r.Height), _accent);  // left stripe
        var sz = Assets.MenuFont.MeasureString(label);
        _sb.DrawString(Assets.MenuFont, label,
            new Vector2(r.X + 14, r.Y + (r.Height - sz.Y) / 2f),
            hov ? Color.White : new Color(200, 195, 225));
    }

    // -----------------------------------------------------------------------
    // Room drawing
    // -----------------------------------------------------------------------

    private void DrawRoom(EditorRoom room, GameTime gt)
    {
        bool isSel  = room == _selected;
        bool isConn = room == _connectFrom;

        var bg     = isSel  ? _roomBgSel : _roomBg;
        var border = isConn ? new Color(232, 180, 60) : isSel ? _accent : _border;

        var b = room.Bounds;
        DrawRect(b, bg);
        DrawMiniShape(room, b);
        DrawBorder(b, border);

        var sz = Assets.MenuFont.MeasureString(room.Label);
        _sb.DrawString(Assets.MenuFont, room.Label,
            new Vector2(b.X + (b.Width - sz.X) / 2f, b.Y + 6), Color.White);

        var sub   = $"{room.Shape.TileCount} tiles";
        var subSz = Assets.MenuFont.MeasureString(sub);
        _sb.DrawString(Assets.MenuFont, sub,
            new Vector2(b.X + (b.Width - subSz.X) / 2f, b.Y + 6 + sz.Y + 2),
            new Color(80, 75, 110));

        if (isSel)
        {
            float pulse = (float)Math.Sin(gt.TotalGameTime.TotalSeconds * 4f) * 0.5f + 0.5f;
            DrawRect(new Rectangle(b.X, b.Y, b.Width, 3),
                Color.Lerp(_accent, new Color(255, 140, 160), pulse));
        }
    }

    private void DrawMiniShape(EditorRoom room, Rectangle bounds)
    {
        const int maxS = RoomShapeData.MaxSize;
        float cw = (bounds.Width  - 4f) / maxS;
        float ch = (bounds.Height - 26f) / maxS;
        int   bx = bounds.X + 2;
        int   by = bounds.Y + bounds.Height - (int)(maxS * ch) - 2;
        foreach (var (col, row) in room.Shape.Tiles)
        {
            int px = bx + (int)(col * cw);
            int py = by + (int)(row * ch);
            DrawRect(new Rectangle(px, py, Math.Max(1,(int)cw), Math.Max(1,(int)ch)),
                new Color(55, 50, 85));
        }
    }

    // -----------------------------------------------------------------------
    // Drawing helpers
    // -----------------------------------------------------------------------

    private void DrawButton(Rectangle r, string label)
    {
        var mp  = Mouse.GetState().Position.ToVector2();
        bool hov = r.Contains(mp);
        DrawRect(r, hov ? new Color(50, 18, 50) : new Color(26, 22, 42));
        DrawBorder(r, hov ? _accent : _border);
        var sz = Assets.MenuFont.MeasureString(label);
        _sb.DrawString(Assets.MenuFont, label,
            new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f),
            hov ? Color.White : new Color(180, 170, 210));
    }

    private void DrawGrid()
    {
        const int Step = 40;
        var col = new Color(20, 18, 32);
        for (int x = CanvasX; x < CanvasX + CanvasW; x += Step)
            DrawRect(new Rectangle(x, CanvasY, 1, CanvasH), col);
        for (int y = CanvasY; y < CanvasY + CanvasH; y += Step)
            DrawRect(new Rectangle(CanvasX, y, CanvasW, 1), col);
    }

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var dir = to - from; float len = dir.Length();
        if (len < 1f) return;
        dir.Normalize();
        for (float t = 0; t < len; t += 3f)
            DrawRect(new Rectangle((int)(from.X + dir.X * t),
                                   (int)(from.Y + dir.Y * t), 2, 2), color);
    }

    private void DrawRect(Rectangle r, Color c) => _sb.Draw(Assets.Pixel, r, c);

    private void DrawBorder(Rectangle r, Color c)
    {
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,     r.Y,      r.Width,  1), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,     r.Bottom, r.Width,  1), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,     r.Y,      1,        r.Height), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.Right, r.Y,      1,        r.Height + 1), c);
    }

    // -----------------------------------------------------------------------
    // Room management
    // -----------------------------------------------------------------------

    private void AddRoom()
    {
        int col = (_rooms.Count % 4);
        int row = (_rooms.Count / 4);
        var room = new EditorRoom
        {
            Id       = $"Room{_nextId}",
            Label    = $"Room {_nextId}",
            Position = new Vector2(
                CanvasX + 30 + col * (EditorRoom.W + 20),
                CanvasY + 30 + row * (EditorRoom.H + 30))
        };
        room.Shape.FillDefault();
        _nextId++;
        _rooms.Add(room);
        _selected = room;
        SetStatus($"Added '{room.Label}'. Click it to open its menu.");
    }

    private void RemoveRoom(EditorRoom room)
    {
        _rooms.Remove(room);
        _connections.RemoveAll(c => c.A == room || c.B == room);
        if (_selected    == room) _selected    = null;
        if (_connectFrom == room) { _connectFrom = null; _connectMode = false; }
        if (_contextRoom == room) _contextRoom = null;
        SetStatus($"Removed '{room.Label}'.");
    }

    private void TryAddConnection(EditorRoom a, EditorRoom b)
    {
        foreach (var c in _connections)
            if ((c.A == a && c.B == b) || (c.A == b && c.B == a))
            { SetStatus("Connection already exists."); return; }
        _connections.Add(new EditorConnection { A = a, B = b });
        SetStatus($"Connected '{a.Label}' <-> '{b.Label}'.");
    }

    private void StartConnect(EditorRoom room)
    {
        _connectFrom = room;
        _connectMode = true;
        _selected    = room;
        SetStatus($"Connecting from '{room.Label}' - click another room to link it.");
    }

    private EditorRoom HitTest(Vector2 point)
    {
        for (int i = _rooms.Count - 1; i >= 0; i--)
            if (_rooms[i].Bounds.Contains(point)) return _rooms[i];
        return null;
    }

    private void ClampToCanvas(EditorRoom room) =>
        room.Position = new Vector2(
            Math.Clamp(room.Position.X, CanvasX, CanvasX + CanvasW - EditorRoom.W),
            Math.Clamp(room.Position.Y, CanvasY, CanvasY + CanvasH - EditorRoom.H));

    // -----------------------------------------------------------------------
    // Open geometry editor
    // -----------------------------------------------------------------------

    private void OpenGeoEditor(EditorRoom room)
    {
        _geoEditor.OpenRoom(room.Id, room.Label, room.Shape,
            _ => SetStatus($"Geometry saved for '{room.Label}'."));
        NavigationBus.RequestNavigate("RoomGeoEditor");
    }

    // -----------------------------------------------------------------------
    // Export
    // -----------------------------------------------------------------------

    private void ExportToJson()
    {
        if (_rooms.Count == 0) { SetStatus("Nothing to export."); return; }
        try { ExportMapJson(); ExportRoomsJsonStubs(); SetStatus($"Exported {_rooms.Count} room(s)."); }
        catch (Exception ex) { SetStatus($"Export failed: {ex.Message}"); }
    }

    private void ExportMapJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"startRoom\": \"{_rooms[0].Id}\",");
        sb.AppendLine("  \"rooms\": [");
        for (int i = 0; i < _rooms.Count; i++)
        {
            var r = _rooms[i]; bool last = i == _rooms.Count - 1;
            float nx = (r.Position.X - CanvasX) / (float)CanvasW;
            float ny = (r.Position.Y - CanvasY) / (float)CanvasH;
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\":         \"{r.Id}\",");
            sb.AppendLine($"      \"label\":      \"{r.Label}\",");
            sb.AppendLine("      \"sceneType\":  \"box\",");
            sb.AppendLine($"      \"position\":   [{nx:F3}, {ny:F3}],");
            sb.AppendLine($"      \"size\":       [{EditorRoom.W / (float)CanvasW:F3}, {EditorRoom.H / (float)CanvasH:F3}],");
            sb.AppendLine("      \"discovered\": false");
            sb.AppendLine(last ? "    }" : "    },");
        }
        sb.AppendLine("  ],");
        sb.AppendLine("  \"connections\": [");
        for (int i = 0; i < _connections.Count; i++)
        {
            var c = _connections[i]; bool last = i == _connections.Count - 1;
            sb.AppendLine($"    {{ \"fromId\": \"{c.A.Id}\", \"toId\": \"{c.B.Id}\" }}{(last ? "" : ",")}");
        }
        sb.AppendLine("  ]"); sb.AppendLine("}");
        System.IO.File.WriteAllText(System.IO.Path.Combine("Data", "map.json"), sb.ToString());
    }

    private void ExportRoomsJsonStubs()
    {
        string path     = System.IO.Path.Combine("Data", "rooms.json");
        string existing = System.IO.File.Exists(path)
            ? System.IO.File.ReadAllText(path) : "{\"rooms\":[]}";
        var node = System.Text.Json.Nodes.JsonNode.Parse(existing);
        var arr  = node!["rooms"]!.AsArray();
        var ids  = new HashSet<string>();
        foreach (var r in arr) ids.Add(r!["id"]!.GetValue<string>());
        foreach (var room in _rooms)
        {
            if (ids.Contains(room.Id)) continue;
            arr.Add(System.Text.Json.Nodes.JsonNode.Parse($@"{{
  ""id"": ""{room.Id}"",
  ""label"": ""{room.Label}"",
  ""tiles"": ""{room.Shape.Serialise()}"",
  ""wallColor"":  [30, 28, 45],
  ""floorColor"": [20, 18, 30],
  ""ceilColor"":  [12, 10, 20],
  ""entities"": []
}}"));
        }
        System.IO.File.WriteAllText(path,
            node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void SetStatus(string msg, double dur = 4.0) { _status = msg; _statusTimer = dur; }
    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}