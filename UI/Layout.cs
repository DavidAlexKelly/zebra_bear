// ======== UI/Layout.cs ========
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace ZebraBear.UI;

/// <summary>
/// Vertical stack layout — the primary building block.
/// Call Begin() to set the region, then Next() to allocate rectangles
/// top-to-bottom. No manual Y math needed.
///
/// Usage:
///   var stack = new VStack();
///   stack.Begin(x, y, width, height);
///   DrawLabel(stack.Next(30), "Title");
///   stack.Space(8);
///   DrawButton(stack.Next(44), "Click me");
///   stack.Divider();  // thin line + spacing
///   var remaining = stack.Remaining(); // everything left
/// </summary>
public class VStack
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public int Padding = 0;
    public int Spacing = 4;

    private int _cursor;

    /// <summary>Full bounds of this stack region.</summary>
    public Rectangle Bounds => new Rectangle(X, Y, Width, Height);

    /// <summary>Current Y cursor position.</summary>
    public int CurrentY => _cursor;

    /// <summary>How much vertical space remains.</summary>
    public int RemainingHeight => Math.Max(0, (Y + Height) - _cursor - Padding);

    /// <summary>True if there's no more room for content.</summary>
    public bool IsFull => RemainingHeight <= 0;

    public void Begin(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        _cursor = y + Padding;
    }

    public void Begin(Rectangle bounds)
    {
        Begin(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Allocate the next rectangle of the given height.
    /// Returns a rect inset by Padding on left/right.
    /// </summary>
    public Rectangle Next(int height)
    {
        var rect = new Rectangle(X + Padding, _cursor, Width - Padding * 2, height);
        _cursor += height + Spacing;
        return rect;
    }

    /// <summary>
    /// Allocate the next rectangle with a specific left/right inset
    /// instead of using the stack's Padding.
    /// </summary>
    public Rectangle Next(int height, int inset)
    {
        var rect = new Rectangle(X + inset, _cursor, Width - inset * 2, height);
        _cursor += height + Spacing;
        return rect;
    }

    /// <summary>Add vertical whitespace.</summary>
    public void Space(int pixels)
    {
        _cursor += pixels;
    }

    /// <summary>
    /// Return a 1px-high rect at the current cursor for drawing a divider line,
    /// then advance by spacing.
    /// </summary>
    public Rectangle Divider()
    {
        var rect = new Rectangle(X + Padding, _cursor, Width - Padding * 2, 1);
        _cursor += 1 + Spacing;
        return rect;
    }

    /// <summary>
    /// Return a rectangle covering all remaining space in the stack.
    /// Does not advance the cursor.
    /// </summary>
    public Rectangle Remaining()
    {
        int h = RemainingHeight;
        return new Rectangle(X + Padding, _cursor, Width - Padding * 2, h);
    }

    /// <summary>
    /// Return a rectangle covering all remaining space, then advance
    /// the cursor to the end (consuming it).
    /// </summary>
    public Rectangle ConsumeRemaining()
    {
        var rect = Remaining();
        _cursor = Y + Height - Padding;
        return rect;
    }

    /// <summary>
    /// Allocate from the BOTTOM of the stack, reducing available space.
    /// Useful for pinning buttons to the bottom of a panel.
    /// </summary>
    public Rectangle NextFromBottom(int height)
    {
        Height -= height + Spacing;
        int bottomY = Y + Height + Spacing;
        return new Rectangle(X + Padding, bottomY, Width - Padding * 2, height);
    }
}

/// <summary>
/// Horizontal row layout — splits a rectangle into columns.
///
/// Usage:
///   var row = new HStack();
///   row.Begin(parentRect);
///   var left = row.Next(200);   // fixed 200px
///   row.Space(1);               // 1px divider gap
///   var right = row.Remaining(); // rest of the space
/// </summary>
public class HStack
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public int Padding = 0;
    public int Spacing = 4;

    private int _cursor;

    public Rectangle Bounds => new Rectangle(X, Y, Width, Height);
    public int CurrentX => _cursor;
    public int RemainingWidth => Math.Max(0, (X + Width) - _cursor - Padding);

    public void Begin(int x, int y, int width, int height)
    {
        X = x; Y = y; Width = width; Height = height;
        _cursor = x + Padding;
    }

    public void Begin(Rectangle bounds)
    {
        Begin(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>Allocate a column of the given width.</summary>
    public Rectangle Next(int width)
    {
        var rect = new Rectangle(_cursor, Y + Padding, width, Height - Padding * 2);
        _cursor += width + Spacing;
        return rect;
    }

    /// <summary>Add horizontal whitespace.</summary>
    public void Space(int pixels)
    {
        _cursor += pixels;
    }

    /// <summary>Return a rect covering all remaining horizontal space.</summary>
    public Rectangle Remaining()
    {
        int w = RemainingWidth;
        return new Rectangle(_cursor, Y + Padding, w, Height - Padding * 2);
    }

    /// <summary>Allocate from the RIGHT side, reducing available space.</summary>
    public Rectangle NextFromRight(int width)
    {
        Width -= width + Spacing;
        int rightX = X + Width + Spacing;
        return new Rectangle(rightX, Y + Padding, width, Height - Padding * 2);
    }
}

/// <summary>
/// Shared drawing helpers that work with layout rectangles.
/// Scenes can inherit from this or use it as a static helper.
/// </summary>
public static class LayoutDraw
{
    // -----------------------------------------------------------------------
    // Colours
    // -----------------------------------------------------------------------
    public static readonly Color Accent     = new Color(232, 0, 61);
    public static readonly Color DimText    = new Color(100, 95, 130);
    public static readonly Color PanelBg    = new Color(16, 14, 28);
    public static readonly Color Border     = new Color(55, 50, 85);
    public static readonly Color BgDark     = new Color(10, 10, 18);
    public static readonly Color TextNormal = new Color(180, 170, 210);
    public static readonly Color TextBright = Color.White;
    public static readonly Color HoverBg    = new Color(50, 18, 50);
    public static readonly Color ButtonBg   = new Color(26, 22, 42);

    // -----------------------------------------------------------------------
    // Primitives
    // -----------------------------------------------------------------------
    public static void Rect(SpriteBatch sb, Rectangle r, Color c) =>
        sb.Draw(Assets.Pixel, r, c);

    public static void BorderRect(SpriteBatch sb, Rectangle r, Color c)
    {
        sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(Assets.Pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    public static void AccentBar(SpriteBatch sb, Rectangle r, int width = 4) =>
        sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Y, width, r.Height), Accent);

    // -----------------------------------------------------------------------
    // Text
    // -----------------------------------------------------------------------

    /// <summary>Draw text centred within a rectangle.</summary>
    public static void TextCentre(SpriteBatch sb, SpriteFont font, string text, Rectangle r, Color color)
    {
        var sz = font.MeasureString(text);
        sb.DrawString(font, text,
            new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f),
            color);
    }

    /// <summary>Draw text left-aligned, vertically centred within a rectangle.</summary>
    public static void TextLeft(SpriteBatch sb, SpriteFont font, string text, Rectangle r, Color color, int leftPad = 0)
    {
        var sz = font.MeasureString(text);
        sb.DrawString(font, text,
            new Vector2(r.X + leftPad, r.Y + (r.Height - sz.Y) / 2f),
            color);
    }

    /// <summary>Draw text at the top-left of a rectangle.</summary>
    public static void TextTopLeft(SpriteBatch sb, SpriteFont font, string text, Rectangle r, Color color, int pad = 0)
    {
        sb.DrawString(font, text, new Vector2(r.X + pad, r.Y + pad), color);
    }

    /// <summary>Draw word-wrapped text within a rectangle. Returns the Y after the last line.</summary>
    public static int TextWrapped(SpriteBatch sb, SpriteFont font, string text,
        Rectangle r, Color color, int pad = 0)
    {
        if (string.IsNullOrEmpty(text)) return r.Y + pad;

        int x = r.X + pad;
        int y = r.Y + pad;
        int maxW = r.Width - pad * 2;
        float lineH = font.LineSpacing + 4f;

        var words = text.Split(' ');
        var line = "";
        foreach (var word in words)
        {
            var test = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureString(test).X > maxW && line.Length > 0)
            {
                if (y + lineH > r.Bottom - pad) break;
                sb.DrawString(font, line, new Vector2(x, y), color);
                y += (int)lineH;
                line = word;
            }
            else line = test;
        }
        if (line.Length > 0 && y + lineH <= r.Bottom)
        {
            sb.DrawString(font, line, new Vector2(x, y), color);
            y += (int)lineH;
        }
        return y;
    }

    // -----------------------------------------------------------------------
    // Widgets
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draw a button. Returns true if the mouse is hovering over it.
    /// </summary>
    public static bool Button(SpriteBatch sb, Rectangle r, string label, Vector2 mousePos)
    {
        bool hov = r.Contains(mousePos);
        Rect(sb, r, hov ? HoverBg : ButtonBg);
        BorderRect(sb, r, hov ? Accent : Border);
        TextCentre(sb, Assets.MenuFont, label, r, hov ? TextBright : TextNormal);
        return hov;
    }

    /// <summary>
    /// Draw a selectable list entry. Returns true if hovered.
    /// </summary>
    public static bool Selectable(SpriteBatch sb, Rectangle r, string label,
        bool selected, Vector2 mousePos)
    {
        bool hov = r.Contains(mousePos);
        if (selected)
            Rect(sb, r, new Color(50, 18, 50));
        else if (hov)
            Rect(sb, r, new Color(24, 20, 36));

        if (selected)
            Rect(sb, new Rectangle(r.X, r.Y, 3, r.Height), Accent);

        TextLeft(sb, Assets.MenuFont, label, r,
            selected ? TextBright : hov ? new Color(160, 155, 190) : new Color(130, 125, 160),
            leftPad: 8);

        return hov;
    }

    /// <summary>
    /// Draw a tab button. Returns true if hovered.
    /// </summary>
    public static bool Tab(SpriteBatch sb, Rectangle r, string label,
        bool active, Vector2 mousePos)
    {
        bool hov = r.Contains(mousePos);
        Rect(sb, r, active ? new Color(30, 26, 48) : new Color(16, 14, 26));

        if (active)
        {
            Rect(sb, new Rectangle(r.X, r.Y, r.Width, 2), Accent);
            sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Y, 1, r.Height), Border);
            sb.Draw(Assets.Pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), Border);
        }
        else
        {
            BorderRect(sb, r, hov ? Border : new Color(35, 32, 55));
            sb.Draw(Assets.Pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Border);
        }

        TextCentre(sb, Assets.MenuFont, label, r,
            active ? TextBright : new Color(120, 115, 160));

        return hov;
    }

    /// <summary>
    /// Draw a checkbox toggle. Returns true if hovered.
    /// </summary>
    public static bool Toggle(SpriteBatch sb, Rectangle r, string label,
        bool value, Vector2 mousePos)
    {
        bool hov = r.Contains(mousePos);
        var bg = value ? new Color(20, 60, 30)
               : hov  ? new Color(30, 26, 44)
               :        new Color(20, 18, 32);
        var border = value ? new Color(60, 180, 80)
                   : hov  ? new Color(120, 110, 160)
                   :        Border;

        Rect(sb, r, bg);
        BorderRect(sb, r, border);

        string check = value ? "[X]" : "[ ]";
        TextLeft(sb, Assets.MenuFont, $"{check} {label}", r,
            value ? new Color(100, 220, 120) : TextNormal, leftPad: 10);

        return hov;
    }

    /// <summary>
    /// Draw a text input area. Returns true if hovered.
    /// For short text (single line height), draws left-aligned.
    /// For tall areas, draws with word wrapping.
    /// </summary>
    public static bool TextArea(SpriteBatch sb, Rectangle r, string text,
        bool editing, Vector2 mousePos, string placeholder = "Click to type...")
    {
        bool hov = r.Contains(mousePos);
        var bg = editing ? new Color(28, 26, 44)
               : hov    ? new Color(26, 24, 40)
               :          new Color(22, 20, 36);
        var border = editing ? Accent
                   : hov    ? new Color(120, 110, 160)
                   :          Border;

        Rect(sb, r, bg);
        BorderRect(sb, r, border);

        int pad = 8;
        float lineH = Assets.MenuFont.LineSpacing + 4f;
        bool isSingleLine = r.Height < lineH * 2 + pad * 2;

        if (!string.IsNullOrEmpty(text))
        {
            if (isSingleLine)
            {
                // Single-line mode: left-aligned, vertically centred
                var textColor = new Color(200, 195, 225);
                var sz = Assets.MenuFont.MeasureString(text);

                // Clamp text to fit width
                string display = text;
                float maxW = r.Width - pad * 2;
                if (sz.X > maxW)
                {
                    // Show the end of the string so the cursor stays visible
                    while (display.Length > 0 && Assets.MenuFont.MeasureString(display).X > maxW)
                        display = display[1..];
                }

                sb.DrawString(Assets.MenuFont, display,
                    new Vector2(r.X + pad, r.Y + (r.Height - sz.Y) / 2f),
                    textColor);

                // Blinking cursor
                if (editing)
                {
                    var displaySz = Assets.MenuFont.MeasureString(display);
                    float cursorX = r.X + pad + displaySz.X + 2;
                    float cursorY = r.Y + (r.Height - sz.Y) / 2f;
                    Rect(sb, new Rectangle((int)cursorX, (int)cursorY, 2, (int)sz.Y), Accent);
                }
            }
            else
            {
                // Multi-line mode: word-wrapped
                TextWrapped(sb, Assets.MenuFont, text, r, new Color(200, 195, 225), pad: pad);
            }
        }
        else
        {
            // Placeholder text
            if (isSingleLine)
            {
                var phText = editing ? "" : placeholder;
                var phSz = Assets.MenuFont.MeasureString(phText.Length > 0 ? phText : "X");
                sb.DrawString(Assets.MenuFont, phText,
                    new Vector2(r.X + pad, r.Y + (r.Height - phSz.Y) / 2f),
                    DimText);

                // Blinking cursor when editing empty field
                if (editing)
                {
                    float cursorY = r.Y + (r.Height - phSz.Y) / 2f;
                    Rect(sb, new Rectangle(r.X + pad, (int)cursorY, 2, (int)phSz.Y), Accent);
                }
            }
            else
            {
                TextLeft(sb, Assets.MenuFont, editing ? "" : placeholder, r, DimText, leftPad: pad);
                if (editing)
                {
                    Rect(sb, new Rectangle(r.X + pad, r.Y + pad, 2, (int)lineH), Accent);
                }
            }
        }

        return hov;
    }

    /// <summary>
    /// Draw a horizontal divider line.
    /// </summary>
    public static void DividerLine(SpriteBatch sb, Rectangle r) =>
        Rect(sb, r, Border);

    /// <summary>
    /// Draw a labelled section header.
    /// </summary>
    public static void SectionHeader(SpriteBatch sb, Rectangle r, string label) =>
        TextLeft(sb, Assets.MenuFont, label, r, new Color(80, 75, 110));
}