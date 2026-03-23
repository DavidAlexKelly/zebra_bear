using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

/// <summary>
/// Central asset registry. Call Assets.Load() once in Game.LoadContent().
///
/// Character portraits are no longer loaded here — GameLoader.LoadCharacters()
/// reads the "portrait" path from characters.json and loads them directly
/// into each CharacterProfile. Add new character portraits to characters.json,
/// not here.
/// </summary>
public static class Assets
{
    // -----------------------------------------------------------------------
    // Fonts
    // -----------------------------------------------------------------------
    public static SpriteFont MenuFont;
    public static SpriteFont TitleFont;

    // -----------------------------------------------------------------------
    // Shared utility texture — 1×1 white pixel used for DrawRect everywhere
    // -----------------------------------------------------------------------
    public static Texture2D Pixel;

    // -----------------------------------------------------------------------
    // Environment / UI textures (add here as needed)
    // -----------------------------------------------------------------------
    // public static Texture2D Door;
    // public static Texture2D NoticeBoard;
    // public static Texture2D DialoguePanel;

    public static void Load(ContentManager content, GraphicsDevice gd)
    {
        MenuFont  = content.Load<SpriteFont>("Fonts/MenuFont");
        TitleFont = content.Load<SpriteFont>("Fonts/TitleFont");

        Pixel = new Texture2D(gd, 1, 1);
        Pixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
    }
}