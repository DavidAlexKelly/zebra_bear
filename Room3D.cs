using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ZebraBear;

public class Room3D
{
    private GraphicsDevice _gd;
    private BasicEffect    _effect;
    private VertexBuffer   _vb;
    private IndexBuffer    _ib;

    private Color _wallColor;
    private Color _floorColor;
    private Color _ceilColor;

    private const float W = 28f;
    private const float H = 6f;
    private const float D = 28f;

    public Room3D(GraphicsDevice gd,
        Color? wallColor  = null,
        Color? floorColor = null,
        Color? ceilColor  = null)
    {
        _gd = gd;
        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            LightingEnabled    = false
        };

        _wallColor  = wallColor  ?? new Color(22, 20, 38);
        _floorColor = floorColor ?? new Color(18, 16, 28);
        _ceilColor  = ceilColor  ?? new Color(12, 12, 22);

        BuildGeometry();
    }

    private void BuildGeometry()
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<short>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
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

        float hw = W / 2f;
        float hd = D / 2f;

        var leftWallColor  = new Color(
            (int)(_wallColor.R * 0.9f),
            (int)(_wallColor.G * 0.9f),
            (int)(_wallColor.B * 0.9f));
        var rightWallColor = leftWallColor;

        // Floor
        AddQuad(
            new Vector3(-hw, -H/2f, -hd),
            new Vector3( hw, -H/2f, -hd),
            new Vector3( hw, -H/2f,  hd),
            new Vector3(-hw, -H/2f,  hd),
            _floorColor);

        // Ceiling
        AddQuad(
            new Vector3(-hw,  H/2f,  hd),
            new Vector3( hw,  H/2f,  hd),
            new Vector3( hw,  H/2f, -hd),
            new Vector3(-hw,  H/2f, -hd),
            _ceilColor);

        // Back wall (Z = -hd)
        AddQuad(
            new Vector3(-hw,  H/2f, -hd),
            new Vector3( hw,  H/2f, -hd),
            new Vector3( hw, -H/2f, -hd),
            new Vector3(-hw, -H/2f, -hd),
            _wallColor);

        // Front wall (Z = +hd)
        AddQuad(
            new Vector3( hw,  H/2f,  hd),
            new Vector3(-hw,  H/2f,  hd),
            new Vector3(-hw, -H/2f,  hd),
            new Vector3( hw, -H/2f,  hd),
            _wallColor);

        // Left wall (X = -hw)
        AddQuad(
            new Vector3(-hw,  H/2f,  hd),
            new Vector3(-hw,  H/2f, -hd),
            new Vector3(-hw, -H/2f, -hd),
            new Vector3(-hw, -H/2f,  hd),
            leftWallColor);

        // Right wall (X = +hw)
        AddQuad(
            new Vector3( hw,  H/2f, -hd),
            new Vector3( hw,  H/2f,  hd),
            new Vector3( hw, -H/2f,  hd),
            new Vector3( hw, -H/2f, -hd),
            rightWallColor);

        // Floor grid lines
        int gridLines = 6;
        for (int i = -gridLines; i <= gridLines; i++)
        {
            float x = i * (hw / gridLines);
            float z = i * (hd / gridLines);
            var lineCol = new Color(
                (int)(_floorColor.R * 1.6f),
                (int)(_floorColor.G * 1.6f),
                (int)(_floorColor.B * 1.6f));

            // Lines along Z axis
            AddQuad(
                new Vector3(x - 0.02f, -H/2f + 0.01f, -hd),
                new Vector3(x + 0.02f, -H/2f + 0.01f, -hd),
                new Vector3(x + 0.02f, -H/2f + 0.01f,  hd),
                new Vector3(x - 0.02f, -H/2f + 0.01f,  hd),
                lineCol);

            // Lines along X axis
            AddQuad(
                new Vector3(-hw, -H/2f + 0.01f, z - 0.02f),
                new Vector3( hw, -H/2f + 0.01f, z - 0.02f),
                new Vector3( hw, -H/2f + 0.01f, z + 0.02f),
                new Vector3(-hw, -H/2f + 0.01f, z + 0.02f),
                lineCol);
        }

        // Wall trim — baseboard along back wall
        var trimColor = new Color(232, 0, 61);
        AddQuad(
            new Vector3(-hw, -H/2f + 0.4f, -hd + 0.01f),
            new Vector3( hw, -H/2f + 0.4f, -hd + 0.01f),
            new Vector3( hw, -H/2f,        -hd + 0.01f),
            new Vector3(-hw, -H/2f,        -hd + 0.01f),
            trimColor);

        // Baseboard on left wall
        AddQuad(
            new Vector3(-hw + 0.01f, -H/2f + 0.4f,  hd),
            new Vector3(-hw + 0.01f, -H/2f + 0.4f, -hd),
            new Vector3(-hw + 0.01f, -H/2f,         -hd),
            new Vector3(-hw + 0.01f, -H/2f,          hd),
            trimColor);

        // Baseboard on right wall
        AddQuad(
            new Vector3( hw - 0.01f, -H/2f + 0.4f, -hd),
            new Vector3( hw - 0.01f, -H/2f + 0.4f,  hd),
            new Vector3( hw - 0.01f, -H/2f,          hd),
            new Vector3( hw - 0.01f, -H/2f,         -hd),
            trimColor);

        // Windows on back wall
        for (int i = -1; i <= 1; i++)
        {
            float wx = i * 7f;

            // Window recess (dark glass)
            AddQuad(
                new Vector3(wx - 1.8f,  H/2f - 0.3f,  -hd + 0.02f),
                new Vector3(wx + 1.8f,  H/2f - 0.3f,  -hd + 0.02f),
                new Vector3(wx + 1.8f, -H/2f + 1.2f,  -hd + 0.02f),
                new Vector3(wx - 1.8f, -H/2f + 1.2f,  -hd + 0.02f),
                new Color(14, 22, 42));

            // Window frame — top
            AddQuad(
                new Vector3(wx - 1.9f,  H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx + 1.9f,  H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx + 1.9f,  H/2f - 0.38f, -hd + 0.03f),
                new Vector3(wx - 1.9f,  H/2f - 0.38f, -hd + 0.03f),
                new Color(32, 30, 52));

            // Window frame — bottom
            AddQuad(
                new Vector3(wx - 1.9f, -H/2f + 1.28f, -hd + 0.03f),
                new Vector3(wx + 1.9f, -H/2f + 1.28f, -hd + 0.03f),
                new Vector3(wx + 1.9f, -H/2f + 1.1f,  -hd + 0.03f),
                new Vector3(wx - 1.9f, -H/2f + 1.1f,  -hd + 0.03f),
                new Color(32, 30, 52));

            // Window frame — left
            AddQuad(
                new Vector3(wx - 1.9f,  H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx - 1.72f, H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx - 1.72f, -H/2f + 1.1f, -hd + 0.03f),
                new Vector3(wx - 1.9f,  -H/2f + 1.1f, -hd + 0.03f),
                new Color(32, 30, 52));

            // Window frame — right
            AddQuad(
                new Vector3(wx + 1.72f, H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx + 1.9f,  H/2f - 0.2f,  -hd + 0.03f),
                new Vector3(wx + 1.9f,  -H/2f + 1.1f, -hd + 0.03f),
                new Vector3(wx + 1.72f, -H/2f + 1.1f, -hd + 0.03f),
                new Color(32, 30, 52));

            // Window cross bar — horizontal
            AddQuad(
                new Vector3(wx - 1.8f, -H/2f + 2.15f + 0.04f, -hd + 0.03f),
                new Vector3(wx + 1.8f, -H/2f + 2.15f + 0.04f, -hd + 0.03f),
                new Vector3(wx + 1.8f, -H/2f + 2.15f - 0.04f, -hd + 0.03f),
                new Vector3(wx - 1.8f, -H/2f + 2.15f - 0.04f, -hd + 0.03f),
                new Color(32, 30, 52));

            // Window cross bar — vertical
            AddQuad(
                new Vector3(wx - 0.04f,  H/2f - 0.3f,  -hd + 0.03f),
                new Vector3(wx + 0.04f,  H/2f - 0.3f,  -hd + 0.03f),
                new Vector3(wx + 0.04f, -H/2f + 1.2f,  -hd + 0.03f),
                new Vector3(wx - 0.04f, -H/2f + 1.2f,  -hd + 0.03f),
                new Color(32, 30, 52));
        }

        // Ceiling light strip — centre of ceiling
        AddQuad(
            new Vector3(-2f,  H/2f - 0.01f, -2f),
            new Vector3( 2f,  H/2f - 0.01f, -2f),
            new Vector3( 2f,  H/2f - 0.01f,  2f),
            new Vector3(-2f,  H/2f - 0.01f,  2f),
            new Color(200, 195, 220));

        _vb = new VertexBuffer(_gd, typeof(VertexPositionColor),
            verts.Count, BufferUsage.WriteOnly);
        _vb.SetData(verts.ToArray());

        _ib = new IndexBuffer(_gd, IndexElementSize.SixteenBits,
            idx.Count, BufferUsage.WriteOnly);
        _ib.SetData(idx.ToArray());
    }

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
}