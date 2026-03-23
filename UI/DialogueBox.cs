using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.Scenes;
using ZebraBear.UI;

namespace ZebraBear;

/// <summary>
/// Danganronpa-style dialogue panel.
///
/// Drives an InteractionNode tree — the single representation authored
/// in the editor and stored in InteractionDef. All branching and navigation
/// are handled here; the scene just calls StartDialogue(entity.Interaction)
/// and checks IsFinished each frame.
///
/// Navigation (InteractionNode.NavigateTarget / InteractionChoice.NavigateTarget)
/// is executed directly via NavigationBus — no compiled delegate callbacks.
/// </summary>
public class DialogueBox
{
    private readonly Game _game;
    private readonly SpriteBatch _spriteBatch;

    // Layout
    private readonly VStack _boxStack = new() { Padding = 0, Spacing = 0 };

    // -----------------------------------------------------------------------
    // Tree state
    // -----------------------------------------------------------------------
    private InteractionNode _currentNode;
    private int _currentLine;
    private bool _showingChoice;
    private int _choiceIndex;

    // -----------------------------------------------------------------------
    // Typewriter state
    // -----------------------------------------------------------------------
    private float _charTimer;
    private const float CharInterval = 0.03f;
    private int _visibleChars;
    private bool _lineComplete;

    // -----------------------------------------------------------------------
    // Public state
    // -----------------------------------------------------------------------
    public string SpeakerName = "";
    public bool IsFinished = false;

    // -----------------------------------------------------------------------
    // Choice button rects (set during Draw, read during Update for mouse clicks)
    // -----------------------------------------------------------------------
    private readonly List<Rectangle> _choiceRects = new();

    // -----------------------------------------------------------------------
    // Input tracking
    // -----------------------------------------------------------------------
    private KeyboardState _prevKb;
    private bool _prevKbInitialised = false;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public DialogueBox(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _spriteBatch = spriteBatch;
    }

    // -----------------------------------------------------------------------
    // Start
    // -----------------------------------------------------------------------

    /// <summary>
    /// Begin dialogue from the root node of an InteractionDef.
    /// Call this when the player interacts with an entity.
    /// </summary>
    public void StartDialogue(InteractionDef interaction)
    {
        IsFinished = false;
        _choiceRects.Clear();
        _prevKbInitialised = false;
        EnterNode(interaction.Root);
    }

    // -----------------------------------------------------------------------
    // Node transition
    // -----------------------------------------------------------------------
    private void EnterNode(InteractionNode node)
    {
        _currentNode = node;
        _currentLine = 0;
        _showingChoice = false;
        _choiceIndex = 0;
        BeginLine();
    }

    private void BeginLine()
    {
        _visibleChars = 0;
        _charTimer = 0f;
        _lineComplete = false;
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime, MouseState mouse, MouseState prevMouse)
    {
        if (IsFinished || _currentNode == null) return;

        var kb = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Skip the very first frame's confirm so the key that opened dialogue
        // doesn't immediately advance it.
        if (!_prevKbInitialised) { _prevKb = kb; _prevKbInitialised = true; }

        bool clicked =
            mouse.LeftButton == ButtonState.Released &&
            prevMouse.LeftButton == ButtonState.Pressed;

        bool confirm =
            clicked ||
            (kb.IsKeyDown(Keys.Enter) && !_prevKb.IsKeyDown(Keys.Enter)) ||
            (kb.IsKeyDown(Keys.Z)     && !_prevKb.IsKeyDown(Keys.Z));

        _prevKb = kb;

        if (_showingChoice)
        {
            UpdateChoiceInput(kb, mouse, clicked, confirm);
            return;
        }

        UpdateTypewriter(dt, confirm);
    }

    private void UpdateTypewriter(float dt, bool confirm)
    {
        var lines = _currentNode.Lines;

        if (lines.Count == 0)
        {
            // No lines — jump straight to choices or finish.
            AdvancePastLines();
            return;
        }

        string currentText = lines[_currentLine];

        if (!_lineComplete)
        {
            _charTimer += dt;
            while (_charTimer >= CharInterval && _visibleChars < currentText.Length)
            {
                _charTimer -= CharInterval;
                _visibleChars++;
            }
            if (_visibleChars >= currentText.Length) _lineComplete = true;
            if (confirm) { _visibleChars = currentText.Length; _lineComplete = true; }
            return;
        }

        if (confirm)
        {
            _currentLine++;
            if (_currentLine >= lines.Count) AdvancePastLines();
            else BeginLine();
        }
    }

    private void AdvancePastLines()
    {
        if (_currentNode.Choices.Count > 0)
        {
            _showingChoice = true;
            _choiceIndex = 0;
            _choiceRects.Clear();
        }
        else
        {
            // Leaf node — handle any navigation target, then finish.
            if (!string.IsNullOrEmpty(_currentNode.NavigateTarget))
                NavigationBus.RequestNavigate(_currentNode.NavigateTarget);

            IsFinished = true;
        }
    }

    private void UpdateChoiceInput(KeyboardState kb, MouseState mouse, bool clicked, bool confirmKey)
    {
        var choices = _currentNode.Choices;

        // Keyboard navigation
        if (kb.IsKeyDown(Keys.Left)  && !_prevKb.IsKeyDown(Keys.Left))
            _choiceIndex = Math.Max(0, _choiceIndex - 1);
        if (kb.IsKeyDown(Keys.Right) && !_prevKb.IsKeyDown(Keys.Right))
            _choiceIndex = Math.Min(choices.Count - 1, _choiceIndex + 1);
        if (kb.IsKeyDown(Keys.A) && !_prevKb.IsKeyDown(Keys.A))
            _choiceIndex = Math.Max(0, _choiceIndex - 1);
        if (kb.IsKeyDown(Keys.D) && !_prevKb.IsKeyDown(Keys.D))
            _choiceIndex = Math.Min(choices.Count - 1, _choiceIndex + 1);

        // Mouse hover
        var mp = mouse.Position;
        for (int i = 0; i < _choiceRects.Count; i++)
            if (_choiceRects[i].Contains(mp)) { _choiceIndex = i; break; }

        // Mouse click
        if (clicked)
        {
            for (int i = 0; i < _choiceRects.Count; i++)
                if (_choiceRects[i].Contains(mp)) { _choiceIndex = i; ConfirmChoice(); return; }
            return;
        }

        if (confirmKey) ConfirmChoice();
    }

