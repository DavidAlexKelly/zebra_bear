using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

public class Billboard
{
    public Vector3 Position;
    public float   Width;
    public float   Height;
    public Color   Tint;
    public string  Name;
    public string[] Dialogue;
    public bool    IsCharacter;

    // For hit detection — returns true if the ray hits this billboard
    public bool Raycast(Ray ray, out float distance)
    {
        distance = float.MaxValue;

        // Treat billboard as an axis-aligned bounding box
        var min = new Vector3(
            Position.X - Width  / 2f,
            Position.Y - Height / 2f,
            Position.Z - 0.2f);
        var max = new Vector3(
            Position.X + Width  / 2f,
            Position.Y + Height / 2f,
            Position.Z + 0.2f);

        var box = new BoundingBox(min, max);
        var hit = ray.Intersects(box);
        if (hit.HasValue)
        {
            distance = hit.Value;
            return true;
        }
        return false;
    }
}