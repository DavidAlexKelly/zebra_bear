using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;

namespace ZebraBear;

/// <summary>
/// Danganronpa-style dialogue panel.
///
/// Drives a DialogueNode tree — flat string[] dialogue is auto-converted.
/// Handles multi-level branching: when the player picks a choice, the box
/// seamlessly continues into the next DialogueNode without scene involvement.
///
/// The scene just calls StartDialogue(entity) and checks IsFinished.
/// All branching, flag checks, and callbacks are handled internally.
/// </summary>
public class DialogueBox
{
    private readonly Game        _game;
    private readonly SpriteBatch _spriteBatch;

    // -----------------------------------------------------------------------
    // Tree state
    // -----------------------------------------------------------------------

    private DialogueNode _currentNode;
    private string[]     _resolvedLines;  // conditional lines evaluated for this node
    private int          _currentLine;
    private bool         _showingChoice;
    private int          _choiceIndex;

    // -----------------------------------------------------------------------
    // Typewriter state
    // -----------------------------------------------------------------------

    private float _charTimer;
    private float _charInterval = 0.03f;
    private int   _visibleChars;
    private bool  _lineComplete;

    // -----------------------------------------------------------------------
    // Public state
    // -----------------------------------------------------------------------

    public string     SpeakerName = "";
    public bool       IsFinished  = false;

    /// <summary>
    /// Fired when a top-level choice is confirmed on a leaf node that has
    /// a legacy OnInteract callback (backward compatibility).
    /// Cleared after firing.
    /// </summary>
    public Action<int> OnChoice;

    // -----------------------------------------------------------------------
    // Visual constants
    // -----------------------------------------------------------------------

    private const int BoxHeight  = 180;
    private const int BoxPadding = 24;
    private Color _accent = new Color(232, 0, 61);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public DialogueBox(Game game, SpriteBatch spriteBatch)
    {
        _game        = game;
        _spriteBatch = spriteBatch;
    }

    // -----------------------------------------------------------------------
    // Start
    // -----------------------------------------------------------------------

    /// <summary>
    /// Start dialogue from a DialogueNode tree.
    /// </summary>
    public void StartDialogue(DialogueNode root)
    {
        IsFinished = false;
        EnterNode(root);
    }

    /// <summary>
    /// Start dialogue from a flat string array (legacy / convenience path).
    /// Automatically converted to a linear DialogueNode.
    /// </summary>
    public void StartDialogue(string[] lines, string[] choices = null)
    {
        // Build a minimal node so the tree path handles it uniformly
        var node = DialogueTreeParser.FromFlatLines(lines);

        // Attach legacy choices as child nodes with no content
        if (choices != null && choices.Length > 0 && node != null)
        {
            foreach (var label in choices)
                node.Choices.Add(new DialogueChoice { Label = label, Next = null });
        }

        IsFinished = false;
        EnterNode(node);
    }

    // -----------------------------------------------------------------------
    // Node transition
    // -----------------------------------------------------------------------

    private void EnterNode(DialogueNode node)
    {
        _currentNode   = node;
        _resolvedLines = node?.ResolveLines() ?? Array.Empty<string>();
        _currentLine   = 0;
        _showingChoice = false;
        _choiceIndex   = 0;

        // Fire onEnter actions
        if (node != null)
            foreach (var action in node.OnEnterActions)
                action?.Invoke(-1);

        BeginLine();
    }

    private void BeginLine()
    {
        _visibleChars = 0;
        _charTimer    = 0f;
        _lineComplete = false;
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    public void Update(GameTime gameTime, MouseState mouse, MouseState prevMouse)
    {
        if (IsFinished || _currentNode == null) return;

        var   kb      = Keyboard.GetState();
        float dt      = (float)gameTime.ElapsedGameTime.TotalSeconds;
        bool  clicked = mouse.LeftButton     == ButtonState.Released &&
                        prevMouse.LeftButton == ButtonState.Pressed;
        bool  confirm = clicked ||
                        (kb.IsKeyDown(Keys.Enter) && !_prevKb.IsKeyDown(Keys.Enter)) ||
                        (kb.IsKeyDown(Keys.Z)     && !_prevKb.IsKeyDown(Keys.Z));

        _prevKb = kb;

        if (_showingChoice)
        {
            UpdateChoiceInput(kb, confirm);
            return;
        }

        UpdateTypewriter(dt, confirm);
    }

    private KeyboardState _prevKb;

    private void UpdateTypewriter(float dt, bool confirm)
    {
        if (_resolvedLines.Length == 0)
        {
            // No lines — jump straight to choices or finish
            AdvancePastLines();
            return;
        }

        var currentText = _resolvedLines[_currentLine];

        if (!_lineComplete)
        {
            _charTimer += dt;
            while (_charTimer >= _charInterval && _visibleChars < currentText.Length)
            {
                _charTimer -= _charInterval;
                _visibleChars++;
            }
            if (_visibleChars >= currentText.Length)
                _lineComplete = true;

            // Skip to end of line
            if (confirm)
            {
                _visibleChars = currentText.Length;
                _lineComplete = true;
            }
            return;
        }

        // Line complete — advance on confirm
        if (confirm)
        {
            _currentLine++;
            if (_currentLine >= _resolvedLines.Length)
                AdvancePastLines();
            else
                BeginLine();
        }
    }

    private void AdvancePastLines()
    {
        if (_currentNode.Choices.Count > 0)
        {
            _showingChoice = true;
            _choiceIndex   = 0;
        }
        else
        {
            // Leaf node — fire legacy OnChoice if present, then finish
            OnChoice?.Invoke(0);
            OnChoice = null;
            IsFinished = true;
        }
    }

    private void UpdateChoiceInput(KeyboardState kb, bool confirm)
    {
        var choices = _currentNode.Choices;

        // Navigate choices
        if (kb.IsKeyDown(Keys.Left)  || kb.IsKeyDown(Keys.A)) _choiceIndex = Math.Max(0, _choiceIndex - 1);
        if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D)) _choiceIndex = Math.Min(choices.Count - 1, _choiceIndex + 1);

