using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Scenes;

namespace ZebraBear;

/// <summary>
/// Application entry point and scene router.
/// Implements IGameHost so that scenes can call back into the host without
/// depending on the concrete ZebraBear.Game type (which shadows
/// Microsoft.Xna.Framework.Game and causes CS1061 in some compiler versions).
/// </summary>
public class Game : Microsoft.Xna.Framework.Game, IGameHost
{
    private GraphicsDeviceManager    _graphics;
    private SpriteBatch              _spriteBatch;

    private SceneManager             _scenes;
    private PauseMenu                _pauseMenu;
    private MainMenuScene            _mainMenuScene;
    private LevelEditorScene         _levelEditorScene;
    private RoomGeometryEditorScene  _roomGeoEditorScene;

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

        ZebraBearEntities.Content = Content;
        ZebraBearEntities.Register();
        ModelExporter.ExportAll();

        Assets.Load(Content, GraphicsDevice);
        GameLoader.LoadCharacters(Content);
        GameLoader.LoadMap();

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

        _roomGeoEditorScene = new RoomGeometryEditorScene(this, _spriteBatch);
        _roomGeoEditorScene.Load();

        _levelEditorScene = new LevelEditorScene(this, _spriteBatch, _roomGeoEditorScene);
        _levelEditorScene.Load();

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
        switch (destination)
        {
            case "MainMenu":    _scenes.ChangeTo(_mainMenuScene);    return;
            case "LevelEditor": _scenes.ChangeTo(_levelEditorScene); return;
            case "RoomGeoEditor": _scenes.ChangeTo(_roomGeoEditorScene); return;
        }

        if (_roomScenes.TryGetValue(destination, out var scene))
        {
            _scenes.ChangeTo(scene);
            return;
        }

        System.Console.WriteLine($"[Game] Unknown destination: '{destination}'.");
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
    // IGameHost implementation
    // -----------------------------------------------------------------------

    public void Resume()       => _scenes.Resume();
    public void GoToMainMenu() => _scenes.ChangeTo(_mainMenuScene);

    // Exit() is already defined on Microsoft.Xna.Framework.Game — no override needed.
    // The interface re-exposes it so callers typed as IGameHost can call it.
    void IGameHost.Exit() => base.Exit();
}