using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;

namespace ZebraBear.Scenes;

/// <summary>
/// Birds-eye tile-grid room geometry editor.
///
/// Opened when the user clicks "Edit Room" on a room node in LevelEditorScene.
/// The editor paints floor tiles on a 20x20 grid. Walls are auto-derived:
/// any edge of a filled tile that borders an empty tile (or the grid border)
/// becomes a wall.
///
/// Brushes:
///   [A] Add Space  — left-click/drag to fill tiles
///   [R] Remove Space — left-click/drag to erase tiles
///
/// Controls:
///   A / 1        — select Add brush
///   R / 2        — select Remove brush
///   Backspace    — clear all tiles
///   Escape       — return to level editor (saving shape)
///   Save button  — same as Escape (explicit save)
/// </summary>
public class RoomGeometryEditorScene : IScene
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const int GridSize   = RoomShapeData.MaxSize; // 20 x 20
    private const int TilePx     = 30;                    // pixels per tile
    private const int WallThick  = 4;                     // wall stroke width (px)

    // Canvas origin (top-left of the grid)
    private const int GridOriginX = 260;
    private const int GridOriginY = 50;
    private const int GridPx      = GridSize * TilePx;    // 600 px

    // Sidebar
    private const int SideW = 240;

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly Game        _game;
    private readonly SpriteBatch _sb;

    // The room being edited + its shape data
    private string        _roomId;
    private string        _roomLabel;
    private RoomShapeData _shape;

    // Callback fired when the user saves and returns to LevelEditorScene
    private Action<RoomShapeData> _onSave;

    // Brush state
    private enum Brush { Add, Remove }
    private Brush _brush = Brush.Add;

    // Paint state — true while left mouse held
    private bool _painting;
    private bool _paintFill; // which value we're painting (depends on brush)

    // Tooltip / hover
    private int _hoverCol = -1;
    private int _hoverRow = -1;

    // Buttons
    private Rectangle _btnAdd;
    private Rectangle _btnRemove;
    private Rectangle _btnClear;
    private Rectangle _btnSave;
    private Rectangle _btnBack;

    // Input
    private MouseState    _prevMouse;
    private KeyboardState _prevKeys;

    // Colours
    private readonly Color _bg         = new Color(10, 10, 18);
    private readonly Color _panel      = new Color(16, 14, 28);
    private readonly Color _border     = new Color(55, 50, 85);
    private readonly Color _accent     = new Color(232, 0, 61);
    private readonly Color _floorColor = new Color(30, 28, 48);
    private readonly Color _floorHov   = new Color(45, 42, 68);
    private readonly Color _wallColor  = new Color(180, 60, 80);
    private readonly Color _emptyColor = new Color(14, 13, 22);
    private readonly Color _gridLine   = new Color(22, 20, 36);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public RoomGeometryEditorScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _sb   = spriteBatch;

        int bx = 10, by = 130, bw = SideW - 20, bh = 44, gap = 10;
        _btnAdd    = new Rectangle(bx, by,                   bw, bh);
        _btnRemove = new Rectangle(bx, by + (bh + gap),      bw, bh);
        _btnClear  = new Rectangle(bx, by + (bh + gap) * 2,  bw, bh);
        _btnSave   = new Rectangle(bx, 560,                  bw, bh);
        _btnBack   = new Rectangle(bx, 614,                  bw, bh);
    }

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Call this before transitioning into the scene to set which room to edit.
    /// </summary>
    public void OpenRoom(string roomId, string roomLabel,
                         RoomShapeData shape, Action<RoomShapeData> onSave)
    {
        _roomId    = roomId;
        _roomLabel = roomLabel;
        _shape     = shape;
        _onSave    = onSave;
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------

    public void Load() { }

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        _painting = false;
    }

    public void OnExit()
    {
        _game.IsMouseVisible = false;
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys  = Keyboard.GetState();
        var mp    = mouse.Position.ToVector2();

        // ── Keyboard shortcuts ────────────────────────────────────────────
        if (IsPressed(keys, _prevKeys, Keys.A) || IsPressed(keys, _prevKeys, Keys.D1))
            _brush = Brush.Add;
        if (IsPressed(keys, _prevKeys, Keys.R) || IsPressed(keys, _prevKeys, Keys.D2))
            _brush = Brush.Remove;
        if (IsPressed(keys, _prevKeys, Keys.Back))
            _shape.FillDefault();
        if (IsPressed(keys, _prevKeys, Keys.Escape))
            SaveAndReturn();

        // ── Button clicks ─────────────────────────────────────────────────
        bool clicked = mouse.LeftButton  == ButtonState.Released &&
                       _prevMouse.LeftButton == ButtonState.Pressed;

        if (clicked && _btnAdd.Contains(mp))    _brush = Brush.Add;
        if (clicked && _btnRemove.Contains(mp)) _brush = Brush.Remove;
        if (clicked && _btnClear.Contains(mp))  _shape.FillDefault();
        if (clicked && _btnSave.Contains(mp))   SaveAndReturn();
        if (clicked && _btnBack.Contains(mp))   SaveAndReturn();

        // ── Grid interaction ──────────────────────────────────────────────
        _hoverCol = -1;
        _hoverRow = -1;

        var gridRect = new Rectangle(GridOriginX, GridOriginY, GridPx, GridPx);

        if (gridRect.Contains(mp))
        {
            _hoverCol = (int)((mp.X - GridOriginX) / TilePx);
            _hoverRow = (int)((mp.Y - GridOriginY) / TilePx);
            _hoverCol = Math.Clamp(_hoverCol, 0, GridSize - 1);
            _hoverRow = Math.Clamp(_hoverRow, 0, GridSize - 1);

            // Start painting on press
            if (mouse.LeftButton == ButtonState.Pressed &&
                _prevMouse.LeftButton == ButtonState.Released)
            {
                _painting  = true;
                _paintFill = (_brush == Brush.Add);
            }
        }

        // Paint while held (even if mouse drifts outside for a moment)
        if (_painting)
        {
            if (mouse.LeftButton == ButtonState.Released)
            {
                _painting = false;
            }
            else if (_hoverCol >= 0)
            {
                if (_paintFill) _shape.Fill(_hoverCol, _hoverRow);
                else            _shape.Clear(_hoverCol, _hoverRow);
            }
        }

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

        // Background
        DrawRect(new Rectangle(0, 0, vp.Width, vp.Height), _bg);

        // ── Sidebar ───────────────────────────────────────────────────────
        DrawRect(new Rectangle(0, 0, SideW, vp.Height), _panel);
        DrawRect(new Rectangle(SideW, 0, 1, vp.Height), _border);

        DrawLabel("ROOM EDITOR", 10, 16, _accent);
        DrawLabel(_roomLabel, 10, 44, Color.White);
        DrawLabel($"Tiles: {_shape.TileCount}", 10, 68, new Color(120, 115, 160));
        DrawLabel("Brush:", 10, 100, new Color(80, 75, 110));

        DrawBrushButton(_btnAdd,    "Add Space  [A]",    _brush == Brush.Add);
        DrawBrushButton(_btnRemove, "Remove Space [R]",  _brush == Brush.Remove);

        // Clear / reset
        DrawButton(_btnClear, "Reset Default");

        // Legend
        int ly = _btnClear.Bottom + 24;
        DrawLabel("Legend:", 10, ly, new Color(80, 75, 110));
        ly += 24;
        DrawRect(new Rectangle(12, ly + 4, 16, 16), _floorColor);
        DrawBorder(new Rectangle(12, ly + 4, 16, 16), _border);
        DrawLabel("Floor tile", 36, ly, new Color(140, 135, 175));
        ly += 24;
        DrawRect(new Rectangle(12, ly + 4, 16, 4), _wallColor);
        DrawLabel("Wall edge", 36, ly, new Color(140, 135, 175));
        ly += 24;
        DrawRect(new Rectangle(12, ly + 4, 16, 16), _emptyColor);
        DrawBorder(new Rectangle(12, ly + 4, 16, 16), new Color(30, 28, 48));
        DrawLabel("Empty", 36, ly, new Color(140, 135, 175));
        ly += 36;
        DrawLabel("[A] Add  [R] Remove", 10, ly, new Color(60, 58, 85));
        ly += 22;
        DrawLabel("[Bksp] Reset grid", 10, ly, new Color(60, 58, 85));
        ly += 22;
        DrawLabel("[Esc] Save + return", 10, ly, new Color(60, 58, 85));

        DrawButton(_btnSave, "Save + Return");
        DrawButton(_btnBack, "< Back");

        // ── Grid ──────────────────────────────────────────────────────────
        DrawGrid(gameTime);

        // Header above grid
        DrawLabel($"Room Geometry  --  {GridSize}x{GridSize} canvas (max)",
            GridOriginX + 2, GridOriginY - 22, new Color(80, 75, 110));

        // Brush cursor hint near mouse
        if (_hoverCol >= 0)
        {
            var mp   = Mouse.GetState().Position;
            var hint = _brush == Brush.Add ? "Add" : "Remove";
            DrawLabel(hint, mp.X + 14, mp.Y - 8, _brush == Brush.Add
                ? new Color(80, 200, 120)
                : new Color(232, 0, 61));
        }

        _sb.End();
    }

    private void DrawGrid(GameTime gameTime)
    {
        double t = gameTime.TotalGameTime.TotalSeconds;

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                int px = GridOriginX + col * TilePx;
                int py = GridOriginY + row * TilePx;

                bool filled  = _shape.IsFilled(col, row);
                bool hovered = (col == _hoverCol && row == _hoverRow);

                // Tile background
                Color bg;
                if (filled)
                    bg = hovered ? _floorHov : _floorColor;
                else
                    bg = hovered
                        ? (_brush == Brush.Add
                            ? new Color(38, 36, 58)
                            : new Color(28, 14, 22))
                        : _emptyColor;

                DrawRect(new Rectangle(px, py, TilePx, TilePx), bg);

                // Grid lines (subtle)
                DrawRect(new Rectangle(px, py, TilePx, 1), _gridLine);
                DrawRect(new Rectangle(px, py, 1, TilePx), _gridLine);

                // Wall edges
                if (filled)
                {
                    var walls = _shape.GetWalls(col, row);
                    DrawWalls(px, py, walls, hovered, t);
                }

                // Hover pulse on empty with Add brush
                if (hovered && !filled && _brush == Brush.Add)
                {
                    float alpha = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f);
                    DrawRect(new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2),
                        new Color(80, 200, 120) * alpha);
                }
                if (hovered && filled && _brush == Brush.Remove)
                {
                    float alpha = (float)(Math.Sin(t * 8f) * 0.3f + 0.5f);
                    DrawRect(new Rectangle(px + 1, py + 1, TilePx - 2, TilePx - 2),
                        _accent * alpha);
                }
            }
        }

        // Outer border of the whole grid
        DrawBorder(new Rectangle(GridOriginX, GridOriginY, GridPx, GridPx), _border);
    }

    private void DrawWalls(int px, int py, WallEdges walls, bool hovered, double t)
    {
        // Pulsing accent on hovered walls
        float pulse = hovered ? (float)(Math.Sin(t * 6f) * 0.2f + 0.8f) : 1f;
        var wc = new Color(
            (int)(_wallColor.R * pulse),
            (int)(_wallColor.G * pulse),
            (int)(_wallColor.B * pulse));

        if ((walls & WallEdges.North) != 0)
            DrawRect(new Rectangle(px, py, TilePx, WallThick), wc);
        if ((walls & WallEdges.South) != 0)
            DrawRect(new Rectangle(px, py + TilePx - WallThick, TilePx, WallThick), wc);
        if ((walls & WallEdges.West) != 0)
            DrawRect(new Rectangle(px, py, WallThick, TilePx), wc);
        if ((walls & WallEdges.East) != 0)
            DrawRect(new Rectangle(px + TilePx - WallThick, py, WallThick, TilePx), wc);
    }

    // -----------------------------------------------------------------------
    // UI helpers
    // -----------------------------------------------------------------------

    private void DrawBrushButton(Rectangle r, string label, bool active)
    {
        var mp  = Mouse.GetState().Position.ToVector2();
        bool hov = r.Contains(mp);

        var bg     = active ? new Color(50, 18, 30)
                   : hov    ? new Color(30, 26, 46)
                   :          new Color(20, 18, 32);
        var border = active ? _accent
                   : hov    ? new Color(100, 90, 140)
                   :          _border;
        var textCol = active ? Color.White : new Color(150, 145, 180);

        DrawRect(r, bg);
        DrawBorder(r, border);

        // Active indicator bar on left
        if (active)
            DrawRect(new Rectangle(r.X, r.Y, 3, r.Height), _accent);

        var sz = Assets.MenuFont.MeasureString(label);
        _sb.DrawString(Assets.MenuFont, label,
            new Vector2(r.X + 12, r.Y + (r.Height - sz.Y) / 2f), textCol);
    }

    private void DrawButton(Rectangle r, string label)
    {
        var mp  = Mouse.GetState().Position.ToVector2();
        bool hov = r.Contains(mp);
        var bg     = hov ? new Color(30, 26, 46) : new Color(20, 18, 32);
        var border = hov ? _accent : _border;

        DrawRect(r, bg);
        DrawBorder(r, border);

        var sz = Assets.MenuFont.MeasureString(label);
        _sb.DrawString(Assets.MenuFont, label,
            new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f),
            hov ? Color.White : new Color(150, 145, 180));
    }

    private void DrawLabel(string text, int x, int y, Color color)
        => _sb.DrawString(Assets.MenuFont, text, new Vector2(x, y), color);

    private void DrawRect(Rectangle r, Color c)
        => _sb.Draw(Assets.Pixel, r, c);

    private void DrawBorder(Rectangle r, Color c)
    {
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,         r.Y,         r.Width, 1), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,         r.Bottom,    r.Width, 1), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.X,         r.Y,         1, r.Height), c);
        _sb.Draw(Assets.Pixel, new Rectangle(r.Right,     r.Y,         1, r.Height + 1), c);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void SaveAndReturn()
    {
        _onSave?.Invoke(_shape);
        NavigationBus.RequestNavigate("LevelEditor");
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}