    private void ConfirmChoice()
    {
        var choices = _currentNode.Choices;
        if (_choiceIndex < 0 || _choiceIndex >= choices.Count) return;

        var chosen = choices[_choiceIndex];

        // Handle navigation on the choice itself.
        if (!string.IsNullOrEmpty(chosen.NavigateTarget))
            NavigationBus.RequestNavigate(chosen.NavigateTarget);

        // Descend into the next node, or finish.
        if (chosen.Next != null)
            EnterNode(chosen.Next);
        else
            IsFinished = true;
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------
    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        int boxH  = Math.Max(140, (int)(vp.Height * 0.22f));
        int boxMX = Math.Max(30, (int)(vp.Width * 0.04f));
        int boxW  = vp.Width - boxMX * 2;
        int boxX  = boxMX;
        int boxY  = vp.Height - boxH - 20;

        _spriteBatch.Begin();

        var boxRect = new Rectangle(boxX, boxY, boxW, boxH);
        LayoutDraw.Rect(_spriteBatch, boxRect, new Color(8, 8, 20, 230));
        LayoutDraw.AccentBar(_spriteBatch, boxRect);
        LayoutDraw.Rect(_spriteBatch, new Rectangle(boxX, boxY, boxW, 2), new Color(60, 60, 100));

        // Speaker name tag
        if (!string.IsNullOrEmpty(SpeakerName))
        {
            var nameSize = Assets.TitleFont.MeasureString(SpeakerName);
            int tagW  = (int)nameSize.X + 24;
            int tagH  = 36;
            int tagX  = boxX + 10;
            int tagY  = boxY - tagH + 4;
            var tagRect = new Rectangle(tagX, tagY, tagW, tagH);
            LayoutDraw.Rect(_spriteBatch, tagRect, new Color(232, 0, 61));
            _spriteBatch.DrawString(Assets.TitleFont, SpeakerName,
                new Vector2(tagX + 12, tagY + (tagH - nameSize.Y) / 2f), Color.White);
        }

        int textX = boxX + 50;
        int textY = boxY + 24;
        int textW = boxW - 100;

        if (_showingChoice)
            DrawChoices(boxX, boxY, boxW, boxH, gameTime);
        else
            DrawCurrentLine(textX, textY, textW, boxY, boxH, gameTime);

        _spriteBatch.End();
    }

    private void DrawCurrentLine(int textX, int textY, int textW, int boxY, int boxH, GameTime gt)
    {
        if (_currentNode == null || _currentNode.Lines.Count == 0) return;

        string fullText = _currentNode.Lines[_currentLine];
        string visible  = fullText[..Math.Min(_visibleChars, fullText.Length)];

        // Word-wrap
        var wrapped = WrapText(Assets.MenuFont, visible, textW);
        _spriteBatch.DrawString(Assets.MenuFont, wrapped,
            new Vector2(textX, textY), new Color(220, 215, 240));

        // Advance indicator (pulsing >>)
        if (_lineComplete)
        {
            float pulse = (float)Math.Sin(gt.TotalGameTime.TotalSeconds * 4f) * 0.5f + 0.5f;
            var   col   = Color.Lerp(new Color(100, 100, 140), Color.White, pulse);
            _spriteBatch.DrawString(Assets.MenuFont, ">>",
                new Vector2(textX + textW - 30, boxY + boxH - 32), col);
        }
    }

    private void DrawChoices(int boxX, int boxY, int boxW, int boxH, GameTime gt)
    {
        if (_currentNode == null) return;
        var choices = _currentNode.Choices;

        _choiceRects.Clear();

        int btnW   = Math.Min(220, (boxW - 40) / Math.Max(1, choices.Count) - 10);
        int btnH   = 44;
        int totalW = choices.Count * (btnW + 10) - 10;
        int startX = boxX + (boxW - totalW) / 2;
        int btnY   = boxY + (boxH - btnH) / 2 + 10;

        for (int i = 0; i < choices.Count; i++)
        {
            bool selected = i == _choiceIndex;
            int  bx       = startX + i * (btnW + 10);
            var  rect     = new Rectangle(bx, btnY, btnW, btnH);
            _choiceRects.Add(rect);

            var bg     = selected ? new Color(232, 0, 61) : new Color(30, 28, 50);
            var border = selected ? new Color(255, 80, 100) : new Color(80, 75, 120);

            if (selected)
            {
                float pulse = (float)Math.Sin(gt.TotalGameTime.TotalSeconds * 5f) * 0.15f + 0.85f;
                bg = new Color((int)(232 * pulse), 0, (int)(61 * pulse));
            }

            LayoutDraw.Rect(_spriteBatch, rect, bg);
            LayoutDraw.BorderRect(_spriteBatch, rect, border);
            LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, choices[i].Label, rect, Color.White);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static string WrapText(SpriteFont font, string text, int maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var words  = text.Split(' ');
        var result = new System.Text.StringBuilder();
        var line   = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            var test = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureString(test).X > maxWidth && line.Length > 0)
            {
                result.AppendLine(line.ToString());
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0) result.Append(line);
        return result.ToString();
    }
}