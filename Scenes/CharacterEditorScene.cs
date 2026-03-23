using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ZebraBear.Core;
using ZebraBear.UI;

namespace ZebraBear.Scenes;

/// <summary>
/// In-editor character profile manager.
///
/// Portrait selection uses a thumbnail dropdown that scans Content/Characters/
/// for compiled textures — no manual path entry required.
/// </summary>
public class CharacterEditorScene : IScene
{
    // -----------------------------------------------------------------------
    // Sprite entry
    // -----------------------------------------------------------------------
    private class SpriteEntry
    {
        public string    ContentPath; // e.g. "Characters/monobear"
        public string    DisplayName; // e.g. "monobear"
        public Texture2D Texture;
    }

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private readonly Game           _game;
    private readonly SpriteBatch    _sb;
    private readonly ContentManager _content;

    private readonly VStack _listStack = new() { Padding = 10, Spacing = 4 };
    private readonly VStack _propStack = new() { Padding = 14, Spacing = 6 };

    private const int ListW = 220;
    private const int PropX = ListW + 1;

    // Character list
    private readonly List<CharacterProfile> _chars = new();
    private CharacterProfile _selected;

    // Edit fields
    private string _editId    = "";
    private string _editName  = "";
    private string _editTitle = "";
    private string _editBio   = "";

    // Sprite picker
    private List<SpriteEntry> _sprites         = new();
    private bool              _showSpritePick  = false;
    private Rectangle         _spritePickBtn;
    private List<Rectangle>   _spritePickRects = new();
    private int               _spriteScroll    = 0;
    private const int         ThumbSize        = 60;
    private const int         ThumbCols        = 3;
    private const int         ThumbRows        = 3;

    // Active text field
    private enum Field { None, Id, Name, Title, Bio }
    private Field _activeField = Field.None;

    // Cached rects
    private Rectangle       _backRect, _saveAllRect, _addRect;
    private List<Rectangle> _listEntryRects  = new();
    private List<Rectangle> _listDeleteRects = new();
    private Rectangle       _fieldId, _fieldName, _fieldTitle, _fieldBio;
    private Rectangle       _applyRect, _deleteRect;

    // Input
    private MouseState    _prevMouse;
    private KeyboardState _prevKeys;
    private Keys[]        _prevPressedKeys = Array.Empty<Keys>();

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public CharacterEditorScene(Game game, SpriteBatch spriteBatch, ContentManager content)
    {
        _game    = game;
        _sb      = spriteBatch;
        _content = content;
    }

    // -----------------------------------------------------------------------
    // IScene
    // -----------------------------------------------------------------------
    public void Load() { }

    public void OnEnter()
    {
        _game.IsMouseVisible = true;
        ScanSprites();
        LoadFromCharacterData();
        _selected       = _chars.Count > 0 ? _chars[0] : null;
        PopulateFields(_selected);
        _activeField    = Field.None;
        _showSpritePick = false;
    }

    public void OnExit() => _game.IsMouseVisible = false;

    // -----------------------------------------------------------------------
    // Sprite scanning — looks in Content/Characters/ for compiled PNGs
    // -----------------------------------------------------------------------
    private void ScanSprites()
    {
        _sprites.Clear();

        // Compiled content sits in bin/.../Content/ alongside the executable.
        // MonoGame compiles PNGs to .xnb — we scan for those.
        var charsDir = Path.Combine(AppContext.BaseDirectory, "Content", "Characters");

        if (!Directory.Exists(charsDir))
        {
            Console.WriteLine("[CharacterEditor] Content/Characters/ not found.");
            return;
        }

        foreach (var file in Directory.GetFiles(charsDir, "*.xnb"))
        {
            var fileName    = Path.GetFileNameWithoutExtension(file);
            var contentPath = "Characters/" + fileName;

            Texture2D tex = null;
            try   { tex = _content.Load<Texture2D>(contentPath); }
            catch { Console.WriteLine($"[CharacterEditor] Could not load: {contentPath}"); }

            _sprites.Add(new SpriteEntry
            {
                ContentPath = contentPath,
                DisplayName = fileName,
                Texture     = tex
            });
        }

        Console.WriteLine($"[CharacterEditor] Found {_sprites.Count} sprite(s).");
    }

