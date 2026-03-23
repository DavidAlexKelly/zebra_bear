using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace ZebraBear;

public class Camera
{
    public Vector3 Position;
    public float   Yaw   = 0f;
    public float   Pitch = 0f;

    public float MoveSpeed = 4f;
    public float LookSpeed = 0.002f;

    private const float MaxPitch = 1.4f;

    private int   _centerX;
    private int   _centerY;
    private float _aspect;

    public Vector3 Forward => Vector3.Normalize(new Vector3(
        (float)(Math.Sin(Yaw) * Math.Cos(Pitch)),
        (float)Math.Sin(Pitch),
        (float)(Math.Cos(Yaw) * Math.Cos(Pitch))));

    public Vector3 Right => Vector3.Normalize(new Vector3(
        (float)Math.Sin(Yaw + MathHelper.PiOver2), 0f,
        (float)Math.Cos(Yaw + MathHelper.PiOver2)));

    public Matrix View =>
        Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);

    public Matrix Projection =>
        Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(75f), _aspect, 0.1f, 200f);

    public Camera(float aspect, Vector3 startPos)
    {
        _aspect  = aspect;
        Position = startPos;
    }

    public void SetViewportCentre(int w, int h)
    {
        _centerX = w / 2;
        _centerY = h / 2;
    }

    /// <summary>
    /// Standard update — box-clamped room (used by GameScene, Room2Scene, etc.)
    /// </summary>
    public void Update(GameTime gameTime, bool captureMouse,
        Func<Vector3, Vector3> resolveCollisions = null)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        ApplyMouseLook(captureMouse);
        ApplyMovement(dt);

        if (resolveCollisions != null)
            Position = resolveCollisions(Position);

        // Standard box clamp
        Position.X = Math.Clamp(Position.X, -13f, 13f);
        Position.Z = Math.Clamp(Position.Z, -13f, 13f);
        Position.Y = 0f;
    }

    /// <summary>
    /// Plus-room update — clamps the player inside the plus-shaped navigable area.
    /// Used by HubScene.
    /// </summary>
    public void UpdateInPlus(GameTime gameTime, bool captureMouse,
        Func<Vector3, Vector3> resolveCollisions = null)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        ApplyMouseLook(captureMouse);
        ApplyMovement(dt);

        if (resolveCollisions != null)
            Position = resolveCollisions(Position);

        Position = PlusRoom3D.ClampToPlus(Position, margin: 0.5f);
        Position.Y = 0f;
    }

    // -----------------------------------------------------------------------
    // Shared movement helpers
    // -----------------------------------------------------------------------

    private void ApplyMouseLook(bool capture)
    {
        if (!capture) return;
        var ms = Mouse.GetState();
        int dx = ms.X - _centerX;
        int dy = ms.Y - _centerY;
        Yaw   -= dx * LookSpeed;
        Pitch -= dy * LookSpeed;
        Pitch  = Math.Clamp(Pitch, -MaxPitch, MaxPitch);
        Mouse.SetPosition(_centerX, _centerY);
    }

    private void ApplyMovement(float dt)
    {
        var kb = Keyboard.GetState();
        var flatForward = Vector3.Normalize(new Vector3(
            (float)Math.Sin(Yaw), 0f, (float)Math.Cos(Yaw)));

        var move = Vector3.Zero;
        if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))    move += flatForward;
        if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))  move -= flatForward;
        if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  move += Right;
        if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) move -= Right;

        if (move != Vector3.Zero)
        {
            move.Normalize();
            float speed = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift)
                ? MoveSpeed * 2f
                : MoveSpeed;
            Position += move * speed * dt;
        }
    }
}