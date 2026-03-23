using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

/// <summary>
/// SHIM — delegates to AssetCache.Instance.
///
/// Kept for source compatibility during the AssetCache migration.
/// LayoutDraw and all scenes that reference Assets.MenuFont / Assets.Pixel
/// will continue to compile unchanged until they are updated to receive
/// an AssetCache directly.
///
/// Once all call-sites are migrated, delete this class.
/// </summary>
public static class Assets
{
    public static SpriteFont MenuFont  => AssetCache.Instance.MenuFont;
    public static SpriteFont TitleFont => AssetCache.Instance.TitleFont;
    public static Texture2D  Pixel     => AssetCache.Instance.Pixel;

    /// <summary>
    /// Called once in Game.LoadContent(). Creates and registers the
    /// AssetCache singleton so existing Assets.X references keep working.
    /// </summary>
    public static void Load(ContentManager content, GraphicsDevice gd)
    {
        AssetCache.Instance = AssetCache.Load(content, gd);
    }
}