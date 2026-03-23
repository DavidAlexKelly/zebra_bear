using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.Scenes;

namespace ZebraBear;

public class Game : Microsoft.Xna.Framework.Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch           _spriteBatch;

    private SceneManager _scenes;
    private PauseMenu    _pauseMenu;

    // Scenes — created once, reused
    private MainMenuScene _mainMenuScene;
    private GameScene     _gameScene;
    private Room2Scene    _room2Scene;

    private KeyboardState _prevKeys;

    public Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth  = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.IsFullScreen = false;
        Content.RootDirectory  = "Content";
        IsMouseVisible         = false;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Load all shared assets first
        Assets.Load(Content, GraphicsDevice);

        // Assign character portraits now that assets are loaded
        CharacterData.AssignPortraits();

        // Build scenes
        _mainMenuScene = new MainMenuScene(this, _spriteBatch);
        _gameScene     = new GameScene(this, _spriteBatch);
        _room2Scene    = new Room2Scene(this, _spriteBatch);

        _mainMenuScene.Load();
        _gameScene.Load();
        _room2Scene.Load();

        // Pause menu is a special IScene wrapper
        _pauseMenu = new PauseMenu(this, _spriteBatch);
        _pauseMenu.Load();

        _scenes = new SceneManager(_pauseMenu);
        _scenes.ChangeTo(_mainMenuScene);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        // Pause shortcut — intercept before routing to current scene
        bool inGame = _scenes.Current is GameScene or Room2Scene;
        if (inGame && keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape))
        {
            _scenes.Pause();
        }

        _scenes.Update(gameTime);

        _prevKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 10, 18));

        // If paused, draw the underlying scene first, then the overlay
        if (_scenes.Current is PauseMenu)
        {
            _scenes.DrawPrePause(gameTime);
        }

        _scenes.Draw(gameTime);

        base.Draw(gameTime);
    }

    // -----------------------------------------------------------------------
    // Public navigation API — called by scenes and the pause menu
    // -----------------------------------------------------------------------

    public void GoToMainMenu()   => _scenes.ChangeTo(_mainMenuScene);
    public void GoToGame()       => _scenes.ChangeTo(_gameScene);
    public void GoToRoom2()      => _scenes.ChangeTo(_room2Scene);
    public void Resume()         => _scenes.Resume();
    public void Pause()          => _scenes.Pause();
}