using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

public class RoomEditorContext
{
    public string CurrentRoomId;
    public List<(string Id, string Label)> ConnectedRooms = new();
}

/// <summary>
/// Room geometry editor — paint floor tiles, place objects and characters.
///
/// Characters tab now shows a picker drawn from CharacterData.Characters
/// instead of a free-text name field. Select a character from the list
/// then click "Place Character", then click a tile.
/// </summary>
public class RoomGeometryEditorScene : IScene
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------
    private const int GridSize  = RoomShapeData.MaxSize; // 20×20
    private const int TilePx    = 30;
    private const int WallThick = 4;
    private const int SideW     = 240;
    private const int PropW     = 300;

    private int GridOriginX => SideW + 16;
    private int GridOriginY => 56;
    private int GridPx      => GridSize * TilePx;

    // -----------------------------------------------------------------------
    // Object catalogue
    // -----------------------------------------------------------------------
    private static readonly ObjectDef[] ObjectCatalogue =
    {
        new("table",       "Table",       new Color(120,  85, 55), 'T'),
        new("chair",       "Chair",       new Color(100,  70, 45), 'C'),
        new("pillar",      "Pillar",      new Color( 70,  68, 90), 'P'),
        new("shelf",       "Shelf",       new Color(120,  95, 65), 'S'),
        new("orientedBox", "Wall Object", new Color( 80,  65, 90), 'W'),
        new("box",         "Crate",       new Color( 90,  80, 55), 'B'),
    };

    private class ObjectDef
    {
        public readonly string Type;
        public readonly string Label;
        public readonly Color  Colour;
        public readonly char   Icon;
        public ObjectDef(string type, string label, Color colour, char icon)
            { Type = type; Label = label; Colour = colour; Icon = icon; }
    }

    // -----------------------------------------------------------------------
    // Tab enum
    // -----------------------------------------------------------------------
    private enum SidebarTab { Brushes, Objects, Characters }

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly Game         _game;
    private readonly SpriteBatch  _sb;

    private string               _roomId;
    private string               _roomLabel;
    private RoomShapeData        _shape;
    private Action<RoomShapeData> _onSave;
    private RoomEditorContext    _context;

    // Layout helpers
    private readonly VStack _sideStack = new() { Padding = 10, Spacing = 6 };
    private readonly VStack _propStack = new() { Padding = 12, Spacing = 8 };
    private readonly HStack _tabRow    = new() { Padding = 0,  Spacing = 2 };

    // Sidebar tab
    private SidebarTab _activeTab = SidebarTab.Brushes;

    // Brushes tab
    private enum Brush { Add, Remove }
    private Brush _brush     = Brush.Add;
    private bool  _painting;
    private bool  _paintFill;

    // Objects tab
    private int  _selectedObjectIdx = 0;
    private bool _placingObject     = false;

    // Characters tab — picker from CharacterData.Characters
    private bool             _placingCharacter = false;
    private int              _selectedCharIdx  = 0;
    private List<Rectangle>  _charPickerRects  = new();
    private Rectangle        _charPlaceRect;

    // Properties panel
    private PlacedObject    _inspectedObject     = null;
    private PlacedCharacter _inspectedCharacter  = null;
    private string          _propCharName        = "";
    private bool            _editingCharPropName = false;

    // Interaction selector
    private bool            _showInteractionDropdown  = false;
    private Rectangle       _interactionSelectRect;
    private Rectangle       _interactionClearRect;
    private List<Rectangle> _interactionDropdownRects = new();
    private Action<string>  _interactionSetCallback;

    // Cached layout rects
    private Rectangle       _tabBrushesRect, _tabObjectsRect, _tabCharsRect;
    private Rectangle       _addBrushRect, _removeBrushRect, _clearRect;
    private Rectangle       _saveRect, _backRect;
    private List<Rectangle> _objRects     = new();
    private Rectangle       _propCloseRect;
    private Rectangle       _propDeleteRect;
    private Rectangle       _propCharNameRect;

    // Hover
    private int _hoverCol = -1;
    private int _hoverRow = -1;

    // Input
    private MouseState    _prevMouse;
    private KeyboardState _prevKeys;
    private Keys[]        _prevPressedKeys = Array.Empty<Keys>();

    // Colours
    private readonly Color _floorColor = new Color(30,  28,  48);
    private readonly Color _floorHov   = new Color(45,  42,  68);
    private readonly Color _wallColor  = new Color(180, 60,  80);
    private readonly Color _emptyColor = new Color(14,  13,  22);
    private readonly Color _gridLine   = new Color(22,  20,  36);
    private readonly Color _charColor  = new Color(180, 120, 220);

    private bool HasInspected => _inspectedObject != null || _inspectedCharacter != null;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public RoomGeometryEditorScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _sb   = spriteBatch;
    }

    // -----------------------------------------------------------------------
    // Properties helpers
    // -----------------------------------------------------------------------
    private void OpenObjectProperties(PlacedObject obj)
    {
        CloseProperties();
        _inspectedObject = obj;
    }

    private void OpenCharacterProperties(PlacedCharacter ch)
    {
        CloseProperties();
        _inspectedCharacter = ch;
        _propCharName = ch.Name;
    }

    private void CloseProperties()
    {
        if (_inspectedCharacter != null)
            _inspectedCharacter.Name = _propCharName;
        _inspectedObject         = null;
        _inspectedCharacter      = null;
        _editingCharPropName     = false;
        _showInteractionDropdown = false;
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------
    public void OpenRoom(string roomId, string roomLabel,
        RoomShapeData shape, Action<RoomShapeData> onSave,
        RoomEditorContext context = null)
    {
        _roomId    = roomId;
        _roomLabel = roomLabel;
        _shape     = shape;
        _onSave    = onSave;
        _context   = context;
        CloseProperties();
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------
    public void Load() { }

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        _painting            = false;
        _placingObject       = false;
        _placingCharacter    = false;
        CloseProperties();
    }

    public void OnExit() => _game.IsMouseVisible = false;

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys  = Keyboard.GetState();
        var mp    = mouse.Position.ToVector2();

        bool clicked = mouse.LeftButton  == ButtonState.Released &&
                       _prevMouse.LeftButton  == ButtonState.Pressed;

        // ── Interaction dropdown intercept ─────────────────────────────────
        if (_showInteractionDropdown && clicked)
        {
            bool hitDrop = false;
            for (int i = 0; i < _interactionDropdownRects.Count; i++)
            {
                if (_interactionDropdownRects[i].Contains(mp) &&
                    i < InteractionStore.Interactions.Count)
                {
                    _interactionSetCallback?.Invoke(InteractionStore.Interactions[i].Id);
                    hitDrop = true;
                    break;
                }
            }
            _showInteractionDropdown = false;
            if (hitDrop) { SaveInput(mouse, keys); return; }
        }

        // ── Properties-name text editing ──────────────────────────────────
        if (_editingCharPropName && clicked && !_propCharNameRect.Contains(mp))
            _editingCharPropName = false;

        if (_editingCharPropName)
        {
            HandleCharPropNameInput(keys);
            if (IsPressed(keys, _prevKeys, Keys.Escape)) _editingCharPropName = false;
            SaveInput(mouse, keys);
            return;
        }

        // ── Global keyboard ───────────────────────────────────────────────
        if (IsPressed(keys, _prevKeys, Keys.Escape)) SaveAndReturn();

        if (_activeTab == SidebarTab.Brushes)
        {
            if (IsPressed(keys, _prevKeys, Keys.A) || IsPressed(keys, _prevKeys, Keys.D1))
                _brush = Brush.Add;
            if (IsPressed(keys, _prevKeys, Keys.R) || IsPressed(keys, _prevKeys, Keys.D2))
                _brush = Brush.Remove;
            if (IsPressed(keys, _prevKeys, Keys.Back))
                _shape.FillDefault();
        }

        if ((_activeTab == SidebarTab.Objects || _activeTab == SidebarTab.Characters) &&
            (IsPressed(keys, _prevKeys, Keys.Delete) || IsPressed(keys, _prevKeys, Keys.Back)))
        {
            if (_hoverCol >= 0)
            {
                var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (obj != null) { if (_inspectedObject == obj) CloseProperties(); _shape.RemoveObject(obj); }
                var ch = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (ch != null) { if (_inspectedCharacter == ch) CloseProperties(); _shape.RemoveCharacter(ch); }
            }
        }

        // ── Tab switching ─────────────────────────────────────────────────
        if (clicked && _tabBrushesRect.Contains(mp))
            { _activeTab = SidebarTab.Brushes;     _placingObject = false; _placingCharacter = false; }
        if (clicked && _tabObjectsRect.Contains(mp))
            { _activeTab = SidebarTab.Objects;     _painting = false;      _placingCharacter = false; }
        if (clicked && _tabCharsRect.Contains(mp))
            { _activeTab = SidebarTab.Characters;  _painting = false;      _placingObject    = false; }

        // ── Properties panel ──────────────────────────────────────────────
        if (HasInspected && clicked)
        {
            if (_propCloseRect.Contains(mp))
                { CloseProperties(); goto AfterProp; }

            if (_inspectedCharacter != null && _propCharNameRect.Contains(mp))
                { _editingCharPropName = true; goto AfterProp; }

            if (_interactionSelectRect.Contains(mp))
                { _showInteractionDropdown = !_showInteractionDropdown; goto AfterProp; }

            if (_interactionClearRect.Contains(mp))
                { _interactionSetCallback?.Invoke(null); goto AfterProp; }

            if (_propDeleteRect.Contains(mp))
            {
                if (_inspectedObject != null)
                {
                    var obj = _inspectedObject;
                    CloseProperties();
                    _shape.RemoveObject(obj);
                }
                else if (_inspectedCharacter != null)
                {
                    var ch = _inspectedCharacter;
                    CloseProperties();
                    _shape.RemoveCharacter(ch);
                }
                goto AfterProp;
            }
        }
        AfterProp:

        // ── Sidebar buttons ───────────────────────────────────────────────
        if (clicked && _saveRect.Contains(mp)) SaveAndReturn();
        if (clicked && _backRect.Contains(mp)) SaveAndReturn();

        // ── Brushes tab buttons ───────────────────────────────────────────
        if (_activeTab == SidebarTab.Brushes && clicked)
        {
            if (_addBrushRect.Contains(mp))    _brush = Brush.Add;
            if (_removeBrushRect.Contains(mp)) _brush = Brush.Remove;
            if (_clearRect.Contains(mp))       _shape.FillDefault();
        }

        // ── Objects tab: select type ──────────────────────────────────────
        if (_activeTab == SidebarTab.Objects && clicked)
        {
            for (int i = 0; i < _objRects.Count; i++)
                if (_objRects[i].Contains(mp)) { _selectedObjectIdx = i; _placingObject = true; }
        }

        // ── Characters tab: picker + place button ─────────────────────────
        if (_activeTab == SidebarTab.Characters && clicked)
        {
            for (int i = 0; i < _charPickerRects.Count; i++)
                if (_charPickerRects[i].Contains(mp))
                    { _selectedCharIdx = i; _placingCharacter = false; }

            if (_charPlaceRect.Contains(mp) && CharacterData.Characters.Count > 0)
                _placingCharacter = true;
        }

        // ── Grid hover ────────────────────────────────────────────────────
        _hoverCol = -1;
        _hoverRow = -1;
        var gridRect = new Rectangle(GridOriginX, GridOriginY, GridPx, GridPx);
        if (gridRect.Contains(mp))
        {
            _hoverCol = Math.Clamp((int)((mp.X - GridOriginX) / TilePx), 0, GridSize - 1);
            _hoverRow = Math.Clamp((int)((mp.Y - GridOriginY) / TilePx), 0, GridSize - 1);
        }

        // ── Brushes tab: paint ────────────────────────────────────────────
        if (_activeTab == SidebarTab.Brushes)
        {
            if (_hoverCol >= 0 &&
                mouse.LeftButton == ButtonState.Pressed &&
                _prevMouse.LeftButton == ButtonState.Released)
                { _painting = true; _paintFill = (_brush == Brush.Add); }

            if (_painting)
            {
                if (mouse.LeftButton == ButtonState.Released) _painting = false;
                else if (_hoverCol >= 0)
                {
                    if (_paintFill) _shape.Fill(_hoverCol, _hoverRow);
                    else            _shape.Clear(_hoverCol, _hoverRow);
                }
            }
        }

        // ── Objects tab: place / inspect / remove ─────────────────────────
        if (_activeTab == SidebarTab.Objects && _hoverCol >= 0)
        {
            if (clicked && _placingObject && _shape.IsFilled(_hoverCol, _hoverRow))
            {
                var existing = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (existing != null) { if (_inspectedObject == existing) CloseProperties(); _shape.RemoveObject(existing); }
                _shape.AddObject(new PlacedObject
                    { Type = ObjectCatalogue[_selectedObjectIdx].Type, Col = _hoverCol, Row = _hoverRow });
                _placingObject = false;
            }
            else if (clicked && !_placingObject)
            {
                var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (obj != null) OpenObjectProperties(obj);
                else
                {
                    var ch = _shape.CharacterAt(_hoverCol, _hoverRow);
                    if (ch != null) OpenCharacterProperties(ch);
                    else CloseProperties();
                }
            }
        }

        // ── Characters tab: place / inspect / remove ──────────────────────
        if (_activeTab == SidebarTab.Characters && _hoverCol >= 0)
        {
            if (clicked && _placingCharacter && _shape.IsFilled(_hoverCol, _hoverRow))
            {
                // Remove anything already on this tile
                var existingCh  = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (existingCh  != null) { if (_inspectedCharacter == existingCh) CloseProperties(); _shape.RemoveCharacter(existingCh); }
                var existingObj = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (existingObj != null) { if (_inspectedObject == existingObj)   CloseProperties(); _shape.RemoveObject(existingObj); }

                // Place using the selected profile's Id as the name
                // (BillboardBuilder looks up CharacterData by profile.Id == entity name)
                var profile = _selectedCharIdx < CharacterData.Characters.Count
                    ? CharacterData.Characters[_selectedCharIdx] : null;

                if (profile != null)
                {
                    _shape.AddCharacter(new PlacedCharacter
                    {
                        Name  = profile.Id,
                        Col   = _hoverCol,
                        Row   = _hoverRow,
                        TintR = 255,
                        TintG = 255,
                        TintB = 255,
                    });
                }
                _placingCharacter = false;
            }
            else if (clicked && !_placingCharacter)
            {
                var ch = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (ch != null) OpenCharacterProperties(ch);
                else
                {
                    var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                    if (obj != null) OpenObjectProperties(obj);
                    else CloseProperties();
                }
            }
        }

        if (clicked && _hoverCol >= 0 && _activeTab == SidebarTab.Brushes)
            CloseProperties();

        SaveInput(mouse, keys);
    }

    private void SaveInput(MouseState mouse, KeyboardState keys)
    {
        _prevMouse       = mouse;
        _prevKeys        = keys;
        _prevPressedKeys = keys.GetPressedKeys();
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

        // Left sidebar
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, SideW, vp.Height), LayoutDraw.PanelBg);
        LayoutDraw.Rect(_sb, new Rectangle(SideW, 0, 1, vp.Height), LayoutDraw.Border);

        _sideStack.Begin(0, 0, SideW, vp.Height);

        // Tab strip
        var tabStripRect = _sideStack.Next(36);
        int tabW = (tabStripRect.Width - 4) / 3;
        _tabRow.Spacing = 2;
        _tabRow.Begin(tabStripRect);
        _tabBrushesRect = _tabRow.Next(tabW);
        _tabObjectsRect = _tabRow.Next(tabW);
        _tabCharsRect   = _tabRow.Remaining();
        LayoutDraw.Tab(_sb, _tabBrushesRect, "Brush",  _activeTab == SidebarTab.Brushes,     mp);
        LayoutDraw.Tab(_sb, _tabObjectsRect, "Object", _activeTab == SidebarTab.Objects,     mp);
        LayoutDraw.Tab(_sb, _tabCharsRect,   "Chars",  _activeTab == SidebarTab.Characters,  mp);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Header info
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "ROOM EDITOR", _sideStack.Next(20), LayoutDraw.Accent);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, _roomLabel ?? "", _sideStack.Next(20), Color.White);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            $"Tiles: {_shape?.TileCount ?? 0}  Obj: {_shape?.Objects.Count ?? 0}  Chars: {_shape?.Characters.Count ?? 0}",
            _sideStack.Next(20), new Color(120, 115, 160));

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Bottom buttons (pinned)
        _backRect = _sideStack.NextFromBottom(44);
        _saveRect = _sideStack.NextFromBottom(44);
        LayoutDraw.Button(_sb, _saveRect, "Save + Return", mp);
        LayoutDraw.Button(_sb, _backRect, "< Back",        mp);

        // Tab content
        switch (_activeTab)
        {
            case SidebarTab.Brushes:    DrawBrushesTab(mp);    break;
            case SidebarTab.Objects:    DrawObjectsTab(mp);    break;
            case SidebarTab.Characters: DrawCharactersTab(mp); break;
        }

        // Grid
        DrawGrid(gameTime);

        _sb.DrawString(Assets.MenuFont, $"Room Geometry - {GridSize}x{GridSize}",
            new Vector2(GridOriginX + 4, GridOriginY - 22), new Color(80, 75, 110));

        // Cursor hint
        if (_hoverCol >= 0)
        {
            var mpos = Mouse.GetState().Position;
            if (_activeTab == SidebarTab.Brushes)
            {
                var hint = _brush == Brush.Add ? "Add" : "Remove";
                _sb.DrawString(Assets.MenuFont, hint, new Vector2(mpos.X + 14, mpos.Y - 8),
                    _brush == Brush.Add ? new Color(80, 200, 120) : LayoutDraw.Accent);
            }
            else if (_placingObject)
            {
                _sb.DrawString(Assets.MenuFont, ObjectCatalogue[_selectedObjectIdx].Label,
                    new Vector2(mpos.X + 14, mpos.Y - 8), ObjectCatalogue[_selectedObjectIdx].Colour);
            }
            else if (_placingCharacter)
            {
                string hint = _selectedCharIdx < CharacterData.Characters.Count
                    ? CharacterData.Characters[_selectedCharIdx].Name
                    : "Character";
                _sb.DrawString(Assets.MenuFont, hint, new Vector2(mpos.X + 14, mpos.Y - 8), _charColor);
            }
            else
            {
                var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                var ch  = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (obj != null || ch != null)
                    _sb.DrawString(Assets.MenuFont, "[Click] Inspect",
                        new Vector2(mpos.X + 14, mpos.Y - 8), LayoutDraw.TextNormal);
            }
        }

        // Properties panel (right side, on top)
        if (HasInspected)
            DrawPropertiesPanel(mp);

        _sb.End();
    }

    // -----------------------------------------------------------------------
    // Brushes tab
    // -----------------------------------------------------------------------
    private void DrawBrushesTab(Vector2 mp)
    {
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), "Brush:");
        _addBrushRect    = _sideStack.Next(44);
        DrawBrushButton(_addBrushRect, "Add Space [A]",    _brush == Brush.Add,    mp);
        _removeBrushRect = _sideStack.Next(44);
        DrawBrushButton(_removeBrushRect, "Remove Space [R]", _brush == Brush.Remove, mp);
        _sideStack.Space(4);
        _clearRect = _sideStack.Next(44);
        LayoutDraw.Button(_sb, _clearRect, "Reset Default", mp);

        _sideStack.Space(8);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());
        var helpCol = new Color(60, 58, 85);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[A] Add  [R] Remove",  _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[Bksp] Reset grid",    _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[Esc] Save + return",  _sideStack.Next(18), helpCol);
    }

    private void DrawBrushButton(Rectangle r, string label, bool active, Vector2 mp)
    {
        bool hov = r.Contains(mp);
        LayoutDraw.Rect(_sb, r,
            active ? new Color(50, 18, 30) : hov ? new Color(30, 26, 46) : new Color(20, 18, 32));
        LayoutDraw.BorderRect(_sb, r,
            active ? LayoutDraw.Accent : hov ? new Color(120, 110, 160) : LayoutDraw.Border);
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, label, r,
            active ? Color.White : hov ? new Color(200, 195, 225) : LayoutDraw.TextNormal);
    }

    // -----------------------------------------------------------------------
    // Objects tab
    // -----------------------------------------------------------------------
    private void DrawObjectsTab(Vector2 mp)
    {
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), "Object type:");
        _sideStack.Space(4);
        _objRects.Clear();

        for (int i = 0; i < ObjectCatalogue.Length; i++)
        {
            if (_sideStack.IsFull) break;
            var def = ObjectCatalogue[i];
            bool sel = i == _selectedObjectIdx;
            var  r   = _sideStack.Next(40);
            _objRects.Add(r);

            bool hov = r.Contains(mp);
            LayoutDraw.Rect(_sb, r, sel ? new Color(30, 20, 45) : hov ? new Color(22, 18, 36) : new Color(18, 16, 28));
            LayoutDraw.BorderRect(_sb, r,
                sel ? LayoutDraw.Accent : hov ? new Color(120, 110, 160) : LayoutDraw.Border);

            var sw = new Rectangle(r.X + 6, r.Y + (r.Height - 18) / 2, 18, 18);
            LayoutDraw.Rect(_sb, sw, def.Colour);
            LayoutDraw.BorderRect(_sb, sw, LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, def.Icon.ToString(),
                new Vector2(sw.X + 3, sw.Y + 2), new Color(240, 240, 240));
            _sb.DrawString(Assets.MenuFont, def.Label,
                new Vector2(r.X + 32, r.Y + (r.Height - 18) / 2),
                sel ? Color.White : LayoutDraw.TextNormal);

            if (sel)
            {
                var tsz = Assets.MenuFont.MeasureString("PLACING");
                _sb.DrawString(Assets.MenuFont, "PLACING",
                    new Vector2(r.Right - tsz.X - 6, r.Y + (r.Height - tsz.Y) / 2), LayoutDraw.Accent);
            }
        }

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());
        var helpCol = new Color(60, 58, 85);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"Placed: {_shape.Objects.Count}",  _sideStack.Next(18), new Color(80, 75, 110));
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click placed: select & inspect",   _sideStack.Next(18), helpCol);
    }

    // -----------------------------------------------------------------------
    // Characters tab
    // -----------------------------------------------------------------------
    private void DrawCharactersTab(Vector2 mp)
    {
        var chars = CharacterData.Characters;

        if (chars.Count == 0)
        {
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No characters defined.",
                _sideStack.Next(20), LayoutDraw.DimText);
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Add some in the",
                _sideStack.Next(18), new Color(60, 58, 85));
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Level Editor > Characters.",
                _sideStack.Next(18), new Color(60, 58, 85));
            _charPlaceRect = Rectangle.Empty;
            _charPickerRects.Clear();
            return;
        }

        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), "Select character:");
        _sideStack.Space(4);

        _charPickerRects.Clear();
        for (int i = 0; i < chars.Count; i++)
        {
            if (_sideStack.IsFull) break;
            var  c      = chars[i];
            bool isSel  = i == _selectedCharIdx;
            var  r      = _sideStack.Next(44);
            _charPickerRects.Add(r);

            var bg     = isSel ? new Color(35, 18, 50) : r.Contains(mp) ? new Color(24, 20, 36) : LayoutDraw.PanelBg;
            var border = isSel ? _charColor : LayoutDraw.Border;
            LayoutDraw.Rect(_sb, r, bg);
            LayoutDraw.BorderRect(_sb, r, border);
            if (isSel)
                LayoutDraw.Rect(_sb, new Rectangle(r.X, r.Y, 3, r.Height), _charColor);

            // Portrait thumbnail
            int thumbSz = 32;
            var thumbR  = new Rectangle(r.X + 6, r.Y + 6, thumbSz, thumbSz);
            if (c.Portrait != null)
                _sb.Draw(c.Portrait, thumbR, Color.White);
            else
            {
                LayoutDraw.Rect(_sb, thumbR, new Color(40, 35, 60));
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, "?", thumbR, new Color(80, 75, 110));
            }

            string displayName  = string.IsNullOrEmpty(c.Name)  ? c.Id : c.Name;
            string displayTitle = c.Title ?? "";
            _sb.DrawString(Assets.MenuFont, displayName,
                new Vector2(r.X + thumbSz + 12, r.Y + 6),
                isSel ? Color.White : LayoutDraw.TextNormal);
            if (!string.IsNullOrEmpty(displayTitle))
                _sb.DrawString(Assets.MenuFont, displayTitle,
                    new Vector2(r.X + thumbSz + 12, r.Y + 26),
                    new Color(130, 80, 100));
        }

        _sideStack.Space(6);

        _charPlaceRect = _sideStack.Next(44);
        bool hovPlace  = _charPlaceRect.Contains(mp);

        if (_placingCharacter)
        {
            LayoutDraw.Rect(_sb, _charPlaceRect, new Color(50, 30, 60));
            LayoutDraw.BorderRect(_sb, _charPlaceRect, _charColor);
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "PLACING - Click tile", _charPlaceRect, _charColor);
        }
        else
        {
            LayoutDraw.Rect(_sb, _charPlaceRect,
                hovPlace ? new Color(40, 25, 55) : new Color(25, 18, 38));
            LayoutDraw.BorderRect(_sb, _charPlaceRect,
                hovPlace ? _charColor : new Color(120, 80, 160));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Place Character", _charPlaceRect,
                hovPlace ? Color.White : new Color(180, 140, 220));
        }

        _sideStack.Space(8);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), $"In room ({_shape.Characters.Count}):");

        foreach (var ch in _shape.Characters)
        {
            if (_sideStack.IsFull) break;
            var  entryRect = _sideStack.Next(28);
            bool isSel     = _inspectedCharacter == ch;
            bool hov       = entryRect.Contains(mp);

            LayoutDraw.Rect(_sb, entryRect,
                isSel ? new Color(35, 18, 45) : hov ? new Color(24, 20, 36) : Color.Transparent);
            if (isSel)
                LayoutDraw.Rect(_sb, new Rectangle(entryRect.X, entryRect.Y, 3, entryRect.Height), _charColor);

            LayoutDraw.Rect(_sb, new Rectangle(entryRect.X + 6, entryRect.Y + 4, 20, 20), _charColor);
            LayoutDraw.BorderRect(_sb, new Rectangle(entryRect.X + 6, entryRect.Y + 4, 20, 20), LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, "@", new Vector2(entryRect.X + 8, entryRect.Y + 5), Color.White);

            // Show display name if we can look up the profile, else show raw id
            var profile = CharacterData.Characters.Find(c => c.Id == ch.Name);
            string label = profile?.Name ?? ch.Name;
            _sb.DrawString(Assets.MenuFont, label, new Vector2(entryRect.X + 32, entryRect.Y + 4),
                isSel ? Color.White : LayoutDraw.TextNormal);
        }

        var helpCol = new Color(60, 58, 85);
        _sideStack.Space(4);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click placed: select & inspect", _sideStack.Next(18), helpCol);
    }

    // -----------------------------------------------------------------------
    // Properties panel
    // -----------------------------------------------------------------------
    private void DrawPropertiesPanel(Vector2 mp)
    {
        var vp = _game.GraphicsDevice.Viewport;
        int px = vp.Width - PropW;

        _propStack.Begin(px, 10, PropW, vp.Height - 20);

        LayoutDraw.Rect(_sb, _propStack.Bounds, LayoutDraw.PanelBg);
        LayoutDraw.BorderRect(_sb, _propStack.Bounds, LayoutDraw.Border);
        LayoutDraw.AccentBar(_sb, _propStack.Bounds);

        var titleRow = _propStack.Next(30);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "PROPERTIES", titleRow, LayoutDraw.Accent);

        _propCloseRect = new Rectangle(titleRow.Right - 30, titleRow.Y, 30, 30);
        bool hovClose  = _propCloseRect.Contains(mp);
        LayoutDraw.Rect(_sb, _propCloseRect, hovClose ? new Color(80, 20, 30) : new Color(30, 28, 48));
        LayoutDraw.BorderRect(_sb, _propCloseRect, hovClose ? LayoutDraw.Accent : LayoutDraw.Border);
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "X", _propCloseRect,
            hovClose ? Color.White : LayoutDraw.TextNormal);

        LayoutDraw.DividerLine(_sb, _propStack.Divider());

        if (_inspectedObject != null)
            DrawObjectProperties(mp);
        else if (_inspectedCharacter != null)
            DrawCharacterProperties(mp);
    }

    private void DrawObjectProperties(Vector2 mp)
    {
        string typeLabel = _inspectedObject.Type;
        var    typeColor = new Color(100, 100, 140);
        char   typeIcon  = '?';
        foreach (var def in ObjectCatalogue)
            if (def.Type == _inspectedObject.Type)
                { typeLabel = def.Label; typeColor = def.Colour; typeIcon = def.Icon; break; }

        var typeRect = _propStack.Next(26);
        var sw = new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22);
        LayoutDraw.Rect(_sb, sw, typeColor);
        LayoutDraw.BorderRect(_sb, sw, LayoutDraw.Border);
        _sb.DrawString(Assets.MenuFont, typeIcon.ToString(), new Vector2(sw.X + 3, sw.Y + 2), Color.White);
        _sb.DrawString(Assets.MenuFont, typeLabel, new Vector2(typeRect.X + 30, typeRect.Y), Color.White);

        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            $"Tile: ({_inspectedObject.Col}, {_inspectedObject.Row})",
            _propStack.Next(20), new Color(120, 115, 160));

        LayoutDraw.DividerLine(_sb, _propStack.Divider());
        DrawInteractionSelector(mp, _inspectedObject.InteractionId,
            id => _inspectedObject.InteractionId = id);

        _propStack.Space(6);
        _propDeleteRect = _propStack.NextFromBottom(40);
        bool hovObjDel = _propDeleteRect.Contains(mp);
        LayoutDraw.Rect(_sb, _propDeleteRect, hovObjDel ? new Color(80, 18, 18) : new Color(45, 14, 14));
        LayoutDraw.BorderRect(_sb, _propDeleteRect, hovObjDel ? LayoutDraw.Accent : new Color(120, 40, 40));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove from Room", _propDeleteRect,
            hovObjDel ? Color.White : new Color(220, 100, 100));
    }

    private void DrawCharacterProperties(Vector2 mp)
    {
        // Resolve display name from CharacterData if possible
        var profile = CharacterData.Characters.Find(c => c.Id == _inspectedCharacter.Name);

        var typeRect = _propStack.Next(26);
        LayoutDraw.Rect(_sb, new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22), _charColor);
        LayoutDraw.BorderRect(_sb, new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22), LayoutDraw.Border);
        _sb.DrawString(Assets.MenuFont, "@", new Vector2(typeRect.X + 4, typeRect.Y + 3), Color.White);
        _sb.DrawString(Assets.MenuFont, profile?.Name ?? _inspectedCharacter.Name,
            new Vector2(typeRect.X + 30, typeRect.Y), Color.White);

        if (profile != null && !string.IsNullOrEmpty(profile.Title))
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, profile.Title,
                _propStack.Next(20), new Color(130, 80, 100));

        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            $"Tile: ({_inspectedCharacter.Col}, {_inspectedCharacter.Row})",
            _propStack.Next(20), new Color(120, 115, 160));

        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            $"ID: {_inspectedCharacter.Name}",
            _propStack.Next(20), LayoutDraw.DimText);

        LayoutDraw.DividerLine(_sb, _propStack.Divider());
        DrawInteractionSelector(mp, _inspectedCharacter.InteractionId,
            id => _inspectedCharacter.InteractionId = id);

        _propStack.Space(6);
        _propDeleteRect = _propStack.NextFromBottom(40);
        bool hovChDel = _propDeleteRect.Contains(mp);
        LayoutDraw.Rect(_sb, _propDeleteRect, hovChDel ? new Color(80, 18, 18) : new Color(45, 14, 14));
        LayoutDraw.BorderRect(_sb, _propDeleteRect, hovChDel ? LayoutDraw.Accent : new Color(120, 40, 40));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove from Room", _propDeleteRect,
            hovChDel ? Color.White : new Color(220, 100, 100));
    }

    // -----------------------------------------------------------------------
    // Interaction selector
    // -----------------------------------------------------------------------
    private void DrawInteractionSelector(Vector2 mp, string currentId, Action<string> setCallback)
    {
        _interactionSetCallback = setCallback;

        LayoutDraw.SectionHeader(_sb, _propStack.Next(22), "Interaction:");
        _propStack.Space(4);

        var current = InteractionStore.FindById(currentId);

        if (current != null)
        {
            var infoRect = _propStack.Next(28);
            LayoutDraw.Rect(_sb, infoRect, new Color(20, 18, 40));
            LayoutDraw.BorderRect(_sb, infoRect, new Color(90, 60, 140));
            string displayName = string.IsNullOrEmpty(current.Name) ? "(unnamed)" : current.Name;
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, displayName, infoRect, new Color(180, 140, 220), 8);

            _propStack.Space(4);

            if (current.Root.Lines.Count > 0)
            {
                int previewH   = Math.Min(60, current.Root.Lines.Count * 20 + 8);
                var previewRect = _propStack.Next(previewH);
                LayoutDraw.Rect(_sb, previewRect, new Color(14, 12, 24));
                int py = previewRect.Y + 4;
                foreach (var line in current.Root.Lines)
                {
                    if (py > previewRect.Bottom - 16) break;
                    _sb.DrawString(Assets.MenuFont, line,
                        new Vector2(previewRect.X + 8, py), new Color(120, 115, 150));
                    py += 18;
                }
            }

            _propStack.Space(4);

            var btnRow = _propStack.Next(30);
            int halfW  = (btnRow.Width - 8) / 2;

            _interactionSelectRect = new Rectangle(btnRow.X, btnRow.Y, halfW, btnRow.Height);
            LayoutDraw.Button(_sb, _interactionSelectRect, "Change", mp);

            _interactionClearRect = new Rectangle(btnRow.X + halfW + 8, btnRow.Y, halfW, btnRow.Height);
            bool hovClear = _interactionClearRect.Contains(mp);
            LayoutDraw.Rect(_sb, _interactionClearRect,
                hovClear ? new Color(60, 20, 20) : new Color(35, 18, 18));
            LayoutDraw.BorderRect(_sb, _interactionClearRect,
                hovClear ? LayoutDraw.Accent : new Color(100, 50, 50));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove", _interactionClearRect,
                hovClear ? Color.White : new Color(200, 120, 120));
        }
        else
        {
            if (InteractionStore.Interactions.Count > 0)
            {
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No interaction set.",
                    _propStack.Next(20), LayoutDraw.DimText);
                _propStack.Space(4);
                _interactionSelectRect = _propStack.Next(32);
                bool hovSel = _interactionSelectRect.Contains(mp);
                LayoutDraw.Rect(_sb, _interactionSelectRect,
                    hovSel ? new Color(25, 18, 40) : new Color(18, 14, 30));
                LayoutDraw.BorderRect(_sb, _interactionSelectRect,
                    hovSel ? new Color(160, 100, 220) : new Color(80, 55, 120));
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Assign Interaction",
                    _interactionSelectRect,
                    hovSel ? Color.White : new Color(160, 120, 210));
                _interactionClearRect = Rectangle.Empty;
            }
            else
            {
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No interactions created.",
                    _propStack.Next(20), LayoutDraw.DimText);
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Create one in the level",
                    _propStack.Next(18), new Color(60, 58, 85));
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "editor first.",
                    _propStack.Next(18), new Color(60, 58, 85));
                _interactionSelectRect = Rectangle.Empty;
            }
            _interactionClearRect = Rectangle.Empty;
        }

        if (_showInteractionDropdown)
            DrawInteractionDropdown(mp);
    }

    private void DrawInteractionDropdown(Vector2 mp)
    {
        var interactions = InteractionStore.Interactions;
        int entryH  = 50;
        int dropW   = PropW - 24;
        int dropH   = Math.Min(interactions.Count * (entryH + 2) + 8, 300);
        int dropX   = _interactionSelectRect.X;
        int dropY   = _interactionSelectRect.Bottom + 4;

        var vp = _game.GraphicsDevice.Viewport;
        if (dropY + dropH > vp.Height - 20)
            dropY = _interactionSelectRect.Y - dropH - 4;

        var dropRect = new Rectangle(dropX, dropY, dropW, dropH);
        LayoutDraw.Rect(_sb, new Rectangle(dropRect.X + 3, dropRect.Y + 3, dropRect.Width, dropRect.Height),
            new Color(0, 0, 0, 140));
        LayoutDraw.Rect(_sb, dropRect, new Color(18, 16, 30));
        LayoutDraw.BorderRect(_sb, dropRect, new Color(120, 80, 200));

        _interactionDropdownRects.Clear();
        int y = dropRect.Y + 4;
        for (int i = 0; i < interactions.Count; i++)
        {
            if (y + entryH > dropRect.Bottom - 4) break;
            var inter     = interactions[i];
            var entryRect = new Rectangle(dropRect.X + 4, y, dropRect.Width - 8, entryH);
            _interactionDropdownRects.Add(entryRect);

            bool hov       = entryRect.Contains(mp);
            string curId   = _inspectedObject?.InteractionId ?? _inspectedCharacter?.InteractionId;
            bool isCurrent = inter.Id == curId;

            LayoutDraw.Rect(_sb, entryRect,
                isCurrent ? new Color(25, 18, 40) : hov ? new Color(30, 28, 48) : new Color(16, 14, 26));
            if (isCurrent)
                LayoutDraw.Rect(_sb, new Rectangle(entryRect.X, entryRect.Y, 3, entryRect.Height),
                    new Color(120, 80, 200));

            string name = string.IsNullOrEmpty(inter.Name) ? "(unnamed)" : inter.Name;
            _sb.DrawString(Assets.MenuFont, name,
                new Vector2(entryRect.X + 10, entryRect.Y + 4),
                hov || isCurrent ? Color.White : new Color(160, 155, 190));

            string info = $"{inter.Root.Lines.Count} lines, {inter.Root.Choices.Count} choices";
            _sb.DrawString(Assets.MenuFont, info,
                new Vector2(entryRect.X + 10, entryRect.Y + 24), new Color(70, 65, 95));

            y += entryH + 2;
        }
    }

    // -----------------------------------------------------------------------
    // Grid drawing
    // -----------------------------------------------------------------------
    private void DrawGrid(GameTime gameTime)
    {
        double t = gameTime.TotalGameTime.TotalSeconds;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int  px      = GridOriginX + col * TilePx;
                int  py      = GridOriginY + row * TilePx;
                bool filled  = _shape.IsFilled(col, row);
                bool hovered = col == _hoverCol && row == _hoverRow;
                bool inspObj = _inspectedObject    != null && _inspectedObject.Col    == col && _inspectedObject.Row    == row;
                bool inspCh  = _inspectedCharacter != null && _inspectedCharacter.Col == col && _inspectedCharacter.Row == row;

                Color bg;
                if (filled)
                    bg = (inspObj || inspCh) ? new Color(50, 30, 60)
                       : hovered             ? _floorHov
                       :                       _floorColor;
                else
                    bg = hovered && _activeTab == SidebarTab.Brushes && _brush == Brush.Add
                        ? new Color(25, 22, 42) : _emptyColor;

                LayoutDraw.Rect(_sb, new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2), bg);
                LayoutDraw.Rect(_sb, new Rectangle(px, py, TilePx, 1), _gridLine);
                LayoutDraw.Rect(_sb, new Rectangle(px, py, 1, TilePx), _gridLine);

                if (filled)
                {
                    // Objects
                    var obj = _shape.ObjectAt(col, row);
                    if (obj != null)
                    {
                        Color oc = new Color(80, 65, 90);
                        char  ic = '?';
                        foreach (var def in ObjectCatalogue)
                            if (def.Type == obj.Type) { oc = def.Colour; ic = def.Icon; break; }
                        var objR = new Rectangle(px + 4, py + 4, TilePx - 8, TilePx - 8);
                        LayoutDraw.Rect(_sb, objR, oc);
                        _sb.DrawString(Assets.MenuFont, ic.ToString(), new Vector2(px + 8, py + 7), Color.White);
                        if (!string.IsNullOrEmpty(obj.InteractionId))
                            LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - 8, py + 2, 6, 6), new Color(120, 80, 200));
                    }

                    // Characters
                    var ch = _shape.CharacterAt(col, row);
                    if (ch != null)
                    {
                        // Try to show portrait thumbnail, fall back to coloured block
                        var chProfile = CharacterData.Characters.Find(c => c.Id == ch.Name);
                        var chR       = new Rectangle(px + 3, py + 3, TilePx - 6, TilePx - 6);
                        if (chProfile?.Portrait != null)
                            _sb.Draw(chProfile.Portrait, chR, Color.White);
                        else
                        {
                            LayoutDraw.Rect(_sb, chR, _charColor);
                            _sb.DrawString(Assets.MenuFont, "@", new Vector2(px + 7, py + 7), Color.White);
                        }
                        if (!string.IsNullOrEmpty(ch.InteractionId))
                            LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - 8, py + 2, 6, 6), new Color(120, 80, 200));
                    }
                }

                // Wall edges
                DrawWalls(col, row, px, py, t, _shape.GetWalls(col, row));
            }
        }

        // Outer border
        LayoutDraw.Rect(_sb, new Rectangle(GridOriginX, GridOriginY + GridPx, GridPx, 1), _gridLine);
        LayoutDraw.Rect(_sb, new Rectangle(GridOriginX + GridPx, GridOriginY, 1, GridPx + 1), _gridLine);
    }

    private void DrawWalls(int col, int row, int px, int py, double t, WallEdges walls)
    {
        if (walls == WallEdges.None) return;

        bool hovWall = col == _hoverCol && row == _hoverRow && _activeTab == SidebarTab.Brushes;
        float pulse  = hovWall ? (float)(Math.Sin(t * 6f) * 0.2f + 0.8f) : 1f;
        var   wc     = new Color(
            (int)(_wallColor.R * pulse),
            (int)(_wallColor.G * pulse),
            (int)(_wallColor.B * pulse));

        if ((walls & WallEdges.North) != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py, TilePx, WallThick), wc);
        if ((walls & WallEdges.South) != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py + TilePx - WallThick, TilePx, WallThick), wc);
        if ((walls & WallEdges.West)  != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py, WallThick, TilePx), wc);
        if ((walls & WallEdges.East)  != 0) LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - WallThick, py, WallThick, TilePx), wc);
    }

    // -----------------------------------------------------------------------
    // Text input (properties panel name field only)
    // -----------------------------------------------------------------------
    private void HandleCharPropNameInput(KeyboardState keys)
    {
        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back)  { if (_propCharName.Length > 0) _propCharName = _propCharName[..^1]; continue; }
            if (key == Keys.Enter) { _editingCharPropName = false; continue; }
            char? c = KeyToChar(key, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
            if (c.HasValue && _propCharName.Length < 30) _propCharName += c.Value;
        }
        if (_inspectedCharacter != null) _inspectedCharacter.Name = _propCharName;
    }

    private List<Keys> NewKeys(KeyboardState keys)
    {
        var result  = new List<Keys>();
        var prevSet = new HashSet<Keys>(_prevPressedKeys);
        foreach (var key in keys.GetPressedKeys())
            if (!prevSet.Contains(key)) result.Add(key);
        return result;
    }

    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z)
        { char c = (char)('a' + (key - Keys.A)); return shift ? char.ToUpper(c) : c; }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (!shift) return (char)('0' + (key - Keys.D0));
            return (key - Keys.D0) switch
            { 1=>'!', 2=>'@', 3=>'#', 4=>'$', 5=>'%', 6=>'^', 7=>'&', 8=>'*', 9=>'(', 0=>')', _=>null };
        }
        return key switch
        {
            Keys.Space             => ' ',
            Keys.OemPeriod         => shift ? '>' : '.',
            Keys.OemComma          => shift ? '<' : ',',
            Keys.OemQuestion       => shift ? '?' : '/',
            Keys.OemSemicolon      => shift ? ':' : ';',
            Keys.OemQuotes         => shift ? '"' : '\'',
            Keys.OemMinus          => shift ? '_' : '-',
            Keys.OemPlus           => shift ? '+' : '=',
            Keys.OemOpenBrackets   => shift ? '{' : '[',
            Keys.OemCloseBrackets  => shift ? '}' : ']',
            Keys.OemPipe           => shift ? '|' : '\\',
            Keys.OemTilde          => shift ? '~' : '`',
            _                      => null
        };
    }

    // -----------------------------------------------------------------------
    // Save
    // -----------------------------------------------------------------------
    private void SaveAndReturn()
    {
        CloseProperties();
        _onSave?.Invoke(_shape);
        NavigationBus.RequestNavigate("LevelEditor");
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}