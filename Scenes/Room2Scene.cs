using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;

namespace ZebraBear;

/// <summary>
/// Generic wing room. Serve four instances with different roomIds:
///   new Room2Scene(game, sb, "RoomNorth")
///   new Room2Scene(game, sb, "RoomSouth")
///   etc.
///
/// Content for each room comes from Data/rooms.json matching the roomId.
/// </summary>
public class Room2Scene : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;
    private readonly string      _roomId;

    private Camera _camera;
    private Room   _room;

    public Room2Scene(Game game, SpriteBatch spriteBatch, string roomId = "Room2")
    {
        _game        = game;
        _spriteBatch = spriteBatch;
        _roomId      = roomId;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        var (wall, floor, ceil, label) = GameLoader.ReadRoomColors(_roomId);

        _camera = new Camera(vp.Width / (float)vp.Height, new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        _room = new Room(_game.GraphicsDevice,
            wallColor:  wall,
            floorColor: floor,
            ceilColor:  ceil,
            label:      label);

        GameLoader.LoadRoom(_roomId, _room);
    }

    public void OnEnter()
    {
        MapData.CurrentRoomId = _roomId;
        MapData.SetDiscovered(_roomId);
        _camera.Position     = new Vector3(0, 0, 10f);
        _camera.Yaw          = 0f;
        _camera.Pitch        = 0f;
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnExit() { }

    public void Update(GameTime gameTime)
    {
        _camera.Update(gameTime, captureMouse: true, _room.ResolveCollisions);
    }

    public void Draw(GameTime gameTime)
    {
        var gd = _game.GraphicsDevice;
        var vp = gd.Viewport;

        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        _room.Draw(_camera, dialogueActive: false);

        _spriteBatch.Begin();
        _spriteBatch.DrawString(Assets.MenuFont, _room.Label,
            new Vector2(20, 20), new Color(60, 55, 80));
        _spriteBatch.DrawString(Assets.MenuFont,
            "WASD move   Shift run   Mouse look   Esc pause",
            new Vector2(16, vp.Height - 28), new Color(60, 55, 80));
        _spriteBatch.End();
    }
}