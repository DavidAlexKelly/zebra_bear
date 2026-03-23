using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ZebraBear;

/// <summary>
/// All information about a character shown in the Characters tab.
/// </summary>
public class CharacterProfile
{
    public string    Id;           // unique identifier, matches BillboardEntity.Name
    public string    Name;         // display name
    public string    Title;        // e.g. "Ultimate Despair", "Ultimate Lucky Student"
    public string[]  Bio;          // short paragraphs shown in the detail panel
    public Texture2D Portrait;     // same sprite used in-game, null = not yet seen
    public bool      Met;          // false = shown as ??? until first interaction
}

/// <summary>
/// Central registry of all characters in the game.
/// 
/// Adding a character:
///   1. Add a CharacterProfile entry to the List below
///   2. Assign Portrait = Assets.CharacterX once the sprite is loaded
///   3. Call CharacterData.SetMet("Id") when the player first speaks to them
///
/// The pause menu Characters tab reads from here automatically.
/// </summary>
public static class CharacterData
{
    public static int SelectedIndex = 0;

    public static readonly List<CharacterProfile> Characters = new()
    {
        new CharacterProfile
        {
            Id      = "Kei",
            Name    = "Kei",
            Title   = "Ultimate ???",
            Bio     = new[]
            {
                "A reserved student who seems reluctant to engage with others.",
                "Despite the cold exterior, there's something calculating behind those eyes.",
                "Met in the Main Hall on arrival."
            },
            Met     = false
        },
        new CharacterProfile
        {
            Id      = "Haru",
            Name    = "Haru",
            Title   = "Ultimate ???",
            Bio     = new[]
            {
                "Actively hostile to conversation.",
                "Refuses to make eye contact.",
                "Standing alone near the back of the Main Hall."
            },
            Met     = false
        },
    };

    /// <summary>
    /// Call when the player first interacts with a character.
    /// Reveals them in the Characters tab.
    /// </summary>
    public static void SetMet(string characterId)
    {
        var c = Characters.Find(c => c.Id == characterId);
        if (c != null) c.Met = true;
    }

    /// <summary>
    /// Assign portrait sprites after Assets.Load() has run.
    /// Call this from Game.LoadContent() after Assets.Load().
    /// </summary>
    public static void AssignPortraits()
    {
        SetPortrait("Kei",  Assets.CharacterKei);
        SetPortrait("Haru", Assets.CharacterHaru);
    }

    private static void SetPortrait(string id, Texture2D sprite)
    {
        var c = Characters.Find(c => c.Id == id);
        if (c != null) c.Portrait = sprite;
    }
}