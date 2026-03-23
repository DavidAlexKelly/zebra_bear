// ======== UI/DialogueBox.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear;

/// <summary>
/// Danganronpa-style dialogue panel.
/// Drives a DialogueNode tree. Handles multi-level branching internally.
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
    private DialogueNode _currentNode;
    private string[] _resolvedLines;
    private int _currentLine;
    private bool _showingChoice;
    private int _choiceIndex;

    // -----------------------------------------------------------------------
    // Typewriter state
    // -----------------------------------------------------------------------
    private float _charTimer;
    private float _charInterval = 0.03f;
    private int _visibleChars;
    private bool _lineComplete;

    // -----------------------------------------------------------------------
    // Public state
    // -----------------------------------------------------------------------
    public string SpeakerName = "";
    public bool IsFinished = false;
    public Action<int> OnChoice;

    // -----------------------------------------------------------------------
    // Choice button rects (set during Draw, read during Update for mouse clicks)
    // -----------------------------------------------------------------------
    private readonly List<Rectangle> _choiceRects = new();

    // -----------------------------------------------------------------------
    // Input tracking
    // -----------------------------------------------------------------------
    private KeyboardState _prevKb;
    private MouseState _prevMouse;
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
    public void StartDialogue(DialogueNode root)
    {
        IsFinished = false;
        _choiceRects.Clear();
        _prevKbInitialised = false;
        EnterNode(root);
    }

    public void StartDialogue(string[] lines, string[] choices = null)
    {
        var node = DialogueTreeParser.FromFlatLines(lines);
        if (choices != null && choices.Length > 0 && node != null)
            foreach (var label in choices)
                node.Choices.Add(new DialogueChoice { Label = label, Next = null });
        IsFinished = false;
        _choiceRects.Clear();
        _prevKbInitialised = false;
        EnterNode(node);
    }

    // -----------------------------------------------------------------------
    // Node transition
    // -----------------------------------------------------------------------
    private void EnterNode(DialogueNode node)
    {
        _currentNode = node;
        _resolvedLines = node?.ResolveLines() ?? Array.Empty<string>();
        _currentLine = 0;
        _showingChoice = false;
        _choiceIndex = 0;
        _choiceRects.Clear();
        if (node != null)
            foreach (var action in node.OnEnterActions)
                action?.Invoke(-1);
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

        // Initialise prev state on first frame to avoid phantom inputs
        if (!_prevKbInitialised)
        {
            _prevKb = kb;
            _prevMouse = mouse;
            _prevKbInitialised = true;
            return;
        }

        bool clicked = mouse.LeftButton == ButtonState.Released &&
                       prevMouse.LeftButton == ButtonState.Pressed;

        bool confirmKey = (kb.IsKeyDown(Keys.Enter) && !_prevKb.IsKeyDown(Keys.Enter)) ||
                          (kb.IsKeyDown(Keys.Z) && !_prevKb.IsKeyDown(Keys.Z));

        if (_showingChoice)
        {
            UpdateChoiceInput(kb, mouse, clicked, confirmKey);
            _prevKb = kb;
            _prevMouse = mouse;
            return;
        }

        bool confirm = clicked || confirmKey;
        UpdateTypewriter(dt, confirm);

        _prevKb = kb;
        _prevMouse = mouse;
    }

    private void UpdateTypewriter(float dt, bool confirm)
    {
        if (_resolvedLines.Length == 0) { AdvancePastLines(); return; }

        var currentText = _resolvedLines[_currentLine];
        if (!_lineComplete)
        {
            _charTimer += dt;
            while (_charTimer >= _charInterval && _visibleChars < currentText.Length)
            {
                _charTimer -= _charInterval;
                _visibleChars++;
            }
            if (_visibleChars >= currentText.Length) _lineComplete = true;
            if (confirm) { _visibleChars = currentText.Length; _lineComplete = true; }
            return;
        }

        if (confirm)
        {
            _currentLine++;
            if (_currentLine >= _resolvedLines.Length) AdvancePastLines();
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
            OnChoice?.Invoke(0);
            OnChoice = null;
            IsFinished = true;
        }
    }

    private void UpdateChoiceInput(KeyboardState kb, MouseState mouse, bool clicked, bool confirmKey)
    {
        var choices = _currentNode.Choices;

        // Keyboard navigation - left/right arrows
        if (kb.IsKeyDown(Keys.Left) && !_prevKb.IsKeyDown(Keys.Left))
            _choiceIndex = Math.Max(0, _choiceIndex - 1);
        if (kb.IsKeyDown(Keys.Right) && !_prevKb.IsKeyDown(Keys.Right))
            _choiceIndex = Math.Min(choices.Count - 1, _choiceIndex + 1);

        // A/D keys for navigation
        if (kb.IsKeyDown(Keys.A) && !_prevKb.IsKeyDown(Keys.A))
            _choiceIndex = Math.Max(0, _choiceIndex - 1);
        if (kb.IsKeyDown(Keys.D) && !_prevKb.IsKeyDown(Keys.D))
            _choiceIndex = Math.Min(choices.Count - 1, _choiceIndex + 1);

        // Mouse hover - update selection based on mouse position
        var mp = mouse.Position;
        for (int i = 0; i < _choiceRects.Count; i++)
        {
            if (_choiceRects[i].Contains(mp))
            {
                _choiceIndex = i;
                break;
            }
        }

        // Mouse click on a choice button
        if (clicked)
        {
            for (int i = 0; i < _choiceRects.Count; i++)
            {
                if (_choiceRects[i].Contains(mp))
                {
                    _choiceIndex = i;
                    ConfirmChoice();
                    return;
                }
            }
            // Click outside choices does nothing
            return;
        }

        // Keyboard confirm
        if (confirmKey)
            ConfirmChoice();
    }

    private void ConfirmChoice()
    {
        var choices = _currentNode.Choices;
        if (_choiceIndex < 0 || _choiceIndex >= choices.Count) return;

        var chosen = choices[_choiceIndex];

        foreach (var action in chosen.OnSelectActions)
            action?.Invoke(_choiceIndex);

        OnChoice?.Invoke(_choiceIndex);
        OnChoice = null;

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
        int boxH = Math.Max(140, (int)(vp.Height * 0.22f));
        int boxMX = Math.Max(30, (int)(vp.Width * 0.04f));
        int boxW = vp.Width - boxMX * 2;
        int boxX = boxMX;
        int boxY = vp.Height - boxH - 20;

        _spriteBatch.Begin();

        // Box background
        var boxRect = new Rectangle(boxX, boxY, boxW, boxH);
        LayoutDraw.Rect(_spriteBatch, boxRect, new Color(8, 8, 20, 230));
        LayoutDraw.AccentBar(_spriteBatch, boxRect);
        LayoutDraw.Rect(_spriteBatch, new Rectangle(boxX, boxY, boxW, 2), new Color(60, 60, 100));

        // Speaker name tag
        if (!string.IsNullOrEmpty(SpeakerName))
        {
            var nameSize = Assets.TitleFont.MeasureString(SpeakerName);
            int tagW = (int)nameSize.X + 24;
            int tagH = (int)nameSize.Y + 12;
            int tagX = boxX + 20;
            int tagY = boxY - tagH + 2;

            LayoutDraw.Rect(_spriteBatch, new Rectangle(tagX, tagY, tagW, tagH), LayoutDraw.Accent);
            LayoutDraw.Rect(_spriteBatch, new Rectangle(tagX, tagY, tagW, 2), new Color(255, 80, 100));
            _spriteBatch.DrawString(Assets.TitleFont, SpeakerName,
                new Vector2(tagX + 12, tagY + 6), Color.White);
        }

        // Content
        _boxStack.Padding = 20;
        _boxStack.Spacing = 0;
        _boxStack.Begin(boxRect);

        if (_showingChoice && _currentNode?.Choices.Count > 0)
            DrawChoices(boxRect, gameTime);
        else if (_resolvedLines.Length > 0 && _currentLine < _resolvedLines.Length)
            DrawCurrentLine(boxRect, gameTime);

        _spriteBatch.End();
    }

    private void DrawCurrentLine(Rectangle boxRect, GameTime gameTime)
    {
        var textArea = _boxStack.ConsumeRemaining();
        var visible = _resolvedLines[_currentLine].Substring(0, _visibleChars);

        LayoutDraw.TextWrapped(_spriteBatch, Assets.MenuFont, visible, textArea, Color.White, pad: 4);

        if (_lineComplete && !IsFinished)
        {
            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f);
            float alpha = 0.6f + pulse * 0.4f;
            var sz = Assets.MenuFont.MeasureString(">>");
            _spriteBatch.DrawString(Assets.MenuFont, ">>",
                new Vector2(boxRect.Right - sz.X - 28, boxRect.Bottom - sz.Y - 18),
                Color.White * alpha);
        }
    }

    private void DrawChoices(Rectangle boxRect, GameTime gameTime)
    {
        var choices = _currentNode.Choices;
        int cx = boxRect.X + boxRect.Width / 2;
        var mousePos = Mouse.GetState().Position;

        // Prompt at top
        var promptRect = _boxStack.Next(28);
        LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, "What will you do?",
            promptRect, new Color(180, 180, 200));

        _boxStack.Space(8);

        // Measure choices for centering
        int choiceH = 44;
        int pad = 24;
        int spacing = 16;

        float totalW = 0;
        var sizes = new Vector2[choices.Count];
        for (int i = 0; i < choices.Count; i++)
        {
            sizes[i] = Assets.MenuFont.MeasureString(choices[i].Label);
            totalW += sizes[i].X + pad * 2;
            if (i < choices.Count - 1) totalW += spacing;
        }

        int choiceY = boxRect.Y + boxRect.Height / 2 - choiceH / 2;
        float startX = cx - totalW / 2f;

        // Rebuild choice rects each frame
        _choiceRects.Clear();

        for (int i = 0; i < choices.Count; i++)
        {
            float cw = sizes[i].X + pad * 2;
            var btnRect = new Rectangle((int)startX, choiceY, (int)cw, choiceH);
            _choiceRects.Add(btnRect);

            bool selected = i == _choiceIndex;
            bool hovered = btnRect.Contains(mousePos);

            var bgCol = selected ? LayoutDraw.Accent
                      : hovered ? new Color(50, 40, 70)
                      : new Color(30, 28, 50);
            var textCol = selected || hovered ? Color.White : new Color(140, 130, 160);
            var borderCol = selected ? new Color(255, 80, 100)
                          : hovered ? new Color(120, 100, 160)
                          : new Color(50, 48, 70);

            LayoutDraw.Rect(_spriteBatch, btnRect, bgCol);
            LayoutDraw.Rect(_spriteBatch, new Rectangle(btnRect.X, btnRect.Y, btnRect.Width, 2), borderCol);
            LayoutDraw.Rect(_spriteBatch, new Rectangle(btnRect.X, btnRect.Y, 3, btnRect.Height),
                selected ? new Color(255, 180, 180) : borderCol);

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f);
            float alpha = selected ? 0.85f + pulse * 0.15f : 1f;

            LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont, choices[i].Label,
                btnRect, textCol * alpha);

            startX += cw + spacing;
        }

        // Hint at bottom
        var hintRect = new Rectangle(boxRect.X, boxRect.Bottom - 28, boxRect.Width, 24);
        LayoutDraw.TextCentre(_spriteBatch, Assets.MenuFont,
            "[Left/Right] or [A/D] Select   [Enter/Click] Confirm",
            hintRect, new Color(80, 75, 100));
    }
}