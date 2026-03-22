using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ZebraBear;

public class MainMenuScene
{
    private Game _game;
    private SpriteBatch _spriteBatch;
    private SpriteFont _font;
    private SpriteFont _titleFont;

    // Menu state
    private int _selectedIndex = 0;
    private string[] _options = { "New Game", "Load", "Quit" };

    // Input debounce
    private KeyboardState _prevKeys;

    // Animation
    private float _titleY = -80f;
    private float _titleTargetY = 120f;
    private float _alpha = 0f;

    // Danganronpa red
    private Color _accentColor = new Color(232, 0, 61);

    public MainMenuScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        _font = _game.Content.Load<SpriteFont>("Fonts/MenuFont");
        _titleFont = _game.Content.Load<SpriteFont>("Fonts/TitleFont");
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keys = Keyboard.GetState();

        // Animate title drop-in
        _titleY = MathHelper.Lerp(_titleY, _titleTargetY, dt * 6f);
        _alpha = MathHelper.Lerp(_alpha, 1f, dt * 3f);

        // Navigate menu
        if (IsPressed(keys, _prevKeys, Keys.Down))
            _selectedIndex = (_selectedIndex + 1) % _options.Length;

        if (IsPressed(keys, _prevKeys, Keys.Up))
            _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;

        // Confirm
        if (IsPressed(keys, _prevKeys, Keys.Enter) || IsPressed(keys, _prevKeys, Keys.Z))
        {
            switch (_selectedIndex)
            {
                case 0: _game.ChangeScene(Scene.Game); break;
                case 1: /* load logic */ break;
                case 2: _game.Exit(); break;
            }
        }

        _prevKeys = keys;
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _game.GraphicsDevice.Viewport;
        float cx = viewport.Width / 2f;

        _spriteBatch.Begin();

        // Title
        var titleText = "Project Zebra Bear";
        var titleSize = _titleFont.MeasureString(titleText);
        _spriteBatch.DrawString(
            _titleFont, titleText,
            new Vector2(cx - titleSize.X / 2f, _titleY),
            _accentColor * _alpha
        );

        // Menu options
        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = i == _selectedIndex;
            var color = selected ? Color.White : new Color(120, 120, 140);
            var text = selected ? $"> {_options[i]} <" : _options[i];
            var size = _font.MeasureString(text);
            var pos = new Vector2(cx - size.X / 2f, 280f + i * 60f);

            // Selection background bar
            if (selected)
            {
                DrawRect(
                    new Rectangle((int)(cx - 160), (int)pos.Y - 6, 320, 44),
                    new Color(232, 0, 61, 40)
                );
            }

            _spriteBatch.DrawString(_font, text, pos, color * _alpha);
        }

        // Version label
        _spriteBatch.DrawString(_font, "v0.1", new Vector2(16, viewport.Height - 30), 
            new Color(60, 60, 80));

        _spriteBatch.End();
    }

    private void DrawRect(Rectangle rect, Color color)
    {
        // Creates a 1x1 pixel texture and stretches it — standard MonoGame technique
        var tex = new Texture2D(_game.GraphicsDevice, 1, 1);
        tex.SetData(new[] { color });
        _spriteBatch.Draw(tex, rect, Color.White);
    }

    private bool IsPressed(KeyboardState current, KeyboardState prev, Keys key)
        => current.IsKeyDown(key) && prev.IsKeyUp(key);
}