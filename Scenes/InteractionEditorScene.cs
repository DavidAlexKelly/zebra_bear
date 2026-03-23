//======== Scenes/InteractionEditorScene.cs ========
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

/// <summary>
/// Visual dialogue/interaction editor.
/// Opened from the level editor's Interactions tab.
/// Edits a single InteractionDef — its node tree of lines, choices, and events.
/// </summary>
public class InteractionEditorScene : IScene
{
    private readonly Game _game;
    private readonly SpriteBatch _sb;

    // Layout
    private readonly VStack _sideStack = new() { Padding = 10, Spacing = 6 };
    private readonly VStack _nodeStack = new() { Padding = 12, Spacing = 6 };

    // State
    private InteractionDef _interaction;
    private InteractionNode _selectedNode;
    private Action _onSave;

    // Editing
    private bool _editingName = false;
    private bool _editingLine = false;
    private int _editingLineIndex = -1;
    private string _editBuffer = "";
    private bool _editingChoiceLabel = false;
    private int _editingChoiceIndex = -1;

    // Connected rooms (for navigate events)
    private RoomEditorContext _context;

    // Nav dropdown
    private bool _showNavDropdown = false;
    private int _navDropdownForChoice = -1; // -1 = node-level nav, 0+ = choice index
    private List<Rectangle> _navDropdownRects = new();
    private Rectangle _navSelectRect;

    // Cached rects
    private Rectangle _saveRect, _backRect;
    private Rectangle _nameRect;
    private Rectangle _addLineRect;
    private List<Rectangle> _lineRects = new();
    private List<Rectangle> _lineDeleteRects = new();
    private Rectangle _addChoiceRect;
    private List<Rectangle> _choiceLabelRects = new();
    private List<Rectangle> _choiceEditRects = new();
    private List<Rectangle> _choiceDeleteRects = new();
    private List<Rectangle> _choiceNavRects = new();
    private Rectangle _nodeNavRect, _nodeNavClearRect;

    // Input
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private Keys[] _prevPressedKeys = Array.Empty<Keys>();

    public InteractionEditorScene(Game game, SpriteBatch spriteBatch)
    {
        _game = game;
        _sb = spriteBatch;
    }

    public void OpenInteraction(InteractionDef interaction, RoomEditorContext context, Action onSave)
    {
        _interaction = interaction;
        _selectedNode = interaction.Root;
        _context = context;
        _onSave = onSave;
        _editingName = false;
        _editingLine = false;
        _editingChoiceLabel = false;
        _showNavDropdown = false;
    }

    public void Load() { }
    public void OnEnter() { _game.IsMouseVisible = true; }
    public void OnExit() { _game.IsMouseVisible = false; }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys = Keyboard.GetState();
        var mp = mouse.Position.ToVector2();

        bool clicked = mouse.LeftButton == ButtonState.Released &&
                       _prevMouse.LeftButton == ButtonState.Pressed;
        bool rightClicked = mouse.RightButton == ButtonState.Released &&
                            _prevMouse.RightButton == ButtonState.Pressed;

        // ── Nav dropdown intercept ────────────────────────────────────────
        if (_showNavDropdown && clicked)
        {
            bool hit = false;
            for (int i = 0; i < _navDropdownRects.Count; i++)
            {
                if (_navDropdownRects[i].Contains(mp))
                {
                    var connected = _context?.ConnectedRooms;
                    if (connected != null && i < connected.Count)
                    {
                        string target = connected[i].Id;
                        if (_navDropdownForChoice == -1)
                            _selectedNode.NavigateTarget = target;
                        else if (_navDropdownForChoice < _selectedNode.Choices.Count)
                            _selectedNode.Choices[_navDropdownForChoice].NavigateTarget = target;
                    }
                    hit = true;
                    break;
                }
            }
            _showNavDropdown = false;
            if (hit) { SaveInput(mouse, keys); return; }
        }

