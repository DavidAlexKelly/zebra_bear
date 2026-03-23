using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;

namespace ZebraBear;

public class PauseMenu : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    // -----------------------------------------------------------------------
    // Layout constants
    // -----------------------------------------------------------------------
    private const int PanelW  = 900;
    private const int PanelH  = 540;
    private const int TabH    = 44;
    private const int TabW    = 180;
    private const int Padding = 28;

    // -----------------------------------------------------------------------
    // Tabs
    // -----------------------------------------------------------------------
    private enum Tab { Menu, Map, Characters }
    private Tab      _activeTab = Tab.Menu;
    private string[] _tabNames  = { "MENU", "MAP", "CHARACTERS" };

    // -----------------------------------------------------------------------
    // Menu tab state
    // -----------------------------------------------------------------------
    private int      _selectedIndex = 0;
    private string[] _options       = { "Resume", "Main Menu", "Quit" };

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
    private Color _accent  = new Color(232, 0, 61);
    private Color _dimText = new Color(100, 95, 130);
    private Color _panelBg = new Color(8, 8, 20, 250);
    private Color _tabBg   = new Color(14, 12, 28);
    private Color _border  = new Color(40, 38, 68);

    public PauseMenu(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load() { }

    public void OnOpen()
    {
        _selectedIndex       = 0;
        _activeTab           = Tab.Menu;
        _game.IsMouseVisible = true;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnClose()
    {
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnEnter() { }
    public void OnExit()  { }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    public void Update(GameTime gameTime)
    {
        float dt   = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var   keys = Keyboard.GetState();

        _mapPulse += dt * 3f;

        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            OnClose();
            _game.Resume();
        }

        // Tab switching — Q cycles left, E cycles right, Tab also cycles
        if (IsPressed(keys, _prevKeys, Keys.Q) ||
            IsPressed(keys, _prevKeys, Keys.Tab))
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
        int count  = Enum.GetValues<Tab>().Length;
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
            case 0: OnClose(); _game.Resume();       break;
            case 1: OnClose(); _game.GoToMainMenu(); break;
            case 2: _game.Exit();                    break;
        }
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        int cx = vp.Width  / 2;
        int cy = vp.Height / 2;

        int panelX = cx - PanelW / 2;
        int panelY = cy - PanelH / 2;

        _spriteBatch.Begin();

        // Fullscreen overlay
        DrawRect(new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 200));

        // Panel
        DrawRect(new Rectangle(panelX,              panelY, PanelW, PanelH), _panelBg);
        DrawRect(new Rectangle(panelX,              panelY, 4,      PanelH), _accent);
        DrawRect(new Rectangle(panelX,              panelY, PanelW, 2),      _border);
        DrawRect(new Rectangle(panelX,              panelY + PanelH - 2, PanelW, 2), _border);
        DrawRect(new Rectangle(panelX + PanelW - 2, panelY, 2, PanelH),     _border);

        // Title
        _spriteBatch.DrawString(Assets.TitleFont, "PAUSED",
            new Vector2(panelX + Padding + 8, panelY + Padding - 4), _accent);

        // Tabs
        int tabY = panelY + 80;
        DrawTabs(panelX, tabY);

        // Content area
        int contentX = panelX + 4;
        int contentY = tabY + TabH;
        int contentW = PanelW - 4;
        int contentH = PanelH - 80 - TabH;

        DrawRect(new Rectangle(contentX, contentY, contentW, contentH),
            new Color(10, 10, 24, 255));
        DrawRect(new Rectangle(contentX, contentY, contentW, 1), _border);

        switch (_activeTab)
        {
            case Tab.Menu:       DrawMenuTab(contentX, contentY, contentW, contentH);       break;
            case Tab.Map:        DrawMapTab(contentX,  contentY, contentW, contentH);        break;
            case Tab.Characters: DrawCharactersTab(contentX, contentY, contentW, contentH); break;
        }

        // Footer
        string hint     = "Q / Tab  prev tab     E  next tab     Esc  resume";
        var    hintSize = Assets.MenuFont.MeasureString(hint);
        _spriteBatch.DrawString(Assets.MenuFont, hint,
            new Vector2(cx - hintSize.X / 2f, panelY + PanelH + 10),
            _dimText);

        _spriteBatch.End();
    }

    // -----------------------------------------------------------------------
    // Tab bar
    // -----------------------------------------------------------------------

    private void DrawTabs(int panelX, int tabY)
    {
        for (int i = 0; i < _tabNames.Length; i++)
        {
            bool active = (int)_activeTab == i;
            int  tabX   = panelX + 4 + i * (TabW + 2);

            DrawRect(new Rectangle(tabX, tabY, TabW, TabH),
                active ? new Color(10, 10, 24) : _tabBg);

            // Active = accent top stripe, inactive = border
            DrawRect(new Rectangle(tabX, tabY, TabW, active ? 3 : 1),
                active ? _accent : _border);

            var  label   = _tabNames[i];
            var  sz      = Assets.MenuFont.MeasureString(label);
            _spriteBatch.DrawString(Assets.MenuFont, label,
                new Vector2(tabX + TabW / 2f - sz.X / 2f,
                            tabY + TabH / 2f - sz.Y / 2f),
                active ? Color.White : _dimText);
        }
    }

    // -----------------------------------------------------------------------
    // Menu tab content
    // -----------------------------------------------------------------------

    private void DrawMenuTab(int x, int y, int w, int h)
    {
        int cx = x + w / 2;

        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = i == _selectedIndex;
            var  color    = selected ? Color.White : new Color(130, 125, 160);
            var  text     = _options[i];
            var  size     = Assets.MenuFont.MeasureString(text);
            var  pos      = new Vector2(cx - size.X / 2f, y + 60f + i * 80f);

            if (selected)
            {
                DrawRect(new Rectangle(x + 40, (int)pos.Y - 12, w - 80, (int)size.Y + 24),
                    new Color(232, 0, 61, 35));
                DrawRect(new Rectangle(x + 40, (int)pos.Y - 12, 3, (int)size.Y + 24),
                    _accent);
                _spriteBatch.DrawString(Assets.MenuFont, ">",
                    new Vector2(x + 52, pos.Y), _accent);
            }

            _spriteBatch.DrawString(Assets.MenuFont, text, pos, color);
        }
    }

    // -----------------------------------------------------------------------
    // Map tab content
    // -----------------------------------------------------------------------

    private void DrawMapTab(int x, int y, int w, int h)
    {
        const int mapPad = 30;
        int mapX = x + mapPad;
        int mapY = y + mapPad;
        int mapW = w - mapPad * 2;
        int mapH = h - mapPad * 2 - 24; // leave room for legend

        // Canvas
        DrawRect(new Rectangle(mapX, mapY, mapW, mapH), new Color(6, 6, 16));
        DrawRect(new Rectangle(mapX,          mapY,          mapW, 1), _border);
        DrawRect(new Rectangle(mapX,          mapY + mapH,   mapW, 1), _border);
        DrawRect(new Rectangle(mapX,          mapY,          1, mapH), _border);
        DrawRect(new Rectangle(mapX + mapW,   mapY,          1, mapH), _border);

        // Connections (behind rooms)
        foreach (var conn in MapData.Connections)
        {
            var from = MapData.Rooms.Find(r => r.Id == conn.FromId);
            var to   = MapData.Rooms.Find(r => r.Id == conn.ToId);
            if (from == null || to == null) continue;
            if (!from.Discovered && !to.Discovered) continue;

            DrawLine(
                RoomCentre(from, mapX, mapY, mapW, mapH),
                RoomCentre(to,   mapX, mapY, mapW, mapH),
                to.Discovered ? new Color(55, 50, 85) : new Color(30, 28, 50));
        }

        // Rooms
        foreach (var room in MapData.Rooms)
        {
            int rX = mapX + (int)(room.Position.X * mapW);
            int rY = mapY + (int)(room.Position.Y * mapH);
            int rW = (int)(room.Size.X * mapW);
            int rH = (int)(room.Size.Y * mapH);

            bool isCurrent = room.Id == MapData.CurrentRoomId;

            if (!room.Discovered)
            {
                DrawRoomBox(rX, rY, rW, rH, new Color(14, 12, 26), new Color(35, 32, 55));
                DrawCentredString("?", rX, rY, rW, rH, new Color(50, 45, 75));
                continue;
            }

            var bg     = isCurrent ? new Color(28, 14, 32) : new Color(16, 14, 28);
            var border = isCurrent ? _accent                : new Color(55, 50, 85);
            DrawRoomBox(rX, rY, rW, rH, bg, border);
            DrawCentredString(room.Label, rX, rY, rW, rH,
                isCurrent ? Color.White : new Color(160, 155, 190));

            // Pulsing location dot
            if (isCurrent)
            {
                float pulse  = (float)Math.Sin(_mapPulse) * 0.5f + 0.5f;
                var   dotCol = Color.Lerp(new Color(232, 0, 61), new Color(255, 140, 160), pulse);
                int   dotR   = 5;
                DrawRect(new Rectangle(
                    rX + rW / 2 - dotR,
                    rY + rH - dotR * 2 - 6,
                    dotR * 2, dotR * 2), dotCol);
            }
        }

        // Legend
        int legendY = mapY + mapH + 8;
        DrawRect(new Rectangle(mapX, legendY + 4, 8, 8), _accent);
        _spriteBatch.DrawString(Assets.MenuFont, "  Current location",
            new Vector2(mapX + 2, legendY), _dimText);
    }

    // -----------------------------------------------------------------------
    // Characters tab content
    // -----------------------------------------------------------------------

    private void DrawCharactersTab(int x, int y, int w, int h)
    {
        const int ListW     = 220;
        const int Pad       = 16;
        const int EntryH    = 64;
        const int ThumbSize = 48;

        // ---- Left panel: character list ----
        int listX = x + Pad;
        int listY = y + Pad;

        for (int i = 0; i < CharacterData.Characters.Count; i++)
        {
            var  c        = CharacterData.Characters[i];
            bool selected = i == _charSelectedIndex;
            int  entryY   = listY + i * (EntryH + 4);

            // Entry background
            var entryBg = selected ? new Color(28, 14, 32) : new Color(14, 13, 26);
            DrawRect(new Rectangle(listX, entryY, ListW, EntryH), entryBg);

            // Left accent on selected
            DrawRect(new Rectangle(listX, entryY, selected ? 3 : 1, EntryH),
                selected ? _accent : _border);

            if (!c.Met)
            {
                // Unknown — show ??? placeholder
                DrawRect(new Rectangle(listX + 8, entryY + 8, ThumbSize, ThumbSize),
                    new Color(20, 18, 35));
                var qSz = Assets.MenuFont.MeasureString("?");
                _spriteBatch.DrawString(Assets.MenuFont, "?",
                    new Vector2(listX + 8 + ThumbSize / 2f - qSz.X / 2f,
                                entryY + 8 + ThumbSize / 2f - qSz.Y / 2f),
                    new Color(50, 45, 75));

                _spriteBatch.DrawString(Assets.MenuFont, "???",
                    new Vector2(listX + ThumbSize + 16, entryY + 12),
                    new Color(60, 55, 85));
                _spriteBatch.DrawString(Assets.MenuFont, "Unknown",
                    new Vector2(listX + ThumbSize + 16, entryY + 34),
                    new Color(45, 42, 65));
            }
            else
            {
                // Thumbnail portrait
                if (c.Portrait != null)
                {
                    var dest = new Rectangle(listX + 8, entryY + 8, ThumbSize, ThumbSize);
                    _spriteBatch.Draw(c.Portrait, dest, Color.White);
                }
                else
                {
                    DrawRect(new Rectangle(listX + 8, entryY + 8, ThumbSize, ThumbSize),
                        new Color(40, 35, 60));
                }

                _spriteBatch.DrawString(Assets.MenuFont, c.Name,
                    new Vector2(listX + ThumbSize + 16, entryY + 10),
                    selected ? Color.White : new Color(160, 155, 190));

                _spriteBatch.DrawString(Assets.MenuFont, c.Title,
                    new Vector2(listX + ThumbSize + 16, entryY + 34),
                    new Color(130, 80, 100));
            }
        }

        // ---- Divider ----
        int divX = x + ListW + Pad * 2;
        DrawRect(new Rectangle(divX, y + Pad, 1, h - Pad * 2), _border);

        // ---- Right panel: detail view ----
        int detailX = divX + Pad;
        int detailY = y + Pad;
        int detailW = w - ListW - Pad * 4 - 1;

        if (_charSelectedIndex < CharacterData.Characters.Count)
        {
            var c = CharacterData.Characters[_charSelectedIndex];

            if (!c.Met)
            {
                // Undiscovered detail
                var unknownSz = Assets.MenuFont.MeasureString("???");
                _spriteBatch.DrawString(Assets.TitleFont, "???",
                    new Vector2(detailX, detailY), _dimText);
                _spriteBatch.DrawString(Assets.MenuFont,
                    "You haven't spoken to this person yet.",
                    new Vector2(detailX, detailY + 60), _dimText);
            }
            else
            {
                // Name + title header
                _spriteBatch.DrawString(Assets.TitleFont, c.Name,
                    new Vector2(detailX, detailY), Color.White);

                _spriteBatch.DrawString(Assets.MenuFont, c.Title,
                    new Vector2(detailX, detailY + 56), new Color(200, 100, 120));

                // Accent line under header
                DrawRect(new Rectangle(detailX, detailY + 80, detailW, 1), _accent);

                // Portrait — tall on right side of detail panel
                if (c.Portrait != null)
                {
                    int portH   = h - Pad * 2 - 90;
                    int portW   = (int)(portH * (float)c.Portrait.Width / c.Portrait.Height);
                    int portMax = detailW / 2;
                    if (portW > portMax) { portW = portMax; portH = (int)(portW * (float)c.Portrait.Height / c.Portrait.Width); }

                    int portX = detailX + detailW - portW;
                    int portY = detailY + 90;
                    _spriteBatch.Draw(c.Portrait,
                        new Rectangle(portX, portY, portW, portH),
                        Color.White);

                    // Fade the portrait edge into the panel background
                    DrawRect(new Rectangle(portX - 20, portY, 20, portH),
                        new Color(10, 10, 24, 180));
                }

                // Bio text — left side of detail panel
                int bioX      = detailX;
                int bioY      = detailY + 96;
                int bioW      = detailW / 2 + 20;
                float lineH   = Assets.MenuFont.LineSpacing + 6f;

                foreach (var line in c.Bio)
                {
                    DrawWrappedText(line, bioX, ref bioY, bioW, lineH,
                        new Color(170, 165, 200));
                    bioY += (int)lineH / 2; // paragraph gap
                }
            }
        }
    }

    private void DrawWrappedText(string text, int x, ref int y, int maxW,
        float lineH, Color color)
    {
        var words = text.Split(' ');
        var line  = "";

        foreach (var word in words)
        {
            var test = line.Length == 0 ? word : line + " " + word;
            if (Assets.MenuFont.MeasureString(test).X > maxW && line.Length > 0)
            {
                _spriteBatch.DrawString(Assets.MenuFont, line,
                    new Vector2(x, y), color);
                y    += (int)lineH;
                line  = word;
            }
            else line = test;
        }
        if (line.Length > 0)
        {
            _spriteBatch.DrawString(Assets.MenuFont, line,
                new Vector2(x, y), color);
            y += (int)lineH;
        }
    }

    private void DrawRoomBox(int x, int y, int w, int h, Color bg, Color border)
    {
        DrawRect(new Rectangle(x,     y,     w, h), bg);
        DrawRect(new Rectangle(x,     y,     w, 1), border);
        DrawRect(new Rectangle(x,     y + h, w, 1), border);
        DrawRect(new Rectangle(x,     y,     1, h), border);
        DrawRect(new Rectangle(x + w, y,     1, h), border);
    }

    private void DrawCentredString(string text, int rx, int ry, int rw, int rh, Color color)
    {
        var sz = Assets.MenuFont.MeasureString(text);
        _spriteBatch.DrawString(Assets.MenuFont, text,
            new Vector2(rx + rw / 2f - sz.X / 2f, ry + rh / 2f - sz.Y / 2f),
            color);
    }

    private Vector2 RoomCentre(MapRoom room, int mapX, int mapY, int mapW, int mapH) =>
        new Vector2(
            mapX + (room.Position.X + room.Size.X / 2f) * mapW,
            mapY + (room.Position.Y + room.Size.Y / 2f) * mapH);

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        var   dir    = to - from;
        float length = dir.Length();
        if (length < 1f) return;
        dir.Normalize();
        for (float t = 0; t < length; t += 2f)
            DrawRect(new Rectangle(
                (int)(from.X + dir.X * t),
                (int)(from.Y + dir.Y * t), 2, 2), color);
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    private void DrawRect(Rectangle rect, Color color) =>
        _spriteBatch.Draw(Assets.Pixel, rect, color);

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}