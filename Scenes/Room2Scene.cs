using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;

namespace ZebraBear;

/// <summary>
/// Second room. Currently empty — populate PopulateRoom() to add content.
/// Follows the exact same pattern as GameScene.
/// </summary>
public class Room2Scene : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private Camera _camera;
    private Room   _room;

    public Room2Scene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        _camera = new Camera(vp.Width / (float)vp.Height, new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        _room = new Room(_game.GraphicsDevice,
            wallColor:  new Color(18, 22, 30),
            floorColor: new Color(14, 12, 20),
            ceilColor:  new Color(10, 12, 18),
            label:      "???");

        PopulateRoom();
    }

    private void PopulateRoom()
    {
        // Add entities here exactly as in GameScene.PopulateRoom()
        // e.g. _room.Add(new BillboardEntity { ... });
        //      _room.Add(MeshEntity.CreateTable(...));
    }

    public void OnEnter()
    {
        MapData.CurrentRoomId = "Room2";
        MapData.SetDiscovered("Room2");
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