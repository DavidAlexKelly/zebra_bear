using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ZebraBear;

/// <summary>
/// All information about a character shown in the Characters tab.
/// </summary>
public class CharacterProfile
{
    public string    Id;
    public string    Name;

    public string PortraitPath = "";
    public string    Title;
    public string[]  Bio;
    public Texture2D Portrait;
    public bool      Met;
    
}

/// <summary>
/// Central registry of all characters in the game.
///
/// Characters are now loaded from Data/characters.json by GameLoader.
/// Do not add entries here directly — edit the JSON file instead.
///
/// The Characters list starts empty; GameLoader.LoadCharacters() fills it.
/// PauseMenu reads from here automatically.
/// </summary>
public static class CharacterData
{
    public static int SelectedIndex = 0;

    /// <summary>
    /// Populated by GameLoader.LoadCharacters().
    /// Do not add to this list manually.
    /// </summary>
    public static readonly List<CharacterProfile> Characters = new();

    /// <summary>
    /// Call when the player first interacts with a character.
    /// Reveals them in the Characters tab.
    /// </summary>
    public static void SetMet(string characterId)
    {
        var c = Characters.Find(c => c.Id == characterId);
        if (c != null) c.Met = true;
    }

    // AssignPortraits() is no longer needed — GameLoader.LoadCharacters()
    // handles portrait loading directly from the JSON "portrait" field.
}