    // -----------------------------------------------------------------------
    // Data helpers
    // -----------------------------------------------------------------------
    private void LoadFromCharacterData()
    {
        _chars.Clear();
        foreach (var c in CharacterData.Characters)
            _chars.Add(c);
    }

    private void PopulateFields(CharacterProfile c)
    {
        if (c == null) { _editId = _editName = _editTitle = _editBio = ""; return; }
        _editId    = c.Id    ?? "";
        _editName  = c.Name  ?? "";
        _editTitle = c.Title ?? "";
        _editBio   = c.Bio   != null ? string.Join("\n", c.Bio) : "";
    }

    private void ApplyFieldsToSelected()
    {
        if (_selected == null) return;
        _selected.Id    = _editId.Trim();
        _selected.Name  = _editName.Trim();
        _selected.Title = _editTitle.Trim();
        _selected.Bio   = _editBio.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private SpriteEntry CurrentSpriteEntry() =>
        _selected == null || string.IsNullOrEmpty(_selected.PortraitPath)
            ? null
            : _sprites.Find(s => s.ContentPath == _selected.PortraitPath);

    // -----------------------------------------------------------------------
    // Save
    // -----------------------------------------------------------------------
    private void SaveAll()
    {
        ApplyFieldsToSelected();

        CharacterData.Characters.Clear();
        foreach (var c in _chars)
        {
            if (!string.IsNullOrWhiteSpace(c.PortraitPath))
                try   { c.Portrait = _content.Load<Texture2D>(c.PortraitPath); }
                catch { c.Portrait = null; }
            else
                c.Portrait = null;

            CharacterData.Characters.Add(c);
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "characters.json");
        var root = new JsonObject();
        var arr  = new JsonArray();

        foreach (var c in _chars)
        {
            var node = new JsonObject();
            node["id"]    = c.Id    ?? "";
            node["name"]  = c.Name  ?? "";
            node["title"] = c.Title ?? "";
            node["met"]   = c.Met;

            var bio = new JsonArray();
            if (c.Bio != null) foreach (var line in c.Bio) bio.Add(line);
            node["bio"] = bio;

            if (!string.IsNullOrWhiteSpace(c.PortraitPath))
                node["portrait"] = c.PortraitPath;

            arr.Add(node);
        }

        root["characters"] = arr;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[CharacterEditor] Saved {_chars.Count} character(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CharacterEditor] Save failed: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------
    public void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys  = Keyboard.GetState();
        var mp    = mouse.Position.ToVector2();

        bool clicked =
            mouse.LeftButton      == ButtonState.Released &&
            _prevMouse.LeftButton == ButtonState.Pressed;

        // Escape
        if (IsPressed(keys, _prevKeys, Keys.Escape))
        {
            if      (_showSpritePick)          _showSpritePick = false;
            else if (_activeField != Field.None) _activeField  = Field.None;
            else { ApplyFieldsToSelected(); NavigationBus.RequestNavigate("LevelEditor"); }
            SaveInput(mouse, keys); return;
        }

        // Sprite picker intercept
        if (_showSpritePick && clicked)
        {
            var pickerArea = GetPickerRect();
            var upBtn      = new Rectangle(pickerArea.Right - 28, pickerArea.Y + 4,      24, 24);
            var downBtn    = new Rectangle(pickerArea.Right - 28, pickerArea.Bottom - 28, 24, 24);
            int totalRows  = (_sprites.Count + ThumbCols - 1) / ThumbCols;
            int maxScroll  = Math.Max(0, totalRows - ThumbRows);
            bool hit       = false;

            if (upBtn.Contains(mp))   { _spriteScroll = Math.Max(0, _spriteScroll - 1); hit = true; }
            if (downBtn.Contains(mp)) { _spriteScroll = Math.Min(maxScroll, _spriteScroll + 1); hit = true; }

            if (!hit)
            {
                for (int i = 0; i < _spritePickRects.Count; i++)
                {
                    if (_spritePickRects[i] != Rectangle.Empty && _spritePickRects[i].Contains(mp))
                    {
                        int idx = _spriteScroll * ThumbCols + i;
                        if (idx < _sprites.Count && _selected != null)
                        {
                            _selected.PortraitPath = _sprites[idx].ContentPath;
                            _selected.Portrait     = _sprites[idx].Texture;
                        }
                        _showSpritePick = false;
                        hit = true;
                        break;
                    }
                }
            }

            if (!hit && !pickerArea.Contains(mp) && !_spritePickBtn.Contains(mp))
                _showSpritePick = false;

            SaveInput(mouse, keys); return;
        }

        // Text editing
        if (_activeField != Field.None)
        {
            HandleTextInput(keys, clicked, mp);
            SaveInput(mouse, keys); return;
        }

        // Clicks
        if (clicked)
        {
            if (_backRect.Contains(mp))
                { ApplyFieldsToSelected(); NavigationBus.RequestNavigate("LevelEditor"); SaveInput(mouse, keys); return; }
            if (_saveAllRect.Contains(mp))
                { SaveAll(); SaveInput(mouse, keys); return; }
            if (_addRect.Contains(mp))
                { AddCharacter(); SaveInput(mouse, keys); return; }

            for (int i = 0; i < _listEntryRects.Count; i++)
            {
                if (i < _listDeleteRects.Count && _listDeleteRects[i].Contains(mp))
                    { DeleteCharacter(i); SaveInput(mouse, keys); return; }
                if (_listEntryRects[i].Contains(mp))
                {
                    ApplyFieldsToSelected();
                    _selected       = _chars[i];
                    PopulateFields(_selected);
                    _activeField    = Field.None;
                    _showSpritePick = false;
                    SaveInput(mouse, keys); return;
                }
            }

            if (_selected != null)
            {
                if      (_fieldId.Contains(mp))      _activeField = Field.Id;
                else if (_fieldName.Contains(mp))     _activeField = Field.Name;
                else if (_fieldTitle.Contains(mp))    _activeField = Field.Title;
                else if (_fieldBio.Contains(mp))      _activeField = Field.Bio;
                else if (_spritePickBtn.Contains(mp))       { _showSpritePick = !_showSpritePick; _spriteScroll = 0; }
                else if (_portraitClearRect.Contains(mp))   { _selected.PortraitPath = ""; _selected.Portrait = null; }
                else if (_applyRect.Contains(mp))           ApplyFieldsToSelected();
                else if (_deleteRect.Contains(mp))          DeleteCharacter(_chars.IndexOf(_selected));
            }
        }

        SaveInput(mouse, keys);
    }

    private void HandleTextInput(KeyboardState keys, bool clicked, Vector2 mp)
    {
        if (IsPressed(keys, _prevKeys, Keys.Escape))
            { _activeField = Field.None; return; }
        if (IsPressed(keys, _prevKeys, Keys.Enter) && _activeField != Field.Bio)
            { _activeField = Field.None; return; }

        if (clicked)
        {
            var r = _activeField switch
            {
                Field.Id    => _fieldId,
                Field.Name  => _fieldName,
                Field.Title => _fieldTitle,
                Field.Bio   => _fieldBio,
                _           => Rectangle.Empty
            };
            if (!r.Contains(mp)) { _activeField = Field.None; return; }
        }

        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        foreach (var key in NewKeys(keys))
        {
            if (key == Keys.Back)
            {
                ref string buf = ref GetFieldRef(_activeField);
                if (buf.Length > 0) buf = buf[..^1];
                continue;
            }
            if (key == Keys.Enter && _activeField == Field.Bio)
                { _editBio += '\n'; continue; }

            char? ch = KeyToChar(key, shift);
            if (ch.HasValue)
            {
                ref string buf = ref GetFieldRef(_activeField);
                buf += ch.Value;
            }
        }
    }

    private ref string GetFieldRef(Field f)
    {
        switch (f)
        {
            case Field.Id:    return ref _editId;
            case Field.Name:  return ref _editName;
            case Field.Title: return ref _editTitle;
            case Field.Bio:   return ref _editBio;
            default:          return ref _editName;
        }
    }

    private void AddCharacter()
    {
        ApplyFieldsToSelected();
        var c = new CharacterProfile
        {
            Id    = $"char_{Guid.NewGuid().ToString()[..6]}",
            Name  = "New Character",
            Title = "",
            Bio   = Array.Empty<string>()
        };
        _chars.Add(c);
        CharacterData.Characters.Add(c);
        _selected    = c;
        PopulateFields(c);
        _activeField = Field.Name;
    }

    private void DeleteCharacter(int index)
    {
        if (index < 0 || index >= _chars.Count) return;
        var c = _chars[index];
        _chars.RemoveAt(index);
        CharacterData.Characters.Remove(c);
        _selected    = _chars.Count > 0 ? _chars[Math.Min(index, _chars.Count - 1)] : null;
        PopulateFields(_selected);
        _activeField = Field.None;
    }

    // -----------------------------------------------------------------------
    // Picker rect
    // -----------------------------------------------------------------------
    private Rectangle GetPickerRect()
    {
        int gap   = 6;
        int w     = ThumbCols * (ThumbSize + gap) - gap + 52;
        int h     = ThumbRows * (ThumbSize + gap) - gap + 44;
        int x     = _spritePickBtn.X;
        int y     = _spritePickBtn.Bottom + 4;
        var vp    = _game.GraphicsDevice.Viewport;
        if (y + h > vp.Height - 20) y = _spritePickBtn.Y - h - 4;
        return new Rectangle(x, y, w, h);
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

        DrawList(vp, mp);
        LayoutDraw.Rect(_sb, new Rectangle(ListW, 0, 1, vp.Height), LayoutDraw.Border);
        DrawProperties(vp, mp);

        if (_showSpritePick) DrawSpritePicker(mp);

        _sb.End();
    }

    // ── Left panel ────────────────────────────────────────────────────────

    private void DrawList(Viewport vp, Vector2 mp)
    {
        LayoutDraw.Rect(_sb, new Rectangle(0, 0, ListW, vp.Height), LayoutDraw.PanelBg);
        _listStack.Begin(0, 0, ListW, vp.Height);

        var titleR = _listStack.Next(36);
        LayoutDraw.AccentBar(_sb, titleR);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont, "CHARACTERS", titleR, LayoutDraw.Accent, 10);

        _listStack.Space(4);
        _addRect = _listStack.Next(34);
        bool hovAdd = _addRect.Contains(mp);
        LayoutDraw.Rect(_sb, _addRect, hovAdd ? new Color(25, 40, 30) : new Color(16, 26, 20));
        LayoutDraw.BorderRect(_sb, _addRect, hovAdd ? new Color(80, 200, 100) : new Color(50, 120, 60));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "+ New Character", _addRect,
            hovAdd ? Color.White : new Color(100, 200, 120));

