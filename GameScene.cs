using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace ZebraBear;

public class GameScene
{
    private Game         _game;
    private SpriteBatch  _spriteBatch;
    private SpriteFont   _font;
    private SpriteFont   _nameFont;
    private Texture2D    _pixel;

    private Camera        _camera;
    private Room3D        _room;
    private BasicEffect   _billboardEffect;
    private BasicEffect   _worldObjectEffect;

    private List<Billboard>     _billboards        = new();
    private List<WorldObject3D> _worldObjects      = new();
    private List<Furniture3D>   _furniture         = new();

    private Billboard     _targeted          = null;
    private WorldObject3D _targetedObject    = null;
    private Furniture3D   _targetedFurniture = null;

    private DialogueBox  _dialogueBox;
    private bool         _dialogueActive = false;

    private MouseState    _prevMouse;
    private KeyboardState _prevKeyboard;

    private VertexPositionColorTexture[] _quadVerts = new VertexPositionColorTexture[4];
    private short[] _quadIdx = new short[] { 0, 1, 2, 0, 2, 3 };

    public GameScene(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Load()
    {
        var vp = _game.GraphicsDevice.Viewport;

        _font     = _game.Content.Load<SpriteFont>("Fonts/MenuFont");
        _nameFont = _game.Content.Load<SpriteFont>("Fonts/TitleFont");

        _pixel = new Texture2D(_game.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _camera = new Camera(
            vp.Width / (float)vp.Height,
            new Vector3(0, 0, 8f));
        _camera.SetViewportCentre(vp.Width, vp.Height);

        _room = new Room3D(_game.GraphicsDevice);

        _billboardEffect = new BasicEffect(_game.GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };

        _worldObjectEffect = new BasicEffect(_game.GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };

        _dialogueBox = new DialogueBox(
            _game, _spriteBatch, _font, _nameFont, _pixel);

        // -------------------------------------------------------
        // Billboards — characters
        // -------------------------------------------------------
        _billboards.Add(new Billboard
        {
            Position    = new Vector3(-4f, -0.75f, -10f),
            Width       = 2.2f,
            Height      = 4.5f,
            Tint        = new Color(180, 160, 220),
            Name        = "Kei",
            IsCharacter = true,
            Dialogue    = new[]
            {
                "Oh? You're actually talking to me?",
                "I didn't think anyone would bother...",
                "Well. Since you're here. My name is Kei.",
                "Try not to die before we get off this island."
            }
        });

        _billboards.Add(new Billboard
        {
            Position    = new Vector3(5f, -1f, -11f),
            Width       = 2f,
            Height      = 4f,
            Tint        = new Color(160, 200, 180),
            Name        = "Haru",
            IsCharacter = true,
            Dialogue    = new[]
            {
                "Don't look at me.",
                "I'm not interested in talking."
            }
        });

        // -------------------------------------------------------
        // World objects — wall mounted
        // -------------------------------------------------------

        // Door frame (decorative)
        _worldObjects.Add(new WorldObject3D(
            name:     "",
            dialogue: null,
            tint:     new Color(40, 30, 22),
            centre:   new Vector3(13.9f, -3f + 3.6f / 2f, -4f),
            w:        2.6f,
            h:        3.6f,
            facing:   WallFacing.East,
            depth:    0.15f
        ));

        // Door (interactive)
        var door = new WorldObject3D(
            name:     "Locked Door",
            dialogue: new[] { "The door is heavy but unlocked.", "Go through?" },
            tint:     new Color(90, 65, 45),
            centre:   new Vector3(13.9f, -3f + 3.2f / 2f, -4f),
            w:        2.2f,
            h:        3.2f,
            facing:   WallFacing.East,
            depth:    0.25f
        );
        _worldObjects.Add(door);

        // Notice board backing (decorative)
        _worldObjects.Add(new WorldObject3D(
            name:     "",
            dialogue: null,
            tint:     new Color(35, 28, 18),
            centre:   new Vector3(-8f, -3f + 4.2f + 1.4f / 2f, -13.9f),
            w:        2.8f,
            h:        2.0f,
            facing:   WallFacing.North,
            depth:    0.1f
        ));

        // Notice board (interactive)
        _worldObjects.Add(new WorldObject3D(
            name:     "Notice Board",
            dialogue: new[]
            {
                "A class schedule pinned up neatly.",
                "Someone has drawn a small bear in the corner.",
                "It's smiling."
            },
            tint:    new Color(160, 130, 85),
            centre:  new Vector3(-8f, -3f + 4.2f + 1.4f / 2f, -13.9f),
            w:       2.4f,
            h:       1.6f,
            facing:  WallFacing.North,
            depth:   0.18f
        ));

        // -------------------------------------------------------
        // Furniture — freestanding
        // -------------------------------------------------------
        _furniture.Add(new Furniture3D(
            name:     "Table",
            dialogue: new[]
            {
                "A plain wooden table.",
                "Nothing on it. Someone cleared it recently."
            },
            position: new Vector3(0f, -3f, 0f),
            w:        2.8f,
            d:        1.4f,
            h:        1.1f,
            tint:     new Color(120, 85, 55)
        ));
    }

    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();

        if (_dialogueActive)
        {
            _game.IsMouseVisible = true;
            _dialogueBox.Update(gameTime, mouse, _prevMouse);
            if (_dialogueBox.IsFinished)
            {
                _dialogueActive      = false;
                _game.IsMouseVisible = false;
                var vp = _game.GraphicsDevice.Viewport;
                Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
            }
            _prevMouse    = mouse;
            _prevKeyboard = Keyboard.GetState();
            return;
        }

        _camera.Update(gameTime, captureMouse: true);

        // -------------------------------------------------------
        // Raycast from camera forward
        // -------------------------------------------------------
        _targeted          = null;
        _targetedObject    = null;
        _targetedFurniture = null;

        var   ray         = BuildCentreRay();
        float closestDist = float.MaxValue;

        foreach (var b in _billboards)
        {
            if (b.Raycast(ray, out float dist) && dist < closestDist && dist < 12f)
            {
                closestDist        = dist;
                _targeted          = b;
                _targetedObject    = null;
                _targetedFurniture = null;
            }
        }

        foreach (var obj in _worldObjects)
        {
            if (string.IsNullOrEmpty(obj.Name)) continue;
            if (obj.Raycast(ray, out float dist) && dist < closestDist && dist < 14f)
            {
                closestDist        = dist;
                _targeted          = null;
                _targetedObject    = obj;
                _targetedFurniture = null;
            }
        }

        foreach (var f in _furniture)
        {
            if (string.IsNullOrEmpty(f.Name)) continue;
            if (f.Raycast(ray, out float dist) && dist < closestDist && dist < 10f)
            {
                closestDist        = dist;
                _targeted          = null;
                _targetedObject    = null;
                _targetedFurniture = f;
            }
        }

        // -------------------------------------------------------
        // Interact
        // -------------------------------------------------------
        var kb = Keyboard.GetState();
        bool interact =
            (mouse.LeftButton      == ButtonState.Released &&
             _prevMouse.LeftButton == ButtonState.Pressed) ||
            (kb.IsKeyDown(Keys.E) && !_prevKeyboard.IsKeyDown(Keys.E));

        if (interact)
        {
            if (_targeted != null)
            {
                _dialogueBox.SpeakerName = _targeted.Name;
                _dialogueBox.StartDialogue(_targeted.Dialogue);
                _dialogueActive = true;
            }
            else if (_targetedObject != null)
            {
                _dialogueBox.SpeakerName = _targetedObject.Name;

                // Door gets a yes/no choice
                if (_targetedObject.Name == "Locked Door")
                {
                    _dialogueBox.OnChoice = (result) =>
                    {
                        if (result == 0) // Yes
                            _game.ChangeScene(Scene.Room2);
                    };
                    _dialogueBox.StartDialogue(
                        _targetedObject.Dialogue,
                        choices: new[] { "Yes", "No" });
                }
                else
                {
                    _dialogueBox.OnChoice = null;
                    _dialogueBox.StartDialogue(_targetedObject.Dialogue);
                }
                _dialogueActive = true;
            }
            else if (_targetedFurniture != null)
            {
                _dialogueBox.SpeakerName = _targetedFurniture.Name;
                _dialogueBox.StartDialogue(_targetedFurniture.Dialogue);
                _dialogueActive = true;
            }
        }

        _prevMouse    = mouse;
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
        DrawWorldObjects();
        DrawFurniture();
        DrawBillboards();
        DrawHUD(vp, gameTime);

        if (_dialogueActive)
            _dialogueBox.Draw(gameTime);
    }

    private void DrawWorldObjects()
    {
        var gd = _game.GraphicsDevice;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullNone;

        _worldObjectEffect.View               = _camera.View;
        _worldObjectEffect.Projection         = _camera.Projection;
        _worldObjectEffect.World              = Matrix.Identity;
        _worldObjectEffect.VertexColorEnabled = true;
        _worldObjectEffect.TextureEnabled     = false;
        _worldObjectEffect.LightingEnabled    = false;

        foreach (var obj in _worldObjects)
        {
            bool targeted = obj == _targetedObject && !_dialogueActive;
            obj.Draw(gd, _worldObjectEffect, targeted);
        }
    }

    private void DrawFurniture()
    {
        var gd = _game.GraphicsDevice;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        _worldObjectEffect.View               = _camera.View;
        _worldObjectEffect.Projection         = _camera.Projection;
        _worldObjectEffect.World              = Matrix.Identity;
        _worldObjectEffect.VertexColorEnabled = true;
        _worldObjectEffect.TextureEnabled     = false;
        _worldObjectEffect.LightingEnabled    = false;

        foreach (var f in _furniture)
        {
            bool targeted = f == _targetedFurniture && !_dialogueActive;
            f.Draw(gd, _worldObjectEffect, targeted);
        }
    }

    private void DrawBillboards()
    {
        var gd = _game.GraphicsDevice;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.BlendState        = BlendState.AlphaBlend;
        gd.RasterizerState   = RasterizerState.CullNone;

        var sorted = new List<Billboard>(_billboards);
        sorted.Sort((a, b) =>
            Vector3.DistanceSquared(b.Position, _camera.Position)
                .CompareTo(
            Vector3.DistanceSquared(a.Position, _camera.Position)));

        _billboardEffect.View               = _camera.View;
        _billboardEffect.Projection         = _camera.Projection;
        _billboardEffect.World              = Matrix.Identity;
        _billboardEffect.VertexColorEnabled = true;
        _billboardEffect.TextureEnabled     = false;
        _billboardEffect.LightingEnabled    = false;

        var camRight = _camera.Right;

        foreach (var b in sorted)
        {
            bool targeted  = b == _targeted && !_dialogueActive;
            var  tint      = targeted ? new Color(255, 220, 255) : b.Tint;
            float hw       = b.Width  / 2f;
            float hh       = b.Height / 2f;

            _quadVerts[0] = new VertexPositionColorTexture(
                b.Position + (-camRight * hw) + (Vector3.Up *  hh), tint, Vector2.Zero);
            _quadVerts[1] = new VertexPositionColorTexture(
                b.Position + ( camRight * hw) + (Vector3.Up *  hh), tint, Vector2.UnitX);
            _quadVerts[2] = new VertexPositionColorTexture(
                b.Position + ( camRight * hw) + (Vector3.Up * -hh), tint, Vector2.One);
            _quadVerts[3] = new VertexPositionColorTexture(
                b.Position + (-camRight * hw) + (Vector3.Up * -hh), tint, Vector2.UnitY);

            foreach (var pass in _billboardEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _quadVerts, 0, 4,
                    _quadIdx,   0, 2);
            }

            if (b.IsCharacter)
            {
                float hr      = b.Width * 0.28f;
                var   headPos = b.Position + Vector3.Up * (hh + hr * 0.6f);
                var   headTint = targeted
                    ? new Color(255, 230, 200)
                    : new Color(
                        Math.Min(255, (int)(b.Tint.R * 1.3f)),
                        Math.Min(255, (int)(b.Tint.G * 1.1f)),
                        Math.Min(255, (int)(b.Tint.B * 1.0f)));

                _quadVerts[0] = new VertexPositionColorTexture(
                    headPos + (-camRight * hr) + (Vector3.Up *  hr), headTint, Vector2.Zero);
                _quadVerts[1] = new VertexPositionColorTexture(
                    headPos + ( camRight * hr) + (Vector3.Up *  hr), headTint, Vector2.UnitX);
                _quadVerts[2] = new VertexPositionColorTexture(
                    headPos + ( camRight * hr) + (Vector3.Up * -hr), headTint, Vector2.One);
                _quadVerts[3] = new VertexPositionColorTexture(
                    headPos + (-camRight * hr) + (Vector3.Up * -hr), headTint, Vector2.UnitY);

                foreach (var pass in _billboardEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _quadVerts, 0, 4,
                        _quadIdx,   0, 2);
                }
            }
        }
    }

    private void DrawHUD(Viewport vp, GameTime gameTime)
    {
        if (_dialogueActive) return;

        _spriteBatch.Begin();

        int cx = vp.Width  / 2;
        int cy = vp.Height / 2;

        bool hasTarget = _targeted != null
                      || _targetedObject    != null
                      || _targetedFurniture != null;

        var crossCol  = hasTarget
            ? new Color(232, 0, 61)
            : new Color(255, 255, 255, 160);
        int crossSize = hasTarget ? 10 : 6;

        _spriteBatch.Draw(_pixel,
            new Rectangle(cx - crossSize, cy - 1, crossSize * 2, 2), crossCol);
        _spriteBatch.Draw(_pixel,
            new Rectangle(cx - 1, cy - crossSize, 2, crossSize * 2), crossCol);

        string targetName = _targeted?.Name
            ?? _targetedObject?.Name
            ?? _targetedFurniture?.Name;

        if (targetName != null)
        {
            float pulse  = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f);
            float alpha  = 0.8f + pulse * 0.2f;
            var   prompt = $"[ {targetName} ]  E / Click";
            var   size   = _font.MeasureString(prompt);
            var   pos    = new Vector2(cx - size.X / 2f, cy + 28f);

            _spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 4,
                    (int)size.X + 20, (int)size.Y + 8),
                new Color(0, 0, 0, 180));
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - 10, (int)pos.Y - 4,
                    3, (int)size.Y + 8),
                new Color(232, 0, 61));

            _spriteBatch.DrawString(_font, prompt, pos, Color.White * alpha);
        }

        _spriteBatch.DrawString(_font,
            "WASD move   Shift run   Mouse look   E interact   Esc pause",
            new Vector2(16, vp.Height - 28),
            new Color(60, 55, 80));

        _spriteBatch.End();
    }

    private Ray BuildCentreRay()
    {
        return new Ray(_camera.Position, _camera.Forward);
    }
}