// ======== Scenes/LevelSelectScene.cs ========
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

/// <summary>
/// Level selection screen shown after clicking "New Game".
/// Lists all available levels (built-in + user-published).
/// </summary>
public class LevelSelectScene : IScene
{
    private readonly Game _game;
    private readonly SpriteBatch _sb;

    // Layout
    private readonly VStack _panelStack = new() { Padding = 24, Spacing = 8 };
    private readonly VStack _listStack = new() { Padding = 0, Spacing = 6 };

    // State
    private List<LevelInfo> _levels = new();
    private int _selectedIndex = 0;
    private KeyboardState _prevKeys;
    private float _alpha = 0f;

    // Cached rects
    private Rectangle _backRect;
    private Rectangle _playRect;
    private List<Rectangle> _entryRects = new();

    public LevelSelectScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _sb = spriteBatch;
    }

    public void Load() { }

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        _levels = LevelData.ListLevels();
        _selectedIndex = 0;
        _alpha = 0f;
    }

    public void OnExit()
    {
        _game.IsMouseVisible = false;
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keys = Keyboard.GetState();
        _alpha = MathHelper.Lerp(_alpha, 1f, dt * 4f);

        if (_levels.Count > 0)
        {
            if (IsPressed(keys, _prevKeys, Keys.Down) || IsPressed(keys, _prevKeys, Keys.S))
                _selectedIndex = (_selectedIndex + 1) % _levels.Count;
            if (IsPressed(keys, _prevKeys, Keys.Up) || IsPressed(keys, _prevKeys, Keys.W))
                _selectedIndex = (_selectedIndex - 1 + _levels.Count) % _levels.Count;

            if (IsPressed(keys, _prevKeys, Keys.Enter) || IsPressed(keys, _prevKeys, Keys.Z))
                LaunchSelected();
        }

        if (IsPressed(keys, _prevKeys, Keys.Escape))
            NavigationBus.RequestNavigate("MainMenu");

        _prevKeys = keys;
    }

    private void LaunchSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _levels.Count) return;
        var level = _levels[_selectedIndex];

        if (level.IsBuiltIn)
        {
            LevelData.ClearOverride();
            LevelData.LoadBuiltIn(_game.Content);
        }
        else
        {
            LevelData.LoadLevel(level.FileName, _game.Content);
        }

        // Navigate to the start room
        NavigationBus.RequestNavigate("__reload_and_start__");
    }

    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        var mp = Mouse.GetState().Position.ToVector2();
        float cx = vp.Width / 2f;

        int panelW = Math.Min(700, (int)(vp.Width * 0.6f));
        int panelH = Math.Min(520, (int)(vp.Height * 0.7f));
        int panelX = (vp.Width - panelW) / 2;
        int panelY = (vp.Height - panelH) / 2;

        _sb.Begin();

        // Background
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, vp.Width, vp.Height), LayoutDraw.BgDark);

        // Title
        var titleText = "SELECT LEVEL";
        var titleSize = Assets.TitleFont.MeasureString(titleText);
        _sb.DrawString(Assets.TitleFont, titleText,
            new Vector2(cx - titleSize.X / 2f, panelY - titleSize.Y - 16),
            LayoutDraw.Accent * _alpha);

        // Panel
        var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
        LayoutDraw.Rect(_sb, panelRect, new Color(12, 11, 22, 240));
        LayoutDraw.BorderRect(_sb, panelRect, LayoutDraw.Border);
        LayoutDraw.AccentBar(_sb, panelRect);

        _panelStack.Begin(panelRect);

        // Back button pinned to bottom
        _backRect = _panelStack.NextFromBottom(40);
        _playRect = _panelStack.NextFromBottom(44);
        _panelStack.NextFromBottom(8);

        // Level list
        var listHeader = _panelStack.Next(24);
        LayoutDraw.SectionHeader(_sb, listHeader, $"Available Levels ({_levels.Count})");

        LayoutDraw.DividerLine(_sb, _panelStack.Divider());

        var listArea = _panelStack.ConsumeRemaining();
        _listStack.Begin(listArea);

        _entryRects.Clear();
        int maxVisible = Math.Max(1, listArea.Height / 76);
        int scrollStart = 0;
        if (_selectedIndex >= maxVisible)
            scrollStart = _selectedIndex - maxVisible + 1;

        for (int vi = 0; vi < Math.Min(_levels.Count, maxVisible); vi++)
        {
            int i = vi + scrollStart;
            if (i >= _levels.Count) break;
            if (_listStack.IsFull) break;

            var level = _levels[i];
            bool selected = i == _selectedIndex;
            var entryRect = _listStack.Next(70);
            _entryRects.Add(entryRect);

            // Entry background
            var bg = selected ? new Color(28, 14, 32) : new Color(14, 13, 26);
            LayoutDraw.Rect(_sb, entryRect, bg);
            LayoutDraw.Rect(_sb,
                new Rectangle(entryRect.X, entryRect.Y, selected ? 3 : 1, entryRect.Height),
                selected ? LayoutDraw.Accent : LayoutDraw.Border);

            // Level name
            var nameColor = selected ? Color.White : new Color(160, 155, 190);
            _sb.DrawString(Assets.MenuFont, level.Name,
                new Vector2(entryRect.X + 12, entryRect.Y + 8), nameColor * _alpha);

            // Subtitle line
            string subtitle = level.IsBuiltIn
                ? "Built-in campaign"
                : $"By {level.Author} - {level.RoomCount} room(s)";
            _sb.DrawString(Assets.MenuFont, subtitle,
                new Vector2(entryRect.X + 12, entryRect.Y + 30),
                new Color(100, 95, 130) * _alpha);

            // Date (for user levels)
            if (!level.IsBuiltIn && !string.IsNullOrEmpty(level.CreatedAt))
            {
                string dateStr = "";
                if (DateTime.TryParse(level.CreatedAt, out var date))
                    dateStr = date.ToString("yyyy-MM-dd HH:mm");
                _sb.DrawString(Assets.MenuFont, dateStr,
                    new Vector2(entryRect.X + 12, entryRect.Y + 50),
                    new Color(70, 65, 95) * _alpha);
            }

            // Built-in badge
            if (level.IsBuiltIn)
            {
                var badge = "DEFAULT";
                var badgeSz = Assets.MenuFont.MeasureString(badge);
                var badgeRect = new Rectangle(
                    entryRect.Right - (int)badgeSz.X - 20,
                    entryRect.Y + (entryRect.Height - 24) / 2,
                    (int)badgeSz.X + 12, 24);
                LayoutDraw.Rect(_sb, badgeRect, new Color(40, 20, 50));
                LayoutDraw.BorderRect(_sb, badgeRect, new Color(100, 60, 120));
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, badge, badgeRect,
                    new Color(160, 120, 200) * _alpha);
            }
        }

        // Play button
        bool canPlay = _levels.Count > 0;
        if (canPlay)
        {
            bool hovPlay = _playRect.Contains(mp);
            LayoutDraw.Rect(_sb, _playRect, hovPlay ? new Color(40, 80, 40) : new Color(20, 50, 20));
            LayoutDraw.BorderRect(_sb, _playRect, hovPlay ? new Color(80, 200, 80) : new Color(50, 120, 50));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "PLAY", _playRect,
                hovPlay ? Color.White : new Color(120, 200, 120));
        }
        else
        {
            LayoutDraw.Rect(_sb, _playRect, new Color(20, 18, 30));
            LayoutDraw.BorderRect(_sb, _playRect, LayoutDraw.Border);
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "No levels available", _playRect, LayoutDraw.DimText);
        }

        // Back button
        LayoutDraw.Button(_sb, _backRect, "< Back", mp);

        // Footer hint
        string hint = "[Up/Down] Select   [Enter] Play   [Esc] Back";
        var hintSize = Assets.MenuFont.MeasureString(hint);
        _sb.DrawString(Assets.MenuFont, hint,
            new Vector2(cx - hintSize.X / 2f, panelY + panelH + 14),
            LayoutDraw.DimText * _alpha);

        _sb.End();
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}