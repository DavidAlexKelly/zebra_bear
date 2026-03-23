// ======== PauseMenu.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear;

public class PauseMenu : IScene
{
    private readonly IGameHost _game;
    private readonly SpriteBatch _spriteBatch;

    // Layout helpers
    private readonly VStack _panelStack = new() { Padding = 0, Spacing = 0 };
    private readonly VStack _contentStack = new() { Padding = 16, Spacing = 6 };
    private readonly HStack _tabRow = new() { Padding = 0, Spacing = 2 };
    private readonly HStack _charRow = new() { Padding = 0, Spacing = 0 };
    private readonly VStack _charListStack = new() { Padding = 0, Spacing = 4 };
    private readonly VStack _charDetailStack = new() { Padding = 0, Spacing = 6 };

    // Cached rects from Draw
    private Rectangle[] _tabRects = new Rectangle[3];
    private Rectangle[] _menuOptionRects = new Rectangle[3];
    private Rectangle[] _charEntryRects = new Rectangle[0];

    private Viewport Vp => _game.GraphicsDevice.Viewport;

    // -----------------------------------------------------------------------
    // Tabs
    // -----------------------------------------------------------------------
    private enum Tab { Menu, Map, Characters }
    private Tab _activeTab = Tab.Menu;
    private string[] _tabNames = { "MENU", "MAP", "CHARACTERS" };

    // -----------------------------------------------------------------------
    // Menu tab state
    // -----------------------------------------------------------------------
    private int _selectedIndex = 0;
    private string[] _options = { "Resume", "Main Menu", "Quit" };

    // -----------------------------------------------------------------------
    // Map tab state
    // -----------------------------------------------------------------------
    private float _mapPulse = 0f;

    // -----------------------------------------------------------------------
    // Characters tab state
    // -----------------------------------------------------------------------
    private int _charSelectedIndex = 0;

    // -----------------------------------------------------------------------
    // Shared
    // -----------------------------------------------------------------------
    private KeyboardState _prevKeys;

    public PauseMenu(IGameHost game, SpriteBatch spriteBatch)
    {
        _game = game;
        _spriteBatch = spriteBatch;
    }

    public void Load() { }

    public void OnOpen()
    {
        _selectedIndex = 0;
        _activeTab = Tab.Menu;
        _game.IsMouseVisible = true;
        var vp = Vp;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnClose()
    {
        _game.IsMouseVisible = false;
        var vp = Vp;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnEnter() { }
    public void OnExit() { }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keys = Keyboard.GetState();
        _mapPulse += dt * 3f;

        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            OnClose();
            _game.Resume();
        }

        if (IsPressed(keys, _prevKeys, Keys.Q) || IsPressed(keys, _prevKeys, Keys.Tab))
            CycleTab(-1);
        if (IsPressed(keys, _prevKeys, Keys.E))
            CycleTab(1);

        if (_activeTab == Tab.Menu)
            UpdateMenuTab(keys);
        if (_activeTab == Tab.Characters)
            UpdateCharactersTab(keys);

        _prevKeys = keys;
    }

    private void CycleTab(int dir)
    {
        int count = Enum.GetValues<Tab>().Length;
        _activeTab = (Tab)(((int)_activeTab + dir + count) % count);
    }

    private void UpdateMenuTab(KeyboardState keys)
    {
        if (IsPressed(keys, _prevKeys, Keys.Down) || IsPressed(keys, _prevKeys, Keys.S))
            _selectedIndex = (_selectedIndex + 1) % _options.Length;
        if (IsPressed(keys, _prevKeys, Keys.Up) || IsPressed(keys, _prevKeys, Keys.W))
            _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;
        if (IsPressed(keys, _prevKeys, Keys.Enter) || IsPressed(keys, _prevKeys, Keys.Z))
            Confirm();
    }

    private void UpdateCharactersTab(KeyboardState keys)
    {
        int count = CharacterData.Characters.Count;
        if (count == 0) return;
        if (IsPressed(keys, _prevKeys, Keys.Down) || IsPressed(keys, _prevKeys, Keys.S))
            _charSelectedIndex = (_charSelectedIndex + 1) % count;
        if (IsPressed(keys, _prevKeys, Keys.Up) || IsPressed(keys, _prevKeys, Keys.W))
            _charSelectedIndex = (_charSelectedIndex - 1 + count) % count;
    }

