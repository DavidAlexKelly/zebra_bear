using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace ZebraBear;

public class Camera
{
    public Vector3 Position;
    public float Yaw   = 0f;
    public float Pitch = 0f;

    public float MoveSpeed  = 4f;
    public float LookSpeed  = 0.002f;

    private const float MaxPitch = 1.4f;

    private int _centerX;
    private int _centerY;
    private float _aspect;

    // Forward includes pitch — used for looking and raycasting
    public Vector3 Forward => Vector3.Normalize(new Vector3(
        (float)(Math.Sin(Yaw) * Math.Cos(Pitch)),
        (float)Math.Sin(Pitch),
        (float)(Math.Cos(Yaw) * Math.Cos(Pitch))));

    // Right is always horizontal — used for strafing and billboard orientation
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

    public void Update(GameTime gameTime, bool captureMouse)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var   kb = Keyboard.GetState();
        var   ms = Mouse.GetState();

        // --- Mouse look ---
        if (captureMouse)
        {
            int dx = ms.X - _centerX;
            int dy = ms.Y - _centerY;
            Yaw   -= dx * LookSpeed;
            Pitch -= dy * LookSpeed;
            Pitch  = Math.Clamp(Pitch, -MaxPitch, MaxPitch);
            Mouse.SetPosition(_centerX, _centerY);
        }

        // --- WASD movement — always horizontal regardless of pitch ---
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
        // Clamp inside room bounds
        Position.X = Math.Clamp(Position.X, -13f, 13f);
        Position.Z = Math.Clamp(Position.Z, -13f, 13f);
        Position.Y = 0f;
    }
}