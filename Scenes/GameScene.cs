using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Entities;

namespace ZebraBear.Scenes;

/// <summary>
/// Room 1 — the starting room (Main Hall).
/// All content defined in Data/rooms.json (id: "MainHall").
/// The door leads to the Hub (plus-shaped connecting room).
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
        var (wall, floor, ceil, label) = GameLoader.ReadRoomColors("MainHall");

        _camera = new Camera(vp.Width / (float)vp.Height, new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        _room = new Room(_game.GraphicsDevice,
            wallColor: wall, floorColor: floor, ceilColor: ceil, label: label);

        // Door leads to Hub
        var overrides = new Dictionary<string, Action<int>>
        {
            ["Locked Door"] = result => { if (result == 0) NavigationBus.RequestNavigate("Hub"); }
        };

        GameLoader.LoadRoom("MainHall", _room, overrides);

        _dialogueBox = new DialogueBox(_game, _spriteBatch);
        _portrait    = new CharacterPortrait(_game, _spriteBatch);
    }

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

        if (entity is BillboardEntity billboard && billboard.Sprite != null)
        {
            _activeSpeaker               = billboard;
            _activeSpeaker.ActiveSpeaker = true;
            _portrait.Show(billboard.Sprite);
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
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

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