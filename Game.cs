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

    private MainMenuScene _mainMenuScene;
    private GameScene     _gameScene;       // Main Hall (starting room)
    private HubScene      _hubScene;        // Plus-shaped connecting room
    private Room2Scene    _roomNorthScene;
    private Room2Scene    _roomSouthScene;
    private Room2Scene    _roomWestScene;
    private Room2Scene    _roomEastScene;

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

        Assets.Load(Content, GraphicsDevice);
        GameLoader.LoadCharacters(Content);
        GameLoader.LoadMap();

        _mainMenuScene  = new MainMenuScene(this, _spriteBatch);
        _gameScene      = new GameScene(this, _spriteBatch);
        _hubScene       = new HubScene(this, _spriteBatch);
        _roomNorthScene = new Room2Scene(this, _spriteBatch, "RoomNorth");
        _roomSouthScene = new Room2Scene(this, _spriteBatch, "RoomSouth");
        _roomWestScene  = new Room2Scene(this, _spriteBatch, "RoomWest");
        _roomEastScene  = new Room2Scene(this, _spriteBatch, "RoomEast");

        _mainMenuScene.Load();
        _gameScene.Load();
        _hubScene.Load();
        _roomNorthScene.Load();
        _roomSouthScene.Load();
        _roomWestScene.Load();
        _roomEastScene.Load();

        _pauseMenu = new PauseMenu(this, _spriteBatch);
        _pauseMenu.Load();

        _scenes = new SceneManager(_pauseMenu);
        _scenes.ChangeTo(_mainMenuScene);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        bool inGame = _scenes.Current is GameScene
                   or HubScene
                   or Room2Scene;

        if (inGame && keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape))
            _scenes.Pause();

        _scenes.Update(gameTime);

        if (NavigationBus.HasRequest)
            HandleNavigation(NavigationBus.Consume());

        _prevKeys = keys;
        base.Update(gameTime);
    }

    private void HandleNavigation(string destination)
    {
        switch (destination)
        {
            case "MainHall":  _scenes.ChangeTo(_gameScene);       break;
            case "Hub":       _scenes.ChangeTo(_hubScene);        break;
            case "RoomNorth": _scenes.ChangeTo(_roomNorthScene);  break;
            case "RoomSouth": _scenes.ChangeTo(_roomSouthScene);  break;
            case "RoomWest":  _scenes.ChangeTo(_roomWestScene);   break;
            case "RoomEast":  _scenes.ChangeTo(_roomEastScene);   break;
            case "MainMenu":  _scenes.ChangeTo(_mainMenuScene);   break;
            default:
                System.Console.WriteLine($"[Game] Unknown destination: '{destination}'");
                break;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 10, 18));
        if (_scenes.Current is PauseMenu)
            _scenes.DrawPrePause(gameTime);
        _scenes.Draw(gameTime);
        base.Draw(gameTime);
    }

    // -----------------------------------------------------------------------
    // Navigation API (used by pause menu, scenes)
    // -----------------------------------------------------------------------

    public void GoToMainMenu() => _scenes.ChangeTo(_mainMenuScene);
    public void GoToGame()     => _scenes.ChangeTo(_gameScene);
    public void GoToHub()      => _scenes.ChangeTo(_hubScene);
    public void Resume()       => _scenes.Resume();
    public void Pause()        => _scenes.Pause();
}