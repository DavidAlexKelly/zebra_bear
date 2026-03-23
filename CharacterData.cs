using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using ZebraBear.Core;

namespace ZebraBear;

/// <summary>
/// All information about a character shown in the Characters tab.
/// </summary>
public class CharacterProfile
{
    public string    Id;
    public string    Name;
    public string    PortraitPath = "";
    public string    Title;
    public string[]  Bio;
    public Texture2D Portrait;
    public bool      Met;
}

/// <summary>
/// SHIM — delegates to GameContext.Instance.
///
/// Kept for source compatibility during the GameContext migration.
/// Once all call-sites reference GameContext directly, delete this class.
/// </summary>
public static class CharacterData
{
    public static int SelectedIndex
    {
        get => GameContext.Instance.SelectedCharacterIndex;
        set => GameContext.Instance.SelectedCharacterIndex = value;
    }

    public static List<CharacterProfile> Characters => GameContext.Instance.Characters;

    public static void SetMet(string characterId) =>
        GameContext.Instance.SetMet(characterId);
}