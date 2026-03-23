using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ZebraBear;

/// <summary>
/// Danganronpa-style dialogue panel.
/// Uses Assets.MenuFont, Assets.TitleFont, Assets.Pixel — no constructor params needed.
/// </summary>
public class DialogueBox
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    private string[] _lines;
    private int      _currentLine  = 0;
    private float    _charTimer    = 0f;
    private float    _charInterval = 0.03f;
    private int      _visibleChars = 0;
    private bool     _lineComplete = false;

    public string SpeakerName = "";
    public bool   IsFinished  = false;

    private string[] _choices       = null;
    private int      _choiceIndex   = 0;
    private bool     _showingChoice = false;
    public  int      ChoiceResult   = -1;

    private const int BoxHeight  = 180;
    private const int BoxPadding = 24;
    private Color _accent = new Color(232, 0, 61);

    /// <summary>
    /// Invoked when the player confirms a choice.
    /// Set this before calling StartDialogue; it's cleared after firing.
    /// </summary>
    public Action<int> OnChoice;

    public DialogueBox(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    public void StartDialogue(string[] lines, string[] choices = null)
    {
        _lines         = lines;
        _choices       = choices;
        _currentLine   = 0;
        IsFinished     = false;
        ChoiceResult   = -1;
        _showingChoice = false;
        BeginLine();
    }

    private void BeginLine()
    {
        _visibleChars = 0;
        _charTimer    = 0f;
        _lineComplete = false;
    }

    public void Update(GameTime gameTime, MouseState mouse, MouseState prevMouse)
    {
        if (IsFinished) return;

        var   kb      = Keyboard.GetState();
        float dt      = (float)gameTime.ElapsedGameTime.TotalSeconds;
        bool  clicked = mouse.LeftButton     == ButtonState.Released &&
                        prevMouse.LeftButton == ButtonState.Pressed;

        if (_showingChoice && _choices != null)
        {
            if (kb.IsKeyDown(Keys.Left)  || kb.IsKeyDown(Keys.A)) _choiceIndex = 0;
            if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) _choiceIndex = 1;

            if (clicked || kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Z))
            {
                ChoiceResult   = _choiceIndex;
                _showingChoice = false;
                IsFinished     = true;
                OnChoice?.Invoke(ChoiceResult);
                OnChoice = null; // clear after firing
            }
            return;
        }

        var currentText = _lines[_currentLine];

        if (!_lineComplete)
        {
            _charTimer += dt;
            while (_charTimer >= _charInterval && _visibleChars < currentText.Length)
            {
                _charTimer -= _charInterval;
                _visibleChars++;
            }
            if (_visibleChars >= currentText.Length) _lineComplete = true;
        }

        if (clicked)
        {
            if (!_lineComplete)
            {
                _visibleChars = currentText.Length;
                _lineComplete = true;
            }
            else
            {
                _currentLine++;
                if (_currentLine >= _lines.Length)
                {
                    if (_choices != null && _choices.Length > 0)
                    {
                        _showingChoice = true;
                        _choiceIndex   = 0;
                    }
                    else
                    {
                        IsFinished = true;
                    }
                }
                else
                {
                    BeginLine();
                }
            }
        }
    }

    public void Draw(GameTime gameTime)
    {
        var vp   = _game.GraphicsDevice.Viewport;
        int boxY = vp.Height - BoxHeight - 20;
        int boxW = vp.Width  - 80;
        int boxX = 40;

        _spriteBatch.Begin();

        DrawRect(new Rectangle(boxX, boxY, boxW, BoxHeight), new Color(8, 8, 20, 230));
        DrawRect(new Rectangle(boxX, boxY, 4, BoxHeight), _accent);
        DrawRect(new Rectangle(boxX, boxY, boxW, 2), new Color(60, 60, 100));

        if (!string.IsNullOrEmpty(SpeakerName))
        {
            var nameSize = Assets.TitleFont.MeasureString(SpeakerName);
            int tagW = (int)nameSize.X + 24;
            int tagH = 40;
            int tagX = boxX + 20;
            int tagY = boxY - tagH + 2;

            DrawRect(new Rectangle(tagX, tagY, tagW, tagH), _accent);
            DrawRect(new Rectangle(tagX, tagY, tagW, 2), new Color(255, 80, 100));
            _spriteBatch.DrawString(Assets.TitleFont, SpeakerName,
                new Vector2(tagX + 12, tagY + 4), Color.White);
        }

        if (_showingChoice && _choices != null)
        {
            DrawChoices(boxX, boxY, boxW, gameTime);
        }
        else if (_lines != null && _currentLine < _lines.Length)
        {
            var visible = _lines[_currentLine].Substring(0, _visibleChars);
            DrawWrappedText(visible,
                new Vector2(boxX + BoxPadding + 10, boxY + BoxPadding),
                boxW - BoxPadding * 2 - 20, Color.White);

            if (_lineComplete && !IsFinished)
            {
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f);
                float alpha = 0.6f + pulse * 0.4f;
                var   sz    = Assets.MenuFont.MeasureString(">>");
                _spriteBatch.DrawString(Assets.MenuFont, ">>",
                    new Vector2(boxX + boxW - sz.X - 20, boxY + BoxHeight - sz.Y - 14),
                    Color.White * alpha);
            }
        }

        _spriteBatch.End();
    }

    private void DrawChoices(int boxX, int boxY, int boxW, GameTime gameTime)
    {
        int   cx        = boxX + boxW / 2;
        int   choiceY   = boxY + BoxHeight / 2 - 20;
        int   choicePad = 24;
        int   choiceH   = 44;
        int   spacing   = 40;

        var prompt     = "What will you do?";
        var promptSize = Assets.MenuFont.MeasureString(prompt);
        _spriteBatch.DrawString(Assets.MenuFont, prompt,
            new Vector2(cx - promptSize.X / 2f, boxY + 20),
            new Color(180, 180, 200));

        float totalW = 0;
        var sizes = new System.Numerics.Vector2[_choices.Length];
        for (int i = 0; i < _choices.Length; i++)
        {
            var s    = Assets.MenuFont.MeasureString(_choices[i]);
            sizes[i] = new System.Numerics.Vector2(s.X, s.Y);
            totalW  += s.X + choicePad * 2 + spacing;
        }
        totalW -= spacing;

        float startX = cx - totalW / 2f;

        for (int i = 0; i < _choices.Length; i++)
        {
            bool  selected  = i == _choiceIndex;
            float cw        = sizes[i].X + choicePad * 2;
            float cx2       = startX;
            var   bgCol     = selected ? _accent : new Color(30, 28, 50);
            var   textCol   = selected ? Color.White : new Color(140, 130, 160);
            var   borderCol = selected ? new Color(255, 80, 100) : new Color(50, 48, 70);

            DrawRect(new Rectangle((int)cx2, choiceY, (int)cw, choiceH), bgCol);
            DrawRect(new Rectangle((int)cx2, choiceY, (int)cw, 2), borderCol);
            DrawRect(new Rectangle((int)cx2, choiceY, 3, choiceH),
                selected ? new Color(255, 180, 180) : borderCol);

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f);
            float alpha = selected ? 0.85f + pulse * 0.15f : 1f;

            _spriteBatch.DrawString(Assets.MenuFont, _choices[i],
                new Vector2(cx2 + choicePad, choiceY + choiceH / 2f - sizes[i].Y / 2f),
                textCol * alpha);

            startX += cw + spacing;
        }

        var hint     = "Left / Right to select   Click or Enter to confirm";
        var hintSize = Assets.MenuFont.MeasureString(hint);
        _spriteBatch.DrawString(Assets.MenuFont, hint,
            new Vector2(cx - hintSize.X / 2f, boxY + BoxHeight - hintSize.Y - 10),
            new Color(80, 75, 100));
    }

    private void DrawWrappedText(string text, Vector2 position, float maxWidth, Color color)
    {
        var   words      = text.Split(' ');
        var   line       = "";
        float y          = position.Y;
        float lineHeight = Assets.MenuFont.LineSpacing + 4f;

        foreach (var word in words)
        {
            var test = line.Length == 0 ? word : line + " " + word;
            if (Assets.MenuFont.MeasureString(test).X > maxWidth && line.Length > 0)
            {
                _spriteBatch.DrawString(Assets.MenuFont, line,
                    new Vector2(position.X, y), color);
                y    += lineHeight;
                line  = word;
            }
            else line = test;
        }

        if (line.Length > 0)
            _spriteBatch.DrawString(Assets.MenuFont, line,
                new Vector2(position.X, y), color);
    }

    private void DrawRect(Rectangle rect, Color color) =>
        _spriteBatch.Draw(Assets.Pixel, rect, color);
}