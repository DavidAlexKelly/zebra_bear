using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Scenes;

namespace ZebraBear;

/// <summary>
/// Application entry point and scene router.
///
/// Scenes are now fully data-driven: any room in map.json is automatically
/// available as a navigation target. Adding a new room requires:
///   1. An entry in Data/map.json  (id, label, position, size, sceneType)
///   2. An entry in Data/rooms.json (id, colours, entities)
///   3. Zero C# changes here.
///
/// Special-case scenes (MainMenu, PauseMenu) are still constructed directly.
/// </summary>
public class Game : Microsoft.Xna.Framework.Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch           _spriteBatch;

    private SceneManager  _scenes;
    private PauseMenu     _pauseMenu;
    private MainMenuScene _mainMenuScene;

    // Room scenes are created on demand and cached by room id.
    private readonly Dictionary<string, IScene> _roomScenes = new();

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

        // Give entity builders access to the content pipeline (needed for fbx type)
        ZebraBearEntities.Content = Content;

        // Register all entity types used by this game
        ZebraBearEntities.Register();

        // Export all MeshBuilder shapes to Data/Models/ as OBJ files.
        // Safe to leave on permanently — skips files that already exist.
        ModelExporter.ExportAll();

        Assets.Load(Content, GraphicsDevice);
        GameLoader.LoadCharacters(Content);
        GameLoader.LoadMap();

        // Build room scenes from map data — no hard-coded room list
        foreach (var mapRoom in MapData.Rooms)
        {
            var sceneType = mapRoom.SceneType == "plus"
                ? RoomSceneType.Plus
                : RoomSceneType.Box;

            var scene = new RoomScene(this, _spriteBatch, mapRoom.Id, sceneType);
            scene.Load();
            _roomScenes[mapRoom.Id] = scene;
        }

        _mainMenuScene = new MainMenuScene(this, _spriteBatch);
        _mainMenuScene.Load();

        _pauseMenu = new PauseMenu(this, _spriteBatch);
        _pauseMenu.Load();

        _scenes = new SceneManager(_pauseMenu);
        _scenes.ChangeTo(_mainMenuScene);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        bool inGame = _scenes.Current is RoomScene;

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
        if (destination == "MainMenu")
        {
            _scenes.ChangeTo(_mainMenuScene);
            return;
        }

        if (_roomScenes.TryGetValue(destination, out var scene))
        {
            _scenes.ChangeTo(scene);
            return;
        }

        System.Console.WriteLine($"[Game] Unknown destination: '{destination}'. " +
            $"Add it to map.json and rooms.json.");
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
    // Navigation API  (kept for code that calls these directly, e.g. PauseMenu)
    // -----------------------------------------------------------------------

    public void GoToMainMenu() => _scenes.ChangeTo(_mainMenuScene);
    public void Resume()       => _scenes.Resume();
    public void Pause()        => _scenes.Pause();
}