    private void Confirm()
    {
        switch (_selectedIndex)
        {
            case 0: OnClose(); _game.Resume(); break;
            case 1: OnClose(); _game.GoToMainMenu(); break;
            case 2: _game.Exit(); break;
        }
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------
    public void Draw(GameTime gameTime)
    {
        var vp = Vp;
        int panelW = (int)(vp.Width * 0.72f);
        int panelH = (int)(vp.Height * 0.75f);
        int panelX = (vp.Width - panelW) / 2;
        int panelY = (vp.Height - panelH) / 2;

        _spriteBatch.Begin();

        // Fullscreen overlay
        LayoutDraw.Rect(_spriteBatch, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 200));

        // Panel background
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        LayoutDraw.Rect(_spriteBatch, panelRect, new Color(8, 8, 20, 250));
        LayoutDraw.AccentBar(_spriteBatch, panelRect);
        LayoutDraw.BorderRect(_spriteBatch, panelRect, LayoutDraw.Border);

        _panelStack.Padding = 0;
        _panelStack.Spacing = 0;
        _panelStack.Begin(panelX + 4, panelY, panelW - 6, panelH);

        // Title
        var titleRect = _panelStack.Next(56, 24);
        _spriteBatch.DrawString(Assets.TitleFont, "PAUSED",
            new Vector2(titleRect.X, titleRect.Y + 12), LayoutDraw.Accent);

        // Tab bar
        var tabStripRect = _panelStack.Next(44);
        int tabW = (tabStripRect.Width - (_tabNames.Length - 1) * 2) / _tabNames.Length;
        _tabRow.Spacing = 2;
        _tabRow.Begin(tabStripRect);
        for (int i = 0; i < _tabNames.Length; i++)
        {
            bool isLast = i == _tabNames.Length - 1;
            var tabRect = isLast ? _tabRow.Remaining() : _tabRow.Next(tabW);
            _tabRects[i] = tabRect;
            LayoutDraw.Tab(_spriteBatch, tabRect, _tabNames[i], (int)_activeTab == i,
                Mouse.GetState().Position.ToVector2());
        }

        // Content area
        var contentRect = _panelStack.ConsumeRemaining();
        LayoutDraw.Rect(_spriteBatch, contentRect, new Color(10, 10, 24, 255));
        LayoutDraw.Rect(_spriteBatch, new Rectangle(contentRect.X, contentRect.Y, contentRect.Width, 1),
            LayoutDraw.Border);

        switch (_activeTab)
        {
            case Tab.Menu:       DrawMenuTab(contentRect); break;
            case Tab.Map:        DrawMapTab(contentRect); break;
            case Tab.Characters: DrawCharactersTab(contentRect); break;
        }

        // Footer hint
        string hint = "[Q/Tab] Prev   [E] Next   [Esc] Resume";
        var hintSize = Assets.MenuFont.MeasureString(hint);
        _spriteBatch.DrawString(Assets.MenuFont, hint,
            new Vector2(vp.Width / 2f - hintSize.X / 2f, panelY + panelH + 12),
            LayoutDraw.DimText);

        _spriteBatch.End();
    }

    // -----------------------------------------------------------------------
    // Menu tab
    // -----------------------------------------------------------------------
    private void DrawMenuTab(Rectangle area)
    {
        _contentStack.Padding = 0;
        _contentStack.Spacing = 8;

        int totalH = _options.Length * 52;
        int startY = area.Y + (area.Height - totalH) / 2;
        _contentStack.Begin(area.X, startY, area.Width, totalH + 20);

        int cx = area.X + area.Width / 2;

        for (int i = 0; i < _options.Length; i++)
        {
            var optRect = _contentStack.Next(44);
            bool selected = i == _selectedIndex;
            var color = selected ? Color.White : new Color(120, 120, 140);
            var text = selected ? $"> {_options[i]} <" : _options[i];
            var size = Assets.MenuFont.MeasureString(text);

            if (selected)
                LayoutDraw.Rect(_spriteBatch,
                    new Rectangle(cx - 160, optRect.Y, 320, optRect.Height),
                    new Color(232, 0, 61, 40));

            _spriteBatch.DrawString(Assets.MenuFont, text,
                new Vector2(cx - size.X / 2f, optRect.Y + (optRect.Height - size.Y) / 2f),
                color);
        }
    }

