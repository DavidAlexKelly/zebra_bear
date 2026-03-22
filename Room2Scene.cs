using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ZebraBear;

public class Room2Scene
{
    private Game        _game;
    private SpriteBatch _spriteBatch;
    private SpriteFont  _font;
    private Texture2D   _pixel;

    private Camera  _camera;
    private Room3D  _room;

    private KeyboardState _prevKeyboard;

    public Room2Scene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        _font  = _game.Content.Load<SpriteFont>("Fonts/MenuFont");
        _pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _camera = new Camera(
            vp.Width / (float)vp.Height,
            new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        // Slightly different wall colours so it feels like a new room
        _room = new Room3D(_game.GraphicsDevice,
            wallColor:  new Color(18, 22, 30),
            floorColor: new Color(14, 12, 20),
            ceilColor:  new Color(10, 12, 18));
    }

    public void OnEnter()
    {
        // Reset camera to entrance point each time we enter
        _camera.Position = new Vector3(0, 0, 10f);
        _camera.Yaw      = 0f;
        _camera.Pitch    = 0f;
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void Update(GameTime gameTime)
    {
        var kb    = Keyboard.GetState();
        var mouse = Mouse.GetState();

        _camera.Update(gameTime, captureMouse: true);

        // Escape still pauses
        if (kb.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
        {
            _game.PauseFrom(Scene.Room2);
        }

        _prevKeyboard = kb;
    }

    public void Draw(GameTime gameTime)
    {
        var gd = _game.GraphicsDevice;
        var vp = gd.Viewport;

        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        _room.Draw(_camera.View, _camera.Projection);

        _spriteBatch.Begin();

        // Room label — top left
        _spriteBatch.DrawString(_font, "???",
            new Vector2(20, 20), new Color(60, 55, 80));

        // Controls hint
        _spriteBatch.DrawString(_font,
            "WASD move   Shift run   Mouse look   Esc pause",
            new Vector2(16, vp.Height - 28),
            new Color(60, 55, 80));

        _spriteBatch.End();
    }
}