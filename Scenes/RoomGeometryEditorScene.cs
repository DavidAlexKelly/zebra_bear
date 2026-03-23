//======== Scenes/RoomGeometryEditorScene.cs ========
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

public class RoomGeometryEditorScene : IScene
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------
    private const int GridSize  = RoomShapeData.MaxSize;
    private const int TilePx   = 30;
    private const int WallThick = 4;
    private const int SideW    = 240;
    private const int PropW    = 300;

    private int GridOriginX => SideW + 16;
    private int GridOriginY => 56;
    private int GridPx => GridSize * TilePx;

    // -----------------------------------------------------------------------
    // Object catalogue
    // -----------------------------------------------------------------------
    private static readonly ObjectDef[] ObjectCatalogue = new[]
    {
        new ObjectDef("table",       "Table",       new Color(120, 85, 55),  'T'),
        new ObjectDef("chair",       "Chair",       new Color(100, 70, 45),  'C'),
        new ObjectDef("pillar",      "Pillar",      new Color( 70, 68, 90),  'P'),
        new ObjectDef("shelf",       "Shelf",       new Color(120, 95, 65),  'S'),
        new ObjectDef("orientedBox", "Wall Object", new Color( 80, 65, 90),  'W'),
        new ObjectDef("box",         "Crate",       new Color( 90, 80, 55),  'B'),
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
    private readonly Game        _game;
    private readonly SpriteBatch _sb;

    private string               _roomId;
    private string               _roomLabel;
    private RoomShapeData        _shape;
    private Action<RoomShapeData> _onSave;
    private RoomEditorContext    _context;

    // Layout helpers
    private readonly VStack _sideStack = new() { Padding = 10, Spacing = 6 };
    private readonly VStack _propStack = new() { Padding = 12, Spacing = 8 };
    private readonly HStack _tabRow    = new() { Padding = 0, Spacing = 2 };

    // Sidebar tab
    private SidebarTab _activeTab = SidebarTab.Brushes;

    // Brushes tab state
    private enum Brush { Add, Remove }
    private Brush _brush = Brush.Add;
    private bool  _painting;
    private bool  _paintFill;

    // Objects tab state
    private int  _selectedObjectIdx = 0;
    private bool _placingObject = false;

    // Characters tab state
    private bool _placingCharacter = false;
    private string _newCharName = "";
    private bool _editingCharName = false;

    // Properties panel
    private PlacedObject _inspectedObject = null;
    private PlacedCharacter _inspectedCharacter = null;
    private string _propCharName = "";
    private bool _editingCharPropName = false;

    // Interaction selector
    private bool _showInteractionDropdown = false;
    private Rectangle _interactionSelectRect;
    private Rectangle _interactionClearRect;
    private List<Rectangle> _interactionDropdownRects = new();
    private Action<string> _interactionSetCallback;

    // Cached layout rects
    private Rectangle _tabBrushesRect, _tabObjectsRect, _tabCharsRect;
    private Rectangle _addBrushRect, _removeBrushRect, _clearRect;
    private Rectangle _saveRect, _backRect;
    private List<Rectangle> _objRects = new();
    private Rectangle _propCloseRect;
    private Rectangle _propCharNameRect;
    private Rectangle _charNameRect, _charPlaceRect;

    // Hover
    private int _hoverCol = -1;
    private int _hoverRow = -1;

    // Input
    private MouseState    _prevMouse;
    private KeyboardState _prevKeys;
    private Keys[] _prevPressedKeys = Array.Empty<Keys>();

    // Colours
    private readonly Color _floorColor = new Color(30, 28, 48);
    private readonly Color _floorHov   = new Color(45, 42, 68);
    private readonly Color _wallColor  = new Color(180, 60, 80);
    private readonly Color _emptyColor = new Color(14, 13, 22);
    private readonly Color _gridLine   = new Color(22, 20, 36);
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
        _inspectedObject = null;
        _inspectedCharacter = null;
        _editingCharPropName = false;
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
        _painting = false;
        _placingObject = false;
        _placingCharacter = false;
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

        bool clicked      = mouse.LeftButton  == ButtonState.Released &&
                            _prevMouse.LeftButton == ButtonState.Pressed;
        bool rightClicked = mouse.RightButton == ButtonState.Released &&
                            _prevMouse.RightButton == ButtonState.Pressed;

        // ── Interaction dropdown intercept ─────────────────────────────────
        if (_showInteractionDropdown && clicked)
        {
            bool hitDrop = false;
            for (int i = 0; i < _interactionDropdownRects.Count; i++)
            {
                if (_interactionDropdownRects[i].Contains(mp) && i < InteractionStore.Interactions.Count)
                {
                    _interactionSetCallback?.Invoke(InteractionStore.Interactions[i].Id);
                    hitDrop = true;
                    break;
                }
            }
            _showInteractionDropdown = false;
            if (hitDrop) { SaveInput(mouse, keys); return; }
        }

        // ── Text editing intercepts ───────────────────────────────────────
        if ((_editingCharName || _editingCharPropName) && clicked)
        {
            if (_editingCharName && !_charNameRect.Contains(mp)) _editingCharName = false;
            if (_editingCharPropName && !_propCharNameRect.Contains(mp)) _editingCharPropName = false;
        }

        if (_editingCharName)
        {
            HandleCharNameInput(keys);
            if (IsPressed(keys, _prevKeys, Keys.Escape)) _editingCharName = false;
            SaveInput(mouse, keys);
            return;
        }

        if (_editingCharPropName)
        {
            HandleCharPropNameInput(keys);
            if (IsPressed(keys, _prevKeys, Keys.Escape)) _editingCharPropName = false;
            SaveInput(mouse, keys);
            return;
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────
        if (IsPressed(keys, _prevKeys, Keys.Escape)) SaveAndReturn();

        if (_activeTab == SidebarTab.Brushes)
        {
            if (IsPressed(keys, _prevKeys, Keys.A) || IsPressed(keys, _prevKeys, Keys.D1)) _brush = Brush.Add;
            if (IsPressed(keys, _prevKeys, Keys.R) || IsPressed(keys, _prevKeys, Keys.D2)) _brush = Brush.Remove;
            if (IsPressed(keys, _prevKeys, Keys.Back)) _shape.FillDefault();
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
        { _activeTab = SidebarTab.Brushes; _placingObject = false; _placingCharacter = false; }
        if (clicked && _tabObjectsRect.Contains(mp))
        { _activeTab = SidebarTab.Objects; _painting = false; _placingCharacter = false; }
        if (clicked && _tabCharsRect.Contains(mp))
        { _activeTab = SidebarTab.Characters; _painting = false; _placingObject = false; }

        // ── Properties panel interaction ──────────────────────────────────
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
        }
        AfterProp:

        // ── Sidebar button clicks ─────────────────────────────────────────
        if (clicked && _saveRect.Contains(mp)) SaveAndReturn();
        if (clicked && _backRect.Contains(mp)) SaveAndReturn();

        // ── Brushes tab buttons ───────────────────────────────────────────
        if (_activeTab == SidebarTab.Brushes && clicked)
        {
            if (_addBrushRect.Contains(mp)) _brush = Brush.Add;
            if (_removeBrushRect.Contains(mp)) _brush = Brush.Remove;
            if (_clearRect.Contains(mp)) _shape.FillDefault();
        }

        // ── Objects tab: select object type ──────────────────────────────
        if (_activeTab == SidebarTab.Objects && clicked)
        {
            for (int i = 0; i < _objRects.Count; i++)
                if (_objRects[i].Contains(mp)) { _selectedObjectIdx = i; _placingObject = true; }
        }

        // ── Characters tab: buttons ──────────────────────────────────────
        if (_activeTab == SidebarTab.Characters && clicked)
        {
            if (_charNameRect.Contains(mp)) _editingCharName = true;
            if (_charPlaceRect.Contains(mp) && _newCharName.Length > 0) _placingCharacter = true;
        }

        // ── Grid hover ───────────────────────────────────────────────────
        _hoverCol = -1;
        _hoverRow = -1;
        var gridRect = new Rectangle(GridOriginX, GridOriginY, GridPx, GridPx);
        if (gridRect.Contains(mp))
        {
            _hoverCol = (int)((mp.X - GridOriginX) / TilePx);
            _hoverRow = (int)((mp.Y - GridOriginY) / TilePx);
            _hoverCol = Math.Clamp(_hoverCol, 0, GridSize - 1);
            _hoverRow = Math.Clamp(_hoverRow, 0, GridSize - 1);
        }

        // ── Brushes tab: paint tiles ──────────────────────────────────────
        if (_activeTab == SidebarTab.Brushes)
        {
            if (_hoverCol >= 0 && mouse.LeftButton == ButtonState.Pressed &&
                _prevMouse.LeftButton == ButtonState.Released)
            { _painting = true; _paintFill = (_brush == Brush.Add); }
            if (_painting)
            {
                if (mouse.LeftButton == ButtonState.Released) _painting = false;
                else if (_hoverCol >= 0)
                { if (_paintFill) _shape.Fill(_hoverCol, _hoverRow); else _shape.Clear(_hoverCol, _hoverRow); }
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
            if (rightClicked)
            {
                var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (obj != null) { if (_inspectedObject == obj) CloseProperties(); _shape.RemoveObject(obj); }
            }
        }

        // ── Characters tab: place / inspect / remove ──────────────────────
        if (_activeTab == SidebarTab.Characters && _hoverCol >= 0)
        {
            if (clicked && _placingCharacter && _shape.IsFilled(_hoverCol, _hoverRow))
            {
                var existingCh = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (existingCh != null) { if (_inspectedCharacter == existingCh) CloseProperties(); _shape.RemoveCharacter(existingCh); }
                var existingObj = _shape.ObjectAt(_hoverCol, _hoverRow);
                if (existingObj != null) { if (_inspectedObject == existingObj) CloseProperties(); _shape.RemoveObject(existingObj); }

                _shape.AddCharacter(new PlacedCharacter
                { Name = _newCharName, Col = _hoverCol, Row = _hoverRow });
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
            if (rightClicked)
            {
                var ch = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (ch != null) { if (_inspectedCharacter == ch) CloseProperties(); _shape.RemoveCharacter(ch); }
            }
        }

        if (clicked && _hoverCol >= 0 && _activeTab == SidebarTab.Brushes)
            CloseProperties();

        SaveInput(mouse, keys);
    }

    private void SaveInput(MouseState mouse, KeyboardState keys)
    {
        _prevMouse = mouse;
        _prevKeys  = keys;
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

        // ── LEFT SIDEBAR ──────────────────────────────────────────────────
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
        _tabCharsRect = _tabRow.Remaining();
        LayoutDraw.Tab(_sb, _tabBrushesRect, "Brush", _activeTab == SidebarTab.Brushes, mp);
        LayoutDraw.Tab(_sb, _tabObjectsRect, "Object", _activeTab == SidebarTab.Objects, mp);
        LayoutDraw.Tab(_sb, _tabCharsRect, "Chars", _activeTab == SidebarTab.Characters, mp);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Header
        var headerRect = _sideStack.Next(20);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "ROOM EDITOR", headerRect, LayoutDraw.Accent);
        var labelRect = _sideStack.Next(20);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, _roomLabel ?? "", labelRect, Color.White);
        var infoRect = _sideStack.Next(20);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            $"Tiles: {_shape?.TileCount ?? 0}  Obj: {_shape?.Objects.Count ?? 0}  Chars: {_shape?.Characters.Count ?? 0}",
            infoRect, new Color(120, 115, 160));

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Bottom buttons
        _backRect = _sideStack.NextFromBottom(44);
        _saveRect = _sideStack.NextFromBottom(44);
        LayoutDraw.Button(_sb, _saveRect, "Save + Return", mp);
        LayoutDraw.Button(_sb, _backRect, "< Back", mp);

        // Tab content
        switch (_activeTab)
        {
            case SidebarTab.Brushes: DrawBrushesTab(mp); break;
            case SidebarTab.Objects: DrawObjectsTab(mp); break;
            case SidebarTab.Characters: DrawCharactersTab(mp); break;
        }

        // ── GRID ──────────────────────────────────────────────────────────
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
                _sb.DrawString(Assets.MenuFont, _newCharName,
                    new Vector2(mpos.X + 14, mpos.Y - 8), _charColor);
            }
            else
            {
                var obj = _shape.ObjectAt(_hoverCol, _hoverRow);
                var ch = _shape.CharacterAt(_hoverCol, _hoverRow);
                if (obj != null || ch != null)
                    _sb.DrawString(Assets.MenuFont, "[Click] Inspect  [RMB] Remove",
                        new Vector2(mpos.X + 14, mpos.Y - 8), LayoutDraw.TextNormal);
            }
        }

        // ── RIGHT SIDEBAR: Properties ─────────────────────────────────────
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
        _addBrushRect = _sideStack.Next(44);
        DrawBrushButton(_addBrushRect, "Add Space [A]", _brush == Brush.Add, mp);
        _removeBrushRect = _sideStack.Next(44);
        DrawBrushButton(_removeBrushRect, "Remove Space [R]", _brush == Brush.Remove, mp);
        _sideStack.Space(4);
        _clearRect = _sideStack.Next(44);
        LayoutDraw.Button(_sb, _clearRect, "Reset Default", mp);

        _sideStack.Space(8);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        var helpCol = new Color(60, 58, 85);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[A] Add  [R] Remove", _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[Bksp] Reset grid", _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[Esc] Save + return", _sideStack.Next(18), helpCol);
    }

    private void DrawBrushButton(Rectangle r, string label, bool active, Vector2 mp)
    {
        bool hov = r.Contains(mp);
        LayoutDraw.Rect(_sb, r, active ? new Color(50, 18, 30) : hov ? new Color(30, 26, 46) : new Color(20, 18, 32));
        LayoutDraw.BorderRect(_sb, r, active ? LayoutDraw.Accent : hov ? new Color(120, 110, 160) : LayoutDraw.Border);
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, label, r, active ? Color.White : LayoutDraw.TextNormal);
    }

    // -----------------------------------------------------------------------
    // Objects tab
    // -----------------------------------------------------------------------
    private void DrawObjectsTab(Vector2 mp)
    {
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click to select, then", _sideStack.Next(18), new Color(80, 75, 110));
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "click a tile to place.", _sideStack.Next(18), new Color(80, 75, 110));
        _sideStack.Space(4);

        _objRects.Clear();
        for (int i = 0; i < ObjectCatalogue.Length; i++)
        {
            var def = ObjectCatalogue[i];
            var r = _sideStack.Next(40);
            _objRects.Add(r);

            bool sel = (_selectedObjectIdx == i && _placingObject);
            bool hov = r.Contains(mp);
            LayoutDraw.Rect(_sb, r, sel ? new Color(40, 20, 50) : hov ? new Color(30, 26, 44) : new Color(20, 18, 32));
            LayoutDraw.BorderRect(_sb, r, sel ? LayoutDraw.Accent : hov ? new Color(120, 110, 160) : LayoutDraw.Border);

            var sw = new Rectangle(r.X + 6, r.Y + (r.Height - 18) / 2, 18, 18);
            LayoutDraw.Rect(_sb, sw, def.Colour);
            LayoutDraw.BorderRect(_sb, sw, LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, def.Icon.ToString(), new Vector2(r.X + 8, r.Y + (r.Height - 18) / 2 + 1), new Color(240, 240, 240));
            _sb.DrawString(Assets.MenuFont, def.Label, new Vector2(r.X + 32, r.Y + (r.Height - 18) / 2), sel ? Color.White : LayoutDraw.TextNormal);

            if (sel)
            {
                var tsz = Assets.MenuFont.MeasureString("PLACING");
                _sb.DrawString(Assets.MenuFont, "PLACING", new Vector2(r.Right - tsz.X - 6, r.Y + (r.Height - tsz.Y) / 2), LayoutDraw.Accent);
            }
        }

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());
        var helpCol = new Color(60, 58, 85);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"Placed: {_shape.Objects.Count}", _sideStack.Next(18), new Color(80, 75, 110));
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[RMB] Remove object", _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click placed to inspect", _sideStack.Next(18), helpCol);
    }

    // -----------------------------------------------------------------------
    // Characters tab
    // -----------------------------------------------------------------------
    private void DrawCharactersTab(Vector2 mp)
    {
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), "Place a Character:");
        _sideStack.Space(4);

        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Name:", _sideStack.Next(20), new Color(120, 115, 160));
        _charNameRect = _sideStack.Next(40);
        LayoutDraw.TextArea(_sb, _charNameRect, _newCharName, _editingCharName, mp, "Enter name...");

        _sideStack.Space(4);

        _charPlaceRect = _sideStack.Next(44);
        bool canPlace = _newCharName.Length > 0;
        bool hovPlace = _charPlaceRect.Contains(mp);

        if (_placingCharacter)
        {
            LayoutDraw.Rect(_sb, _charPlaceRect, new Color(50, 30, 60));
            LayoutDraw.BorderRect(_sb, _charPlaceRect, _charColor);
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "PLACING - Click tile", _charPlaceRect, _charColor);
        }
        else if (canPlace)
        {
            LayoutDraw.Rect(_sb, _charPlaceRect, hovPlace ? new Color(40, 25, 55) : new Color(25, 18, 38));
            LayoutDraw.BorderRect(_sb, _charPlaceRect, hovPlace ? _charColor : new Color(120, 80, 160));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Place Character", _charPlaceRect,
                hovPlace ? Color.White : new Color(180, 140, 220));
        }
        else
        {
            LayoutDraw.Rect(_sb, _charPlaceRect, new Color(20, 18, 30));
            LayoutDraw.BorderRect(_sb, _charPlaceRect, LayoutDraw.Border);
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Enter name first", _charPlaceRect, LayoutDraw.DimText);
        }

        _sideStack.Space(8);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), $"Characters ({_shape.Characters.Count}):");

        foreach (var ch in _shape.Characters)
        {
            if (_sideStack.IsFull) break;
            var entryRect = _sideStack.Next(28);
            bool isSel = _inspectedCharacter == ch;
            bool hov = entryRect.Contains(mp);

            LayoutDraw.Rect(_sb, entryRect, isSel ? new Color(35, 18, 45) : hov ? new Color(24, 20, 36) : Color.Transparent);
            if (isSel) LayoutDraw.Rect(_sb, new Rectangle(entryRect.X, entryRect.Y, 3, entryRect.Height), _charColor);

            LayoutDraw.Rect(_sb, new Rectangle(entryRect.X + 6, entryRect.Y + 4, 20, 20), _charColor);
            LayoutDraw.BorderRect(_sb, new Rectangle(entryRect.X + 6, entryRect.Y + 4, 20, 20), LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, "@", new Vector2(entryRect.X + 8, entryRect.Y + 5), Color.White);
            _sb.DrawString(Assets.MenuFont, ch.Name, new Vector2(entryRect.X + 32, entryRect.Y + 4),
                isSel ? Color.White : new Color(160, 140, 190));
        }

        _sideStack.Space(4);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());
        var helpCol = new Color(60, 58, 85);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "[RMB] Remove character", _sideStack.Next(18), helpCol);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Click placed to inspect", _sideStack.Next(18), helpCol);
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

        // Title + close
        var titleRow = _propStack.Next(30);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "PROPERTIES", titleRow, LayoutDraw.Accent);
        _propCloseRect = new Rectangle(titleRow.Right - 30, titleRow.Y, 30, 30);
        bool hovClose = _propCloseRect.Contains(mp);
        LayoutDraw.Rect(_sb, _propCloseRect, hovClose ? new Color(80, 20, 30) : new Color(30, 28, 48));
        LayoutDraw.BorderRect(_sb, _propCloseRect, hovClose ? LayoutDraw.Accent : LayoutDraw.Border);
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "X", _propCloseRect, hovClose ? Color.White : LayoutDraw.TextNormal);

        LayoutDraw.DividerLine(_sb, _propStack.Divider());

        if (_inspectedObject != null)
            DrawObjectProperties(mp);
        else if (_inspectedCharacter != null)
            DrawCharacterProperties(mp);
    }

    private void DrawObjectProperties(Vector2 mp)
    {
        string typeLabel = _inspectedObject.Type;
        Color typeColor = new Color(100, 100, 140);
        char typeIcon = '?';
        foreach (var def in ObjectCatalogue)
            if (def.Type == _inspectedObject.Type)
            { typeLabel = def.Label; typeColor = def.Colour; typeIcon = def.Icon; break; }

        var typeRect = _propStack.Next(26);
        var sw = new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22);
        LayoutDraw.Rect(_sb, sw, typeColor);
        LayoutDraw.BorderRect(_sb, sw, LayoutDraw.Border);
        _sb.DrawString(Assets.MenuFont, typeIcon.ToString(), new Vector2(sw.X + 3, sw.Y + 2), Color.White);
        _sb.DrawString(Assets.MenuFont, typeLabel, new Vector2(typeRect.X + 30, typeRect.Y), Color.White);

        LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"Tile: ({_inspectedObject.Col}, {_inspectedObject.Row})",
            _propStack.Next(20), new Color(120, 115, 160));

        LayoutDraw.DividerLine(_sb, _propStack.Divider());

        DrawInteractionSelector(mp, _inspectedObject.InteractionId,
            id => _inspectedObject.InteractionId = id);
    }

    private void DrawCharacterProperties(Vector2 mp)
    {
        var typeRect = _propStack.Next(26);
        LayoutDraw.Rect(_sb, new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22), _charColor);
        LayoutDraw.BorderRect(_sb, new Rectangle(typeRect.X, typeRect.Y + 2, 22, 22), LayoutDraw.Border);
        _sb.DrawString(Assets.MenuFont, "@", new Vector2(typeRect.X + 4, typeRect.Y + 3), Color.White);
        _sb.DrawString(Assets.MenuFont, "Character", new Vector2(typeRect.X + 30, typeRect.Y), Color.White);

        LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"Tile: ({_inspectedCharacter.Col}, {_inspectedCharacter.Row})",
            _propStack.Next(20), new Color(120, 115, 160));

        LayoutDraw.DividerLine(_sb, _propStack.Divider());

        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Name:", _propStack.Next(20), new Color(120, 115, 160));
        _propCharNameRect = _propStack.Next(40);
        LayoutDraw.TextArea(_sb, _propCharNameRect, _propCharName, _editingCharPropName, mp, "Enter name...");

        _propStack.Space(8);
        LayoutDraw.DividerLine(_sb, _propStack.Divider());

        DrawInteractionSelector(mp, _inspectedCharacter.InteractionId,
            id => _inspectedCharacter.InteractionId = id);
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

            // Preview lines
            if (current.Root.Lines.Count > 0)
            {
                int previewH = Math.Min(60, current.Root.Lines.Count * 20 + 8);
                var previewRect = _propStack.Next(previewH);
                LayoutDraw.Rect(_sb, previewRect, new Color(14, 12, 24));
                int py = previewRect.Y + 4;
                foreach (var line in current.Root.Lines)
                {
                    if (py > previewRect.Bottom - 16) break;
                    _sb.DrawString(Assets.MenuFont, line, new Vector2(previewRect.X + 8, py), new Color(120, 115, 150));
                    py += 18;
                }
            }

            _propStack.Space(4);

            var btnRow = _propStack.Next(30);
            int halfW = (btnRow.Width - 8) / 2;

            _interactionSelectRect = new Rectangle(btnRow.X, btnRow.Y, halfW, btnRow.Height);
            LayoutDraw.Button(_sb, _interactionSelectRect, "Change", mp);

            _interactionClearRect = new Rectangle(btnRow.X + halfW + 8, btnRow.Y, halfW, btnRow.Height);
            bool hovClear = _interactionClearRect.Contains(mp);
            LayoutDraw.Rect(_sb, _interactionClearRect, hovClear ? new Color(60, 20, 20) : new Color(35, 18, 18));
            LayoutDraw.BorderRect(_sb, _interactionClearRect, hovClear ? LayoutDraw.Accent : new Color(100, 50, 50));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove", _interactionClearRect,
                hovClear ? Color.White : new Color(200, 120, 120));
        }
        else
        {
            if (InteractionStore.Interactions.Count > 0)
            {
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No interaction set.", _propStack.Next(20), LayoutDraw.DimText);
                _propStack.Space(4);
                _interactionSelectRect = _propStack.Next(40);
                bool hovSel = _interactionSelectRect.Contains(mp);
                LayoutDraw.Rect(_sb, _interactionSelectRect, hovSel ? new Color(30, 20, 45) : new Color(20, 14, 32));
                LayoutDraw.BorderRect(_sb, _interactionSelectRect, hovSel ? new Color(160, 100, 220) : new Color(90, 60, 130));
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Attach Interaction...", _interactionSelectRect,
                    hovSel ? Color.White : new Color(160, 120, 210));
            }
            else
            {
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No interactions created.", _propStack.Next(20), LayoutDraw.DimText);
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Create one in the level", _propStack.Next(18), new Color(60, 58, 85));
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "editor first.", _propStack.Next(18), new Color(60, 58, 85));
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
        int entryH = 50;
        int dropW = PropW - 24;
        int dropH = Math.Min(interactions.Count * (entryH + 2) + 8, 300);
        int dropX = _interactionSelectRect.X;
        int dropY = _interactionSelectRect.Bottom + 4;

        var vp = _game.GraphicsDevice.Viewport;
        if (dropY + dropH > vp.Height - 20) dropY = _interactionSelectRect.Y - dropH - 4;

        var dropRect = new Rectangle(dropX, dropY, dropW, dropH);
        LayoutDraw.Rect(_sb, new Rectangle(dropRect.X + 3, dropRect.Y + 3, dropRect.Width, dropRect.Height), new Color(0, 0, 0, 140));
        LayoutDraw.Rect(_sb, dropRect, new Color(18, 16, 30));
        LayoutDraw.BorderRect(_sb, dropRect, new Color(120, 80, 200));

        _interactionDropdownRects.Clear();
        int y = dropRect.Y + 4;
        for (int i = 0; i < interactions.Count; i++)
        {
            if (y + entryH > dropRect.Bottom - 4) break;
            var inter = interactions[i];
            var entryRect = new Rectangle(dropRect.X + 4, y, dropRect.Width - 8, entryH);
            _interactionDropdownRects.Add(entryRect);

            bool hov = entryRect.Contains(mp);
            string currentId = _inspectedObject?.InteractionId ?? _inspectedCharacter?.InteractionId;
            bool isCurrent = inter.Id == currentId;

            LayoutDraw.Rect(_sb, entryRect, isCurrent ? new Color(25, 18, 40) : hov ? new Color(30, 28, 48) : new Color(16, 14, 26));
            if (isCurrent) LayoutDraw.Rect(_sb, new Rectangle(entryRect.X, entryRect.Y, 3, entryRect.Height), new Color(120, 80, 200));

            string name = string.IsNullOrEmpty(inter.Name) ? "(unnamed)" : inter.Name;
            _sb.DrawString(Assets.MenuFont, name, new Vector2(entryRect.X + 10, entryRect.Y + 4),
                hov || isCurrent ? Color.White : new Color(160, 155, 190));

            string info = $"{inter.Root.Lines.Count} lines, {inter.Root.Choices.Count} choices";
            _sb.DrawString(Assets.MenuFont, info, new Vector2(entryRect.X + 10, entryRect.Y + 24), new Color(70, 65, 95));

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
                int px = GridOriginX + col * TilePx;
                int py = GridOriginY + row * TilePx;
                bool filled = _shape.IsFilled(col, row);
                bool hovered = (col == _hoverCol && row == _hoverRow);
                bool inspObj = _inspectedObject != null && _inspectedObject.Col == col && _inspectedObject.Row == row;
                bool inspCh = _inspectedCharacter != null && _inspectedCharacter.Col == col && _inspectedCharacter.Row == row;

                Color bg;
                if (filled)
                    bg = (inspObj || inspCh) ? new Color(50, 30, 60) : hovered ? _floorHov : _floorColor;
                else
                    bg = hovered ? (_activeTab == SidebarTab.Brushes && _brush == Brush.Add ? new Color(38, 36, 58) : new Color(28, 14, 22)) : _emptyColor;

                LayoutDraw.Rect(_sb, new Rectangle(px, py, TilePx, TilePx), bg);
                LayoutDraw.Rect(_sb, new Rectangle(px, py, TilePx, 1), _gridLine);
                LayoutDraw.Rect(_sb, new Rectangle(px, py, 1, TilePx), _gridLine);

                if (filled) DrawWalls(px, py, _shape.GetWalls(col, row), hovered, t);

                if (_activeTab == SidebarTab.Brushes)
                {
                    if (hovered && !filled && _brush == Brush.Add)
                    { float a = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f); LayoutDraw.Rect(_sb, new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2), new Color(80, 200, 120) * a); }
                    if (hovered && filled && _brush == Brush.Remove)
                    { float a = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f); LayoutDraw.Rect(_sb, new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2), LayoutDraw.Accent * a); }
                }

                if (_activeTab == SidebarTab.Objects && hovered && filled && _placingObject)
                { float a = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f); LayoutDraw.Rect(_sb, new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2), ObjectCatalogue[_selectedObjectIdx].Colour * a); }

                if (_activeTab == SidebarTab.Characters && hovered && filled && _placingCharacter)
                { float a = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f); LayoutDraw.Rect(_sb, new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2), _charColor * a); }
            }
        }

        // Placed objects
        foreach (var obj in _shape.Objects)
        {
            int px = GridOriginX + obj.Col * TilePx;
            int py = GridOriginY + obj.Row * TilePx;
            Color objCol = new Color(100, 100, 140);
            char icon = '?';
            foreach (var def in ObjectCatalogue)
                if (def.Type == obj.Type) { objCol = def.Colour; icon = def.Icon; break; }

            bool hovObj = (obj.Col == _hoverCol && obj.Row == _hoverRow);
            bool inspObj = (_inspectedObject == obj);

            LayoutDraw.Rect(_sb, new Rectangle(px + 2, py + 2, TilePx - 4, TilePx - 4), objCol);
            LayoutDraw.BorderRect(_sb, new Rectangle(px + 2, py + 2, TilePx - 4, TilePx - 4),
                inspObj ? LayoutDraw.Accent : hovObj ? new Color(220, 80, 80) : Color.White * 0.4f);

            var isz = Assets.MenuFont.MeasureString(icon.ToString());
            _sb.DrawString(Assets.MenuFont, icon.ToString(),
                new Vector2(px + (TilePx - isz.X) / 2f, py + (TilePx - isz.Y) / 2f), Color.White);

            if (!string.IsNullOrEmpty(obj.InteractionId))
            {
                LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - 10, py + 2, 8, 8), new Color(60, 180, 80));
                LayoutDraw.BorderRect(_sb, new Rectangle(px + TilePx - 10, py + 2, 8, 8), new Color(40, 120, 50));
            }
        }

        // Placed characters
        foreach (var ch in _shape.Characters)
        {
            int px = GridOriginX + ch.Col * TilePx;
            int py = GridOriginY + ch.Row * TilePx;
            bool hovCh = (ch.Col == _hoverCol && ch.Row == _hoverRow);
            bool inspCh = (_inspectedCharacter == ch);

            LayoutDraw.Rect(_sb, new Rectangle(px + 2, py + 2, TilePx - 4, TilePx - 4), _charColor);
            LayoutDraw.BorderRect(_sb, new Rectangle(px + 2, py + 2, TilePx - 4, TilePx - 4),
                inspCh ? LayoutDraw.Accent : hovCh ? new Color(220, 160, 255) : Color.White * 0.4f);

            var isz = Assets.MenuFont.MeasureString("@");
            _sb.DrawString(Assets.MenuFont, "@",
                new Vector2(px + (TilePx - isz.X) / 2f, py + (TilePx - isz.Y) / 2f), Color.White);

            if (!string.IsNullOrEmpty(ch.InteractionId))
            {
                LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - 10, py + 2, 8, 8), new Color(60, 180, 80));
                LayoutDraw.BorderRect(_sb, new Rectangle(px + TilePx - 10, py + 2, 8, 8), new Color(40, 120, 50));
            }
        }

        LayoutDraw.BorderRect(_sb, new Rectangle(GridOriginX, GridOriginY, GridPx, GridPx), LayoutDraw.Border);
    }

    private void DrawWalls(int px, int py, WallEdges walls, bool hovered, double t)
    {
        float pulse = hovered ? (float)(Math.Sin(t * 6f) * 0.2f + 0.8f) : 1f;
        var wc = new Color((int)(_wallColor.R * pulse), (int)(_wallColor.G * pulse), (int)(_wallColor.B * pulse));
        if ((walls & WallEdges.North) != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py, TilePx, WallThick), wc);
        if ((walls & WallEdges.South) != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py + TilePx - WallThick, TilePx, WallThick), wc);
        if ((walls & WallEdges.West) != 0) LayoutDraw.Rect(_sb, new Rectangle(px, py, WallThick, TilePx), wc);
        if ((walls & WallEdges.East) != 0) LayoutDraw.Rect(_sb, new Rectangle(px + TilePx - WallThick, py, WallThick, TilePx), wc);
    }

    // -----------------------------------------------------------------------
    // Text input handlers
    // -----------------------------------------------------------------------
    private void HandleCharNameInput(KeyboardState keys)
    {
        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back) { if (_newCharName.Length > 0) _newCharName = _newCharName[..^1]; continue; }
            if (key == Keys.Enter) { _editingCharName = false; continue; }
            char? c = KeyToChar(key, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
            if (c.HasValue && _newCharName.Length < 30) _newCharName += c.Value;
        }
    }

    private void HandleCharPropNameInput(KeyboardState keys)
    {
        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back) { if (_propCharName.Length > 0) _propCharName = _propCharName[..^1]; continue; }
            if (key == Keys.Enter) { _editingCharPropName = false; continue; }
            char? c = KeyToChar(key, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
            if (c.HasValue && _propCharName.Length < 30) _propCharName += c.Value;
        }
        if (_inspectedCharacter != null) _inspectedCharacter.Name = _propCharName;
    }

    private List<Keys> NewKeys(KeyboardState keys)
    {
        var result = new List<Keys>();
        foreach (var key in keys.GetPressedKeys())
        {
            bool was = false;
            foreach (var prev in _prevPressedKeys) if (prev == key) { was = true; break; }
            if (!was) result.Add(key);
        }
        return result;
    }

    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z) { char c = (char)('a' + (key - Keys.A)); return shift ? char.ToUpper(c) : c; }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (!shift) return (char)('0' + (key - Keys.D0));
            return (key - Keys.D0) switch { 1=>'!',2=>'@',3=>'#',4=>'$',5=>'%',6=>'^',7=>'&',8=>'*',9=>'(',0=>')',_=>null };
        }
        return key switch
        {
            Keys.Space => ' ', Keys.OemPeriod => shift ? '>' : '.', Keys.OemComma => shift ? '<' : ',',
            Keys.OemQuestion => shift ? '?' : '/', Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'', Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPlus => shift ? '+' : '=', Keys.OemOpenBrackets => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']', Keys.OemPipe => shift ? '|' : '\\',
            Keys.OemTilde => shift ? '~' : '`', _ => null
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