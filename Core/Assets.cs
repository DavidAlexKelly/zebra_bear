using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

/// <summary>
/// Central asset registry. Call Assets.Load() once in Game.LoadContent().
/// All scenes and entities reference assets from here — no duplicate loads,
/// no passing ContentManager around.
/// </summary>
public static class Assets
{
    // -----------------------------------------------------------------------
    // Fonts
    // -----------------------------------------------------------------------
    public static SpriteFont MenuFont;
    public static SpriteFont TitleFont;

    // -----------------------------------------------------------------------
    // Characters
    // Add a field here when you add a character sprite to Content/Characters/
    // -----------------------------------------------------------------------
    public static Texture2D CharacterKei;
    public static Texture2D CharacterHaru;

    // -----------------------------------------------------------------------
    // Objects / environment
    // -----------------------------------------------------------------------
    // public static Texture2D Door;
    // public static Texture2D NoticeBoard;

    // -----------------------------------------------------------------------
    // UI
    // -----------------------------------------------------------------------
    // public static Texture2D DialoguePanel;

    // -----------------------------------------------------------------------
    // Shared utility texture — 1×1 white pixel used for DrawRect everywhere
    // -----------------------------------------------------------------------
    public static Texture2D Pixel;

    public static void Load(ContentManager content, GraphicsDevice gd)
    {
        MenuFont  = content.Load<SpriteFont>("Fonts/MenuFont");
        TitleFont = content.Load<SpriteFont>("Fonts/TitleFont");

        Pixel = new Texture2D(gd, 1, 1);
        Pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });

        // Uncomment as you add PNGs to Content/:
         CharacterKei  = content.Load<Texture2D>("Characters/monobear");
         CharacterHaru = content.Load<Texture2D>("Characters/usami");
        // Door          = content.Load<Texture2D>("Objects/Door");
        // NoticeBoard   = content.Load<Texture2D>("Objects/NoticeBoard");
    }
}