        // ── Text editing intercept ────────────────────────────────────────
        if (_editingName || _editingLine || _editingChoiceLabel)
        {
            HandleTextEditing(keys, clicked, mp);
            SaveInput(mouse, keys);
            return;
        }

        // ── Escape ────────────────────────────────────────────────────────
        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            _onSave?.Invoke();
            NavigationBus.RequestNavigate("LevelEditor");
            SaveInput(mouse, keys);
            return;
        }

        // ── Clicks ────────────────────────────────────────────────────────
        if (clicked)
        {
            if (_saveRect.Contains(mp))
            { _onSave?.Invoke(); NavigationBus.RequestNavigate("LevelEditor"); }
            else if (_backRect.Contains(mp))
            { _onSave?.Invoke(); NavigationBus.RequestNavigate("LevelEditor"); }
            else if (_nameRect.Contains(mp))
            { _editingName = true; _editBuffer = _interaction.Name; }
            else if (_addLineRect.Contains(mp))
            { _selectedNode.Lines.Add("New line"); }
            else if (_addChoiceRect.Contains(mp))
            {
                _selectedNode.Choices.Add(new InteractionChoice
                {
                    Label = "Choice",
                    Next = new InteractionNode()
                });
            }
            else
            {
                // Line clicks
                for (int i = 0; i < _lineRects.Count; i++)
                {
                    if (_lineDeleteRects[i].Contains(mp))
                    { if (i < _selectedNode.Lines.Count) _selectedNode.Lines.RemoveAt(i); break; }
                    if (_lineRects[i].Contains(mp))
                    { _editingLine = true; _editingLineIndex = i; _editBuffer = _selectedNode.Lines[i]; break; }
                }

                // Choice clicks
                for (int i = 0; i < _choiceLabelRects.Count; i++)
                {
                    if (i < _choiceDeleteRects.Count && _choiceDeleteRects[i].Contains(mp))
                    { if (i < _selectedNode.Choices.Count) _selectedNode.Choices.RemoveAt(i); break; }
                    if (i < _choiceEditRects.Count && _choiceEditRects[i].Contains(mp) &&
                        _selectedNode.Choices[i].Next != null)
                    { _selectedNode = _selectedNode.Choices[i].Next; break; }
                    if (i < _choiceNavRects.Count && _choiceNavRects[i].Contains(mp))
                    { _showNavDropdown = true; _navDropdownForChoice = i; _navSelectRect = _choiceNavRects[i]; break; }
                    if (_choiceLabelRects[i].Contains(mp))
                    { _editingChoiceLabel = true; _editingChoiceIndex = i; _editBuffer = _selectedNode.Choices[i].Label; break; }
                }

                // Node nav
                if (_nodeNavRect.Contains(mp))
                { _showNavDropdown = true; _navDropdownForChoice = -1; _navSelectRect = _nodeNavRect; }
                if (_nodeNavClearRect.Contains(mp))
                    _selectedNode.NavigateTarget = null;
            }
        }

        SaveInput(mouse, keys);
    }

    private void HandleTextEditing(KeyboardState keys, bool clicked, Vector2 mp)
    {
        if (IsPressed(keys, _prevKeys, Keys.Escape) || IsPressed(keys, _prevKeys, Keys.Enter))
        {
            CommitEdit();
            return;
        }

        // Click outside to commit
        if (clicked)
        {
            bool insideField = false;
            if (_editingName && _nameRect.Contains(mp)) insideField = true;
            if (_editingLine && _editingLineIndex < _lineRects.Count && _lineRects[_editingLineIndex].Contains(mp)) insideField = true;
            if (_editingChoiceLabel && _editingChoiceIndex < _choiceLabelRects.Count && _choiceLabelRects[_editingChoiceIndex].Contains(mp)) insideField = true;
            if (!insideField) { CommitEdit(); return; }
        }

        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back) { if (_editBuffer.Length > 0) _editBuffer = _editBuffer[..^1]; continue; }
            char? c = KeyToChar(key, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
            if (c.HasValue && _editBuffer.Length < 200) _editBuffer += c.Value;
        }
    }

    private void CommitEdit()
    {
        if (_editingName)
            _interaction.Name = _editBuffer;
        if (_editingLine && _editingLineIndex >= 0 && _editingLineIndex < _selectedNode.Lines.Count)
            _selectedNode.Lines[_editingLineIndex] = _editBuffer;
        if (_editingChoiceLabel && _editingChoiceIndex >= 0 && _editingChoiceIndex < _selectedNode.Choices.Count)
            _selectedNode.Choices[_editingChoiceIndex].Label = _editBuffer;

        _editingName = false;
        _editingLine = false;
        _editingChoiceLabel = false;
    }

    private void SaveInput(MouseState mouse, KeyboardState keys)
    {
        _prevMouse = mouse;
        _prevKeys = keys;
        _prevPressedKeys = keys.GetPressedKeys();
    }

    // -----------------------------------------------------------------------
    // Draw
    // -----------------------------------------------------------------------
    public void Draw(GameTime gameTime)
    {
        var vp = _game.GraphicsDevice.Viewport;
        var mp = Mouse.GetState().Position.ToVector2();

        _sb.Begin();

        LayoutDraw.Rect(_sb, new Rectangle(0, 0, vp.Width, vp.Height), LayoutDraw.BgDark);

        // ── LEFT SIDEBAR ──────────────────────────────────────────────────
        int sideW = 240;
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, sideW, vp.Height), LayoutDraw.PanelBg);
        LayoutDraw.Rect(_sb, new Rectangle(sideW, 0, 1, vp.Height), LayoutDraw.Border);

        _sideStack.Begin(0, 0, sideW, vp.Height);

        var titleRect = _sideStack.Next(36);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "INTERACTION", titleRect, LayoutDraw.Accent);

        _sideStack.Space(4);

        // Interaction name
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Name:", _sideStack.Next(20), new Color(120, 115, 160));
        _nameRect = _sideStack.Next(40);
        string nameDisplay = _editingName ? _editBuffer : _interaction.Name;
        LayoutDraw.TextArea(_sb, _nameRect, nameDisplay, _editingName, mp, "Enter name...");

        _sideStack.Space(8);
        LayoutDraw.DividerLine(_sb, _sideStack.Divider());

        // Node tree navigation
        LayoutDraw.SectionHeader(_sb, _sideStack.Next(20), "Node Tree:");
        DrawNodeTree(_interaction.Root, 0, mp);

        // Bottom buttons
        _backRect = _sideStack.NextFromBottom(44);
        _saveRect = _sideStack.NextFromBottom(44);
        LayoutDraw.Button(_sb, _saveRect, "Save + Return", mp);
        LayoutDraw.Button(_sb, _backRect, "< Back", mp);

        // ── MAIN AREA: Node editor ────────────────────────────────────────
        int mainX = sideW + 16;
        int mainY = 16;
        int mainW = vp.Width - sideW - 32;
        int mainH = vp.Height - 32;

        LayoutDraw.Rect(_sb, new Rectangle(mainX, mainY, mainW, mainH), new Color(12, 11, 22));
        LayoutDraw.BorderRect(_sb, new Rectangle(mainX, mainY, mainW, mainH), LayoutDraw.Border);

        _nodeStack.Begin(mainX, mainY, mainW, mainH);

        // Node header
        var nodeTitle = _nodeStack.Next(30);
        bool isRoot = _selectedNode == _interaction.Root;
        string nodeLabel = isRoot ? "Root Node" : $"Node: {_selectedNode.Id[..Math.Min(12, _selectedNode.Id.Length)]}";
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, nodeLabel, nodeTitle, LayoutDraw.Accent);

        if (!isRoot)
        {
            // Back to parent button
            var backToParent = new Rectangle(nodeTitle.Right - 120, nodeTitle.Y, 120, nodeTitle.Height);
            if (LayoutDraw.Button(_sb, backToParent, "< Parent", mp))
            { /* hover only */ }
            if (_prevMouse.LeftButton == ButtonState.Pressed &&
                Mouse.GetState().LeftButton == ButtonState.Released &&
                backToParent.Contains(mp))
            {
                var parent = FindParent(_interaction.Root, _selectedNode);
                if (parent != null) _selectedNode = parent;
            }
        }

        LayoutDraw.DividerLine(_sb, _nodeStack.Divider());

        // ── Lines section ─────────────────────────────────────────────────
        LayoutDraw.SectionHeader(_sb, _nodeStack.Next(22), $"Dialogue Lines ({_selectedNode.Lines.Count}):");
        _nodeStack.Space(4);

        _lineRects.Clear();
        _lineDeleteRects.Clear();

        for (int i = 0; i < _selectedNode.Lines.Count; i++)
        {
            var lineRow = _nodeStack.Next(36);
            _lineRects.Add(lineRow);

            // Line number
            _sb.DrawString(Assets.MenuFont, $"{i + 1}.",
                new Vector2(lineRow.X + 4, lineRow.Y + 8), new Color(80, 75, 110));

            // Line text (editable)
            var textRect = new Rectangle(lineRow.X + 30, lineRow.Y, lineRow.Width - 70, lineRow.Height);
            bool isEditing = _editingLine && _editingLineIndex == i;
            string display = isEditing ? _editBuffer : _selectedNode.Lines[i];
            LayoutDraw.TextArea(_sb, textRect, display, isEditing, mp, "Click to edit...");

            // Delete button
            var delRect = new Rectangle(lineRow.Right - 34, lineRow.Y + 4, 28, 28);
            _lineDeleteRects.Add(delRect);
            bool hovDel = delRect.Contains(mp);
            LayoutDraw.Rect(_sb, delRect, hovDel ? new Color(80, 20, 20) : new Color(30, 18, 18));
            LayoutDraw.BorderRect(_sb, delRect, hovDel ? LayoutDraw.Accent : new Color(80, 40, 40));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "X", delRect, hovDel ? Color.White : new Color(180, 80, 80));
        }

        _addLineRect = _nodeStack.Next(36);
        bool hovAddLine = _addLineRect.Contains(mp);
        LayoutDraw.Rect(_sb, _addLineRect, hovAddLine ? new Color(25, 40, 30) : new Color(16, 28, 20));
        LayoutDraw.BorderRect(_sb, _addLineRect, hovAddLine ? new Color(80, 180, 100) : new Color(50, 100, 60));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "+ Add Line", _addLineRect,
            hovAddLine ? Color.White : new Color(100, 180, 120));

        _nodeStack.Space(8);
        LayoutDraw.DividerLine(_sb, _nodeStack.Divider());

        // ── Choices section ───────────────────────────────────────────────
        LayoutDraw.SectionHeader(_sb, _nodeStack.Next(22), $"Choices ({_selectedNode.Choices.Count}):");
        _nodeStack.Space(4);

        _choiceLabelRects.Clear();
        _choiceEditRects.Clear();
        _choiceDeleteRects.Clear();
        _choiceNavRects.Clear();

        for (int i = 0; i < _selectedNode.Choices.Count; i++)
        {
            var choice = _selectedNode.Choices[i];
            var choiceRow = _nodeStack.Next(60);

            // Background
            LayoutDraw.Rect(_sb, choiceRow, new Color(16, 14, 28));
            LayoutDraw.BorderRect(_sb, choiceRow, LayoutDraw.Border);
            LayoutDraw.Rect(_sb, new Rectangle(choiceRow.X, choiceRow.Y, 3, choiceRow.Height), new Color(120, 80, 200));

            // Label (editable)
            var labelRect = new Rectangle(choiceRow.X + 10, choiceRow.Y + 4, choiceRow.Width / 2 - 20, 26);
            _choiceLabelRects.Add(labelRect);
            bool isEditingLabel = _editingChoiceLabel && _editingChoiceIndex == i;
            string labelDisplay = isEditingLabel ? _editBuffer : choice.Label;
            LayoutDraw.TextArea(_sb, labelRect, labelDisplay, isEditingLabel, mp, "Label...");

            // Edit child node button
            var editRect = new Rectangle(choiceRow.X + choiceRow.Width / 2, choiceRow.Y + 4, 80, 26);
            _choiceEditRects.Add(editRect);
            if (choice.Next != null)
            {
                bool hovEdit = editRect.Contains(mp);
                LayoutDraw.Rect(_sb, editRect, hovEdit ? new Color(30, 30, 55) : new Color(20, 20, 38));
                LayoutDraw.BorderRect(_sb, editRect, hovEdit ? new Color(120, 120, 200) : LayoutDraw.Border);
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Edit ->", editRect,
                    hovEdit ? Color.White : new Color(140, 140, 200));
            }

            // Nav event for this choice
            var navRect = new Rectangle(choiceRow.X + 10, choiceRow.Y + 34, choiceRow.Width / 2 - 20, 22);
            _choiceNavRects.Add(navRect);
            if (!string.IsNullOrEmpty(choice.NavigateTarget))
            {
                string navLabel = choice.NavigateTarget;
                if (_context?.ConnectedRooms != null)
                    foreach (var (id, label) in _context.ConnectedRooms)
                        if (id == choice.NavigateTarget) { navLabel = label; break; }
                LayoutDraw.Rect(_sb, navRect, new Color(20, 40, 30));
                LayoutDraw.BorderRect(_sb, navRect, new Color(50, 120, 70));
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"-> {navLabel}", navRect, new Color(100, 220, 130), 6);
            }
            else
            {
                bool hovNav = navRect.Contains(mp);
                LayoutDraw.Rect(_sb, navRect, hovNav ? new Color(20, 30, 25) : Color.Transparent);
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "+ Navigate...", navRect,
                    hovNav ? new Color(100, 180, 120) : new Color(60, 80, 70), 6);
            }

            // Delete button
            var delRect = new Rectangle(choiceRow.Right - 34, choiceRow.Y + 4, 28, 28);
            _choiceDeleteRects.Add(delRect);
            bool hovDel = delRect.Contains(mp);
            LayoutDraw.Rect(_sb, delRect, hovDel ? new Color(80, 20, 20) : new Color(30, 18, 18));
            LayoutDraw.BorderRect(_sb, delRect, hovDel ? LayoutDraw.Accent : new Color(80, 40, 40));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "X", delRect, hovDel ? Color.White : new Color(180, 80, 80));
        }

        _addChoiceRect = _nodeStack.Next(36);
        bool hovAddChoice = _addChoiceRect.Contains(mp);
        LayoutDraw.Rect(_sb, _addChoiceRect, hovAddChoice ? new Color(30, 20, 45) : new Color(20, 14, 32));
        LayoutDraw.BorderRect(_sb, _addChoiceRect, hovAddChoice ? new Color(160, 100, 220) : new Color(90, 60, 130));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "+ Add Choice", _addChoiceRect,
            hovAddChoice ? Color.White : new Color(160, 120, 210));

        _nodeStack.Space(8);
        LayoutDraw.DividerLine(_sb, _nodeStack.Divider());

        // ── Node-level navigate (for leaf nodes) ──────────────────────────
        if (_selectedNode.Choices.Count == 0)
        {
            LayoutDraw.SectionHeader(_sb, _nodeStack.Next(22), "End Event:");
            _nodeStack.Space(4);

            if (!string.IsNullOrEmpty(_selectedNode.NavigateTarget))
            {
                string navLabel = _selectedNode.NavigateTarget;
                if (_context?.ConnectedRooms != null)
                    foreach (var (id, label) in _context.ConnectedRooms)
                        if (id == _selectedNode.NavigateTarget) { navLabel = label; break; }

                var navInfo = _nodeStack.Next(24);
                LayoutDraw.Rect(_sb, navInfo, new Color(20, 40, 30));
                LayoutDraw.BorderRect(_sb, navInfo, new Color(50, 120, 70));
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, $"Navigate to: {navLabel}", navInfo, new Color(100, 220, 130), 8);

                _nodeStack.Space(4);
                var btnRow = _nodeStack.Next(30);
                int halfW = (btnRow.Width - 8) / 2;
                _nodeNavRect = new Rectangle(btnRow.X, btnRow.Y, halfW, btnRow.Height);
                LayoutDraw.Button(_sb, _nodeNavRect, "Change", mp);
                _nodeNavClearRect = new Rectangle(btnRow.X + halfW + 8, btnRow.Y, halfW, btnRow.Height);
                bool hovClear = _nodeNavClearRect.Contains(mp);
                LayoutDraw.Rect(_sb, _nodeNavClearRect, hovClear ? new Color(60, 20, 20) : new Color(35, 18, 18));
                LayoutDraw.BorderRect(_sb, _nodeNavClearRect, hovClear ? LayoutDraw.Accent : new Color(100, 50, 50));
                LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove", _nodeNavClearRect, hovClear ? Color.White : new Color(200, 120, 120));
            }
            else
            {
                bool hasConns = _context?.ConnectedRooms != null && _context.ConnectedRooms.Count > 0;
                _nodeNavRect = _nodeStack.Next(36);
                if (hasConns)
                {
                    bool hovNav = _nodeNavRect.Contains(mp);
                    LayoutDraw.Rect(_sb, _nodeNavRect, hovNav ? new Color(30, 50, 40) : new Color(18, 32, 25));
                    LayoutDraw.BorderRect(_sb, _nodeNavRect, hovNav ? new Color(80, 180, 100) : new Color(50, 100, 60));
                    LayoutDraw.TextCentre(_sb, Assets.MenuFont, "+ Navigate to Room...", _nodeNavRect,
                        hovNav ? Color.White : new Color(120, 200, 140));
                }
                else
                {
                    LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No connected rooms.", _nodeNavRect, LayoutDraw.DimText);
                }
                _nodeNavClearRect = Rectangle.Empty;
            }
        }
        else
        {
            _nodeNavRect = Rectangle.Empty;
            _nodeNavClearRect = Rectangle.Empty;
        }

        // ── Nav dropdown overlay ──────────────────────────────────────────
        if (_showNavDropdown && _context?.ConnectedRooms != null)
            DrawNavDropdown(mp);

        _sb.End();
    }

    // -----------------------------------------------------------------------
    // Node tree sidebar
    // -----------------------------------------------------------------------
    private void DrawNodeTree(InteractionNode node, int depth, Vector2 mp)
    {
        if (_sideStack.IsFull) return;

        var rect = _sideStack.Next(24);
        bool isSel = node == _selectedNode;
        bool hov = rect.Contains(mp);

        if (isSel) LayoutDraw.Rect(_sb, rect, new Color(35, 18, 45));
        else if (hov) LayoutDraw.Rect(_sb, rect, new Color(24, 20, 36));
        if (isSel) LayoutDraw.Rect(_sb, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Color(120, 80, 200));

        string prefix = new string(' ', depth * 2);
        string label = node == _interaction.Root ? "Root" : node.Id[..Math.Min(10, node.Id.Length)];
        string display = $"{prefix}{(depth > 0 ? "|- " : "")}{label}";
        _sb.DrawString(Assets.MenuFont, display,
            new Vector2(rect.X + 6, rect.Y + 2),
            isSel ? Color.White : new Color(140, 130, 170));

        // Click to select
        if (_prevMouse.LeftButton == ButtonState.Pressed &&
            Mouse.GetState().LeftButton == ButtonState.Released &&
            rect.Contains(mp))
            _selectedNode = node;

        // Recurse into choice children
        foreach (var choice in node.Choices)
            if (choice.Next != null)
                DrawNodeTree(choice.Next, depth + 1, mp);
    }

    private InteractionNode FindParent(InteractionNode root, InteractionNode target)
    {
        foreach (var choice in root.Choices)
        {
            if (choice.Next == target) return root;
            if (choice.Next != null)
            {
                var found = FindParent(choice.Next, target);
                if (found != null) return found;
            }
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Nav dropdown
    // -----------------------------------------------------------------------
    private void DrawNavDropdown(Vector2 mp)
    {
        var connected = _context.ConnectedRooms;
        int entryH = 34;
        int dropW = 250;
        int dropH = connected.Count * (entryH + 2) + 8;
        int dropX = _navSelectRect.X;
        int dropY = _navSelectRect.Bottom + 4;

        var vp = _game.GraphicsDevice.Viewport;
        if (dropY + dropH > vp.Height - 20) dropY = _navSelectRect.Y - dropH - 4;
        if (dropX + dropW > vp.Width - 10) dropX = vp.Width - dropW - 10;

        var dropRect = new Rectangle(dropX, dropY, dropW, dropH);
        LayoutDraw.Rect(_sb, new Rectangle(dropRect.X + 3, dropRect.Y + 3, dropRect.Width, dropRect.Height), new Color(0, 0, 0, 140));
        LayoutDraw.Rect(_sb, dropRect, new Color(18, 16, 30));
        LayoutDraw.BorderRect(_sb, dropRect, LayoutDraw.Accent);

        _navDropdownRects.Clear();
        int y = dropRect.Y + 4;
        for (int i = 0; i < connected.Count; i++)
        {
            var (id, label) = connected[i];
            var entryRect = new Rectangle(dropRect.X + 4, y, dropRect.Width - 8, entryH);
            _navDropdownRects.Add(entryRect);

            bool hov = entryRect.Contains(mp);
            LayoutDraw.Rect(_sb, entryRect, hov ? new Color(30, 28, 48) : new Color(16, 14, 26));
            _sb.DrawString(Assets.MenuFont, label,
                new Vector2(entryRect.X + 10, entryRect.Y + (entryRect.Height - 18) / 2),
                hov ? Color.White : new Color(160, 155, 190));

            y += entryH + 2;
        }
    }

    // -----------------------------------------------------------------------
    // Input helpers
    // -----------------------------------------------------------------------
    private List<Keys> NewKeys(KeyboardState keys)
    {
        var result = new List<Keys>();
        foreach (var key in keys.GetPressedKeys())
        {
            bool was = false;
            foreach (var prev in _prevPressedKeys) if (prev == key) { was = true; break; }
            if (!was) result.Add(key);
        }
        return result;
    }

    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys key)
        => cur.IsKeyDown(key) && prev.IsKeyUp(key);

    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z) { char c = (char)('a' + (key - Keys.A)); return shift ? char.ToUpper(c) : c; }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (!shift) return (char)('0' + (key - Keys.D0));
            return (key - Keys.D0) switch { 1=>'!',2=>'@',3=>'#',4=>'$',5=>'%',6=>'^',7=>'&',8=>'*',9=>'(',0=>')',_=>null };
        }
        return key switch
        {
            Keys.Space => ' ', Keys.OemPeriod => shift ? '>' : '.', Keys.OemComma => shift ? '<' : ',',
            Keys.OemQuestion => shift ? '?' : '/', Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'', Keys.OemMinus => shift ? '_' : '-',
            Keys.OemPlus => shift ? '+' : '=', _ => null
        };
    }
}