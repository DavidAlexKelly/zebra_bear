using Microsoft.Xna.Framework;

namespace ZebraBear.Core;

/// <summary>
/// Holds the active scene and handles transitions.
/// Game.cs creates this and calls Update/Draw each frame.
///
/// Pause is handled as an overlay: the pre-pause scene is preserved and
/// drawn underneath the pause menu each frame.
/// </summary>
public class SceneManager
{
    private IScene _current;
    private IScene _prePause;

    // Injected so SceneManager can tell the pause menu to open/close
    // without knowing anything else about it.
    private readonly IScene _pauseMenu;

    public IScene Current => _current;
    public bool   IsPaused => _current == _pauseMenu;

    public SceneManager(IScene pauseMenu)
    {
        _pauseMenu = pauseMenu;
    }

    /// <summary>
    /// Transition to a new scene. Calls OnExit on the current scene
    /// and OnEnter on the next.
    /// </summary>
    public void ChangeTo(IScene next)
    {
        _current?.OnExit();
        _current = next;
        _current.OnEnter();
    }

    /// <summary>
    /// Overlay the pause menu on top of whatever is currently running.
    /// Does nothing if already paused.
    /// </summary>
    public void Pause()
    {
        if (IsPaused) return;
        _prePause = _current;
        _current  = _pauseMenu;
        _pauseMenu.OnEnter();
    }

    /// <summary>
    /// Return to the scene that was running before pause.
    /// Does nothing if not paused.
    /// </summary>
    public void Resume()
    {
        if (!IsPaused) return;
        _pauseMenu.OnExit();
        _current = _prePause;
        _current.OnEnter();
    }

    /// <summary>
    /// Draw the scene that was active before pausing.
    /// Call this from Game.Draw() before drawing the pause overlay.
    /// </summary>
    public void DrawPrePause(GameTime gameTime) => _prePause?.Draw(gameTime);

    public void Update(GameTime gameTime) => _current?.Update(gameTime);
    public void Draw(GameTime gameTime)   => _current?.Draw(gameTime);
}