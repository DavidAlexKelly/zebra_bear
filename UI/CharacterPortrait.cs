using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZebraBear;

/// <summary>
/// Renders a character portrait in 2D screenspace behind the dialogue box.
/// Animates in by scaling up from slightly smaller and fading in,
/// giving the impression of moving toward the camera.
/// </summary>
public class CharacterPortrait
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private Texture2D _sprite;
    private float     _alpha       = 0f;
    private float     _targetAlpha = 0f;
    private float     _scale       = 0.85f;   // starts small, animates to 1
    private float     _targetScale = 1f;
    private const float FadeSpeed  = 8f;

    // Portrait height as fraction of screen height
    private const float HeightFraction = 0.72f;
    // How far up from the bottom of the screen (to clear the dialogue box)
    private const int   BottomMargin   = 170;

    public bool IsVisible => _alpha > 0.01f;

    public CharacterPortrait(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void Show(Texture2D sprite)
    {
        _sprite      = sprite;
        _targetAlpha = 1f;
        _targetScale = 1f;
        // Start small and faded — animates forward
        _alpha       = 0f;
        _scale       = 0.85f;
    }

    public void Hide()
    {
        _targetAlpha = 0f;
        _targetScale = 0.92f;
    }

    public void Update(float dt)
    {
        _alpha = MathHelper.Lerp(_alpha, _targetAlpha, dt * FadeSpeed);
        _scale = MathHelper.Lerp(_scale, _targetScale, dt * FadeSpeed);

        if (_targetAlpha == 0f && _alpha < 0.01f)
        {
            _alpha  = 0f;
            _sprite = null;
        }
    }

    public void Draw()
    {
        if (_sprite == null || _alpha <= 0.01f) return;

        var vp = _game.GraphicsDevice.Viewport;

        // Target height based on screen height, preserve aspect ratio
        float targetH = vp.Height * HeightFraction * _scale;
        float aspect  = (float)_sprite.Width / _sprite.Height;
        float targetW = targetH * aspect;

        // Centred horizontally, sitting just above the dialogue box
        int drawX = (int)(vp.Width  / 2f - targetW / 2f);
        int drawY = (int)(vp.Height - targetH - BottomMargin);

        var dest = new Rectangle(drawX, drawY, (int)targetW, (int)targetH);

        _spriteBatch.Begin(blendState: BlendState.AlphaBlend);

        // Drop shadow
        _spriteBatch.Draw(Assets.Pixel,
            new Rectangle(drawX + 8, drawY + 8, (int)targetW, (int)targetH),
            new Color(0, 0, 0, (int)(100 * _alpha)));

        _spriteBatch.Draw(_sprite, dest, Color.White * _alpha);

        _spriteBatch.End();
    }
}