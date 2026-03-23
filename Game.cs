using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Scenes;

namespace ZebraBear;

public class Game : Microsoft.Xna.Framework.Game, IGameHost
{
    private GraphicsDeviceManager   _graphics;
    private SpriteBatch             _spriteBatch;
    private SceneManager            _scenes;
    private PauseMenu               _pauseMenu;
    private MainMenuScene           _mainMenuScene;
    private LevelSelectScene        _levelSelectScene;
    private LevelEditorScene        _levelEditorScene;
    private RoomGeometryEditorScene _roomGeoEditorScene;
    private InteractionEditorScene  _interactionEditorScene;
    private CharacterEditorScene    _characterEditorScene;

    private Dictionary<string, IScene> _roomScenes = new();
    private KeyboardState _prevKeys;

    public Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth  = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.IsFullScreen       = false;
        _graphics.HardwareModeSwitch = false;
        Content.RootDirectory = "Content";
        IsMouseVisible        = false;
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
        BuildRoomScenes();

        _mainMenuScene = new MainMenuScene(this, _spriteBatch);
        _mainMenuScene.Load();

        _levelSelectScene = new LevelSelectScene(this, _spriteBatch);
        _levelSelectScene.Load();

        _roomGeoEditorScene = new RoomGeometryEditorScene(this, _spriteBatch);
        _roomGeoEditorScene.Load();

        _interactionEditorScene = new InteractionEditorScene(this, _spriteBatch);
        _interactionEditorScene.Load();

        _characterEditorScene = new CharacterEditorScene(this, _spriteBatch, Content);
        _characterEditorScene.Load();

        _levelEditorScene = new LevelEditorScene(
            this, _spriteBatch,
            _roomGeoEditorScene,
            _interactionEditorScene,
            _characterEditorScene);
        _levelEditorScene.Load();

        _pauseMenu = new PauseMenu(this, _spriteBatch);
        _pauseMenu.Load();

        _scenes = new SceneManager(_pauseMenu);
        _scenes.ChangeTo(_mainMenuScene);
    }

    private void BuildRoomScenes()
    {
        _roomScenes.Clear();
        foreach (var mapRoom in MapData.Rooms)
        {
            var sceneType = mapRoom.SceneType == "plus"
                ? RoomSceneType.Plus
                : RoomSceneType.Box;
            var scene = new RoomScene(this, _spriteBatch, mapRoom.Id, sceneType);
            scene.Load();
            _roomScenes[mapRoom.Id] = scene;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        bool inGame = _scenes.Current is RoomScene;
        if (inGame && keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape))
            _scenes.Pause();

        bool f11 = keys.IsKeyDown(Keys.F11) && _prevKeys.IsKeyUp(Keys.F11);
        bool altEnter = (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt))
                        && keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter);
        if (f11 || altEnter)
            ToggleFullscreen();

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
            case "MainMenu":
                LevelData.ClearOverride();
                _scenes.ChangeTo(_mainMenuScene);
                return;
            case "LevelSelect":
                _scenes.ChangeTo(_levelSelectScene);
                return;
            case "LevelEditor":
                _scenes.ChangeTo(_levelEditorScene);
                return;
            case "RoomGeoEditor":
                _scenes.ChangeTo(_roomGeoEditorScene);
                return;
            case "InteractionEditor":
                _scenes.ChangeTo(_interactionEditorScene);
                return;
            case "CharacterEditor":
                _scenes.ChangeTo(_characterEditorScene);
                return;
            case "__reload_and_start__":
                BuildRoomScenes();
                if (_roomScenes.TryGetValue(MapData.CurrentRoomId, out var startScene))
                    _scenes.ChangeTo(startScene);
                else
                    System.Console.WriteLine($"[Game] Start room '{MapData.CurrentRoomId}' not found.");
                return;
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

    private void ToggleFullscreen()
    {
        if (_graphics.IsFullScreen)
        {
            _graphics.PreferredBackBufferWidth  = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.IsFullScreen = false;
        }
        else
        {
            _graphics.PreferredBackBufferWidth  = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.IsFullScreen = true;
        }
        _graphics.ApplyChanges();
    }

    public void Resume() => _scenes.Resume();

    public void GoToMainMenu()
    {
        LevelData.ClearOverride();
        _scenes.ChangeTo(_mainMenuScene);
    }

    void IGameHost.Exit() => base.Exit();
}