using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;

namespace ZebraBear;

public class MainMenuScene : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private int      _selectedIndex = 0;
    // Editor is index 2; Quit is index 3
    private string[] _options       = { "New Game", "Load", "Editor", "Quit" };

    private KeyboardState _prevKeys;

    private float _titleY       = -80f;
    private float _titleTargetY = 120f;
    private float _alpha        = 0f;

    private Color _accent = new Color(232, 0, 61);

    public MainMenuScene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load() { }  // Assets already loaded centrally

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        // Reset animation so it plays again each time we return
        _titleY = -80f;
        _alpha  = 0f;
    }

    public void OnExit()
    {
        _game.IsMouseVisible = false;
    }

    public void Update(GameTime gameTime)
    {
        float dt   = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var   keys = Keyboard.GetState();

        _titleY = MathHelper.Lerp(_titleY, _titleTargetY, dt * 6f);
        _alpha  = MathHelper.Lerp(_alpha,  1f,            dt * 3f);

        if (IsPressed(keys, _prevKeys, Keys.Down))
            _selectedIndex = (_selectedIndex + 1) % _options.Length;
        if (IsPressed(keys, _prevKeys, Keys.Up))
            _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;

        if (IsPressed(keys, _prevKeys, Keys.Enter) || IsPressed(keys, _prevKeys, Keys.Z))
        {
            switch (_selectedIndex)
            {
                case 0: NavigationBus.RequestNavigate(MapData.CurrentRoomId); break;
                case 1: /* load logic */ break;
                case 2: NavigationBus.RequestNavigate("LevelEditor");         break;
                case 3: _game.Exit();                                         break;
            }
        }

        _prevKeys = keys;
    }

    public void Draw(GameTime gameTime)
    {
        var   vp = _game.GraphicsDevice.Viewport;
        float cx = vp.Width / 2f;

        _spriteBatch.Begin();

        var titleText = "Project Zebra Bear";
        var titleSize = Assets.TitleFont.MeasureString(titleText);
        _spriteBatch.DrawString(Assets.TitleFont, titleText,
            new Vector2(cx - titleSize.X / 2f, _titleY),
            _accent * _alpha);

        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = i == _selectedIndex;
            var  color    = selected ? Color.White : new Color(120, 120, 140);
            var  text     = selected ? $"> {_options[i]} <" : _options[i];

            // Draw a small dim separator line above "Editor" to group it visually
            if (i == 2)
            {
                float sepY = 280f + i * 60f - 12f;
                _spriteBatch.Draw(Assets.Pixel,
                    new Rectangle((int)(cx - 100), (int)sepY, 200, 1),
                    new Color(50, 45, 75));
            }

            var  size     = Assets.MenuFont.MeasureString(text);
            var  pos      = new Vector2(cx - size.X / 2f, 280f + i * 60f);

            if (selected)
            {
                _spriteBatch.Draw(Assets.Pixel,
                    new Rectangle((int)(cx - 160), (int)pos.Y - 6, 320, 44),
                    new Color(232, 0, 61, 40));
            }

            // "Editor" gets a subtle tint to distinguish it from game options
            var drawColor = (i == 2 && !selected)
                ? new Color(100, 120, 180)
                : color;

            _spriteBatch.DrawString(Assets.MenuFont, text, pos, drawColor * _alpha);
        }

        _spriteBatch.DrawString(Assets.MenuFont, "v0.1",
            new Vector2(16, vp.Height - 30), new Color(60, 60, 80));

        _spriteBatch.End();
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);
}