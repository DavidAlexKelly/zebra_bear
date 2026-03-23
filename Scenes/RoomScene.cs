// ======== Scenes/RoomScene.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.Entities;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

/// <summary>
/// Generic first-person room scene.
/// </summary>
public enum RoomSceneType { Box, Plus }

public class RoomScene : IScene
{
    private readonly Game _game;
    private readonly SpriteBatch _spriteBatch;
    private readonly string _roomId;
    private readonly RoomSceneType _sceneType;

    private Camera _camera;
    private Room _room;
    private PlusRoom3D _plusGeometry;
    private BasicEffect _meshEffect;
    private BasicEffect _billboardEffect;
    private DialogueBox _dialogueBox;
    private CharacterPortrait _portrait;
    private Entity _targeted;
    private BillboardEntity _activeSpeaker;
    private bool _dialogueActive;
    private MouseState _prevMouse;
    private KeyboardState _prevKeyboard;

    public RoomScene(Game game, SpriteBatch spriteBatch, string roomId,
        RoomSceneType sceneType = RoomSceneType.Box)
    {
        _game = game;
        _spriteBatch = spriteBatch;
        _roomId = roomId;
        _sceneType = sceneType;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;
        var (wall, floor, ceil, label) = GameLoader.ReadRoomColors(_roomId);

        if (_sceneType == RoomSceneType.Plus)
        {
            _camera = new Camera(vp.Width / (float)vp.Height, Vector3.Zero);
            _camera.MoveSpeed = 5f;
            _plusGeometry = new PlusRoom3D(_game.GraphicsDevice, wall, floor, ceil);
            _room = new Room(_game.GraphicsDevice, label: label);
            _meshEffect = new BasicEffect(_game.GraphicsDevice)
            { VertexColorEnabled = true, TextureEnabled = false, LightingEnabled = false };
            _billboardEffect = new BasicEffect(_game.GraphicsDevice)
            { VertexColorEnabled = true, TextureEnabled = false, LightingEnabled = false };
        }
        else
        {
            _camera = new Camera(vp.Width / (float)vp.Height, new Vector3(0, 0, 8f));
            _room = new Room(_game.GraphicsDevice, wall, floor, ceil, label);
        }

        _camera.SetViewportCentre(vp.Width, vp.Height);
        GameLoader.LoadRoom(_roomId, _room);
        _dialogueBox = new DialogueBox(_game, _spriteBatch);
        _portrait = new CharacterPortrait(_game, _spriteBatch);
    }

    public void OnEnter()
    {
        MapData.CurrentRoomId = _roomId;
        MapData.SetDiscovered(_roomId);
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        _camera.SetViewportCentre(vp.Width, vp.Height);
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);

        if (_sceneType == RoomSceneType.Plus)
        { _camera.Position = Vector3.Zero; _camera.Yaw = 0f; _camera.Pitch = 0f; }
        else
        { _camera.Position = new Vector3(0, 0, 8f); _camera.Yaw = 0f; _camera.Pitch = 0f; }
    }

    public void OnExit() { }

    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _portrait.Update(dt);

        if (_dialogueActive)
        {
            _game.IsMouseVisible = true;
            _dialogueBox.Update(gameTime, mouse, _prevMouse);
            if (_dialogueBox.IsFinished)
            {
                _dialogueActive = false;
                _game.IsMouseVisible = false;
                _portrait.Hide();
                if (_activeSpeaker != null) { _activeSpeaker.ActiveSpeaker = false; _activeSpeaker = null; }
                var vp = _game.GraphicsDevice.Viewport;
                Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
            }
            _prevMouse = mouse;
            _prevKeyboard = Keyboard.GetState();
            return;
        }

        if (_sceneType == RoomSceneType.Plus)
            _camera.UpdateInPlus(gameTime, captureMouse: true, _room.ResolveCollisions);
        else
            _camera.Update(gameTime, captureMouse: true, _room.ResolveCollisions);

        float maxDist = _sceneType == RoomSceneType.Plus ? 18f : 12f;
        _targeted = _room.UpdateRaycast(new Ray(_camera.Position, _camera.Forward), maxDist);

        var kb = Keyboard.GetState();
        bool interact =
            (mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed) ||
            (kb.IsKeyDown(Keys.E) && !_prevKeyboard.IsKeyDown(Keys.E));

        if (interact && _targeted != null)
            StartDialogue(_targeted);

        _prevMouse = mouse;
        _prevKeyboard = kb;
    }

    private void StartDialogue(Entity entity)
    {
        if (entity.Interaction == null) return;
 
        _dialogueBox.SpeakerName = entity.Name;
        _dialogueBox.StartDialogue(entity.Interaction);
        _dialogueActive = true;
 
        if (entity is BillboardEntity bb && bb.Sprite != null)
        {
            _activeSpeaker = bb;
            _activeSpeaker.ActiveSpeaker = true;
            _portrait.Show(bb.Sprite);
        }
 
        if (!string.IsNullOrEmpty(entity.Name))
            CharacterData.SetMet(entity.Name);
    }
 

    public void Draw(GameTime gameTime)
    {
        var gd = _game.GraphicsDevice;
        var vp = gd.Viewport;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState = BlendState.Opaque;
        gd.RasterizerState = RasterizerState.CullCounterClockwise;

        if (_sceneType == RoomSceneType.Plus)
        {
            _plusGeometry.Draw(_camera.View, _camera.Projection);
            _room.DrawEntitiesOnly(_camera, _dialogueActive, dt, _meshEffect, _billboardEffect);
        }
        else
            _room.Draw(_camera, _dialogueActive, dt);

        DrawHUD(vp, gameTime);

        if (_dialogueActive || _portrait.IsVisible)
            _portrait.Draw();
        if (_dialogueActive)
            _dialogueBox.Draw(gameTime);
    }

    private void DrawHUD(Viewport vp, GameTime gameTime)
    {
        if (_dialogueActive) return;

        _spriteBatch.Begin();

        int cx = vp.Width / 2;
        int cy = vp.Height / 2;
        bool hasTarget = _targeted != null;

        // Crosshair
        var crossCol = hasTarget ? LayoutDraw.Accent : new Color(255, 255, 255, 160);
        int crossSize = hasTarget ? 10 : 6;
        LayoutDraw.Rect(_spriteBatch, new Rectangle(cx - 1, cy - crossSize, 2, crossSize * 2), crossCol);
        LayoutDraw.Rect(_spriteBatch, new Rectangle(cx - crossSize, cy - 1, crossSize * 2, 2), crossCol);

        // Interact prompt
        if (hasTarget && !string.IsNullOrEmpty(_targeted.Name))
        {
            var label = $"[E] {_targeted.Name}";
            var labelSize = Assets.MenuFont.MeasureString(label);
            _spriteBatch.DrawString(Assets.MenuFont, label,
                new Vector2(cx - labelSize.X / 2f, cy + 20), LayoutDraw.Accent);
        }

        // Room label - top left
        _spriteBatch.DrawString(Assets.MenuFont, _room.Label,
            new Vector2(20, 16), new Color(60, 55, 80));

        // Controls - bottom left
        _spriteBatch.DrawString(Assets.MenuFont, "WASD Move   Shift Run   Mouse Look   Esc Pause",
            new Vector2(16, vp.Height - 32), new Color(50, 48, 70));

        _spriteBatch.End();
    }
}