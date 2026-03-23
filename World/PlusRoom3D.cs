using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ZebraBear;

/// <summary>
/// A plus-shaped hub room built from procedural vertex-colour geometry.
///
/// Layout (top-down, Y is up):
///
///          [ North corridor ]
///                 |
///   [West] - [ Centre ] - [East]
///                 |
///          [ South corridor ]
///
/// All measurements in world units.
/// A door is placed at the far end of each corridor.
///
/// Construction:
///   var hub = new PlusRoom3D(graphicsDevice);
///   hub.Draw(view, projection);
///
/// Navigable bounds are exposed via CorridorBounds so Camera can clamp
/// the player inside the plus shape.
/// </summary>
public class PlusRoom3D
{
    // -----------------------------------------------------------------------
    // Dimensions
    // -----------------------------------------------------------------------

    /// <summary>Half-width of the square centre section.</summary>
    public const float CH = 6f;     // centre half-extent (so centre is 12×12)

    /// <summary>Half-width of each corridor (the narrow axis).</summary>
    public const float CW = 3f;     // corridor half-width (corridors are 6 units wide)

    /// <summary>Length of each corridor (from centre edge to outer wall).</summary>
    public const float CL = 10f;    // corridor length

    /// <summary>Room height (floor to ceiling).</summary>
    public const float H  = 6f;

    // Derived: outer extent of the whole plus
    public const float Extent = CH + CL;   // = 16 units from origin to outer wall

    private readonly GraphicsDevice _gd;
    private readonly BasicEffect    _effect;
    private VertexBuffer            _vb;
    private IndexBuffer             _ib;

    // Colours
    private readonly Color _wallColor;
    private readonly Color _floorColor;
    private readonly Color _ceilColor;
    private readonly Color _accent = new Color(232, 0, 61);

    public PlusRoom3D(GraphicsDevice gd,
        Color? wallColor  = null,
        Color? floorColor = null,
        Color? ceilColor  = null)
    {
        _gd        = gd;
        _wallColor  = wallColor  ?? new Color(22, 20, 38);
        _floorColor = floorColor ?? new Color(18, 16, 28);
        _ceilColor  = ceilColor  ?? new Color(12, 12, 22);

        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            LightingEnabled    = false
        };