        _listStack.Space(4);
        LayoutDraw.DividerLine(_sb, _listStack.Divider());

        _listEntryRects.Clear();
        _listDeleteRects.Clear();

        for (int i = 0; i < _chars.Count; i++)
        {
            if (_listStack.IsFull) break;
            var  c     = _chars[i];
            bool isSel = c == _selected;
            var  entry = _listStack.Next(48);
            _listEntryRects.Add(entry);

            LayoutDraw.Rect(_sb, entry,
                isSel ? new Color(30, 20, 48) : entry.Contains(mp) ? new Color(22, 18, 36) : LayoutDraw.PanelBg);
            LayoutDraw.BorderRect(_sb, entry, isSel ? LayoutDraw.Accent : LayoutDraw.Border);
            if (isSel) LayoutDraw.Rect(_sb, new Rectangle(entry.X, entry.Y, 3, entry.Height), LayoutDraw.Accent);

            int   ts    = 36;
            var   tR    = new Rectangle(entry.X + 6, entry.Y + 6, ts, ts);
            if (c.Portrait != null) _sb.Draw(c.Portrait, tR, Color.White);
            else { LayoutDraw.Rect(_sb, tR, new Color(40, 35, 60)); LayoutDraw.TextCentre(_sb, Assets.MenuFont, "?", tR, new Color(80, 75, 110)); }

            _sb.DrawString(Assets.MenuFont, string.IsNullOrEmpty(c.Name) ? "(unnamed)" : c.Name,
                new Vector2(entry.X + ts + 12, entry.Y + 7), isSel ? Color.White : LayoutDraw.TextNormal);
            if (!string.IsNullOrEmpty(c.Title))
                _sb.DrawString(Assets.MenuFont, c.Title, new Vector2(entry.X + ts + 12, entry.Y + 27), new Color(130, 80, 100));

            var del = new Rectangle(entry.Right - 26, entry.Y + 10, 20, 20);
            bool hovD = del.Contains(mp);
            _listDeleteRects.Add(del);
            LayoutDraw.Rect(_sb, del, hovD ? new Color(80, 20, 20) : new Color(40, 18, 18));
            LayoutDraw.BorderRect(_sb, del, hovD ? LayoutDraw.Accent : new Color(80, 40, 40));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "X", del, hovD ? Color.White : new Color(180, 80, 80));
        }

        _saveAllRect = _listStack.NextFromBottom(44);
        _backRect    = _listStack.NextFromBottom(44);
        _listStack.NextFromBottom(8);
        LayoutDraw.Button(_sb, _saveAllRect, "Save to Disk", mp);
        LayoutDraw.Button(_sb, _backRect,    "< Back",       mp);
    }

    // ── Right panel ───────────────────────────────────────────────────────

    private Rectangle _portraitClearRect;

    private void DrawProperties(Viewport vp, Vector2 mp)
    {
        int px = PropX + 20;
        int pw = vp.Width - PropX - 20;
        _propStack.Begin(PropX, 0, vp.Width - PropX, vp.Height);

        var titleR = _propStack.Next(36);
        LayoutDraw.AccentBar(_sb, titleR);
        LayoutDraw.TextLeft(_sb, Assets.MenuFont,
            _selected == null ? "No character selected"
                : string.IsNullOrEmpty(_selected.Name) ? "New Character" : _selected.Name,
            titleR, LayoutDraw.TextBright, 10);

        if (_selected == null)
        {
            _propStack.Space(12);
            LayoutDraw.TextLeft(_sb, Assets.MenuFont, "Select or create a character.",
                _propStack.Next(24), LayoutDraw.DimText);
            _fieldId = _fieldName = _fieldTitle = _fieldBio = _spritePickBtn = _applyRect = _deleteRect = Rectangle.Empty;
            return;
        }

        _propStack.Space(8);

        // Fields
        LayoutDraw.SectionHeader(_sb, _propStack.Next(20), "ID:");
        _fieldId = _propStack.Next(32);
        LayoutDraw.TextArea(_sb, _fieldId, _editId, _activeField == Field.Id, mp, "e.g. kei");

        _propStack.Space(4);
        LayoutDraw.SectionHeader(_sb, _propStack.Next(20), "Display Name:");
        _fieldName = _propStack.Next(32);
        LayoutDraw.TextArea(_sb, _fieldName, _editName, _activeField == Field.Name, mp, "e.g. Kei Nakamura");

        _propStack.Space(4);
        LayoutDraw.SectionHeader(_sb, _propStack.Next(20), "Title / Role:");
        _fieldTitle = _propStack.Next(32);
        LayoutDraw.TextArea(_sb, _fieldTitle, _editTitle, _activeField == Field.Title, mp, "e.g. The Quiet One");

        _propStack.Space(4);
        LayoutDraw.SectionHeader(_sb, _propStack.Next(20), "Bio:");
        _fieldBio = _propStack.Next(80);
        LayoutDraw.TextArea(_sb, _fieldBio, _editBio, _activeField == Field.Bio, mp, "Character description...");

        _propStack.Space(8);
        LayoutDraw.DividerLine(_sb, _propStack.Divider());
        _propStack.Space(6);

        // Portrait section
        LayoutDraw.SectionHeader(_sb, _propStack.Next(20), "Portrait Sprite:");
        _propStack.Space(4);

        var current = CurrentSpriteEntry();

        if (current != null && current.Texture != null)
        {
            // Preview row
            var previewRow = _propStack.Next(64);
            var previewR   = new Rectangle(previewRow.X, previewRow.Y, 60, 60);
            _sb.Draw(current.Texture, previewR, Color.White);
            LayoutDraw.BorderRect(_sb, previewR, LayoutDraw.Border);
            _sb.DrawString(Assets.MenuFont, current.DisplayName,
                new Vector2(previewRow.X + 68, previewRow.Y + 4), Color.White);
            _sb.DrawString(Assets.MenuFont, current.ContentPath,
                new Vector2(previewRow.X + 68, previewRow.Y + 26), LayoutDraw.DimText);

            _propStack.Space(4);

            // Change / Remove buttons
            var btnRow = _propStack.Next(32);
            int half   = (btnRow.Width - 8) / 2;
            _spritePickBtn       = new Rectangle(btnRow.X,          btnRow.Y, half, btnRow.Height);
            _portraitClearRect   = new Rectangle(btnRow.X + half + 8, btnRow.Y, half, btnRow.Height);

            DrawPickerButton(_spritePickBtn, "Change Sprite", mp);

            bool hovClr = _portraitClearRect.Contains(mp);
            LayoutDraw.Rect(_sb, _portraitClearRect, hovClr ? new Color(60, 20, 20) : new Color(35, 18, 18));
            LayoutDraw.BorderRect(_sb, _portraitClearRect, hovClr ? LayoutDraw.Accent : new Color(100, 50, 50));
            LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Remove", _portraitClearRect,
                hovClr ? Color.White : new Color(200, 120, 120));
        }
        else
        {
            if (_sprites.Count == 0)
            {
                LayoutDraw.TextLeft(_sb, Assets.MenuFont, "No sprites found in Content/Characters/.",
                    _propStack.Next(20), LayoutDraw.DimText);
                _spritePickBtn = Rectangle.Empty;
            }
            else
            {
                _spritePickBtn = _propStack.Next(36);
                DrawPickerButton(_spritePickBtn, "Select Sprite...", mp);
            }
            _portraitClearRect = Rectangle.Empty;
        }

        _propStack.Space(10);
        LayoutDraw.DividerLine(_sb, _propStack.Divider());
        _propStack.Space(6);

        // Apply / Delete
        int bw = (pw - 10) / 2;
        _applyRect  = new Rectangle(px, _propStack.CurrentY, bw, 40);
        _deleteRect = new Rectangle(px + bw + 10, _propStack.CurrentY, bw, 40);

        bool hovApply = _applyRect.Contains(mp);
        LayoutDraw.Rect(_sb, _applyRect, hovApply ? new Color(20, 40, 60) : new Color(14, 28, 42));
        LayoutDraw.BorderRect(_sb, _applyRect, hovApply ? new Color(80, 160, 220) : new Color(50, 100, 140));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Apply Changes", _applyRect,
            hovApply ? Color.White : new Color(120, 180, 220));

        bool hovDel = _deleteRect.Contains(mp);
        LayoutDraw.Rect(_sb, _deleteRect, hovDel ? new Color(60, 18, 18) : new Color(36, 14, 14));
        LayoutDraw.BorderRect(_sb, _deleteRect, hovDel ? LayoutDraw.Accent : new Color(100, 40, 40));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, "Delete Character", _deleteRect,
            hovDel ? Color.White : new Color(200, 100, 100));
    }

    private void DrawPickerButton(Rectangle r, string label, Vector2 mp)
    {
        bool hov = r.Contains(mp) || _showSpritePick;
        LayoutDraw.Rect(_sb, r, hov ? new Color(30, 20, 50) : new Color(20, 14, 36));
        LayoutDraw.BorderRect(_sb, r, hov ? new Color(160, 100, 220) : new Color(90, 60, 130));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, label, r, hov ? Color.White : new Color(160, 120, 210));
    }

    // ── Sprite picker dropdown ─────────────────────────────────────────────

    private void DrawSpritePicker(Vector2 mp)
    {
        var rect      = GetPickerRect();
        int gap       = 6;
        int totalRows = (_sprites.Count + ThumbCols - 1) / ThumbCols;
        int maxScroll = Math.Max(0, totalRows - ThumbRows);

        // Shadow + background
        LayoutDraw.Rect(_sb, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width, rect.Height), new Color(0, 0, 0, 140));
        LayoutDraw.Rect(_sb, rect, new Color(14, 12, 24));
        LayoutDraw.BorderRect(_sb, rect, new Color(120, 80, 200));

        int startX = rect.X + 8;
        int startY = rect.Y + 8;

        _spritePickRects.Clear();

        for (int row = 0; row < ThumbRows; row++)
        {
            for (int col = 0; col < ThumbCols; col++)
            {
                int idx = (_spriteScroll + row) * ThumbCols + col;

                if (idx >= _sprites.Count)
                {
                    _spritePickRects.Add(Rectangle.Empty);
                    continue;
                }

                var sprite    = _sprites[idx];
                int x         = startX + col * (ThumbSize + gap);
                int y         = startY + row * (ThumbSize + gap);
                var thumbRect = new Rectangle(x, y, ThumbSize, ThumbSize);
                _spritePickRects.Add(thumbRect);

                bool hov     = thumbRect.Contains(mp);
                bool current = _selected != null && _selected.PortraitPath == sprite.ContentPath;

                LayoutDraw.Rect(_sb, thumbRect,
                    current ? new Color(40, 20, 60) : hov ? new Color(30, 26, 46) : new Color(20, 18, 32));
                LayoutDraw.BorderRect(_sb, thumbRect,
                    current ? new Color(160, 100, 220) : hov ? new Color(120, 90, 180) : new Color(55, 50, 85));

                if (sprite.Texture != null)
                    _sb.Draw(sprite.Texture, new Rectangle(x + 2, y + 2, ThumbSize - 4, ThumbSize - 22), Color.White);
                else
                    LayoutDraw.TextCentre(_sb, Assets.MenuFont, "?", thumbRect, LayoutDraw.DimText);

                // Name at bottom of thumb
                string dn = sprite.DisplayName.Length > 9 ? sprite.DisplayName[..8] + "~" : sprite.DisplayName;
                var    sz = Assets.MenuFont.MeasureString(dn);
                _sb.DrawString(Assets.MenuFont, dn,
                    new Vector2(x + (ThumbSize - sz.X) / 2f, y + ThumbSize - 18),
                    current ? Color.White : new Color(130, 125, 160));
            }
        }

        // Scroll arrows
        var upBtn   = new Rectangle(rect.Right - 28, rect.Y + 8,          24, 24);
        var downBtn = new Rectangle(rect.Right - 28, rect.Bottom - 32,     24, 24);
        DrawScrollArrow(upBtn,   "^", _spriteScroll > 0,           mp);
        DrawScrollArrow(downBtn, "v", _spriteScroll < maxScroll,   mp);

        // Footer
        _sb.DrawString(Assets.MenuFont, $"{_sprites.Count} sprites",
            new Vector2(rect.X + 8, rect.Bottom - 20), LayoutDraw.DimText);
    }

    private void DrawScrollArrow(Rectangle r, string label, bool active, Vector2 mp)
    {
        bool hov = r.Contains(mp) && active;
        LayoutDraw.Rect(_sb, r, hov ? new Color(40, 30, 60) : new Color(22, 18, 36));
        LayoutDraw.BorderRect(_sb, r, active ? new Color(100, 80, 160) : new Color(40, 36, 60));
        LayoutDraw.TextCentre(_sb, Assets.MenuFont, label, r,
            active ? (hov ? Color.White : new Color(160, 140, 200)) : LayoutDraw.DimText);
    }

    // -----------------------------------------------------------------------
    // Input helpers
    // -----------------------------------------------------------------------
    private bool IsPressed(KeyboardState cur, KeyboardState prev, Keys k)
        => cur.IsKeyDown(k) && prev.IsKeyUp(k);

    private Keys[] NewKeys(KeyboardState cur)
    {
        var result  = new List<Keys>();
        var prevSet = new HashSet<Keys>(_prevPressedKeys);
        foreach (var k in cur.GetPressedKeys())
            if (!prevSet.Contains(k)) result.Add(k);
        return result.ToArray();
    }

    private static char? KeyToChar(Keys key, bool shift)
    {
        if (key >= Keys.A && key <= Keys.Z)
            return shift ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (!shift) return (char)('0' + (key - Keys.D0));
            return "!@#$%^&*()"[key - Keys.D0];
        }
        return key switch
        {
            Keys.Space            => ' ',
            Keys.OemPeriod        => shift ? '>' : '.',
            Keys.OemComma         => shift ? '<' : ',',
            Keys.OemMinus         => shift ? '_' : '-',
            Keys.OemPlus          => shift ? '+' : '=',
            Keys.OemQuestion      => shift ? '?' : '/',
            Keys.OemSemicolon     => shift ? ':' : ';',
            Keys.OemQuotes        => shift ? '"' : '\'',
            Keys.OemOpenBrackets  => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']',
            Keys.OemPipe          => shift ? '|' : '\\',
            Keys.OemTilde         => shift ? '~' : '`',
            _                     => null
        };
    }

    private void SaveInput(MouseState mouse, KeyboardState keys)
    {
        _prevMouse       = mouse;
        _prevKeys        = keys;
        _prevPressedKeys = keys.GetPressedKeys();
    }
}