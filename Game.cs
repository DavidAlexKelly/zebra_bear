using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ZebraBear;

public enum Scene { MainMenu, Game, Paused, Room2 }

public class Game : Microsoft.Xna.Framework.Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch           _spriteBatch;
    private Scene                 _currentScene    = Scene.MainMenu;
    private Scene                 _prePauseScene   = Scene.Game;

    private MainMenuScene _mainMenu;
    private GameScene     _gameScene;
    private Room2Scene    _room2Scene;
    private PauseMenu     _pauseMenu;

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

        _mainMenu   = new MainMenuScene(this, _spriteBatch);
        _mainMenu.Load();

        _gameScene  = new GameScene(this, _spriteBatch);
        _gameScene.Load();

        _room2Scene = new Room2Scene(this, _spriteBatch);
        _room2Scene.Load();

        var font      = Content.Load<SpriteFont>("Fonts/MenuFont");
        var titleFont = Content.Load<SpriteFont>("Fonts/TitleFont");
        var pixel     = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        _pauseMenu = new PauseMenu(this, _spriteBatch, font, titleFont, pixel);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        if ((_currentScene == Scene.Game || _currentScene == Scene.Room2) &&
            keys.IsKeyDown(Keys.Escape) && _prevKeys.IsKeyUp(Keys.Escape))
        {
            PauseFrom(_currentScene);
        }

        switch (_currentScene)
        {
            case Scene.MainMenu: _mainMenu.Update(gameTime);   break;
            case Scene.Game:     _gameScene.Update(gameTime);  break;
            case Scene.Room2:    _room2Scene.Update(gameTime); break;
            case Scene.Paused:   _pauseMenu.Update(gameTime);  break;
        }

        _prevKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 10, 18));

        switch (_currentScene)
        {
            case Scene.MainMenu:
                _mainMenu.Draw(gameTime);
                break;
            case Scene.Game:
                _gameScene.Draw(gameTime);
                break;
            case Scene.Room2:
                _room2Scene.Draw(gameTime);
                break;
            case Scene.Paused:
                // Draw whatever was behind the pause menu
                if (_prePauseScene == Scene.Room2)
                    _room2Scene.Draw(gameTime);
                else
                    _gameScene.Draw(gameTime);
                _pauseMenu.Draw(gameTime);
                break;
        }

        base.Draw(gameTime);
    }

    public void ChangeScene(Scene scene)
    {
        _currentScene = scene;
        IsMouseVisible = scene == Scene.MainMenu;

        if (scene == Scene.Room2)
            _room2Scene.OnEnter();
    }

    public void PauseFrom(Scene origin)
    {
        _prePauseScene = origin;
        _pauseMenu.OnOpen();
        _currentScene  = Scene.Paused;
    }
}