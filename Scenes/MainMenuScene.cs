// ======== Scenes/MainMenuScene.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear;

public class MainMenuScene : IScene
{
    private readonly Game _game;
    private readonly SpriteBatch _spriteBatch;

    private int _selectedIndex = 0;
    private string[] _options = { "New Game", "Load", "Editor", "Quit" };
    private KeyboardState _prevKeys;
    private float _titleY = -80f;
    private float _alpha = 0f;

    // Layout
    private readonly VStack _menuStack = new() { Padding = 0, Spacing = 8 };

    public MainMenuScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _spriteBatch = spriteBatch;
    }

    public void Load() { }

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        _titleY = -80f;
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
        var vp = _game.GraphicsDevice.Viewport;

        float titleTargetY = vp.Height * 0.15f;
        _titleY = MathHelper.Lerp(_titleY, titleTargetY, dt * 6f);
        _alpha = MathHelper.Lerp(_alpha, 1f, dt * 3f);

        if (IsPressed(keys, _prevKeys, Keys.Down))
            _selectedIndex = (_selectedIndex + 1) % _options.Length;
        if (IsPressed(keys, _prevKeys, Keys.Up))
            _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;

        if (IsPressed(keys, _prevKeys, Keys.Enter) || IsPressed(keys, _prevKeys, Keys.Z))
        {
            switch (_selectedIndex)
            {
                case 0: NavigationBus.RequestNavigate("LevelSelect"); break;
                case 1: /* load logic */ break;
                case 2: NavigationBus.RequestNavigate("LevelEditor"); break;
                case 3: _game.Exit(); break;
            }
        }

        _prevKeys = keys;
    }

    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        float cx = vp.Width / 2f;
        float titleTargetY = vp.Height * 0.15f;

        _spriteBatch.Begin();

        // Title
        var titleText = "Project Zebra Bear";
        var titleSize = Assets.TitleFont.MeasureString(titleText);
        _spriteBatch.DrawString(Assets.TitleFont, titleText,
            new Vector2(cx - titleSize.X / 2f, _titleY),
            LayoutDraw.Accent * _alpha);

        // Menu items - stacked from below title
        float menuStartY = titleTargetY + titleSize.Y + 40f;
        int menuW = 320;
        _menuStack.Begin((int)(cx - menuW / 2f), (int)menuStartY, menuW, vp.Height - (int)menuStartY - 60);

        for (int i = 0; i < _options.Length; i++)
        {
            // Separator before Editor
            if (i == 2)
            {
                var sepRect = _menuStack.Divider();
                LayoutDraw.Rect(_spriteBatch,
                    new Rectangle(sepRect.X + 40, sepRect.Y, sepRect.Width - 80, 1),
                    new Color(50, 45, 75) * _alpha);
                _menuStack.Space(4);
            }

            var optRect = _menuStack.Next(44);
            bool selected = i == _selectedIndex;
            var text = selected ? $"> {_options[i]} <" : _options[i];
            var size = Assets.MenuFont.MeasureString(text);

            if (selected)
                LayoutDraw.Rect(_spriteBatch,
                    new Rectangle(optRect.X, optRect.Y, optRect.Width, optRect.Height),
                    new Color(232, 0, 61, 40));

            var color = selected ? Color.White
                      : (i == 2) ? new Color(100, 120, 180)
                      : new Color(120, 120, 140);

            _spriteBatch.DrawString(Assets.MenuFont, text,
                new Vector2(optRect.X + (optRect.Width - size.X) / 2f,
                    optRect.Y + (optRect.Height - size.Y) / 2f),
                color * _alpha);
        }

        // Version - bottom left
        _spriteBatch.DrawString(Assets.MenuFont, "v0.1",
            new Vector2(16, vp.Height - 36), new Color(60, 60, 80));

        // Controls hint - bottom centre
        string hint = "[Up/Down] Navigate   [Enter] Select";
        var hintSize = Assets.MenuFont.MeasureString(hint);
        _spriteBatch.DrawString(Assets.MenuFont, hint,
            new Vector2(cx - hintSize.X / 2f, vp.Height - 36),
            new Color(50, 48, 70) * _alpha);

        _spriteBatch.End();
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}