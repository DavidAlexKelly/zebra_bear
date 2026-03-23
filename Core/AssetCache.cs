using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

/// <summary>
/// Loaded rendering assets for the game.
///
/// Game.cs creates one instance in LoadContent() via AssetCache.Load()
/// and passes it to scenes alongside GameContext.
///
/// Replaces the static Assets class. Assets is kept temporarily as a
/// thin delegating shim while call-sites are migrated.
/// </summary>
public class AssetCache
{
    // -----------------------------------------------------------------------
    // Singleton — used only by the legacy Assets shim during migration.
    // New code should receive AssetCache via constructor injection.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Set once by Assets.Load() (the shim) or directly in Game.LoadContent().
    /// Only the Assets shim reads this. All other code should hold a reference
    /// passed through constructors.
    /// </summary>
    public static AssetCache Instance { get; set; }

    // -----------------------------------------------------------------------
    // Fonts
    // -----------------------------------------------------------------------
    public SpriteFont MenuFont  { get; private set; }
    public SpriteFont TitleFont { get; private set; }

    // -----------------------------------------------------------------------
    // Shared utility texture — 1×1 white pixel used by LayoutDraw everywhere
    // -----------------------------------------------------------------------
    public Texture2D Pixel { get; private set; }

    // -----------------------------------------------------------------------
    // Environment / UI textures (add here as the game grows)
    // -----------------------------------------------------------------------
    // public Texture2D Door          { get; private set; }
    // public Texture2D NoticeBoard   { get; private set; }
    // public Texture2D DialoguePanel { get; private set; }

    private AssetCache() { }

    /// <summary>
    /// Load all assets and return a populated AssetCache.
    /// Call once in Game.LoadContent().
    /// </summary>
    public static AssetCache Load(ContentManager content, GraphicsDevice gd)
    {
        var cache = new AssetCache
        {
            MenuFont  = content.Load<SpriteFont>("Fonts/MenuFont"),
            TitleFont = content.Load<SpriteFont>("Fonts/TitleFont"),
        };

        cache.Pixel = new Texture2D(gd, 1, 1);
        cache.Pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });

        Instance = cache;
        return cache;
    }
}