        if (!confirm) return;

        var chosen = choices[_choiceIndex];

        // Fire onSelect actions for this choice
        foreach (var action in chosen.OnSelectActions)
            action?.Invoke(_choiceIndex);

        // Fire legacy OnChoice (backward compat)
        OnChoice?.Invoke(_choiceIndex);
        OnChoice = null;

        // Transition to next node or finish
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

        if (_showingChoice && _currentNode?.Choices.Count > 0)
            DrawChoices(boxX, boxY, boxW, gameTime);
        else if (_resolvedLines.Length > 0 && _currentLine < _resolvedLines.Length)
            DrawCurrentLine(boxX, boxY, boxW, gameTime);

        _spriteBatch.End();
    }

    private void DrawCurrentLine(int boxX, int boxY, int boxW, GameTime gameTime)
    {
        var visible = _resolvedLines[_currentLine].Substring(0, _visibleChars);
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

    private void DrawChoices(int boxX, int boxY, int boxW, GameTime gameTime)
    {
        var choices = _currentNode.Choices;
        int cx      = boxX + boxW / 2;
        int choiceY = boxY + BoxHeight / 2 - 20;
        int pad     = 24;
        int choiceH = 44;
        int spacing = 40;

        var prompt     = "What will you do?";
        var promptSize = Assets.MenuFont.MeasureString(prompt);
        _spriteBatch.DrawString(Assets.MenuFont, prompt,
            new Vector2(cx - promptSize.X / 2f, boxY + 20),
            new Color(180, 180, 200));

        // Measure all labels
        float totalW = 0;
        var sizes = new Vector2[choices.Count];
        for (int i = 0; i < choices.Count; i++)
        {
            var s    = Assets.MenuFont.MeasureString(choices[i].Label);
            sizes[i] = s;
            totalW  += s.X + pad * 2 + spacing;
        }
        totalW -= spacing;

        float startX = cx - totalW / 2f;

        for (int i = 0; i < choices.Count; i++)
        {
            bool  selected  = i == _choiceIndex;
            float cw        = sizes[i].X + pad * 2;
            var   bgCol     = selected ? _accent : new Color(30, 28, 50);
            var   textCol   = selected ? Color.White : new Color(140, 130, 160);
            var   borderCol = selected ? new Color(255, 80, 100) : new Color(50, 48, 70);

            DrawRect(new Rectangle((int)startX, choiceY, (int)cw, choiceH), bgCol);
            DrawRect(new Rectangle((int)startX, choiceY, (int)cw, 2), borderCol);
            DrawRect(new Rectangle((int)startX, choiceY, 3, choiceH),
                selected ? new Color(255, 180, 180) : borderCol);

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f);
            float alpha = selected ? 0.85f + pulse * 0.15f : 1f;

            _spriteBatch.DrawString(Assets.MenuFont, choices[i].Label,
                new Vector2(startX + pad, choiceY + choiceH / 2f - sizes[i].Y / 2f),
                textCol * alpha);

            startX += cw + spacing;
        }

        var hint     = "Left / Right to select   Click or Enter to confirm";
        var hintSize = Assets.MenuFont.MeasureString(hint);
        _spriteBatch.DrawString(Assets.MenuFont, hint,
            new Vector2(cx - hintSize.X / 2f, boxY + BoxHeight - hintSize.Y - 10),
            new Color(80, 75, 100));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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
                _spriteBatch.DrawString(Assets.MenuFont, line, new Vector2(position.X, y), color);
                y    += lineHeight;
                line  = word;
            }
            else line = test;
        }
        if (line.Length > 0)
            _spriteBatch.DrawString(Assets.MenuFont, line, new Vector2(position.X, y), color);
    }

    private void DrawRect(Rectangle rect, Color color) =>
        _spriteBatch.Draw(Assets.Pixel, rect, color);
}