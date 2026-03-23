using Microsoft.Xna.Framework;

namespace ZebraBear.Core;

/// <summary>
/// Contract every scene must fulfil.
/// Game.cs holds one IScene at a time and just calls these three methods.
/// No enum, no switches — add a new scene by implementing this interface.
/// </summary>
public interface IScene
{
    /// <summary>Called once when the scene is first created.</summary>
    void Load();

    /// <summary>Called every frame while this scene is active.</summary>
    void Update(GameTime gameTime);

    /// <summary>Called every frame while this scene is active.</summary>
    void Draw(GameTime gameTime);

    /// <summary>
    /// Called when transitioning INTO this scene.
    /// Use to reset camera, re-capture mouse, play music, etc.
    /// </summary>
    void OnEnter() { }

    /// <summary>
    /// Called when transitioning OUT of this scene.
    /// Use to release mouse, stop music, etc.
    /// </summary>
    void OnExit() { }
}