    // -----------------------------------------------------------------------
    // Map tab
    // -----------------------------------------------------------------------
    private void DrawMapTab(Rectangle area)
    {
        int pad = 24;
        int mapX = area.X + pad;
        int mapY = area.Y + pad;
        int mapW = area.Width - pad * 2;
        int mapH = area.Height - pad * 2 - 24;

        LayoutDraw.Rect(_spriteBatch, new Rectangle(mapX, mapY, mapW, mapH), new Color(8, 8, 18));

        // Connections
        foreach (var conn in MapData.Connections)
        {
            var fromRoom = MapData.Rooms.Find(r => r.Id == conn.FromId);
            var toRoom = MapData.Rooms.Find(r => r.Id == conn.ToId);
            if (fromRoom == null || toRoom == null) continue;
            var from = RoomCentre(fromRoom, mapX, mapY, mapW, mapH);
            var to = RoomCentre(toRoom, mapX, mapY, mapW, mapH);
            DrawLine(from, to, fromRoom.Discovered && toRoom.Discovered
                ? new Color(55, 50, 85) : new Color(30, 28, 50));
        }

        // Rooms
        foreach (var room in MapData.Rooms)
        {
            int rX = mapX + (int)(room.Position.X * mapW);
            int rY = mapY + (int)(room.Position.Y * mapH);
            int rW = Math.Max(60, (int)(room.Size.X * mapW));
            int rH = Math.Max(36, (int)(room.Size.Y * mapH));
            bool isCurrent = room.Id == MapData.CurrentRoomId;

            var roomRect = new Rectangle(rX, rY, rW, rH);

            if (!room.Discovered)
            {
                LayoutDraw.Rect(_spriteBatch, roomRect, new Color(14, 12, 26));
                LayoutDraw.BorderRect(_spriteBatch, roomRect, new Color(35, 32, 55));
                LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, "?", roomRect, new Color(50, 45, 75));
                continue;
            }

            var bg = isCurrent ? new Color(28, 14, 32) : new Color(16, 14, 28);
            var border = isCurrent ? LayoutDraw.Accent : new Color(55, 50, 85);
            LayoutDraw.Rect(_spriteBatch, roomRect, bg);
            LayoutDraw.BorderRect(_spriteBatch, roomRect, border);
            LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, room.Label, roomRect,
                isCurrent ? Color.White : new Color(160, 155, 190));

