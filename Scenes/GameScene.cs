using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using ZebraBear.Core;
using ZebraBear.Entities;

namespace ZebraBear.Scenes;

/// <summary>
/// Room 1 — the starting room.
/// Add content in PopulateRoom() only. Nothing else needs to change.
/// </summary>
public class GameScene : IScene
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private Camera            _camera;
    private Room              _room;
    private DialogueBox       _dialogueBox;
    private CharacterPortrait _portrait;

    private Entity          _targeted;
    private BillboardEntity _activeSpeaker;
    private bool            _dialogueActive;

    private MouseState    _prevMouse;
    private KeyboardState _prevKeyboard;

    public GameScene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        _camera = new Camera(vp.Width / (float)vp.Height, new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        _room = new Room(_game.GraphicsDevice, label: "Main Hall");

        PopulateRoom();

        _dialogueBox = new DialogueBox(_game, _spriteBatch);
        _portrait    = new CharacterPortrait(_game, _spriteBatch);
    }

    private void PopulateRoom()
    {
        // --- Characters ---

        _room.Add(new BillboardEntity
        {
            Name        = "Kei",
            Position    = new Vector3(-4f, -0.75f, -10f),
            Width       = 2.2f,
            Height      = 4.5f,
            Tint        = new Color(180, 160, 220),
            IsCharacter = true,
            // Sprite   = Assets.CharacterKei,
            Dialogue    = new[]
            {
                "Oh? You're actually talking to me?",
                "I didn't think anyone would bother...",
                "Well. Since you're here. My name is Kei.",
                "Try not to die before we get off this island."
            }
        });

        _room.Add(new BillboardEntity
        {
            Name        = "Haru",
            Position    = new Vector3(5f, -1f, -11f),
            Width       = 2f,
            Height      = 4f,
            Tint        = new Color(160, 200, 180),
            IsCharacter = true,
            // Sprite   = Assets.CharacterHaru,
            Dialogue    = new[]
            {
                "Don't look at me.",
                "I'm not interested in talking."
            }
        });

        // --- Wall objects ---

        // Door frame (decorative — no name = not interactable, not solid)
        var doorFrame = MeshEntity.CreateOrientedBox(
            name:     "",
            dialogue: null,
            centre:   new Vector3(13.9f, -3f + 3.6f / 2f, -4f),
            w: 2.6f,  h: 3.6f,
            normal:   MeshBuilder.FaceEast,
            tint:     new Color(40, 30, 22),
            depth:    0.15f);
        doorFrame.Solid = false;
        _room.Add(doorFrame);

        // Door (interactive)
        var door = MeshEntity.CreateOrientedBox(
            name:     "Locked Door",
            dialogue: new[] { "The door is heavy but unlocked.", "Go through?" },
            centre:   new Vector3(13.9f, -3f + 3.2f / 2f, -4f),
            w: 2.2f,  h: 3.2f,
            normal:   MeshBuilder.FaceEast,
            tint:     new Color(90, 65, 45),
            depth:    0.25f);

        door.OnInteract = (result) => { if (result == 0) _game.GoToRoom2(); };
        _room.Add(door);

        // Notice board backing (decorative — not solid)
        var boardBacking = MeshEntity.CreateOrientedBox(
            name:     "",
            dialogue: null,
            centre:   new Vector3(-8f, -3f + 4.2f + 1.4f / 2f, -13.9f),
            w: 2.8f,  h: 2.0f,
            normal:   MeshBuilder.FaceNorth,
            tint:     new Color(35, 28, 18),
            depth:    0.1f);
        boardBacking.Solid = false;
        _room.Add(boardBacking);

        // Notice board (interactive)
        _room.Add(MeshEntity.CreateOrientedBox(
            name:     "Notice Board",
            dialogue: new[]
            {
                "A class schedule pinned up neatly.",
                "Someone has drawn a small bear in the corner.",
                "It's smiling."
            },
            centre:   new Vector3(-8f, -3f + 4.2f + 1.4f / 2f, -13.9f),
            w: 2.4f,  h: 1.6f,
            normal:   MeshBuilder.FaceNorth,
            tint:     new Color(160, 130, 85),
            depth:    0.18f));

        // --- Furniture ---

        _room.Add(MeshEntity.CreateTable(
            name:     "Table",
            dialogue: new[]
            {
                "A plain wooden table.",
                "Nothing on it. Someone cleared it recently."
            },
            position: new Vector3(0f, -3f, 0f),
            w: 2.8f, d: 1.4f, h: 1.1f,
            tint:     new Color(120, 85, 55)));
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------

    public void OnEnter()
    {
        MapData.CurrentRoomId = "MainHall";
        _game.IsMouseVisible  = false;
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    public void OnExit() { }

    public void Update(GameTime gameTime)
    {
        var   mouse = Mouse.GetState();
        float dt    = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Always update portrait so it can finish fading out after dialogue ends
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

        _camera.Update(gameTime, captureMouse: true, _room.ResolveCollisions);

        _targeted = _room.UpdateRaycast(new Ray(_camera.Position, _camera.Forward));

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

        // Show portrait and fade out the billboard if this is a character
        if (entity is BillboardEntity billboard && billboard.Sprite != null)
        {
            _activeSpeaker              = billboard;
            _activeSpeaker.ActiveSpeaker = true;
            _portrait.Show(billboard.Sprite);
        }

        // Mark character as met for the Characters tab
        if (!string.IsNullOrEmpty(entity.Name))
            CharacterData.SetMet(entity.Name);
    }

    public void Draw(GameTime gameTime)
    {
        var gd = _game.GraphicsDevice;
        var vp = gd.Viewport;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        _room.Draw(_camera, _dialogueActive, dt);
        DrawHUD(vp, gameTime);

        // Portrait sits above the room, below the dialogue box
        if (_dialogueActive || _portrait.IsVisible)
            _portrait.Draw();

        if (_dialogueActive)
            _dialogueBox.Draw(gameTime);
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

        _spriteBatch.DrawString(Assets.MenuFont,
            "WASD move   Shift run   Mouse look   E interact   Esc pause",
            new Vector2(16, vp.Height - 28), new Color(60, 55, 80));

        _spriteBatch.End();
    }
}