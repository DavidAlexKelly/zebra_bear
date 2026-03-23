using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using ZebraBear.Entities;

namespace ZebraBear;

/// <summary>
/// A self-contained room: geometry + entities + interaction logic.
/// Scenes create and configure a Room; they don't manage entity lists directly.
/// </summary>
public class Room
{
    public string Label = "";

    private readonly GraphicsDevice _gd;
    private readonly Room3D         _geometry;
    private readonly BasicEffect    _meshEffect;
    private readonly BasicEffect    _billboardEffect;

    private readonly List<Entity>   _entities = new();
    private Entity _targeted;

    public Room(GraphicsDevice gd,
        Color? wallColor  = null,
        Color? floorColor = null,
        Color? ceilColor  = null,
        string label      = "")
    {
        _gd   = gd;
        Label = label;

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

    public Vector3 ResolveCollisions(Vector3 position)
    {
        const float PlayerRadius = 0.4f;

        var playerMin = new Vector3(position.X - PlayerRadius, 0, position.Z - PlayerRadius);
        var playerMax = new Vector3(position.X + PlayerRadius, 0, position.Z + PlayerRadius);

        foreach (var e in _entities)
        {
            if (!e.Solid) continue;

            var bounds = e.Bounds;
            if (playerMax.X <= bounds.Min.X || playerMin.X >= bounds.Max.X) continue;
            if (playerMax.Z <= bounds.Min.Z || playerMin.Z >= bounds.Max.Z) continue;

            float overlapNegX = playerMax.X - bounds.Min.X;
            float overlapPosX = bounds.Max.X - playerMin.X;
            float overlapNegZ = playerMax.Z - bounds.Min.Z;
            float overlapPosZ = bounds.Max.Z - playerMin.Z;

            float minOverlap = MathF.Min(
                MathF.Min(overlapNegX, overlapPosX),
                MathF.Min(overlapNegZ, overlapPosZ));

            if      (minOverlap == overlapNegX) position.X = bounds.Min.X - PlayerRadius;
            else if (minOverlap == overlapPosX) position.X = bounds.Max.X + PlayerRadius;
            else if (minOverlap == overlapNegZ) position.Z = bounds.Min.Z - PlayerRadius;
            else                                position.Z = bounds.Max.Z + PlayerRadius;

            playerMin.X = position.X - PlayerRadius;
            playerMax.X = position.X + PlayerRadius;
            playerMin.Z = position.Z - PlayerRadius;
            playerMax.Z = position.Z + PlayerRadius;
        }

        return position;
    }

    // -----------------------------------------------------------------------
    // Raycast
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
    // Draw — full (geometry + entities)
    // -----------------------------------------------------------------------

    public void Draw(Camera camera, bool dialogueActive, float dt = 0f)
    {
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.RasterizerState   = RasterizerState.CullCounterClockwise;

        foreach (var e in _entities)
            if (e is BillboardEntity b) b.UpdateSpeakerFade(dt);

        _geometry.Draw(camera.View, camera.Projection);

        DrawMeshEntities(camera, dialogueActive);
        DrawBillboardEntities(camera, dialogueActive);
    }

    // -----------------------------------------------------------------------
    // Draw — entities only (no Room3D geometry)
    // Used by scenes that supply their own room geometry (e.g. HubScene).
    // -----------------------------------------------------------------------

    public void DrawEntitiesOnly(Camera camera, bool dialogueActive, float dt,
        BasicEffect meshEffect, BasicEffect billboardEffect)
    {
        foreach (var e in _entities)
            if (e is BillboardEntity b) b.UpdateSpeakerFade(dt);

        DrawMeshEntitiesWithEffect(camera, dialogueActive, meshEffect);
        DrawBillboardEntitiesWithEffect(camera, dialogueActive, billboardEffect);
    }

    // -----------------------------------------------------------------------
    // Private draw helpers
    // -----------------------------------------------------------------------

    private void DrawMeshEntities(Camera camera, bool dialogueActive)
    {
        DrawMeshEntitiesWithEffect(camera, dialogueActive, _meshEffect);
    }

    private void DrawMeshEntitiesWithEffect(Camera camera, bool dialogueActive, BasicEffect fx)
    {
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.RasterizerState   = RasterizerState.CullNone;

        fx.View               = camera.View;
        fx.Projection         = camera.Projection;
        fx.World              = Matrix.Identity;
        fx.VertexColorEnabled = true;
        fx.TextureEnabled     = false;
        fx.LightingEnabled    = false;

        foreach (var e in _entities)
        {
            if (e is not MeshEntity) continue;
            bool targeted = e == _targeted && !dialogueActive;
            e.Draw(_gd, fx, targeted);
        }
    }

    private void DrawBillboardEntities(Camera camera, bool dialogueActive)
    {
        DrawBillboardEntitiesWithEffect(camera, dialogueActive, _billboardEffect);
    }

    private void DrawBillboardEntitiesWithEffect(Camera camera, bool dialogueActive, BasicEffect fx)
    {
        _gd.DepthStencilState = DepthStencilState.DepthRead;
        _gd.BlendState        = BlendState.AlphaBlend;
        _gd.RasterizerState   = RasterizerState.CullNone;

        var billboards = new List<BillboardEntity>();
        foreach (var e in _entities)
            if (e is BillboardEntity b) billboards.Add(b);

        billboards.Sort((a, b) =>
            Vector3.DistanceSquared(b.Position, camera.Position)
                .CompareTo(Vector3.DistanceSquared(a.Position, camera.Position)));

        fx.View               = camera.View;
        fx.Projection         = camera.Projection;
        fx.World              = Matrix.Identity;
        fx.VertexColorEnabled = true;
        fx.LightingEnabled    = false;

        BillboardEntity.CamRight = camera.Right;

        foreach (var b in billboards)
        {
            bool targeted = b == _targeted && !dialogueActive;
            fx.Alpha = b.SpeakerAlpha;
            b.Draw(_gd, fx, targeted);
        }
        fx.Alpha = 1f;
    }
}