            if (isCurrent)
            {
                float pulse = (float)Math.Sin(_mapPulse) * 0.5f + 0.5f;
                var dotCol = Color.Lerp(new Color(232, 0, 61), new Color(255, 140, 160), pulse);
                int dotR = 4;
                LayoutDraw.Rect(_spriteBatch, new Rectangle(
                    rX + rW / 2 - dotR, rY + rH - dotR * 2 - 4,
                    dotR * 2, dotR * 2), dotCol);
            }
        }

        // Legend
        int legendY = mapY + mapH + 8;
        LayoutDraw.Rect(_spriteBatch, new Rectangle(mapX, legendY + 4, 8, 8), LayoutDraw.Accent);
        _spriteBatch.DrawString(Assets.MenuFont, "  Current location",
            new Vector2(mapX + 10, legendY), LayoutDraw.DimText);
    }

    // -----------------------------------------------------------------------
    // Characters tab
    // -----------------------------------------------------------------------
    private void DrawCharactersTab(Rectangle area)
    {
        const int ListW = 220;
        const int EntryH = 64;
        const int ThumbSize = 48;
        int pad = 16;

        _charRow.Padding = pad;
        _charRow.Spacing = 0;
        _charRow.Begin(area);

        var listArea = _charRow.Next(ListW);
        // 1px divider
        LayoutDraw.Rect(_spriteBatch,
            new Rectangle(_charRow.CurrentX, area.Y + pad, 1, area.Height - pad * 2),
            LayoutDraw.Border);
        _charRow.Space(1 + pad);
        var detailArea = _charRow.Remaining();

        // Character list
        _charListStack.Padding = 0;
        _charListStack.Spacing = 4;
        _charListStack.Begin(listArea);

        int totalChars = CharacterData.Characters.Count;
        int maxVisible = Math.Max(1, listArea.Height / (EntryH + 4));
        int scrollStart = 0;
        if (_charSelectedIndex >= maxVisible)
            scrollStart = _charSelectedIndex - maxVisible + 1;

        for (int vi = 0; vi < Math.Min(totalChars, maxVisible); vi++)
        {
            int i = vi + scrollStart;
            if (i >= totalChars) break;

            var c = CharacterData.Characters[i];
            bool selected = i == _charSelectedIndex;
            var entryRect = _charListStack.Next(EntryH);

            var entryBg = selected ? new Color(28, 14, 32) : new Color(14, 13, 26);
            LayoutDraw.Rect(_spriteBatch, entryRect, entryBg);
            LayoutDraw.Rect(_spriteBatch,
                new Rectangle(entryRect.X, entryRect.Y, selected ? 3 : 1, entryRect.Height),
                selected ? LayoutDraw.Accent : LayoutDraw.Border);

            int thumbX = entryRect.X + 8;
            int thumbY = entryRect.Y + (entryRect.Height - ThumbSize) / 2;
            int textX = thumbX + ThumbSize + 10;
            int nameY = entryRect.Y + 14;
            int titleY = entryRect.Y + 36;

            if (!c.Met)
            {
                LayoutDraw.Rect(_spriteBatch, new Rectangle(thumbX, thumbY, ThumbSize, ThumbSize),
                    new Color(20, 18, 35));
                LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, "?",
                    new Rectangle(thumbX, thumbY, ThumbSize, ThumbSize), new Color(50, 45, 75));
                _spriteBatch.DrawString(Assets.MenuFont, "???",
                    new Vector2(textX, nameY), new Color(60, 55, 85));
                _spriteBatch.DrawString(Assets.MenuFont, "Unknown",
                    new Vector2(textX, titleY), new Color(45, 42, 65));
            }
            else
            {
                if (c.Portrait != null)
                    _spriteBatch.Draw(c.Portrait,
                        new Rectangle(thumbX, thumbY, ThumbSize, ThumbSize), Color.White);
                else
                    LayoutDraw.Rect(_spriteBatch,
                        new Rectangle(thumbX, thumbY, ThumbSize, ThumbSize), new Color(40, 35, 60));

                _spriteBatch.DrawString(Assets.MenuFont, c.Name,
                    new Vector2(textX, nameY),
                    selected ? Color.White : new Color(160, 155, 190));
                _spriteBatch.DrawString(Assets.MenuFont, c.Title,
                    new Vector2(textX, titleY), new Color(130, 80, 100));
            }
        }

        // Detail pane
        if (_charSelectedIndex < totalChars)
        {
            var c = CharacterData.Characters[_charSelectedIndex];

            _charDetailStack.Padding = 0;
            _charDetailStack.Spacing = 6;
            _charDetailStack.Begin(detailArea);

            if (!c.Met)
            {
                var unknownName = _charDetailStack.Next(50);
                _spriteBatch.DrawString(Assets.TitleFont, "???",
                    new Vector2(unknownName.X, unknownName.Y), LayoutDraw.DimText);

                var unknownHint = _charDetailStack.Next(24);
                _spriteBatch.DrawString(Assets.MenuFont,
                    "You haven't spoken to this person yet.",
                    new Vector2(unknownHint.X, unknownHint.Y), LayoutDraw.DimText);
            }
            else
            {
                // Name
                var nameH = (int)Assets.TitleFont.MeasureString(c.Name).Y;
                var nameRect = _charDetailStack.Next(nameH);
                _spriteBatch.DrawString(Assets.TitleFont, c.Name,
                    new Vector2(nameRect.X, nameRect.Y), LayoutDraw.Accent);

                // Title
                var titleRect = _charDetailStack.Next(24);
                _spriteBatch.DrawString(Assets.MenuFont, c.Title,
                    new Vector2(titleRect.X, titleRect.Y), new Color(180, 100, 120));

                _charDetailStack.Space(8);

                // Portrait + bio side by side
                var bioArea = _charDetailStack.ConsumeRemaining();
                int bioW = bioArea.Width;

                if (c.Portrait != null)
                {
                    int portMaxH = bioArea.Height;
                    int portMaxW = bioArea.Width / 2 - 16;
                    float aspect = (float)c.Portrait.Width / c.Portrait.Height;
                    int portH = Math.Min(portMaxH, (int)(portMaxW / aspect));
                    int portW = (int)(portH * aspect);
                    if (portW > portMaxW) { portW = portMaxW; portH = (int)(portW / aspect); }

                    int portX = bioArea.X + bioArea.Width - portW;
                    int portY = bioArea.Y;

                    LayoutDraw.Rect(_spriteBatch,
                        new Rectangle(portX - 16, portY, 16, portH), new Color(10, 10, 24, 180));
                    _spriteBatch.Draw(c.Portrait,
                        new Rectangle(portX, portY, portW, portH), Color.White);

                    bioW = bioArea.Width - portW - 24;
                }

                if (c.Bio != null)
                {
                    int bioY = bioArea.Y;
                    float lineH = Assets.MenuFont.LineSpacing + 6f;
                    var bioRect = new Rectangle(bioArea.X, bioArea.Y, bioW, bioArea.Height);
                    foreach (var line in c.Bio)
                    {
                        if (bioY > bioArea.Bottom - 20) break;
                        bioY = LayoutDraw.TextWrapped(_spriteBatch, Assets.MenuFont, line,
                            new Rectangle(bioArea.X, bioY, bioW, bioArea.Bottom - bioY),
                            new Color(170, 165, 200));
                        bioY += (int)lineH / 2;
                    }
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private Vector2 RoomCentre(MapRoom room, int mapX, int mapY, int mapW, int mapH) =>
        new Vector2(
            mapX + (room.Position.X + room.Size.X / 2f) * mapW,
            mapY + (room.Position.Y + room.Size.Y / 2f) * mapH);

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var dir = to - from;
        float length = dir.Length();
        if (length < 1f) return;
        dir.Normalize();
        for (float t = 0; t < length; t += 2f)
            LayoutDraw.Rect(_spriteBatch, new Rectangle(
                (int)(from.X + dir.X * t),
                (int)(from.Y + dir.Y * t), 2, 2), color);
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}