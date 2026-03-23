// ======== UI/CharacterPortrait.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using ZebraBear.UI;

namespace ZebraBear;

/// <summary>
/// Renders a character portrait in 2D screenspace behind the dialogue box.
/// </summary>
public class CharacterPortrait
{
    private readonly Game _game;
    private readonly SpriteBatch _spriteBatch;

    private Texture2D _sprite;
    private float _alpha = 0f;
    private float _targetAlpha = 0f;
    private float _scale = 0.85f;
    private float _targetScale = 1f;
    private const float FadeSpeed = 8f;
    private const float HeightFraction = 0.65f;

    public bool IsVisible => _alpha > 0.01f;

    public CharacterPortrait(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _spriteBatch = spriteBatch;
    }

    public void Show(Texture2D sprite)
    {
        _sprite = sprite;
        _targetAlpha = 1f;
        _targetScale = 1f;
        _alpha = 0f;
        _scale = 0.85f;
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
            _alpha = 0f;
            _sprite = null;
        }
    }

    public void Draw()
    {
        if (_sprite == null || _alpha <= 0.01f) return;

        var vp = _game.GraphicsDevice.Viewport;

        float targetH = vp.Height * HeightFraction * _scale;
        float aspect = (float)_sprite.Width / _sprite.Height;
        float targetW = targetH * aspect;

        float maxW = vp.Width * 0.4f;
        if (targetW > maxW) { targetW = maxW; targetH = targetW / aspect; }

        int bottomMargin = (int)(vp.Height * 0.26f);
        int drawX = (int)(vp.Width / 2f - targetW / 2f);
        int drawY = (int)(vp.Height - targetH - bottomMargin);
        var dest = new Rectangle(drawX, drawY, (int)targetW, (int)targetH);

        _spriteBatch.Begin(blendState: BlendState.AlphaBlend);

        // Shadow
        LayoutDraw.Rect(_spriteBatch,
            new Rectangle(drawX + 6, drawY + 6, (int)targetW, (int)targetH),
            new Color(0, 0, 0, (int)(80 * _alpha)));

        _spriteBatch.Draw(_sprite, dest, Color.White * _alpha);
        _spriteBatch.End();
    }
}