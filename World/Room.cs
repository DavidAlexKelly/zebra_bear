using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using ZebraBear.Entities;

namespace ZebraBear;

/// <summary>
/// A self-contained room: geometry + entities + interaction logic.
/// Scenes create and configure a Room; they don't manage entity lists directly.
///
/// To make a new room:
///   var room = new Room(gd, wallColor, floorColor, ceilColor, label: "Library");
///   room.Add(MeshEntity.CreateTable(...));
///   room.Add(new BillboardEntity { ... });
/// </summary>
public class Room
{
    // -----------------------------------------------------------------------
    // Public config
    // -----------------------------------------------------------------------
    public string Label = "";   // shown top-left in the HUD

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------
    private readonly GraphicsDevice _gd;
    private readonly Room3D         _geometry;
    private readonly BasicEffect    _meshEffect;
    private readonly BasicEffect    _billboardEffect;

    private readonly List<Entity>   _entities = new();

    private Entity _targeted;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public Room(GraphicsDevice gd,
        Color? wallColor  = null,
        Color? floorColor = null,
        Color? ceilColor  = null,
        string label      = "")
    {
        _gd    = gd;
        Label  = label;

        _geometry = new Room3D(gd, wallColor, floorColor, ceilColor);

        _meshEffect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };

        _billboardEffect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false
        };
    }

    // -----------------------------------------------------------------------
    // Entity management
    // -----------------------------------------------------------------------

    public void Add(Entity entity)
    {
        if (entity is BillboardEntity b) b.UpdateBounds();
        _entities.Add(entity);
    }

    public void Remove(Entity entity) => _entities.Remove(entity);

    // -----------------------------------------------------------------------
    // Collision
    // -----------------------------------------------------------------------

    /// <summary>
    /// Given a proposed player position, pushes it out of any solid entity
    /// bounds and returns the corrected position.
    ///
    /// Uses a player capsule approximated as a small AABB:
    ///   width/depth = PlayerRadius * 2, height = PlayerHeight.
    ///
    /// Call this from Camera.Update() after applying movement, before clamping.
    /// </summary>
    public Vector3 ResolveCollisions(Vector3 position)
    {
        const float PlayerRadius = 0.4f;

        var playerMin = new Vector3(position.X - PlayerRadius, 0, position.Z - PlayerRadius);
        var playerMax = new Vector3(position.X + PlayerRadius, 0, position.Z + PlayerRadius);

        foreach (var e in _entities)
        {
            if (!e.Solid) continue;

            var bounds = e.Bounds;

            // No overlap — skip (Y check omitted: player is always on the floor,
            // so we resolve horizontally against any object regardless of height)
            if (playerMax.X <= bounds.Min.X || playerMin.X >= bounds.Max.X) continue;
            if (playerMax.Z <= bounds.Min.Z || playerMin.Z >= bounds.Max.Z) continue;

            // Overlap on all axes — find the smallest penetration and push out
            float overlapNegX = playerMax.X - bounds.Min.X;
            float overlapPosX = bounds.Max.X - playerMin.X;
            float overlapNegZ = playerMax.Z - bounds.Min.Z;
            float overlapPosZ = bounds.Max.Z - playerMin.Z;

            // Only resolve horizontally — we don't want vertical pushback
            // (player is always clamped to floor anyway)
            float minOverlap = MathF.Min(
                MathF.Min(overlapNegX, overlapPosX),
                MathF.Min(overlapNegZ, overlapPosZ));

            if (minOverlap == overlapNegX) position.X = bounds.Min.X - PlayerRadius;
            else if (minOverlap == overlapPosX) position.X = bounds.Max.X + PlayerRadius;
            else if (minOverlap == overlapNegZ) position.Z = bounds.Min.Z - PlayerRadius;
            else                                position.Z = bounds.Max.Z + PlayerRadius;

            // Recompute player bounds after each push so multiple
            // overlapping objects resolve correctly
            playerMin.X = position.X - PlayerRadius;
            playerMax.X = position.X + PlayerRadius;
            playerMin.Z = position.Z - PlayerRadius;
            playerMax.Z = position.Z + PlayerRadius;
        }

        return position;
    }

    // -----------------------------------------------------------------------
    // Update — raycast only; interaction is handled by the owning scene
    // -----------------------------------------------------------------------

    public Entity UpdateRaycast(Ray ray, float maxDistance = 14f)
    {
        _targeted = null;
        float closest = float.MaxValue;

        foreach (var e in _entities)
        {
            if (string.IsNullOrEmpty(e.Name)) continue;
            if (e.Raycast(ray, out float dist) && dist < closest && dist < maxDistance)
            {
                closest   = dist;
                _targeted = e;
            }
        }

        return _targeted;
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public void Draw(Camera camera, bool dialogueActive, float dt = 0f)
    {
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        // Update billboard fade for active speaker
        foreach (var e in _entities)
            if (e is BillboardEntity b) b.UpdateSpeakerFade(dt);

        _geometry.Draw(camera.View, camera.Projection);

        DrawMeshEntities(camera, dialogueActive);
        DrawBillboardEntities(camera, dialogueActive);
    }

    private void DrawMeshEntities(Camera camera, bool dialogueActive)
    {
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.RasterizerState   = RasterizerState.CullNone;

        _meshEffect.View               = camera.View;
        _meshEffect.Projection         = camera.Projection;
        _meshEffect.World              = Matrix.Identity;
        _meshEffect.VertexColorEnabled = true;
        _meshEffect.TextureEnabled     = false;
        _meshEffect.LightingEnabled    = false;

        foreach (var e in _entities)
        {
            if (e is not MeshEntity) continue;
            bool targeted = e == _targeted && !dialogueActive;
            e.Draw(_gd, _meshEffect, targeted);
        }
    }

    private void DrawBillboardEntities(Camera camera, bool dialogueActive)
    {
        // Read depth but don't write it — transparent pixels on sprites
        // won't occlude geometry behind them
        _gd.DepthStencilState = DepthStencilState.DepthRead;
        _gd.BlendState        = BlendState.AlphaBlend;
        _gd.RasterizerState   = RasterizerState.CullNone;

        // Depth-sort billboards back to front
        var billboards = new List<BillboardEntity>();
        foreach (var e in _entities)
            if (e is BillboardEntity b) billboards.Add(b);

        billboards.Sort((a, b) =>
            Vector3.DistanceSquared(b.Position, camera.Position)
                .CompareTo(Vector3.DistanceSquared(a.Position, camera.Position)));

        _billboardEffect.View               = camera.View;
        _billboardEffect.Projection         = camera.Projection;
        _billboardEffect.World              = Matrix.Identity;
        _billboardEffect.VertexColorEnabled = true;
        _billboardEffect.LightingEnabled    = false;

        BillboardEntity.CamRight = camera.Right;

        foreach (var b in billboards)
        {
            bool targeted      = b == _targeted && !dialogueActive;
            _billboardEffect.Alpha = b.SpeakerAlpha;
            b.Draw(_gd, _billboardEffect, targeted);
        }
        _billboardEffect.Alpha = 1f; // reset
    }
}