        Build();
    }

    // -----------------------------------------------------------------------
    // Geometry builder
    // -----------------------------------------------------------------------

    private void Build()
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        float hf = H / 2f;   // half-height (floor at -hf, ceiling at +hf)

        // Slightly darkened side-wall tone for corridors for depth
        var sideWall   = Darken(_wallColor, 0.88f);
        var trimColor  = _accent;
        var lineColor  = new Color(
            (int)(_floorColor.R * 1.6f),
            (int)(_floorColor.G * 1.6f),
            (int)(_floorColor.B * 1.6f));

        // ------------------------------------------------------------------
        // Helper — adds a quad (two triangles)
        // ------------------------------------------------------------------
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            short i = (short)verts.Count;
            verts.Add(new VertexPositionColor(a, col));
            verts.Add(new VertexPositionColor(b, col));
            verts.Add(new VertexPositionColor(c, col));
            verts.Add(new VertexPositionColor(d, col));
            idx.AddRange(new short[] {
                i, (short)(i+1), (short)(i+2),
                i, (short)(i+2), (short)(i+3) });
        }

        // ------------------------------------------------------------------
        // Helper — axis-aligned floor slab with grid
        // ------------------------------------------------------------------
        void FloorSlab(float x0, float x1, float z0, float z1)
        {
            Quad(new Vector3(x0, -hf, z0),
                 new Vector3(x1, -hf, z0),
                 new Vector3(x1, -hf, z1),
                 new Vector3(x0, -hf, z1), _floorColor);
        }

        void CeilSlab(float x0, float x1, float z0, float z1)
        {
            Quad(new Vector3(x0,  hf, z1),
                 new Vector3(x1,  hf, z1),
                 new Vector3(x1,  hf, z0),
                 new Vector3(x0,  hf, z0), _ceilColor);
        }

        // ------------------------------------------------------------------
        // Helper — solid wall panel (always vertical)
        // ------------------------------------------------------------------
        // Facing +X  (normal points right)
        void WallPX(float x, float z0, float z1, Color col)
        {
            Quad(new Vector3(x,  hf, z0), new Vector3(x,  hf, z1),
                 new Vector3(x, -hf, z1), new Vector3(x, -hf, z0), col);
        }
        // Facing -X
        void WallNX(float x, float z0, float z1, Color col)
        {
            Quad(new Vector3(x,  hf, z1), new Vector3(x,  hf, z0),
                 new Vector3(x, -hf, z0), new Vector3(x, -hf, z1), col);
        }
        // Facing +Z
        void WallPZ(float z, float x0, float x1, Color col)
        {
            Quad(new Vector3(x1,  hf, z), new Vector3(x0,  hf, z),
                 new Vector3(x0, -hf, z), new Vector3(x1, -hf, z), col);
        }
        // Facing -Z
        void WallNZ(float z, float x0, float x1, Color col)
        {
            Quad(new Vector3(x0,  hf, z), new Vector3(x1,  hf, z),
                 new Vector3(x1, -hf, z), new Vector3(x0, -hf, z), col);
        }

        // ------------------------------------------------------------------
        // Helper — baseboard strip along a wall
        // ------------------------------------------------------------------
        void BaseboardPX(float x, float z0, float z1)
        {
            Quad(new Vector3(x + 0.01f, -hf + 0.4f, z0),
                 new Vector3(x + 0.01f, -hf + 0.4f, z1),
                 new Vector3(x + 0.01f, -hf,         z1),
                 new Vector3(x + 0.01f, -hf,         z0), trimColor);
        }
        void BaseboardNX(float x, float z0, float z1)
        {
            Quad(new Vector3(x - 0.01f, -hf + 0.4f, z1),
                 new Vector3(x - 0.01f, -hf + 0.4f, z0),
                 new Vector3(x - 0.01f, -hf,         z0),
                 new Vector3(x - 0.01f, -hf,         z1), trimColor);
        }
        void BaseboardPZ(float z, float x0, float x1)
        {
            Quad(new Vector3(x1, -hf + 0.4f, z + 0.01f),
                 new Vector3(x0, -hf + 0.4f, z + 0.01f),
                 new Vector3(x0, -hf,         z + 0.01f),
                 new Vector3(x1, -hf,         z + 0.01f), trimColor);
        }
        void BaseboardNZ(float z, float x0, float x1)
        {
            Quad(new Vector3(x0, -hf + 0.4f, z - 0.01f),
                 new Vector3(x1, -hf + 0.4f, z - 0.01f),
                 new Vector3(x1, -hf,         z - 0.01f),
                 new Vector3(x0, -hf,         z - 0.01f), trimColor);
        }

        // ------------------------------------------------------------------
        // Helper — floor grid lines over a rectangular area
        // ------------------------------------------------------------------
        void GridLines(float x0, float x1, float z0, float z1, int lines)
        {
            float xStep = (x1 - x0) / lines;
            float zStep = (z1 - z0) / lines;
            for (int i = 0; i <= lines; i++)
            {
                float x = x0 + i * xStep;
                float z = z0 + i * zStep;
                const float t = 0.015f;
                const float y = 0.005f;
                // Along Z
                Quad(new Vector3(x-t, -hf+y, z0), new Vector3(x+t, -hf+y, z0),
                     new Vector3(x+t, -hf+y, z1), new Vector3(x-t, -hf+y, z1), lineColor);
                // Along X
                Quad(new Vector3(x0, -hf+y, z-t), new Vector3(x1, -hf+y, z-t),
                     new Vector3(x1, -hf+y, z+t), new Vector3(x0, -hf+y, z+t), lineColor);
            }
        }

        // ------------------------------------------------------------------
        // Helper — ceiling light panel
        // ------------------------------------------------------------------
        void CeilLight(float cx, float cz, float w, float d)
        {
            float hw = w / 2f, hd = d / 2f;
            Quad(new Vector3(cx-hw, hf-0.01f, cz-hd),
                 new Vector3(cx+hw, hf-0.01f, cz-hd),
                 new Vector3(cx+hw, hf-0.01f, cz+hd),
                 new Vector3(cx-hw, hf-0.01f, cz+hd),
                 new Color(200, 195, 220));
        }

        // ------------------------------------------------------------------
        // Helper — door opening recess on an end wall (just the dark glass)
        // ------------------------------------------------------------------
        void DoorRecess(bool northSouth, float wallCoord, float lateral, Color doorGlass)
        {
            float dw = 2.0f;  // door half-width
            float dh = 3.0f;  // door height from floor
            float y0 = -hf;
            float y1 = -hf + dh;

            if (northSouth)
            {
                // wall runs along X; lateral = X centre
                float z = wallCoord;
                Quad(new Vector3(lateral - dw,  y1, z + 0.02f),
                     new Vector3(lateral + dw,  y1, z + 0.02f),
                     new Vector3(lateral + dw,  y0, z + 0.02f),
                     new Vector3(lateral - dw,  y0, z + 0.02f), doorGlass);
            }
            else
            {
                // wall runs along Z; lateral = Z centre
                float x = wallCoord;
                Quad(new Vector3(x + 0.02f,  y1, lateral - dw),
                     new Vector3(x + 0.02f,  y1, lateral + dw),
                     new Vector3(x + 0.02f,  y0, lateral + dw),
                     new Vector3(x + 0.02f,  y0, lateral - dw), doorGlass);
            }
        }

        // ==================================================================
        // Geometry assembly
        // ==================================================================

        // ------ CENTRE SECTION  (-CH .. +CH on both X and Z) ------
        FloorSlab(-CH, CH, -CH, CH);
        CeilSlab (-CH, CH, -CH, CH);
        GridLines(-CH, CH, -CH, CH, 4);
        CeilLight(0, 0, 3f, 3f);

        // Centre section corner walls (the bits between corridor openings)
        // North face: left and right of north corridor opening
        WallNZ(-CH, -CH, -CW, _wallColor);
        WallNZ(-CH,  CW,  CH, _wallColor);
        // South face
        WallPZ( CH, -CH, -CW, _wallColor);
        WallPZ( CH,  CW,  CH, _wallColor);
        // West face: above and below west corridor opening
        WallPX(-CH, -CH, -CW, sideWall);
        WallPX(-CH,  CW,  CH, sideWall);
        // East face
        WallNX( CH, -CH, -CW, sideWall);
        WallNX( CH,  CW,  CH, sideWall);

        // Centre baseboards
        BaseboardNZ(-CH, -CH, -CW);  BaseboardNZ(-CH,  CW,  CH);
        BaseboardPZ( CH, -CH, -CW);  BaseboardPZ( CH,  CW,  CH);
        BaseboardPX(-CH, -CH, -CW);  BaseboardPX(-CH,  CW,  CH);
        BaseboardNX( CH, -CH, -CW);  BaseboardNX( CH,  CW,  CH);

        // ------ NORTH CORRIDOR  (Z goes from -CH to -Extent) ------
        {
            float z0 = -Extent, z1 = -CH;
            FloorSlab(-CW, CW, z0, z1);
            CeilSlab (-CW, CW, z0, z1);
            GridLines(-CW, CW, z0, z1, 3);
            CeilLight(0, (z0+z1)/2f, 1.5f, 2f);

            WallPX(-CW, z0, z1, sideWall);   // west corridor wall
            WallNX( CW, z0, z1, sideWall);   // east corridor wall
            // End wall (outer north) — with door gap
            float ew = z0;
            // Left of door
            WallNZ(ew, -CW, -2.0f, _wallColor);
            // Right of door
            WallNZ(ew,  2.0f,  CW, _wallColor);
            // Above door
            Quad(new Vector3(-2.0f,  hf,    ew), new Vector3(2.0f,  hf,    ew),
                 new Vector3( 2.0f, -hf+3f, ew), new Vector3(-2.0f,-hf+3f, ew), _wallColor);

            DoorRecess(true, ew, 0f, new Color(14, 22, 42));

            BaseboardPX(-CW, z0, z1);
            BaseboardNX( CW, z0, z1);
            BaseboardNZ(ew, -CW, -2.1f);
            BaseboardNZ(ew,  2.1f,  CW);
        }

        // ------ SOUTH CORRIDOR  (Z from +CH to +Extent) ------
        {
            float z0 = CH, z1 = Extent;
            FloorSlab(-CW, CW, z0, z1);
            CeilSlab (-CW, CW, z0, z1);
            GridLines(-CW, CW, z0, z1, 3);
            CeilLight(0, (z0+z1)/2f, 1.5f, 2f);

            WallPX(-CW, z0, z1, sideWall);
            WallNX( CW, z0, z1, sideWall);
            float ew = z1;
            WallPZ(ew, -CW, -2.0f, _wallColor);
            WallPZ(ew,  2.0f,  CW, _wallColor);
            Quad(new Vector3(-2.0f,  hf,    ew), new Vector3(2.0f,  hf,    ew),
                 new Vector3( 2.0f, -hf+3f, ew), new Vector3(-2.0f,-hf+3f, ew), _wallColor);

            DoorRecess(true, ew, 0f, new Color(14, 22, 42));

            BaseboardPX(-CW, z0, z1);
            BaseboardNX( CW, z0, z1);
            BaseboardPZ(ew, -CW, -2.1f);
            BaseboardPZ(ew,  2.1f,  CW);
        }

        // ------ WEST CORRIDOR  (X from -Extent to -CH) ------
        {
            float x0 = -Extent, x1 = -CH;
            FloorSlab(x0, x1, -CW, CW);
            CeilSlab (x0, x1, -CW, CW);
            GridLines(x0, x1, -CW, CW, 3);
            CeilLight((x0+x1)/2f, 0, 2f, 1.5f);

            WallNZ(-CW, x0, x1, _wallColor);   // north wall of west corridor
            WallPZ( CW, x0, x1, _wallColor);   // south wall
            float ew = x0;
            WallPX(ew, -CW, -2.0f, sideWall);
            WallPX(ew,  2.0f,  CW, sideWall);
            Quad(new Vector3(ew,  hf,   -2.0f), new Vector3(ew,  hf,    2.0f),
                 new Vector3(ew, -hf+3f, 2.0f), new Vector3(ew, -hf+3f,-2.0f), sideWall);

            DoorRecess(false, ew, 0f, new Color(14, 22, 42));

            BaseboardNZ(-CW, x0, x1);
            BaseboardPZ( CW, x0, x1);
            BaseboardPX(ew, -CW, -2.1f);
            BaseboardPX(ew,  2.1f,  CW);
        }

        // ------ EAST CORRIDOR  (X from +CH to +Extent) ------
        {
            float x0 = CH, x1 = Extent;
            FloorSlab(x0, x1, -CW, CW);
            CeilSlab (x0, x1, -CW, CW);
            GridLines(x0, x1, -CW, CW, 3);
            CeilLight((x0+x1)/2f, 0, 2f, 1.5f);

            WallNZ(-CW, x0, x1, _wallColor);
            WallPZ( CW, x0, x1, _wallColor);
            float ew = x1;
            WallNX(ew, -CW, -2.0f, sideWall);
            WallNX(ew,  2.0f,  CW, sideWall);
            Quad(new Vector3(ew,  hf,   -2.0f), new Vector3(ew,  hf,    2.0f),
                 new Vector3(ew, -hf+3f, 2.0f), new Vector3(ew, -hf+3f,-2.0f), sideWall);

            DoorRecess(false, ew, 0f, new Color(14, 22, 42));

            BaseboardNZ(-CW, x0, x1);
            BaseboardPZ( CW, x0, x1);
            BaseboardNX(ew, -CW, -2.1f);
            BaseboardNX(ew,  2.1f,  CW);
        }

        // ------ CORNER CEILING FILLS (the four L-shaped ceiling corners) ------
        // These cover the ceiling above the corners that aren't part of any corridor or centre.
        // (We leave them open — the camera never sees them — so no geometry needed.)

        // ------------------------------------------------------------------
        // Upload to GPU
        // ------------------------------------------------------------------
        _vb = new VertexBuffer(_gd, typeof(VertexPositionColor),
            verts.Count, BufferUsage.WriteOnly);
        _vb.SetData(verts.ToArray());

        _ib = new IndexBuffer(_gd, IndexElementSize.SixteenBits,
            idx.Count, BufferUsage.WriteOnly);
        _ib.SetData(idx.ToArray());
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------

    public void Draw(Matrix view, Matrix projection)
    {
        _gd.SetVertexBuffer(_vb);
        _gd.Indices = _ib;

        _effect.View       = view;
        _effect.Projection = projection;
        _effect.World      = Matrix.Identity;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawIndexedPrimitives(
                PrimitiveType.TriangleList, 0, 0,
                _ib.IndexCount / 3);
        }
    }

    // -----------------------------------------------------------------------
    // Collision helper — clamps a position inside the plus shape
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns true if the XZ position is inside the plus-shaped navigable area
    /// (centre square OR any of the four corridors).
    /// </summary>
    public static bool InsidePlus(float x, float z)
    {
        bool inCentre   = x >= -CH && x <= CH && z >= -CH && z <= CH;
        bool inNorth    = x >= -CW && x <= CW && z >= -Extent && z <= -CH;
        bool inSouth    = x >= -CW && x <= CW && z >=  CH     && z <=  Extent;
        bool inWest     = x >= -Extent && x <= -CH && z >= -CW && z <= CW;
        bool inEast     = x >=  CH     && x <=  Extent && z >= -CW && z <= CW;
        return inCentre || inNorth || inSouth || inWest || inEast;
    }

    /// <summary>
    /// Clamps position to the navigable plus area with a margin inset.
    /// Call this from Camera.Update() instead of the simple box clamp.
    /// </summary>
    public static Vector3 ClampToPlus(Vector3 pos, float margin = 0.5f)
    {
        float x = pos.X, z = pos.Z;

        // Determine which zone the player is in (or closest to)
        bool inCentre = x >= -(CH-margin) && x <= (CH-margin)
                     && z >= -(CH-margin) && z <= (CH-margin);
        bool inNorth  = x >= -(CW-margin) && x <= (CW-margin)
                     && z <= -(CH) && z >= -(Extent-margin);
        bool inSouth  = x >= -(CW-margin) && x <= (CW-margin)
                     && z >= (CH)  && z <=  (Extent-margin);
        bool inWest   = z >= -(CW-margin) && z <= (CW-margin)
                     && x <= -(CH) && x >= -(Extent-margin);
        bool inEast   = z >= -(CW-margin) && z <= (CW-margin)
                     && x >= (CH)  && x <=  (Extent-margin);

        if (inCentre || inNorth || inSouth || inWest || inEast)
            return pos; // already fine

        // Not cleanly in any zone — clamp to nearest valid position
        // Simple approach: clamp to the overall bounding square then check corridors
        x = System.Math.Clamp(x, -(Extent-margin), Extent-margin);
        z = System.Math.Clamp(z, -(Extent-margin), Extent-margin);

        // If still outside the plus (in a corner), push to nearest corridor/centre edge
        bool nowOk = (x >= -(CH-margin) && x <= (CH-margin))
                  || (z >= -(CH-margin) && z <= (CH-margin))
                  || (System.Math.Abs(x) <= CW-margin)
                  || (System.Math.Abs(z) <= CW-margin);

        if (!nowOk)
        {
            // Push toward centre
            if (System.Math.Abs(x) < System.Math.Abs(z))
                z = System.Math.Sign(z) * (CH - margin);
            else
                x = System.Math.Sign(x) * (CH - margin);
        }

        return new Vector3(x, pos.Y, z);
    }

    // -----------------------------------------------------------------------
    private static Color Darken(Color c, float f) =>
        new Color((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
}