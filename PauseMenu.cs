using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ZebraBear;

public class PauseMenu
{
    private Game        _game;
    private SpriteBatch _spriteBatch;
    private SpriteFont  _font;
    private SpriteFont  _titleFont;
    private Texture2D   _pixel;

    private int      _selectedIndex = 0;
    private string[] _options       = { "Resume", "Main Menu", "Quit" };

    private KeyboardState _prevKeys;
    private Color _accent = new Color(232, 0, 61);

    public PauseMenu(Game game, SpriteBatch spriteBatch,
        SpriteFont font, SpriteFont titleFont, Texture2D pixel)
    {
        _game       = game;
        _spriteBatch = spriteBatch;
        _font       = font;
        _titleFont  = titleFont;
        _pixel      = pixel;
    }

    public void OnOpen()
    {
        _selectedIndex       = 0;
        _game.IsMouseVisible = true;
        // Re-centre mouse so it doesn't jump when reopening
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnClose()
    {
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        // Escape closes the menu again
        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            OnClose();
            _game.ChangeScene(Scene.Game);
        }

        if (IsPressed(keys, _prevKeys, Keys.Down))
            _selectedIndex = (_selectedIndex + 1) % _options.Length;

        if (IsPressed(keys, _prevKeys, Keys.Up))
            _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;

        if (IsPressed(keys, _prevKeys, Keys.Enter) ||
            IsPressed(keys, _prevKeys, Keys.Z))
        {
            Confirm();
        }

        _prevKeys = keys;
    }

    private void Confirm()
    {
        switch (_selectedIndex)
        {
            case 0: // Resume
                OnClose();
                _game.ChangeScene(Scene.Game);
                break;
            case 1: // Main Menu
                OnClose();
                _game.ChangeScene(Scene.MainMenu);
                break;
            case 2: // Quit
                _game.Exit();
                break;
        }
    }

    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        int cx = vp.Width  / 2;
        int cy = vp.Height / 2;

        _spriteBatch.Begin();

        // Fullscreen dark overlay
        _spriteBatch.Draw(_pixel,
            new Rectangle(0, 0, vp.Width, vp.Height),
            new Color(0, 0, 0, 180));

        // Panel
        int panelW = 400;
        int panelH = 280;
        int panelX = cx - panelW / 2;
        int panelY = cy - panelH / 2;

        // Panel background
        _spriteBatch.Draw(_pixel,
            new Rectangle(panelX, panelY, panelW, panelH),
            new Color(8, 8, 20, 245));

        // Left accent bar
        _spriteBatch.Draw(_pixel,
            new Rectangle(panelX, panelY, 4, panelH),
            _accent);

        // Top border
        _spriteBatch.Draw(_pixel,
            new Rectangle(panelX, panelY, panelW, 2),
            new Color(60, 60, 100));

        // Title
        var title     = "PAUSED";
        var titleSize = _titleFont.MeasureString(title);
        _spriteBatch.DrawString(_titleFont, title,
            new Vector2(cx - titleSize.X / 2f, panelY + 24f),
            _accent);

        // Divider
        _spriteBatch.Draw(_pixel,
            new Rectangle(panelX + 20, panelY + 80, panelW - 40, 1),
            new Color(40, 40, 70));

        // Menu options
        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = i == _selectedIndex;
            var  color    = selected ? Color.White : new Color(120, 120, 140);
            var  text     = selected ? $"> {_options[i]}" : $"  {_options[i]}";
            var  size     = _font.MeasureString(text);
            var  pos      = new Vector2(cx - size.X / 2f, panelY + 110f + i * 52f);

            if (selected)
            {
                _spriteBatch.Draw(_pixel,
                    new Rectangle((int)pos.X - 12, (int)pos.Y - 6,
                        (int)size.X + 24, (int)size.Y + 12),
                    new Color(232, 0, 61, 40));

                _spriteBatch.Draw(_pixel,
                    new Rectangle((int)pos.X - 12, (int)pos.Y - 6,
                        3, (int)size.Y + 12),
                    _accent);
            }

            _spriteBatch.DrawString(_font, text, pos, color);
        }

        _spriteBatch.End();
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}