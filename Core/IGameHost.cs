using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear.Core;

/// <summary>
/// Abstracts the handful of Game methods that scenes (PauseMenu, etc.)
/// need to call back into the host, without depending on the concrete
/// ZebraBear.Game class (which inherits Microsoft.Xna.Framework.Game and
/// can cause ambiguous-type resolution in the compiler).
/// </summary>
public interface IGameHost
{
    GraphicsDevice GraphicsDevice { get; }
    bool IsMouseVisible { get; set; }

    void Resume();
    void GoToMainMenu();
    void Exit();
}