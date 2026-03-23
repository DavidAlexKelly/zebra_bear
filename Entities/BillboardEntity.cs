using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ZebraBear.Entities;

/// <summary>
/// A world entity that always faces the camera.
///
/// Renders as:
///   - A coloured placeholder quad when Sprite is null
///   - A textured quad when Sprite is assigned (real character art)
///
/// IsCharacter = true adds a separate head quad above the body,
/// matching the Danganronpa two-part cutout look.
///
/// Usage:
///   var kei = new BillboardEntity
///   {
///       Name        = "Kei",
///       Position    = new Vector3(-4f, -0.75f, -10f),
///       Width       = 2.2f,
///       Height      = 4.5f,
///       Tint        = new Color(180, 160, 220),
///       IsCharacter = true,
///       Sprite      = Assets.CharacterKei,  // null = placeholder
///       Dialogue    = new[] { "Hello." }
///   };
///   room.Add(kei);
/// </summary>
public class BillboardEntity : Entity
{
    // -----------------------------------------------------------------------
    // Public configuration
    // -----------------------------------------------------------------------

    public Vector3   Position;
    public float     Width;
    public float     Height;

    /// <summary>Vertex colour used when Sprite is null (placeholder mode).</summary>
    public Color     Tint        = Color.White;

    /// <summary>
    /// Texture to display. Set to null to use the coloured Tint placeholder.
    /// Assign Assets.CharacterX once art is ready — no other code changes needed.
    /// </summary>
    public Texture2D Sprite      = null;

    /// <summary>
    /// When true the billboard fades out during dialogue.
    /// Set by the scene when this entity is the active speaker.
    /// </summary>
    public bool  ActiveSpeaker  = false;
    private float _speakerAlpha = 1f;
    public float  SpeakerAlpha  => _speakerAlpha;

    public void UpdateSpeakerFade(float dt)
    {
        float target  = ActiveSpeaker ? 0f : 1f;
        _speakerAlpha = MathHelper.Lerp(_speakerAlpha, target, dt * 8f);
    }

    /// <summary>
    /// When true, draws a second rounded head quad above the body.
    /// Matches the Danganronpa two-part cutout silhouette.
    /// </summary>
    public bool IsCharacter = false;

    // -----------------------------------------------------------------------
    // Internal — shared across all instances to avoid per-frame allocations
    // -----------------------------------------------------------------------

    private static readonly VertexPositionColorTexture[] _quad =
        new VertexPositionColorTexture[4];

    private static readonly short[] _quadIdx = { 0, 1, 2, 0, 2, 3 };

    /// <summary>
    /// Set by Room.DrawBillboards() before iterating.
    /// All billboards in a frame share the same camera-right vector.
    /// </summary>
    internal static Vector3 CamRight;

    // -----------------------------------------------------------------------
    // Bounds
    // -----------------------------------------------------------------------

    /// <summary>
    /// Must be called after setting Position / Width / Height.
    /// Room.Add() calls this automatically.
    /// </summary>
    public void UpdateBounds()
    {
        _bounds = new BoundingBox(
            new Vector3(Position.X - Width  / 2f, Position.Y - Height / 2f, Position.Z - 0.2f),
            new Vector3(Position.X + Width  / 2f, Position.Y + Height / 2f, Position.Z + 0.2f));
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public override void Draw(GraphicsDevice gd, BasicEffect effect, bool targeted)
    {
        DrawBodyQuad(gd, effect, targeted);

        // Only draw the placeholder head quad when there's no sprite —
        // real sprites contain the full character already
        if (IsCharacter && Sprite == null)
            DrawHeadQuad(gd, effect, targeted);
    }

    private void DrawBodyQuad(GraphicsDevice gd, BasicEffect effect, bool targeted)
    {
        bool useTexture = Sprite != null;

        // BasicEffect requires these to be mutually exclusive
        effect.TextureEnabled      = useTexture;
        effect.VertexColorEnabled  = !useTexture;

        if (useTexture)
        {
            effect.Texture = Sprite;
            var tint = targeted ? new Color(255, 220, 255) : Color.White;

            float hw = Width  / 2f;
            float hh = Height / 2f;

            _quad[0] = Q(Position + (-CamRight * hw) + Vector3.Up *  hh, tint, Vector2.Zero);
            _quad[1] = Q(Position + ( CamRight * hw) + Vector3.Up *  hh, tint, Vector2.UnitX);
            _quad[2] = Q(Position + ( CamRight * hw) + Vector3.Up * -hh, tint, Vector2.One);
            _quad[3] = Q(Position + (-CamRight * hw) + Vector3.Up * -hh, tint, Vector2.UnitY);
        }
        else
        {
            var   tint = targeted ? new Color(255, 220, 255) : Tint;
            float hw   = Width  / 2f;
            float hh   = Height / 2f;

            _quad[0] = Q(Position + (-CamRight * hw) + Vector3.Up *  hh, tint, Vector2.Zero);
            _quad[1] = Q(Position + ( CamRight * hw) + Vector3.Up *  hh, tint, Vector2.UnitX);
            _quad[2] = Q(Position + ( CamRight * hw) + Vector3.Up * -hh, tint, Vector2.One);
            _quad[3] = Q(Position + (-CamRight * hw) + Vector3.Up * -hh, tint, Vector2.UnitY);
        }

        Submit(gd, effect);
    }

    private void DrawHeadQuad(GraphicsDevice gd, BasicEffect effect, bool targeted)
    {
        effect.TextureEnabled     = false;
        effect.VertexColorEnabled = true;

        float hh      = Height / 2f;
        float hr      = Width  * 0.28f;
        var   headPos = Position + Vector3.Up * (hh + hr * 0.6f);
        var   tint    = targeted
            ? new Color(255, 230, 200)
            : new Color(
                Math.Min(255, (int)(Tint.R * 1.3f)),
                Math.Min(255, (int)(Tint.G * 1.1f)),
                Math.Min(255, (int)(Tint.B * 1.0f)));

        _quad[0] = Q(headPos + (-CamRight * hr) + Vector3.Up *  hr, tint, Vector2.Zero);
        _quad[1] = Q(headPos + ( CamRight * hr) + Vector3.Up *  hr, tint, Vector2.UnitX);
        _quad[2] = Q(headPos + ( CamRight * hr) + Vector3.Up * -hr, tint, Vector2.One);
        _quad[3] = Q(headPos + (-CamRight * hr) + Vector3.Up * -hr, tint, Vector2.UnitY);

        Submit(gd, effect);
    }

    private static VertexPositionColorTexture Q(Vector3 pos, Color col, Vector2 uv)
        => new VertexPositionColorTexture(pos, col, uv);

    private static void Submit(GraphicsDevice gd, BasicEffect effect)
    {
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _quad, 0, 4,
                _quadIdx, 0, 2);
        }
    }
}