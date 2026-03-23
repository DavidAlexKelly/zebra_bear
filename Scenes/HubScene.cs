using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Entities;

namespace ZebraBear.Scenes;

/// <summary>
/// The plus-shaped hub room that connects the four wing rooms.
///
///          [ North door → Room North ]
///                  |
///  [West door] - [Hub] - [East door]
///                  |
///          [ South door → Room South ]
///
/// To change destinations, edit the door OnInteract callbacks in Load().
/// Room content (signs, objects) goes in rooms.json under id "Hub".
/// </summary>
public class HubScene : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private Camera      _camera;
    private PlusRoom3D  _geometry;
    private Room        _room;       // entity container (uses a 1×1 invisible Room3D as dummy)
    private BasicEffect _meshEffect;
    private BasicEffect _billboardEffect;

    private DialogueBox       _dialogueBox;
    private CharacterPortrait _portrait;

    private Entity          _targeted;
    private BillboardEntity _activeSpeaker;
    private bool            _dialogueActive;

    private MouseState    _prevMouse;
    private KeyboardState _prevKeyboard;

    // Four door entities — kept as fields so we can attach callbacks after construction
    private MeshEntity _doorNorth;
    private MeshEntity _doorSouth;
    private MeshEntity _doorWest;
    private MeshEntity _doorEast;

    public HubScene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        _camera = new Camera(vp.Width / (float)vp.Height, Vector3.Zero);
        _camera.SetViewportCentre(vp.Width, vp.Height);
        _camera.MoveSpeed = 5f;

        // Read colours from rooms.json if present, else use defaults
        var (wall, floor, ceil, _) = GameLoader.ReadRoomColors("Hub");
        _geometry = new PlusRoom3D(_game.GraphicsDevice, wall, floor, ceil);

        // We use a dummy Room purely as an entity container — its Room3D geometry
        // is never drawn; we draw PlusRoom3D directly instead.
        _room = new Room(_game.GraphicsDevice, label: "Hub");

        // Utility effects for entity rendering
        _meshEffect = new BasicEffect(_game.GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };
        _billboardEffect = new BasicEffect(_game.GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };

        BuildDoors();

        // Load any extra entities from rooms.json (signs, characters, objects)
        GameLoader.LoadRoom("Hub", _room);

        _dialogueBox = new DialogueBox(_game, _spriteBatch);
        _portrait    = new CharacterPortrait(_game, _spriteBatch);
    }

    // -----------------------------------------------------------------------
    // Door construction
    // -----------------------------------------------------------------------

    private void BuildDoors()
    {
        float hf     = PlusRoom3D.H  / 2f;
        float extent = PlusRoom3D.Extent;
        float doorY  = -hf + 1.6f;   // centre of door vertically

        // ---- North door (end of north corridor, wall at Z = -Extent) ----
        _doorNorth = MeshEntity.CreateOrientedBox(
            name:     "North Door",
            dialogue: new[] { "A heavy door. Beyond it lies the north wing.", "Go through?" },
            centre:   new Vector3(0f, doorY, -extent + 0.15f),
            w: 3.8f,  h: 3.2f,
            normal:   MeshBuilder.FaceNorth,
            tint:     new Color(90, 65, 45),
            depth:    0.22f);
        _doorNorth.OnInteract = result => { if (result == 0) NavigationBus.RequestNavigate("RoomNorth"); };
        _room.Add(_doorNorth);

        // ---- South door ----
        _doorSouth = MeshEntity.CreateOrientedBox(
            name:     "South Door",
            dialogue: new[] { "A heavy door. Beyond it lies the south wing.", "Go through?" },
            centre:   new Vector3(0f, doorY, extent - 0.15f),
            w: 3.8f,  h: 3.2f,
            normal:   MeshBuilder.FaceSouth,
            tint:     new Color(90, 65, 45),
            depth:    0.22f);
        _doorSouth.OnInteract = result => { if (result == 0) NavigationBus.RequestNavigate("RoomSouth"); };
        _room.Add(_doorSouth);

        // ---- West door ----
        _doorWest = MeshEntity.CreateOrientedBox(
            name:     "West Door",
            dialogue: new[] { "A heavy door. Beyond it lies the west wing.", "Go through?" },
            centre:   new Vector3(-extent + 0.15f, doorY, 0f),
            w: 3.8f,  h: 3.2f,
            normal:   MeshBuilder.FaceWest,
            tint:     new Color(90, 65, 45),
            depth:    0.22f);
        _doorWest.OnInteract = result => { if (result == 0) NavigationBus.RequestNavigate("RoomWest"); };
        _room.Add(_doorWest);

        // ---- East door ----
        _doorEast = MeshEntity.CreateOrientedBox(
            name:     "East Door",
            dialogue: new[] { "A heavy door. Beyond it lies the east wing.", "Go through?" },
            centre:   new Vector3(extent - 0.15f, doorY, 0f),
            w: 3.8f,  h: 3.2f,
            normal:   MeshBuilder.FaceEast,
            tint:     new Color(90, 65, 45),
            depth:    0.22f);
        _doorEast.OnInteract = result => { if (result == 0) NavigationBus.RequestNavigate("RoomEast"); };
        _room.Add(_doorEast);
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------

    public void OnEnter()
    {
        MapData.CurrentRoomId = "Hub";
        MapData.SetDiscovered("Hub");
        _camera.Position     = Vector3.Zero;
        _camera.Yaw          = 0f;
        _camera.Pitch        = 0f;
        _game.IsMouseVisible = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnExit() { }

    public void Update(GameTime gameTime)
    {
        var   mouse = Mouse.GetState();
        float dt    = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _portrait.Update(dt);

        if (_dialogueActive)
        {
            _game.IsMouseVisible = true;
            _dialogueBox.Update(gameTime, mouse, _prevMouse);

            if (_dialogueBox.IsFinished)
            {
                _dialogueActive      = false;
                _game.IsMouseVisible = false;
                _portrait.Hide();
                if (_activeSpeaker != null)
                {
                    _activeSpeaker.ActiveSpeaker = false;
                    _activeSpeaker = null;
                }
                var vp = _game.GraphicsDevice.Viewport;
                Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
            }

            _prevMouse    = mouse;
            _prevKeyboard = Keyboard.GetState();
            return;
        }

        // Use plus-aware collision instead of simple box clamp
        _camera.UpdateInPlus(gameTime, captureMouse: true, _room.ResolveCollisions);

        _targeted = _room.UpdateRaycast(
            new Ray(_camera.Position, _camera.Forward), maxDistance: 18f);

        var  kb       = Keyboard.GetState();
        bool interact =
            (mouse.LeftButton      == ButtonState.Released &&
             _prevMouse.LeftButton == ButtonState.Pressed) ||
            (kb.IsKeyDown(Keys.E) && !_prevKeyboard.IsKeyDown(Keys.E));

        if (interact && _targeted != null)
            StartDialogue(_targeted);

        _prevMouse    = mouse;
        _prevKeyboard = kb;
    }

    private void StartDialogue(Entity entity)
    {
        _dialogueBox.SpeakerName = entity.Name;
        _dialogueBox.OnChoice    = entity.HasChoice ? entity.OnInteract : null;
        _dialogueBox.StartDialogue(
            entity.Dialogue,
            choices: entity.HasChoice ? new[] { "Yes", "No" } : null);
        _dialogueActive = true;

        if (entity is BillboardEntity bb && bb.Sprite != null)
        {
            _activeSpeaker               = bb;
            _activeSpeaker.ActiveSpeaker = true;
            _portrait.Show(bb.Sprite);
        }

        if (!string.IsNullOrEmpty(entity.Name))
            CharacterData.SetMet(entity.Name);
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public void Draw(GameTime gameTime)
    {
        var gd = _game.GraphicsDevice;
        var vp = gd.Viewport;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        // Draw plus geometry directly (bypasses Room3D)
        _geometry.Draw(_camera.View, _camera.Projection);

        // Draw entities (doors, signs, characters) via shared effects
        DrawMeshEntities();
        DrawBillboardEntities();

        DrawHUD(vp, gameTime);

        if (_dialogueActive || _portrait.IsVisible)
            _portrait.Draw();
        if (_dialogueActive)
            _dialogueBox.Draw(gameTime);
    }

    private void DrawMeshEntities()
    {
        var gd = _game.GraphicsDevice;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullNone;

        _meshEffect.View               = _camera.View;
        _meshEffect.Projection         = _camera.Projection;
        _meshEffect.World              = Matrix.Identity;
        _meshEffect.VertexColorEnabled = true;
        _meshEffect.TextureEnabled     = false;

        // We access the room's entity list via Room.Draw's internal path,
        // but since Room doesn't expose entities we replicate the call here.
        // Workaround: call _room.Draw with dialogueActive suppressing billboard tint,
        // but skip the Room3D geometry by passing a dummy camera... instead we
        // draw entities directly via the public Draw overload below.
        _room.DrawEntitiesOnly(_camera, _dialogueActive, 0f, _meshEffect, _billboardEffect);
    }

    private void DrawBillboardEntities()
    {
        // Handled inside DrawEntitiesOnly above
    }

    private void DrawHUD(Viewport vp, GameTime gameTime)
    {
        if (_dialogueActive) return;

        _spriteBatch.Begin();

        int  cx        = vp.Width  / 2;
        int  cy        = vp.Height / 2;
        bool hasTarget = _targeted != null;
        var  crossCol  = hasTarget ? new Color(232, 0, 61) : new Color(255, 255, 255, 160);
        int  crossSize = hasTarget ? 10 : 6;

        _spriteBatch.Draw(Assets.Pixel,
            new Rectangle(cx - crossSize, cy - 1, crossSize * 2, 2), crossCol);
        _spriteBatch.Draw(Assets.Pixel,
            new Rectangle(cx - 1, cy - crossSize, 2, crossSize * 2), crossCol);

        if (_targeted?.Name != null)
        {
            float pulse  = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f);
            float alpha  = 0.8f + pulse * 0.2f;
            var   prompt = $"[ {_targeted.Name} ]  E / Click";
            var   size   = Assets.MenuFont.MeasureString(prompt);
            var   pos    = new Vector2(cx - size.X / 2f, cy + 28f);

            _spriteBatch.Draw(Assets.Pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 4,
                    (int)size.X + 20, (int)size.Y + 8),
                new Color(0, 0, 0, 180));
            _spriteBatch.Draw(Assets.Pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 4, 3, (int)size.Y + 8),
                new Color(232, 0, 61));
            _spriteBatch.DrawString(Assets.MenuFont, prompt, pos, Color.White * alpha);
        }

        _spriteBatch.DrawString(Assets.MenuFont, "Hub",
            new Vector2(20, 20), new Color(60, 55, 80));

        _spriteBatch.DrawString(Assets.MenuFont,
            "WASD move   Shift run   Mouse look   E interact   Esc pause",
            new Vector2(16, vp.Height - 28), new Color(60, 55, 80));

        _spriteBatch.End();
    }
}