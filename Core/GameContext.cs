using System.Collections.Generic;
using ZebraBear.Scenes;

namespace ZebraBear.Core;

/// <summary>
/// Single owner of all mutable game state.
///
/// Game.cs creates one instance in LoadContent() and passes it to every
/// scene and loader. Nothing outside this class should hold game state as
/// static fields.
///
/// Replaces the following static classes (kept temporarily as thin
/// delegating shims while call-sites are migrated):
///   - MapData
///   - CharacterData
///   - InteractionStore
///   - GameFlags
///   - NavigationBus
///   - LevelData  (state portion only — file I/O stays in LevelData)
/// </summary>
public class GameContext
{
    // -----------------------------------------------------------------------
    // Singleton — used only by the legacy shims (MapData, CharacterData, etc.)
    // during the migration. New code should receive GameContext via constructor
    // injection and never touch this property.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Set once by Game.LoadContent() before any scene is created.
    /// Only the shim classes (MapData, NavigationBus, etc.) read this.
    /// All other code should hold a reference passed through constructors.
    /// </summary>
    public static GameContext Instance { get; set; }

    // -----------------------------------------------------------------------
    // Map
    // -----------------------------------------------------------------------

    /// <summary>Id of the room the player is currently in.</summary>
    public string CurrentRoomId = "MainHall";

    public readonly List<MapRoom>       Rooms       = new();
    public readonly List<MapConnection> Connections = new();

    public void SetDiscovered(string roomId)
    {
        var room = Rooms.Find(r => r.Id == roomId);
        if (room != null) room.Discovered = true;
    }

    public MapRoom FindRoom(string id) => Rooms.Find(r => r.Id == id);

    // -----------------------------------------------------------------------
    // Characters
    // -----------------------------------------------------------------------

    /// <summary>Index of the selected character in the pause-menu character list.</summary>
    public int SelectedCharacterIndex = 0;

    public readonly List<CharacterProfile> Characters = new();

    public void SetMet(string characterId)
    {
        var c = Characters.Find(c => c.Id == characterId);
        if (c != null) c.Met = true;
    }

    // -----------------------------------------------------------------------
    // Interactions
    // -----------------------------------------------------------------------

    public readonly List<InteractionDef> Interactions = new();

    public InteractionDef FindInteractionById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Interactions.Find(i => i.Id == id);
    }

    public InteractionDef FindInteractionByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return Interactions.Find(i =>
            i.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // Flags
    // -----------------------------------------------------------------------

    private readonly System.Collections.Generic.HashSet<string> _flags =
        new(System.StringComparer.OrdinalIgnoreCase);

    public void  SetFlag(string flag)   => _flags.Add(flag);
    public void  ClearFlag(string flag) => _flags.Remove(flag);
    public bool  IsFlagSet(string flag) => _flags.Contains(flag);
    public void  ResetFlags()           => _flags.Clear();

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private string _pendingNavigation;

    public bool   HasNavigationRequest      => _pendingNavigation != null;
    public string PendingNavigation         => _pendingNavigation;

    public void   RequestNavigate(string dest) => _pendingNavigation = dest;

    public string ConsumeNavigation()
    {
        var dest = _pendingNavigation;
        _pendingNavigation = null;
        return dest;
    }

    // -----------------------------------------------------------------------
    // Level
    // -----------------------------------------------------------------------

    /// <summary>Display name of the currently loaded level.</summary>
    public string CurrentLevelName = "";

    /// <summary>
    /// When non-null, overrides the default rooms.json path.
    /// Set by LevelData.LoadLevel() when a player-made level is active.
    /// </summary>
    public string LevelRoomsOverride = null;

    public void ClearLevelOverride()
    {
        if (LevelRoomsOverride != null &&
            System.IO.File.Exists(LevelRoomsOverride))
        {
            try { System.IO.File.Delete(LevelRoomsOverride); } catch { }
        }
        LevelRoomsOverride = null;
    }

    // -----------------------------------------------------------------------
    // Reset
    // -----------------------------------------------------------------------

    /// <summary>
    /// Clears all runtime state. Call before loading a new level.
    /// </summary>
    public void Reset()
    {
        Rooms.Clear();
        Connections.Clear();
        Characters.Clear();
        Interactions.Clear();
        ResetFlags();
        _pendingNavigation  = null;
        CurrentRoomId       = "MainHall";
        CurrentLevelName    = "";
        SelectedCharacterIndex = 0;
    }
}