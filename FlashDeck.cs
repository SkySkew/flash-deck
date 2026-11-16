// Flash Deck - GUI for adb/fastboot. Build with build.bat (C# 5, no install needed).
// Language (English default / Bahasa Melayu) and theme (light / dark) are saved in lang.txt / theme.txt.
// Dev switch: start with --page=N (0 home, 1 install ROM, 2 tools, 3 check, 4 unlock, 5 apps, 6 log) to open a specific page.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic;

// ---------- small UI controls ----------
class RBtn : Button   // rounded button; optional icon glyph, and "tile" mode with a second description line
{
    public Color Fill = App.CardBg, Edge = App.EdgeC, Fore = App.Ink, GlyphColor = Color.Empty, SubColor = App.Muted;
    public float Shift = -0.06f;   // hover: <0 darken, >0 lighten
    public bool LeftText, Tile;
    public string Glyph, Sub;
    bool hover, down;

    public RBtn() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; }   // stays focusable so the keyboard (Tab, Space, Enter) works

    static Color Mix(Color c, float a)
    {
        int t = a < 0 ? 0 : 255; a = Math.Abs(a);
        return Color.FromArgb((int)(c.R + (t - c.R) * a), (int)(c.G + (t - c.G) * a), (int)(c.B + (t - c.B) * a));
    }
    public static GraphicsPath Round(Rectangle r, int d)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color grey = App.Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(229, 231, 235), dis = Color.FromArgb(156, 163, 175);
        Color f = !Enabled ? grey : down ? Mix(Fill, Shift * 2) : hover ? Mix(Fill, Shift) : Fill;
        using (var p = Round(new Rectangle(0, 0, Width - 1, Height - 1), Tile ? 20 : 12))
        using (var b = new SolidBrush(f))
        using (var pen = new Pen(Enabled ? Edge : grey))
        {
            g.FillPath(b, p); g.DrawPath(pen, p);
            if (Focused && ShowFocusCues) using (var fp = new Pen(App.Accent, 2f)) g.DrawPath(fp, p);   // keyboard focus ring
        }

        bool ic = Glyph != null && App.IconFont != null;
        int x = 16;
        var mid = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        if (ic)
        {
            Color gc = !Enabled ? dis : GlyphColor.IsEmpty ? Fore : GlyphColor;
            TextRenderer.DrawText(g, Glyph, App.IconFont, new Rectangle(x - 4, 0, 28, Height), gc, mid);
            x += 30;
        }
        var left = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        if (Tile)
        {
            using (var bf = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(g, Text, bf, new Rectangle(x, 12, Width - x - 8, 22), Enabled ? Fore : dis, left | TextFormatFlags.Top);
            using (var sf = new Font(Font.FontFamily, 8.5f))
                TextRenderer.DrawText(g, Sub ?? "", sf, new Rectangle(x, 34, Width - x - 8, Height - 36), Enabled ? SubColor : dis, left | TextFormatFlags.Top);
        }
        else if (LeftText || ic)
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x - 8, Height), Enabled ? Fore : dis, left | TextFormatFlags.VerticalCenter);
        else
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? Fore : dis, mid | TextFormatFlags.EndEllipsis);
    }
}

class Card : FlowLayoutPanel   // bordered card, vertical layout; Active = highlighted current step
{
    public bool Active;
    public Card()
    {
        FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = App.CardBg; ForeColor = App.Ink; Padding = new Padding(16, 12, 16, 12); Margin = new Padding(0, 0, 0, 12); MinimumSize = new Size(690, 0);
        DoubleBuffered = true;
    }
    // rounded corners: the corners are painted in the page colour, then the rounded card on top (children sit inside the padding, clear of the corners)
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent != null ? Parent.BackColor : App.PageBg); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int bw = Active ? 2 : 1;
        using (var p = RBtn.Round(new Rectangle(bw / 2, bw / 2, Width - 1 - bw, Height - 1 - bw), 24))
        {
            using (var b = new SolidBrush(App.CardBg)) g.FillPath(b, p);
            using (var pen = new Pen(Active ? App.Accent : App.Border, bw)) g.DrawPath(pen, p);
        }
    }
}

class Box : Panel   // bordered panel (status card), rounded like Card
{
    static readonly Color Tint = App.Dark ? App.CardBg : Color.FromArgb(247, 247, 244);   // light theme: soft tint, no border
    public Box() { BackColor = Tint; ForeColor = App.Ink; }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent != null ? Parent.BackColor : App.PageBg); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), 24))
        {
            using (var b = new SolidBrush(Tint)) g.FillPath(b, p);
            if (App.Dark) using (var pen = new Pen(App.Border)) g.DrawPath(pen, p);
        }
    }
}

class Badge : Control   // numbered circle for steps
{
    public Badge(int n) { Text = n.ToString(); Width = 26; Height = 26; Margin = new Padding(0, 0, 8, 0); Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(App.Accent)) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

class Logo : Control   // app logo in the sidebar
{
    public Logo() { Width = 40; Height = 40; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), 10))   // flat blue square with a white bolt (same drawing as app.ico)
        using (var br = new SolidBrush(Color.FromArgb(37, 99, 235)))
            g.FillPath(br, p);
        float k = Width / 112f;
        var bolt = new[] { new PointF(62 * k, 18 * k), new PointF(36 * k, 62 * k), new PointF(55 * k, 62 * k), new PointF(49 * k, 94 * k), new PointF(78 * k, 48 * k), new PointF(58 * k, 48 * k) };
        using (var wb = new SolidBrush(Color.White)) using (var wp = new Pen(Color.White, 1.4f) { LineJoin = LineJoin.Round }) { g.FillPolygon(wb, bolt); g.DrawPolygon(wp, bolt); }
    }
}

// Brand profile: real fastboot commands differ per manufacturer. Alt = fallback command if the first one fails.
class Profile
{
    public string Name, Match, Unlock, Alt, Part, NoteEn, NoteMs; public bool Code;
    public Profile(string name, string match, string unlock, string alt, string part, bool code, string noteEn, string noteMs)
    { Name = name; Match = match; Unlock = unlock; Alt = alt; Part = part; Code = code; NoteEn = noteEn; NoteMs = noteMs; }
    public string Note { get { return App.T(NoteEn, NoteMs); } }
    public string Lock { get { return Unlock == null ? null : Unlock.Replace("unlock", "lock"); } }
    public string LockAlt { get { return Alt == null ? null : Alt.Replace("unlock", "lock"); } }
}

class BrandChip : Control   // quiet brand line in the sidebar: icon, brand, how it was found, and a Change link (click opens the menu)
{
    public string Caption = "";
    public BrandChip() { Height = 52; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), 20))
        using (var b = new SolidBrush(App.Dark ? Color.FromArgb(22, 30, 46) : Color.FromArgb(247, 247, 244))) g.FillPath(b, p);
        var left = TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        if (App.IconFont != null)
            TextRenderer.DrawText(g, "", App.IconFont, new Rectangle(8, 0, 26, Height), App.SideMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using (var bf = new Font("Segoe UI", 9.5f, FontStyle.Bold)) TextRenderer.DrawText(g, Text, bf, new Rectangle(38, 8, Width - 38 - 40, 18), App.SideText, left);
        using (var sf = new Font("Segoe UI", 8f)) TextRenderer.DrawText(g, Caption, sf, new Rectangle(38, 28, Width - 38 - 38, 16), App.SideMuted, left);
        // a small icon button on the right (the whole chip is clickable; the chevron says "a menu opens")
        var btn = new Rectangle(Width - 32, (Height - 24) / 2, 24, 24);
        using (var bp = RBtn.Round(btn, 9)) { using (var bb = new SolidBrush(App.CardBg)) g.FillPath(bb, bp); using (var bpen = new Pen(App.Dark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225))) g.DrawPath(bpen, bp); }
        if (App.IconFont != null) TextRenderer.DrawText(g, "", App.IconFont, btn, App.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

class MenuColors : ProfessionalColorTable   // colours for the themed pop-up menu
{
    static Color Sel { get { return App.Dark ? Color.FromArgb(40, 60, 100) : Color.FromArgb(219, 234, 254); } }
    public override Color ToolStripDropDownBackground { get { return App.CardBg; } }
    public override Color MenuBorder { get { return App.Border; } }
    public override Color MenuItemBorder { get { return Sel; } }
    public override Color MenuItemSelected { get { return Sel; } }
    public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
    public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
    public override Color ImageMarginGradientBegin { get { return App.CardBg; } }
    public override Color ImageMarginGradientMiddle { get { return App.CardBg; } }
    public override Color ImageMarginGradientEnd { get { return App.CardBg; } }
}

class Seg : Control   // segmented tabs (the list type on the Apps page)
{
    string[] items = new string[0]; int sel;
    public event EventHandler Changed;
    public int Count { get { return items.Length; } }
    public int SelectedIndex { get { return sel; } set { if (value == sel) return; sel = value; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); } }
    public Seg() { Height = 36; Font = new Font("Segoe UI", 9.5f); Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    int W(int i) { return TextRenderer.MeasureText(items[i], Font).Width + 26; }
    public void SetItems(params string[] it) { items = it; int w = 6; for (int i = 0; i < it.Length; i++) w += W(i); Width = w; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), 16)) using (var b = new SolidBrush(App.Dark ? Color.FromArgb(22, 30, 46) : Color.FromArgb(244, 244, 241))) g.FillPath(b, p);
        int x = 3;
        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rectangle(x, 3, W(i), Height - 7);
            if (i == sel)
            {
                using (var p = RBtn.Round(r, 12)) { using (var b = new SolidBrush(App.CardBg)) g.FillPath(b, p); using (var pen = new Pen(App.Border)) g.DrawPath(pen, p); }
            }
            TextRenderer.DrawText(g, items[i], Font, r, i == sel ? App.Ink : App.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += r.Width;
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int x = 3;
        for (int i = 0; i < items.Length; i++) { if (e.X >= x && e.X < x + W(i)) { SelectedIndex = i; return; } x += W(i); }
    }
}

class AppRow { public string Pkg, Title, Sub, Tag, Head; public Color Dot; public bool Checked, Locked; }

class AppList : Control   // the apps as plain rows under coloured group headers, with its own scrolling
{
    public List<AppRow> Rows = new List<AppRow>(); public string Empty = ""; public string Selected;
    public event EventHandler Changed;
    const int RowH = 46, HeadH = 32;
    int top, hover = -1; readonly VScrollBar vs = new VScrollBar();
    public AppList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9.5f);
        vs.Visible = false; vs.Scroll += (s, e) => { top = vs.Value; Invalidate(); }; Controls.Add(vs);
    }
    static int H(AppRow r) { return r.Head != null ? HeadH : RowH; }
    int Total() { int t = 0; foreach (var r in Rows) t += H(r); return t; }
    public int AppCount { get { int n = 0; foreach (var r in Rows) if (r.Head == null) n++; return n; } }
    public List<string> CheckedPkgs() { var l = new List<string>(); foreach (var r in Rows) if (r.Head == null && r.Checked) l.Add(r.Pkg); return l; }
    public void ClearChecks() { foreach (var r in Rows) r.Checked = false; }
    public void SetRows(List<AppRow> rows) { Rows = rows; top = 0; hover = -1; Selected = null; Relayout(); Invalidate(); }
    void Relayout()
    {
        int total = Total(); bool need = Height > 40 && total > Height - 2;   // (a new control starts at 0 x 0)
        vs.Visible = need; vs.SetBounds(Width - 19, 6, 17, Math.Max(10, Height - 12));
        if (need) { vs.Minimum = 0; vs.LargeChange = Math.Max(1, Height - 2); vs.SmallChange = RowH; vs.Maximum = total - 1; top = Math.Max(0, Math.Min(top, total - Height + 2)); vs.Value = top; } else top = 0;
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
    int IndexAt(int y)
    {
        int yy = -top;
        for (int i = 0; i < Rows.Count; i++) { int h = H(Rows[i]); if (y >= yy && y < yy + h) return i; yy += h; }
        return -1;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!vs.Visible) return;
        top = Math.Max(0, Math.Min(top - e.Delta / 120 * RowH * 2, Total() - Height + 2)); vs.Value = top; Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.Y); if (i >= 0 && (Rows[i].Head != null)) i = -1;
        if (i != hover) { hover = i; Cursor = i >= 0 && !Rows[i].Locked ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = IndexAt(e.Y); if (i < 0 || Rows[i].Head != null) return;
        Selected = Rows[i].Pkg; if (!Rows[i].Locked) Rows[i].Checked = !Rows[i].Checked;
        Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : App.PageBg); g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = Width - 1;
        using (var path = RBtn.Round(new Rectangle(0, 0, w, Height - 1), 24))
        {
            using (var b = new SolidBrush(App.CardBg)) g.FillPath(b, path);
            g.SetClip(path);
            if (Rows.Count == 0)
                TextRenderer.DrawText(g, Empty, Font, new Rectangle(20, 0, w - 40, Height), App.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            int y = -top; int rw = vs.Visible ? w - 22 : w;
            using (var bold = new Font("Segoe UI", 8.5f, FontStyle.Bold)) using (var small = new Font("Segoe UI", 8f))
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i]; int h = H(r);
                if (y + h > 0 && y < Height)
                {
                    if (r.Head != null)
                    {
                        using (var b = new SolidBrush(App.Dark ? Color.FromArgb(22, 30, 46) : Color.FromArgb(247, 247, 244))) g.FillRectangle(b, 0, y, w, h);
                        using (var b = new SolidBrush(r.Dot)) g.FillEllipse(b, 18, y + h / 2 - 4, 8, 8);
                        TextRenderer.DrawText(g, r.Head, bold, new Rectangle(34, y, rw - 40, h), App.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                    else
                    {
                        if (i == hover || r.Pkg == Selected) using (var b = new SolidBrush(Color.FromArgb(i == hover ? 16 : 26, 37, 99, 235))) g.FillRectangle(b, 0, y, w, h);
                        using (var pen = new Pen(App.Border)) g.DrawLine(pen, 0, y, w, y);
                        var cb = new Rectangle(18, y + (h - 16) / 2, 16, 16);
                        using (var p = RBtn.Round(cb, 8))
                        {
                            if (r.Checked) using (var b = new SolidBrush(App.Accent)) g.FillPath(b, p);
                            else using (var pen = new Pen(r.Locked ? App.Border : App.EdgeC, 1.4f)) g.DrawPath(pen, p);
                        }
                        if (r.Checked) using (var pen = new Pen(Color.White, 2f)) g.DrawLines(pen, new[] { new Point(cb.X + 4, cb.Y + 8), new Point(cb.X + 7, cb.Y + 11), new Point(cb.X + 12, cb.Y + 5) });
                        var fore = r.Locked ? App.Muted : App.Ink;
                        TextRenderer.DrawText(g, r.Title, Font, new Rectangle(46, y + 6, rw - 46 - (r.Tag != null ? 120 : 10), 20), fore, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                        TextRenderer.DrawText(g, r.Sub, small, new Rectangle(46, y + 25, rw - 46 - (r.Tag != null ? 120 : 10), 16), App.Muted, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                        if (r.Tag != null) TextRenderer.DrawText(g, r.Tag, small, new Rectangle(rw - 112, y, 104, h), r.Locked ? App.DangerC : Color.DarkOrange, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                }
                y += h;
            }
            g.ResetClip();
            using (var pen = new Pen(App.Border)) g.DrawPath(pen, path);
        }
    }
}

class IconCircle : Control   // round tinted badge with an icon (the phone state in the status card)
{
    public IconCircle() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); ForeColor = Color.Gray; Text = ""; }
    protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Color.FromArgb(App.Dark ? 70 : 40, ForeColor))) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
        if (App.IconFont != null)
            using (var f = new Font(App.IconFont.FontFamily, 17f))
                TextRenderer.DrawText(g, Text, f, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

class Pill : Control   // small rounded status chip ("Ready", "Fastboot"...)
{
    public Color Tint = Color.Gray;
    public Pill() { Height = 26; Font = new Font("Segoe UI", 9f); }
    public void Set(string text, Color tint) { Text = text; Tint = tint; Width = TextRenderer.MeasureText(text, Font).Width + 26; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), Height))
        using (var b = new SolidBrush(Color.FromArgb(App.Dark ? 70 : 40, Tint))) g.FillPath(b, p);
        Color fg = App.Dark ? Color.FromArgb(Math.Min(255, Tint.R + 90), Math.Min(255, Tint.G + 90), Math.Min(255, Tint.B + 90)) : Tint;   // lighter text on dark surfaces
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

// Shown instead of the main window while Google's platform-tools (adb + fastboot) are missing
class SetupForm : Form
{
    public SetupForm()
    {
        Text = App.Cap; ClientSize = new Size(640, 410); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; BackColor = App.PageBg; ForeColor = App.Ink; Font = new Font("Segoe UI", 9.5f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
        string root = Path.GetDirectoryName(Application.ExecutablePath);

        var card = new Card { Left = 20, Top = 20, MinimumSize = new Size(600, 0) };
        card.Controls.Add(new Label { Text = App.T("Welcome to Flash Deck", "Selamat datang ke Flash Deck"), AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) });
        Action<string> line = s => card.Controls.Add(new Label { Text = s, AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = App.Muted, UseMnemonic = false, Margin = new Padding(0, 4, 0, 2) });
        line(App.T("Flash Deck uses Google's platform-tools (adb and fastboot). They are free but not included, so one quick setup step is needed:",
                   "Flash Deck menggunakan platform-tools Google (adb dan fastboot). Ia percuma tetapi tak disertakan, jadi satu langkah persediaan diperlukan:"));
        line("1.  " + App.T("Download platform-tools for Windows (button below) and extract the zip.", "Muat turun platform-tools untuk Windows (butang di bawah) dan extract zip itu."));
        line("2.  " + App.T("Put the 'platform-tools' folder next to Flash Deck.exe, so that this file exists:", "Letak folder 'platform-tools' di sebelah Flash Deck.exe, supaya fail ini wujud:"));
        card.Controls.Add(new Label { Text = Path.Combine(root, "platform-tools", "adb.exe"), AutoSize = true, MaximumSize = new Size(520, 0), Font = new Font("Consolas", 9f), UseMnemonic = false, Margin = new Padding(22, 2, 0, 2) });
        line("3.  " + App.T("Press 'Check again'.", "Tekan 'Semak semula'."));
        Controls.Add(card);

        Func<string, string, int, bool, RBtn> mk = (text, glyph, w, primary) =>
        {
            var b = new RBtn { Text = text, Glyph = glyph, Width = w, Height = 40, Font = Font, GlyphColor = App.Accent };
            if (primary) { b.Fill = App.Accent; b.Edge = App.Accent; b.Fore = Color.White; b.GlyphColor = Color.White; }
            Controls.Add(b); return b;
        };
        var dl = mk(App.T("Download platform-tools", "Muat turun platform-tools"), "", 220, true); dl.Left = 20; dl.Top = 340;
        dl.Click += (s, e) => { try { Process.Start("https://developer.android.com/tools/releases/platform-tools"); } catch (Exception) { } };
        var op = mk(App.T("Open this folder", "Buka folder ini"), "", 150, false); op.Left = 250; op.Top = 340;
        op.Click += (s, e) => { try { Process.Start("explorer.exe", root); } catch (Exception) { } };
        var ck = mk(App.T("Check again", "Semak semula"), "", 150, false); ck.Left = 410; ck.Top = 340; ck.DialogResult = DialogResult.Retry;
        AcceptButton = ck;
        var ex = mk(App.T("Exit", "Keluar"), null, 50, false); ex.Left = 570; ex.Top = 340; ex.DialogResult = DialogResult.Cancel; CancelButton = ex;
    }
}

// ROM option for the guide: which zip to look for in the Rom folder, and what to remember about it
class RomOpt
{
    public string Name, Pattern, NoteEn, NoteMs;
    public RomOpt(string name, string pattern, string noteEn, string noteMs) { Name = name; Pattern = pattern; NoteEn = noteEn; NoteMs = noteMs; }
    public string Note { get { return App.T(NoteEn, NoteMs); } }
}

class App : Form
{
    static readonly string Root = Path.GetDirectoryName(Application.ExecutablePath);
    static readonly string LangFile = Path.Combine(Root, "lang.txt");
    static readonly string ThemeFile = Path.Combine(Root, "theme.txt");
    static readonly string Lang = LoadLang();   // "en" (default) or "ms"
    static string LoadLang() { try { return File.Exists(LangFile) && File.ReadAllText(LangFile).Trim() == "ms" ? "ms" : "en"; } catch (Exception) { return "en"; } }
    public static string T(string en, string ms) { return Lang == "ms" ? ms : en; }

    public static readonly bool Dark = LoadDark();
    static bool LoadDark() { try { return File.Exists(ThemeFile) && File.ReadAllText(ThemeFile).Trim() == "dark"; } catch (Exception) { return false; } }

    public static readonly Color
        PageBg = Dark ? Color.FromArgb(24, 31, 46) : Color.White,
        SideBg = Dark ? Color.FromArgb(10, 15, 28) : Color.White,
        CardBg = Dark ? Color.FromArgb(30, 41, 59) : Color.White,
        Border = Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240),
        EdgeC = Dark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225),
        Ink = Dark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42),
        Muted = Dark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139),
        Accent = Color.FromArgb(37, 99, 235), DangerC = Color.FromArgb(220, 38, 38),
        SideTitle = Dark ? Color.White : Color.FromArgb(15, 23, 42),          // sidebar + log colours follow the theme
        SideText = Dark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(51, 65, 85),
        SideMuted = Dark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139),
        SideEdge = Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240),
        LogBg = Dark ? Color.FromArgb(10, 15, 28) : Color.FromArgb(248, 250, 252),
        LogFg = Dark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
    public static readonly float SideShift = Dark ? 0.1f : -0.06f;   // sidebar hover: lighten on dark, darken on light

    // Icon font: Segoe Fluent Icons (Windows 11) or Segoe MDL2 Assets (Windows 10). Null = no icons drawn.
    public static readonly Font IconFont = MakeIconFont();
    static Font MakeIconFont()
    {
        foreach (var n in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            foreach (var f in FontFamily.Families)
                if (f.Name == n) return new Font(f, 12f);
        return null;
    }

    static readonly Profile[] Profiles = {
        new Profile("Xiaomi / Redmi / POCO", "xiaomi", "oem unlock", null, "recovery", false,
            "Needs a Mi account + Mi Unlock Tool (sometimes a 7-30 day wait). Fastboot: hold Volume Down + Power.",
            "Perlu akaun Mi + Mi Unlock Tool (kadang-kadang tunggu 7-30 hari). Fastboot: tahan Volume Down + Power."),
        new Profile("Google Pixel", "google", "flashing unlock", "oem unlock", "boot", false,
            "Enable 'OEM unlocking' in Developer options. Fastboot: hold Volume Down + Power. Factory images use flash-all.",
            "Hidupkan 'OEM unlocking' dalam Developer options. Fastboot: tahan Volume Down + Power. Factory image guna flash-all."),
        new Profile("Motorola", "motorola", "oem unlock", null, "recovery", true,
            "Needs an unlock code from the Motorola website. Enable 'OEM unlocking' first.",
            "Perlu kod unlock dari laman Motorola. Hidupkan 'OEM unlocking' dahulu."),
        new Profile("OnePlus / OPPO / Realme", "oneplus|oppo|realme", "flashing unlock", "oem unlock", "recovery", false,
            "Enable 'OEM unlocking'. OPPO/Realme may need the 'Deep Testing' app first.",
            "Hidupkan 'OEM unlocking'. OPPO/Realme mungkin perlu app 'Deep Testing' dahulu."),
        new Profile("Sony", "sony", "oem unlock", null, "boot", true,
            "Needs an unlock code from the Sony website. Fastboot: hold Volume Up while connecting USB.",
            "Perlu kod unlock dari laman Sony. Fastboot: tahan Volume Up sambil sambung USB."),
        new Profile("Nothing", "nothing", "flashing unlock", "oem unlock", "boot", false,
            "Enable 'OEM unlocking' in Developer options.",
            "Hidupkan 'OEM unlocking' dalam Developer options."),
        new Profile(T("Samsung (not supported)", "Samsung (tak disokong)"), "samsung", null, null, "recovery", false,
            "Samsung uses Odin and Download mode, not fastboot. This tool can only reboot and sideload.",
            "Samsung guna Odin dan mod Download, bukan fastboot. Alat ini hanya boleh reboot dan sideload."),
        new Profile(T("Other", "Lain-lain"), "", "flashing unlock", "oem unlock", "recovery", false,
            "Generic commands. Check the manufacturer's guide for how to unlock and the recovery partition name.",
            "Cuba arahan umum. Semak panduan pengilang untuk cara unlock dan nama partition recovery."),
    };

    // ROMs listed for the Mi 10T (apollon) on thecustomrom.com, synced 15 Nov 2026. File-name patterns are loose guesses - the app falls back to a file picker.
    static readonly RomOpt[] Roms = {
        new RomOpt("LineageOS", "lineage-*.zip",
            "Official build for this phone. Needs the Android 12 stock firmware first. GApps are optional (MindTheGapps).",
            "Binaan rasmi untuk telefon ini. Perlu firmware stok Android 12 dahulu. GApps pilihan (MindTheGapps)."),
        new RomOpt("RisingOS", "*rising*.zip",
            "Listed with Google apps built in (check the build you download). Follow the RisingOS install page: it may need extra images flashed first.",
            "Disenaraikan dengan app Google terbina dalam (semak binaan yang dimuat turun). Ikut halaman pemasangan RisingOS: mungkin perlu imej tambahan di-flash dahulu."),
        new RomOpt("AlphaDroid", "*alpha*.zip",
            "Listed with Google apps built in (Android 16, older build). Follow the AlphaDroid install page: it may need extra images flashed first.",
            "Disenaraikan dengan app Google terbina dalam (Android 16, binaan lebih lama). Ikut halaman pemasangan AlphaDroid: mungkin perlu imej tambahan di-flash dahulu."),
        new RomOpt(T("Other ROM", "ROM lain"), "*.zip",
            "Uses any zip in the Rom folder (GApps zips are skipped). Follow that ROM's own install guide.",
            "Guna mana-mana zip dalam folder Rom (zip GApps dilangkau). Ikut panduan pemasangan ROM itu sendiri."),
    };
    static readonly string RomFile = Path.Combine(Root, "rom.txt");   // remembers the chosen ROM

    static int ProfileIndex(string mfr)
    {
        mfr = (mfr ?? "").ToLower();
        for (int i = 0; i < Profiles.Length; i++)
            foreach (var m in Profiles[i].Match.Split('|'))
                if (m != "" && mfr.Contains(m)) return i;
        return Profiles.Length - 1;
    }
    static readonly string LastFile = Path.Combine(Root, "last-device.txt");   // line 1 = manufacturer, line 2 = codename
    Profile Prof() { return Profiles[brandIdx]; }
    static string ShortName(Profile p) { return p.Name.Split(new[] { " /", " (" }, StringSplitOptions.None)[0]; }   // "Xiaomi / Redmi / POCO" -> "Xiaomi"

    // the brand is normally detected from the phone (src = auto); "last" = remembered from the last phone, "manual" = chosen by the user
    void SetBrand(int i, string src)
    {
        bool changed = i != brandIdx; brandIdx = i; brandSrc = src;
        brandChip.Text = ShortName(Profiles[i]);
        brandChip.Caption = src == "auto" ? T("auto-detected", "dikesan auto") : src == "last" ? T("last phone", "telefon terakhir") : T("your choice", "pilihan awak");
        brandChip.Invalidate();
        if (changed && unlockPage != null) BuildUnlock();
    }

    void ShowBrandMenu()
    {
        var m = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false, Font = Font, BackColor = CardBg, ForeColor = Ink, Padding = new Padding(4), Renderer = new ToolStripProfessionalRenderer(new MenuColors()) };
        for (int i = 0; i < Profiles.Length; i++)
        {
            int k = i;
            var it = new ToolStripMenuItem((k == brandIdx ? "✓   " : "      ") + Profiles[k].Name) { ForeColor = k == brandIdx ? Accent : Ink, Padding = new Padding(2, 4, 2, 4) };
            it.Click += (s, e) => SetBrand(k, "manual");
            m.Items.Add(it);
        }
        m.Opened += (s, e) => { using (var p = RBtn.Round(new Rectangle(0, 0, m.Width, m.Height), 16)) m.Region = new Region(p); };   // soft corners
        m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
        m.Show(brandChip.PointToScreen(new Point(0, -m.GetPreferredSize(Size.Empty).Height - 6)));   // opens above the chip
    }

    static readonly string Pt = Path.Combine(Root, "platform-tools");
    static readonly string RomDir = Path.Combine(Root, "Rom");
    static readonly string Adb = Path.Combine(Pt, "adb.exe");
    static readonly string Fb = Path.Combine(Pt, "fastboot.exe");
    public const string Cap = "Flash Deck", Version = "1.2.0";
    static Size WorkArea;   // usable screen size; --wa=WxH overrides it for testing small screens
    const int PgHome = 0, PgGuide = 1, PgTools = 2, PgCheck = 3, PgUnlock = 4, PgApps = 5, PgLog = 6;
    static int StartPage = 0;
    static bool DemoApps, AutoLoad, GuideOpen;   // dev switches: --demo-apps (sample list), --autoload (load the real list once the phone is found)
    static int AutoFilter = -1;       // --filter=N picks the list type (0 user, 1 system, 2 all, 3 disabled)

    Label status, info, hint, hintHome, hRec, hBoot, hRom, hGapps;
    IconCircle statusIcon; Pill statusChip;
    Dictionary<string, string> lastProps = new Dictionary<string, string>();   // properties read from the phone (system mode)
    string PropOf(string k) { string v; return lastProps.TryGetValue(k, out v) ? v : ""; }
    TextBox log;
    ProgressBar prog;
    FlowLayoutPanel wizPage, toolPage, checkPage, checkList, unlockPage, appsPage;   // content flows
    Panel wizScroll, toolScroll, checkScroll, unlockScroll, appsScroll, logPage;     // scrolling containers around them
    RBtn navWiz, navTool, navCheck, navUnlock, navApps, navLog, navHome;
    FlowLayoutPanel homePage; Panel homeScroll; Label homeLast;   // Home page: where to start
    AppList appList; Seg appFilter; TextBox appSearch; Label appCount; RBtn btnDisable, appMore;   // Apps page (remove bloatware)
    bool appShowBg, appLoaded, appAutoTried; string appHiddenNote = "", appNote = "";   // appNote: result of the last Disable/Enable, shown until you tick something   // appShowBg: also list background parts (hidden by default)
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr h, int bar, bool show);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
    List<string> appAll = new List<string>();
    CheckBox follow;
    int brandIdx; string brandSrc = "last";   // index into Profiles, and where it came from (auto, last, manual)
    BrandChip brandChip;
    Label romNote;
    Seg romSel;   // index into Roms
    readonly List<Card> wizCards = new List<Card>();
    readonly List<Button> btnList = new List<Button>();
    volatile bool busy, polling;
    volatile string state = "none";
    string codename = "", lastSt = "none", phoneName = "";   // phoneName e.g. "Xiaomi M2007J3SY (apollo)"
    int curPage, curStep;

    string WipeWord { get { return T("WIPE", "PADAM"); } }

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        WorkArea = Screen.PrimaryScreen.WorkingArea.Size;
        foreach (var a in args)
        {
            if (a == "--demo-apps") DemoApps = true;
            else if (a == "--autoload") AutoLoad = true;
            else if (a == "--guide-open") GuideOpen = true;
            else if (a.StartsWith("--filter=")) int.TryParse(a.Substring(9), out AutoFilter);
            else if (a.StartsWith("--page=")) int.TryParse(a.Substring(7), out StartPage);
            else if (a.StartsWith("--wa="))
            {
                var wh = a.Substring(5).Split('x'); int w, h;
                if (wh.Length == 2 && int.TryParse(wh[0], out w) && int.TryParse(wh[1], out h)) WorkArea = new Size(w, h);
            }
        }
        if (Array.IndexOf(args, "--selftest") >= 0) { SelfTest(); return; }
        // first run: until adb and fastboot are next to the app, show the setup window (Check again = Retry)
        while (!File.Exists(Adb) || !File.Exists(Fb))
            if (new SetupForm().ShowDialog() != DialogResult.Retry) return;
        if (Array.IndexOf(args, "--phonetest") >= 0) { new App().PhoneSelfTest(); return; }
        Application.Run(new App());
    }

    // Flash Deck.exe --selftest writes selftest.txt: quick checks of the pure logic (ROM detection, brand matching, lock commands)
    static void SelfTest()
    {
        var r = new StringBuilder();
        Action<string, bool> chk = (n, ok) => r.AppendLine((ok ? "PASS " : "FAIL ") + n);
        chk("lineage flavor", CustomRomName("lineage_apollon-user") == "LineageOS");
        chk("rising flavor", CustomRomName("rising_apollon-userdebug") == "Rising");
        chk("stock flavor is not a custom ROM", CustomRomName("apollo-user") == "");
        chk("null flavor", CustomRomName(null) == "");
        chk("brand short names", ShortName(Profiles[0]) == "Xiaomi" && ShortName(Profiles[1]) == "Google Pixel" && ShortName(Profiles[6]) == "Samsung");
        chk("brand xiaomi", ProfileIndex("Xiaomi") == 0);
        chk("brand google", ProfileIndex("Google") == 1);
        chk("brand unknown falls back to Other", ProfileIndex("Acme") == Profiles.Length - 1);
        chk("samsung has no unlock command", Profiles[ProfileIndex("samsung")].Unlock == null);
        chk("lock commands derive from unlock", Profiles[0].Lock == "oem lock" && Profiles[1].LockAlt == "oem lock");
        chk("settings app is protected", IsProtected("com.android.settings"));
        chk("providers prefix is protected", IsProtected("com.android.providers.contacts"));
        chk("a normal app is not protected", !IsProtected("org.fdroid.fdroid"));
        chk("package name accepted", PkgName.IsMatch("org.fdroid.fdroid"));
        chk("package name rejects extra command", !PkgName.IsMatch("a.b; reboot") && !PkgName.IsMatch("a b") && !PkgName.IsMatch(""));
        HomePkgs = new HashSet<string> { "com.vendor.launcher" };
        chk("the home screen app is protected on any brand", IsProtected("com.vendor.launcher") && AppKind("com.vendor.launcher") == "core");
        HomePkgs = new HashSet<string>();
        UserPkgs = new HashSet<string> { "com.example.mine" }; LauncherPkgs = new HashSet<string> { "com.example.sysicon" };
        UserPkgs.Add("com.google.android.safetycore"); UserPkgs.Add("com.brave.browser");
        chk("advice: app you installed", AppKind("com.example.mine") == "yours" && AppKind("com.brave.browser") == "yours");
        chk("advice: a Google part installed with GApps is not 'yours'", AppKind("com.google.android.safetycore") == "gapps");
        chk("advice: unlisted system app with an icon", AppKind("com.example.sysicon") == "sysicon");
        chk("advice: unlisted background part", AppKind("com.example.bg") == "sysbg");
        chk("advice: a hint wins over 'yours'", AppKind("com.android.vending") == "careful");
        LauncherPkgs = new HashSet<string>(); UserPkgs = new HashSet<string>();
        chk("advice: no extra information -> unknown", AppKind("com.example.bg") == "unknown");
        chk("advice: ads app is safe", AppKind("com.miui.analytics") == "safe");
        chk("advice: protected beats any hint", AppKind("com.android.settings") == "core");
        chk("advice: unlisted app is unknown", AppKind("com.example.whatever") == "unknown");
        chk("advice: careful app", AppKind("com.android.vending") == "careful");
        chk("apps grouped: safe first, core last", GroupOf("safe") == 0 && GroupOf("gapps") == GroupOf("careful") && GroupOf("core") == GroupTitles.Length - 1 && GroupOf("sysicon") == GroupOf("unknown"));
        chk("row text: hinted app has a description, others a label", AppDesc("com.miui.analytics", "safe") != "" && AppDesc("com.example.whatever", "unknown") == "" && KindLabel("core") != "");
        File.WriteAllText(Path.Combine(Root, "selftest.txt"), r.ToString());
    }

    App()
    {
        Text = Cap;
        Width = Math.Min(1000, WorkArea.Width - 20); Height = Math.Min(900, WorkArea.Height - 20);   // fit small screens (e.g. 1366x768 laptops)
        MinimumSize = new Size(Math.Min(880, Width), Math.Min(700, Height));   // below this the sidebar buttons would overlap
        Font = new Font("Segoe UI", 9.5f); StartPosition = FormStartPosition.CenterScreen; BackColor = PageBg; ForeColor = Ink;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        // --- sidebar ---
        var side = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = SideBg };
        side.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = EdgeC });   // thin divider
        side.Controls.Add(new Logo { Left = 16, Top = 18 });
        side.Controls.Add(new Label { Text = Cap, Left = 64, Top = 18, AutoSize = true, ForeColor = SideTitle, Font = new Font("Segoe UI", 12.5f, FontStyle.Bold) });
        side.Controls.Add(new Label { Text = "adb · fastboot · v" + Version, Left = 66, Top = 42, AutoSize = true, ForeColor = SideMuted, Font = new Font("Segoe UI", 8.5f) });
        navWiz = NavBtn(T("ROM Guide", "Panduan ROM"), "", 100, PgGuide);
        navTool = NavBtn(T("Tools", "Alat"), "", 148, PgTools);
        navCheck = NavBtn(T("Pre-flight check", "Semakan pra-flash"), "", 196, PgCheck);
        navUnlock = NavBtn(T("Unlock guide", "Panduan unlock"), "", 244, PgUnlock);
        navApps = NavBtn(T("Apps (beta)", "Aplikasi (beta)"), "", 292, PgApps);
        navLog = NavBtn("Log", "", 340, PgLog);
        side.Controls.AddRange(new Control[] { navWiz, navTool, navCheck, navUnlock, navApps, navLog });
        // sidebar grouped as Home / Set up / Use, with Log at the bottom (names and positions are set here so the icon lines above stay untouched)
        navHome = NavBtn(T("Home", "Utama"), "", 96, PgHome);
        navWiz.Text = T("Install ROM", "Pasang ROM"); navCheck.Text = T("Check phone", "Semak telefon");
        navUnlock.Top = 170; navCheck.Top = 214; navWiz.Top = 258; navApps.Top = 330; navTool.Top = 374;
        side.Controls.Add(navHome);
        side.Controls.Add(new Label { Text = T("Set up", "Persediaan"), Left = 20, Top = 148, AutoSize = true, ForeColor = SideMuted, Font = new Font("Segoe UI", 8.5f) });
        side.Controls.Add(new Label { Text = T("Use", "Guna"), Left = 20, Top = 308, AutoSize = true, ForeColor = SideMuted, Font = new Font("Segoe UI", 8.5f) });
        side.Resize += (s, e) => navLog.Top = side.Height - 256;
        follow = new CheckBox { Text = T("Auto-follow phone", "Ikut telefon auto"), Checked = true, AutoSize = true, Left = 16, ForeColor = SideText, BackColor = SideBg };
        var themeBtn = new RBtn { Text = Dark ? T("Light mode", "Mod cerah") : T("Dark mode", "Mod gelap"), Glyph = Dark ? "" : "", Left = 12, Width = 176, Height = 40, LeftText = true, Shift = SideShift, Fill = SideBg, Edge = SideEdge, Fore = SideText, GlyphColor = SideMuted };
        themeBtn.Click += (s, e) => SwitchSetting(ThemeFile, Dark ? "light" : "dark");
        var langBtn = new RBtn { Text = T("Bahasa Melayu", "English"), Glyph = "", Left = 12, Width = 176, Height = 40, LeftText = true, Shift = SideShift, Fill = SideBg, Edge = SideEdge, Fore = SideText, GlyphColor = SideMuted };
        langBtn.Click += (s, e) => SwitchSetting(LangFile, Lang == "ms" ? "en" : "ms");
        side.Controls.AddRange(new Control[] { follow, themeBtn, langBtn });
        side.Resize += (s, e) => { langBtn.Top = side.Height - 56; themeBtn.Top = side.Height - 102; follow.Top = side.Height - 138; };

        // --- content ---
        var content = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(16) };

        // compact phone card: icon badge, "<phone> connected", one line of facts, and a state chip on the right
        var statusCard = new Box { Dock = DockStyle.Top, Height = 84 };
        statusIcon = new IconCircle { Left = 18, Top = 18, Width = 48, Height = 48 };
        status = new Label { Left = 80, Top = 17, AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Ink };
        info = new Label { Left = 82, Top = 47, AutoSize = true, ForeColor = Muted };
        hint = new Label { AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(3, 0, 0, 8) };       // "Next: ..." on the Install ROM page
        hintHome = new Label { AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Margin = new Padding(3, 0, 0, 8) };   // the same line on Home
        statusChip = new Pill { Top = 29 };
        statusCard.Controls.AddRange(new Control[] { statusIcon, status, info, statusChip });
        statusCard.Resize += (s, e) => statusChip.Left = statusCard.Width - statusChip.Width - 20;
        statusChip.SizeChanged += (s, e) => statusChip.Left = statusCard.Width - statusChip.Width - 20;
        int start = 0;
        try
        {
            if (File.Exists(LastFile))   // remember last phone (fastboot gives no brand info)
            {
                var lines = File.ReadAllLines(LastFile);
                if (lines.Length > 0) start = ProfileIndex(lines[0]);
                if (lines.Length > 1) codename = lines[1].Trim();
                if (lines.Length > 2) phoneName = lines[2].Trim();
            }
        }
        catch (Exception) { }
        // the brand is detected automatically, so it is a quiet line with a Change link instead of a dropdown
        brandChip = new BrandChip { Left = 12, Width = 176 };
        brandChip.Click += (s, e) => ShowBrandMenu();
        new ToolTip().SetToolTip(brandChip, T("Change the phone brand", "Tukar jenama telefon"));
        side.Controls.Add(brandChip);
        side.Resize += (s, e) => brandChip.Top = side.Height - 196;
        SetBrand(start, "last");

        wizPage = NewPage(out wizScroll); toolPage = NewPage(out toolScroll); checkPage = NewPage(out checkScroll); unlockPage = NewPage(out unlockScroll); appsPage = NewPage(out appsScroll); homePage = NewPage(out homeScroll);
        var pages = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
        pages.Controls.Add(wizScroll); pages.Controls.Add(toolScroll); pages.Controls.Add(checkScroll); pages.Controls.Add(unlockScroll); pages.Controls.Add(appsScroll); pages.Controls.Add(homeScroll);

        logPage = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Visible = false };
        var logBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, BackColor = PageBg };
        logBar.Controls.Add(new Label { Text = "Log", AutoSize = true, ForeColor = Muted, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 9, 12, 0) });
        logBar.Controls.Add(Small(T("Save log", "Simpan log"), "", SaveLog));
        logBar.Controls.Add(Small(T("Clear", "Kosongkan"), "", () => log.Clear()));
        log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = LogBg, ForeColor = LogFg, Font = new Font("Consolas", 9f) };
        prog = new ProgressBar { Dock = DockStyle.Top, Height = 4, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = false };
        logPage.Controls.Add(log); logPage.Controls.Add(logBar);
        pages.Controls.Add(logPage);

        // add order: the last one added sits at the outer edge (status card, progress bar, gap, pages)
        content.Controls.Add(pages);
        content.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = PageBg });
        content.Controls.Add(prog);
        content.Controls.Add(statusCard);
        Controls.Add(content); Controls.Add(side);

        // --- LineageOS guide (the card for the current phone state is highlighted) ---
        wizPage.Controls.Add(hint);
        var a = NewCard(wizPage, T("Enter Fastboot and recovery", "Masuk Fastboot dan recovery"), 1);
        Row(a, T("Reboot to Bootloader", "Reboot ke Bootloader"), "", () => Reboot("bootloader"), false);
        hRec = Row(a, "Flash recovery.img", "", FlashRecovery, true);
        hBoot = Row(a, T("Boot recovery temporarily", "Boot recovery sementara"), "", BootRecovery, false);
        Row(a, T("Flash extra images (optional)", "Flash imej tambahan (pilihan)"), "", FlashMany, false);
        Note(a, T("Files are looked up in the 'Rom' folder. Use 'Boot temporarily' if Volume Up + Power does not enter recovery.",
                  "Fail dicari dalam folder 'Rom'. Guna 'Boot sementara' kalau Volume Up + Power tak masuk recovery."));
        var b = NewCard(wizPage, T("On the phone (in recovery)", "Pada telefon (dalam recovery)"), 2);
        // the one step done on the phone itself: two short lines in full-strength text so it is not skipped
        foreach (var ln in new[] { T("1.  Factory Reset > Format data", "1.  Factory Reset > Format data"), T("2.  Apply update > Apply from ADB", "2.  Apply update > Apply from ADB") })
            b.Controls.Add(new Label { Text = ln, AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", 10.5f), UseMnemonic = false, Margin = new Padding(0, 2, 0, 2) });
        var c = NewCard(wizPage, T("Install ROM, then GApps", "Pasang ROM, kemudian GApps"), 3);
        var selRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        selRow.Controls.Add(new Label { Text = "ROM:", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 6, 8, 0) });
        romSel = new Seg();
        var romNames = new List<string>(); foreach (var ro in Roms) romNames.Add(ro.Name);
        romSel.SetItems(romNames.ToArray());
        int rs = 0;
        try { if (File.Exists(RomFile)) int.TryParse(File.ReadAllText(RomFile).Trim(), out rs); } catch (Exception) { }
        romSel.SelectedIndex = rs >= 0 && rs < Roms.Length ? rs : 0;
        selRow.Controls.Add(romSel); c.Controls.Add(selRow);
        romNote = new Label { AutoSize = true, MaximumSize = new Size(700, 0), ForeColor = Muted, UseMnemonic = false, Margin = new Padding(0, 0, 0, 6), Text = Roms[romSel.SelectedIndex].Note };
        c.Controls.Add(romNote);
        romSel.Changed += (s, e) =>
        {
            romNote.Text = Roms[romSel.SelectedIndex].Note;
            try { File.WriteAllText(RomFile, romSel.SelectedIndex.ToString()); } catch (Exception) { }
            RefreshHints();
        };
        hRom = Row(c, T("Install ROM zip", "Pasang zip ROM"), "", () => SideloadAuto(null, true), true);
        hGapps = Row(c, T("Install GApps (optional)", "Pasang GApps (pilihan)"), "", () => SideloadAuto("MindTheGapps*.zip"), false);
        Note(c, T("For GApps, choose Apply from ADB again before pressing the button.", "Untuk GApps, pilih Apply from ADB sekali lagi sebelum menekan butang."));
        var d = NewCard(wizPage, T("Done", "Siap"), 4);
        Row(d, T("Reboot to System", "Reboot ke Sistem"), "", () => Reboot(""), false);
        wizCards.AddRange(new[] { a, b, c, d });

        // --- Tools ---
        var r1 = ToolCard(toolPage, T("Modes & reboot", "Mod & reboot"));
        Tile(r1, T("Reboot to Bootloader", "Reboot ke Bootloader"), T("Enter fastboot mode", "Masuk mod fastboot"), "", () => Reboot("bootloader"), false);
        Tile(r1, T("Reboot to Recovery", "Reboot ke Recovery"), T("Open the recovery menu", "Buka menu recovery"), "", () => Reboot("recovery"), false);
        Tile(r1, T("Reboot to System", "Reboot ke Sistem"), T("Start Android normally", "Mulakan Android"), "", () => Reboot(""), false);
        var r2 = ToolCard(toolPage, T("Files", "Fail"));
        Tile(r2, T("Flash .img file", "Flash fail .img"), T("Write an image to a partition", "Tulis imej ke partition"), "", FlashImg, false);
        Tile(r2, T("Sideload .zip file", "Sideload fail .zip"), T("Install a ROM or add-on", "Pasang ROM atau add-on"), "", () => SideloadAuto(null), false);
        Tile(r2, T("Check file MD5", "Semak MD5 fail"), T("Verify a downloaded file", "Semak fail yang dimuat turun"), "", Md5, false);
        Tile(r2, T("Flash several images", "Flash beberapa imej"), T("In order, stops on error", "Ikut urutan, henti ralat"), "", FlashMany, false);
        foreach (Control rc in r1.Controls) rc.Height = 54;   // rarely used: lower tiles, so the everyday ones below stand out
        var r6 = ToolCard(toolPage, T("Phone tools (phone on, USB debugging)", "Alat telefon (telefon hidup, USB debugging)"));
        Tile(r6, T("Install APK", "Pasang APK"), T("Install an app from the PC", "Pasang app dari PC"), "", InstallApk, false);
        Tile(r6, T("Screenshot", "Tangkapan skrin"), T("Saved in Pictures", "Disimpan dalam Pictures"), "", Screenshot, false);
        Tile(r6, T("Copy file to phone", "Salin fail ke telefon"), T("Goes to Download folder", "Masuk folder Download"), "", PushFile, false);
        Tile(r6, T("Copy file from phone", "Salin fail dari telefon"), T("Enter the path on the phone", "Masukkan laluan pada telefon"), "", PullFile, false);
        Tile(r6, T("Battery info", "Maklumat bateri"), T("Level, status, temperature", "Tahap, status, suhu"), "", Battery, false);
        Tile(r6, T("Backup files", "Backup fail"), T("DCIM, Pictures, Download", "DCIM, Pictures, Download"), "", Backup, false);
        var r7 = ToolCard(toolPage, T("Downloads (opens your browser)", "Muat turun (buka pelayar)"));
        Tile(r7, "LineageOS", T("Official builds", "Binaan rasmi"), "", LineageDownloads, false);
        Tile(r7, "GApps", T("MindTheGapps list", "Senarai MindTheGapps"), "", () => OpenUrl("https://wiki.lineageos.org/gapps/"), false);
        Tile(r7, T("Stock ROM page", "Halaman ROM asal"), T("Xiaomi / Pixel", "Xiaomi / Pixel"), "", StockPage, false);
        Tile(r7, T("Find ROMs", "Cari ROM"), T("Web search for your phone", "Carian web untuk telefon awak"), "", FindRoms, false);
        foreach (Control rc in r7.Controls) rc.Height = 54;
        var r3 = ToolCard(toolPage, T("Stock Xiaomi ROM (extracted fastboot ROM)", "ROM asal Xiaomi (fastboot ROM yang sudah di-extract)"));
        Tile(r3, T("Install - keep data", "Pasang - simpan data"), T("Restore stock, keep files", "Pulih ROM asal, simpan fail"), "", () => FastbootRom("flash_all_except_storage.bat", null), false);
        Tile(r3, T("Install - wipe all", "Pasang - padam semua"), T("Restore stock, erase all", "Pulih ROM asal, padam semua"), "", () => FastbootRom("flash_all.bat", WipeWord), true);
        Tile(r3, T("Format data (wipe)", "Format data (wipe)"), T("Erase all user data", "Padam semua data pengguna"), "", Wipe, true);
        var r4 = ToolCard(toolPage, "Bootloader");
        Tile(r4, T("Unlock bootloader", "Unlock bootloader"), T("For custom ROMs", "Untuk ROM custom"), "", Unlock, true);
        Tile(r4, T("LOCK bootloader", "LOCK bootloader"), T("Only after the stock ROM!", "Hanya selepas ROM asal!"), "", Lock, true);
        var r5 = ToolCard(toolPage, T("Other", "Lain-lain"));
        Tile(r5, T("Device info", "Info peranti"), T("Model, Android, bootloader", "Model, Android, bootloader"), "", DeviceInfo, false);
        Tile(r5, T("Open Rom folder", "Buka folder Rom"), T("Where ROM files are kept", "Tempat fail ROM disimpan"), "", () => { Directory.CreateDirectory(RomDir); Process.Start("explorer.exe", RomDir); }, false);
        Tile(r5, T("Tool versions", "Versi alat"), T("adb and fastboot", "adb dan fastboot"), "", ToolVersions, false);
        Tile(r5, T("Open logs folder", "Buka folder log"), T("Saved automatically", "Disimpan secara automatik"), "", () => { Directory.CreateDirectory(LogDir); Process.Start("explorer.exe", LogDir); }, false);
        Tile(r5, T("Get platform-tools", "Dapatkan platform-tools"), T("Official download page", "Halaman muat turun rasmi"), "", () => OpenUrl("https://developer.android.com/tools/releases/platform-tools"), false);

        // --- Pre-flight check page ---
        var ck = NewCard(checkPage, T("Pre-flight check", "Semakan pra-flash"), 0);
        Note(ck, T("Checks the phone, the files in the Rom folder and the ROM codename before you flash anything.", "Semak telefon, fail dalam folder Rom dan codename ROM sebelum awak flash apa-apa."));
        ck.Controls.Add(Btn(T("Run checks", "Jalankan semakan"), "", PreFlight, 1, 200));
        checkList = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 10, 0, 0) };
        ck.Controls.Add(checkList);
        var tr = NewCard(checkPage, T("Phone not detected?", "Telefon tak dikesan?"), 0);
        Note(tr, T("1. Use a data-capable USB cable, preferably in a USB 2.0 port.", "1. Guna kabel USB yang boleh pindah data, lebih baik di port USB 2.0."));
        Note(tr, T("2. On the phone: Developer options > USB debugging, then tap Allow on the prompt.", "2. Pada telefon: Developer options > USB debugging, kemudian tekan Allow pada prompt."));
        Note(tr, T("3. Set the USB mode to File transfer.", "3. Tetapkan mod USB kepada File transfer."));
        Note(tr, T("4. Install the USB driver (Xiaomi: Mi USB driver; others: Google USB driver).", "4. Pasang pemacu USB (Xiaomi: Mi USB driver; lain-lain: Google USB driver)."));
        var trRow = ToolRow(tr); trRow.Margin = new Padding(0, 8, 0, 0);
        Tile(trRow, T("Restart ADB server", "Mulakan semula ADB"), T("Fixes stuck detection", "Betulkan pengesanan tersekat"), "", RestartAdb, false);
        Tile(trRow, T("Open Device Manager", "Buka Device Manager"), T("Look for a warning icon", "Cari ikon amaran"), "", () => { try { Process.Start("devmgmt.msc"); } catch (Exception) { } }, false);

        // --- Apps page: list the phone's apps and remove the ones you do not want ---
        var ap = NewCard(appsPage, T("Remove apps (bloatware) - beta", "Buang aplikasi (bloatware) - beta"), 0);
        Note(ap, T("Hide the apps you do not use. Nothing is deleted, and you can undo it.", "Sembunyikan aplikasi yang tak digunakan. Tiada apa dipadam, dan boleh dibatalkan."));
        // the how-to is collapsed behind a button so the page starts compact
        var guide = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Visible = GuideOpen, Margin = new Padding(0, 0, 0, 4) };
        var guideBtn = new RBtn { Text = T("How to use", "Cara guna"), Glyph = GuideOpen ? "" : "", Width = 160, Height = 32, Font = Font, GlyphColor = Accent, Margin = new Padding(0, 6, 0, 2) };
        guideBtn.Click += (s, e) => { guide.Visible = !guide.Visible; guideBtn.Glyph = guide.Visible ? "" : ""; guideBtn.Invalidate(); };
        ap.Controls.Add(guideBtn); ap.Controls.Add(guide);
        var steps = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(655, 0), Margin = new Padding(0, 6, 0, 2) };
        Action<int, string> stepOf = (n, txt) =>
        {
            var bd = new Badge(n); bd.Margin = new Padding(0, 0, 6, 0); steps.Controls.Add(bd);
            steps.Controls.Add(new Label { Text = txt, AutoSize = true, ForeColor = Ink, UseMnemonic = false, Margin = new Padding(0, 4, 22, 0) });
        };
        stepOf(1, T("Wait for the list", "Tunggu senarai"));
        stepOf(2, T("Tick the ones you do not need", "Tandakan yang tak perlu"));
        stepOf(3, T("Press Disable", "Tekan Lumpuhkan"));
        guide.Controls.Add(steps);
        Note(guide, T("Changed your mind? Open the Disabled tab, tick the app and press Enable. 'Remove' (under More) uninstalls instead: it works for apps you installed yourself, but system apps usually need root. Not sure what an app is? Click it, then More > Search online. Change one or two at a time, restart the phone, and check that everything still works. The group headings are general advice, not a guarantee.",
                   "Berubah fikiran? Buka tab Dilumpuhkan, tandakan aplikasi dan tekan Hidupkan. 'Buang' (di bawah Lagi) menyahpasang: ia berfungsi untuk aplikasi yang awak pasang sendiri, tetapi aplikasi sistem biasanya perlu root. Tak pasti apa aplikasi itu? Klik, kemudian Lagi > Cari maklumat. Ubah satu atau dua sahaja, mulakan semula telefon, dan semak semuanya masih berfungsi. Tajuk kumpulan ialah nasihat umum, bukan jaminan."));
        var apBar = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 6) };
        appFilter = new Seg { Margin = new Padding(0, 2, 12, 0) };
        appFilter.SetItems(T("Your apps", "Aplikasi awak"), T("System", "Sistem"), T("All", "Semua"), T("Disabled", "Dilumpuhkan"));
        if (AutoFilter >= 0 && AutoFilter < appFilter.Count) appFilter.SelectedIndex = AutoFilter;
        appFilter.Changed += (s, e) => { if (state == "device") LoadApps(); else FillApps(); };   // a different list type means a different list from the phone
        appSearch = new TextBox { Width = 140, Margin = new Padding(0, 7, 10, 0) };
        if (Dark) { appSearch.BackColor = CardBg; appSearch.ForeColor = Ink; }
        appSearch.HandleCreated += (s, e) => SendMessage(appSearch.Handle, 0x1501, (IntPtr)1, T("Search apps", "Cari aplikasi"));   // placeholder text
        appSearch.TextChanged += (s, e) => FillApps();
        apBar.Controls.Add(appFilter);
        apBar.Controls.Add(appSearch);
        apBar.Controls.Add(Btn(T("Reload", "Muat semula"), "", LoadApps, 0, 132));
        ap.Controls.Add(apBar);
        appList = new AppList { Width = 690, Height = 320, Margin = new Padding(0, 4, 0, 10), Empty = T("Connect the phone and its apps load here.\nPress Reload if nothing shows up.", "Sambung telefon dan aplikasinya dimuat di sini.\nTekan Muat semula kalau tiada apa dipaparkan.") };
        appList.Changed += (s, e) => { appNote = ""; UpdateAppBar(); };
        appsScroll.ClientSizeChanged += (s, e) => appList.Height = Math.Max(160, Math.Min(440, appsScroll.ClientSize.Height - 340));   // short windows: a shorter list, so the buttons below stay in view
        ap.Controls.Add(appList);
        var apRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        appCount = new Label { AutoSize = true, MaximumSize = new Size(700, 0), ForeColor = Muted, UseMnemonic = false, Margin = new Padding(3, 0, 0, 8) };
        ap.Controls.Add(appCount);   // the count line sits above the buttons, so the buttons never wrap on a narrow window
        apRow.Controls.Add(Btn(T("Enable", "Hidupkan"), "", () => DisableApps(true), 0, 110));
        btnDisable = Btn(T("Disable", "Lumpuhkan"), "", () => DisableApps(false), 1, 170);
        apRow.Controls.Add(btnDisable);
        appMore = Btn(T("More", "Lagi"), "", () => ShowAppsMenu(), 0, 90);
        apRow.Controls.Add(appMore);
        ap.Controls.Add(apRow);
        UpdateAppBar();

        if (DemoApps)   // --demo-apps: fill the list with sample packages so the layout can be checked without a phone
        {
            appAll = new List<string> { "android.autoinstalls.config.Xiaomi.qssi", "com.android.settings", "com.android.vending", "com.facebook.appmanager", "com.google.android.apps.youtube.music", "com.google.android.youtube", "com.miui.analytics", "com.miui.msa.global", "com.example.something.unknown", "org.lineageos.recorder", "com.brave.browser", "com.android.dialer", "com.google.ar.core" };
            UserPkgs = new HashSet<string> { "com.brave.browser" }; LauncherPkgs = new HashSet<string> { "com.android.dialer" };
            FillApps();
        }
        // --- Home: where to start ---
        homePage.Controls.Add(hintHome);
        var hc = NewCard(homePage, T("What do you want to do?", "Apa yang awak mahu buat?"), 0);
        Note(hc, T("Pick one. Flash Deck checks the phone before it changes anything.", "Pilih satu. Flash Deck menyemak telefon sebelum mengubah apa-apa."));
        var grid = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(660, 0), Margin = new Padding(0, 8, 0, 0), Tag = 2 };
        Action<string, string, string, int> pick = (title, sub, glyph, page) =>
        {
            var hb = new RBtn { Text = title, Sub = sub, Glyph = glyph, Tile = true, Width = 322, Height = 84, Font = Font, GlyphColor = Accent };
            hb.Click += (s, e) => Nav(page);
            grid.Controls.Add(hb);
        };
        pick(T("Install a ROM", "Pasang ROM"), T("Step by step, with checks", "Langkah demi langkah, dengan semakan"), "", PgGuide);
        pick(T("Hide apps you do not use", "Sembunyi aplikasi tak digunakan"), T("Reversible, no root", "Boleh dibalikkan, tanpa root"), "", PgApps);
        pick(T("Unlock the bootloader", "Unlock bootloader"), T("A guide for your brand", "Panduan untuk jenama awak"), "", PgUnlock);
        pick(T("Phone tools", "Alat telefon"), T("Screenshot, copy files, battery", "Tangkapan skrin, salin fail, bateri"), "", PgTools);
        hc.Controls.Add(grid);
        hc.SizeChanged += (s, e) => FitTiles(hc, grid);
        var checkFirst = new RBtn { Text = T("Check my phone first", "Semak telefon saya dahulu"), Glyph = "", Width = 250, Height = 38, Font = Font, GlyphColor = Accent, Margin = new Padding(3, 12, 0, 0) };
        checkFirst.Click += (s, e) => Nav(PgCheck);
        hc.Controls.Add(checkFirst);
        homeLast = new Label { AutoSize = true, ForeColor = Muted, UseMnemonic = false, Margin = new Padding(3, 10, 0, 0) };
        hc.Controls.Add(homeLast);

        BuildUnlock();
        Nav(StartPage);
        ShowState("none", "");
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (s, e) => Poll();
        timer.Start();
        Shown += (s, e) =>
        {
            CleanLogs(); Log("Flash Deck v" + Version + " started"); RefreshHints(); Poll();
            if (AutoLoad)   // wait for the first poll to find the phone, then load the list
            {
                var al = new System.Windows.Forms.Timer { Interval = 6000 };
                al.Tick += (s2, e2) => { al.Stop(); LoadApps(); };
                al.Start();
            }
        };
    }

    void SwitchSetting(string file, string value)
    {
        if (busy) { MessageBox.Show(T("Wait for the current task to finish first.", "Tunggu tugasan semasa siap dahulu."), Cap); return; }
        try { File.WriteAllText(file, value); } catch (Exception ex) { MessageBox.Show(ex.Message, Cap); return; }
        Application.Restart();   // rebuilds the whole UI with the new language / theme
    }

    // ---------- UI helpers ----------
    // a self-sizing flow inside a scrolling panel (a scrolling FlowLayoutPanel itself tends to open scrolled to the bottom)
    FlowLayoutPanel NewPage(out Panel scroll)
    {
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = PageBg, Location = new Point(0, 0) };
        var sc = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg };
        sc.Controls.Add(flow);
        // cards stretch to the width of the page, so they line up with the phone card above them
        int lastW = 0;
        Action stretch = () =>
        {
            int w = Math.Max(300, sc.ClientSize.Width);
            flow.MaximumSize = new Size(w, 0);   // never wider than the page, so no sideways scrollbar
            lastW = w;
            if (sc.IsHandleCreated) ShowScrollBar(sc.Handle, 0, false);   // pages never scroll sideways (the cards always fit the width)
            foreach (Control c in flow.Controls)
            {
                var cd = c as Card; if (cd == null) continue;
                cd.MinimumSize = new Size(w, 0);
                int inner = Math.Max(300, w - cd.Padding.Horizontal);
                foreach (Control k in cd.Controls)
                {
                    var fp = k as FlowLayoutPanel; if (fp != null) FitTiles(cd, fp);
                    var al = k as AppList; if (al != null) al.Width = Math.Max(480, inner);   // the app list follows the card
                    var lb = k as Label; if (lb != null && lb.AutoSize && lb.MaximumSize.Width > 0) lb.MaximumSize = new Size(inner, 0);   // notes wrap inside the card
                }
            }
        };
        sc.ClientSizeChanged += (s, e) => stretch();
        sc.Layout += (s, e) => { if (sc.IsHandleCreated) ShowScrollBar(sc.Handle, 0, false); };
        flow.ControlAdded += (s, e) => stretch();
        scroll = sc;
        return flow;
    }

    RBtn NavBtn(string text, string glyph, int top, int idx)
    {
        var b = new RBtn { Text = text, Glyph = glyph, Left = 12, Top = top, Width = 176, Height = 40, LeftText = true, Font = new Font("Segoe UI", 10f), Shift = SideShift, Edge = SideBg, Fill = SideBg };
        b.Click += (s, e) => Nav(idx);
        return b;
    }

    void Nav(int i)
    {
        curPage = i;
        if (i == PgApps && !appLoaded && !appAutoTried && !DemoApps && state == "device" && !busy) { appAutoTried = true; LoadApps(); }   // the list loads itself the first time
        homeScroll.Visible = i == PgHome; wizScroll.Visible = i == PgGuide; toolScroll.Visible = i == PgTools; checkScroll.Visible = i == PgCheck; unlockScroll.Visible = i == PgUnlock; appsScroll.Visible = i == PgApps; logPage.Visible = i == PgLog;
        wizScroll.AutoScrollPosition = new Point(0, 0); toolScroll.AutoScrollPosition = new Point(0, 0); checkScroll.AutoScrollPosition = new Point(0, 0);
        var navs = new RBtn[7];   // indexed by page number
        navs[PgHome] = navHome; navs[PgGuide] = navWiz; navs[PgTools] = navTool; navs[PgCheck] = navCheck; navs[PgUnlock] = navUnlock; navs[PgApps] = navApps; navs[PgLog] = navLog;
        for (int k = 0; k < navs.Length; k++)
        {
            bool on = k == i;
            // light theme: soft blue pill with blue text; dark theme keeps the solid accent
            Color onFill = Dark ? Accent : Color.FromArgb(204, 225, 252), onFore = Dark ? Color.White : Color.FromArgb(29, 78, 216);
            navs[k].Fill = on ? onFill : SideBg; navs[k].Edge = navs[k].Fill; navs[k].Fore = on ? onFore : SideText;
            navs[k].GlyphColor = on ? onFore : SideMuted; navs[k].Invalidate();
        }
    }

    Card NewCard(Control page, string title, int num)
    {
        var c = new Card();
        var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
        if (num > 0) head.Controls.Add(new Badge(num));
        head.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Ink, UseMnemonic = false, Margin = new Padding(0, num > 0 ? 3 : 0, 0, 0) });
        c.Controls.Add(head);
        page.Controls.Add(c);
        return c;
    }

    FlowLayoutPanel ToolRow(Control card)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MaximumSize = new Size(660, 0), Margin = new Padding(0) };
        card.Controls.Add(row);
        card.SizeChanged += (s, e) => FitTiles(card, row);   // tiles share the card's width, three to a line
        return row;
    }

    void FitTiles(Control card, FlowLayoutPanel row)
    {
        int inner = card.MinimumSize.Width - card.Padding.Horizontal;   // the width the page gives the card, not what its content happens to need
        if (inner < 300) return;
        int cols = row.Tag is int ? (int)row.Tag : 3;   // tiles per line (Home uses two)
        int w = (inner - cols * 6) / cols;
        row.MaximumSize = new Size(inner, 0);
        foreach (Control c in row.Controls) { var b = c as RBtn; if (b != null && b.Tile && b.Width != w) b.Width = w; }
    }

    FlowLayoutPanel ToolCard(Control page, string title) { return ToolRow(NewCard(page, title, 0)); }

    void Note(Control p, string s) { p.Controls.Add(new Label { Text = s, AutoSize = true, MaximumSize = new Size(700, 0), ForeColor = Muted, UseMnemonic = false, Margin = new Padding(0, 4, 0, 2) }); }

    // style: 0 normal, 1 primary (blue), 2 danger (red)
    RBtn Btn(string text, string glyph, Action act, int style, int width)
    {
        var b = new RBtn { Text = text, Glyph = glyph, Width = width, Height = 38, Font = Font, GlyphColor = Accent };
        if (style == 1) { b.Fill = Accent; b.Edge = Accent; b.Fore = Color.White; b.GlyphColor = Color.White; b.SubColor = Color.FromArgb(219, 234, 254); }
        else if (style == 2) { b.Fore = DangerC; b.Edge = Color.FromArgb(252, 165, 165); b.GlyphColor = DangerC; }
        b.Click += (s, e) => act();
        btnList.Add(b);
        return b;
    }

    void Tile(Control p, string title, string sub, string glyph, Action act, bool danger)
    {
        var b = Btn(title, glyph, act, danger ? 2 : 0, 212);
        b.Tile = true; b.Sub = sub; b.Height = 64;
        p.Controls.Add(b);
    }

    RBtn Small(string text, string glyph, Action act)
    {
        var b = new RBtn { Text = text, Glyph = glyph, Width = 112, Height = 28, Font = Font, GlyphColor = Muted };
        b.Click += (s, e) => act();
        return b;
    }

    Label Row(Control p, string text, string glyph, Action act, bool primary)
    {
        var r = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        var h = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(8, 11, 0, 0), UseMnemonic = false };
        r.Controls.Add(Btn(text, glyph, act, primary ? 1 : 0, 300)); r.Controls.Add(h); p.Controls.Add(r);
        return h;
    }

    void Ui(Action a) { if (IsHandleCreated) BeginInvoke(a); }
    static readonly string LogDir = Path.Combine(Root, "logs");
    static readonly object LogLock = new object();
    // every line also goes to logs\flashdeck-yyyyMMdd.log so a failed flash can be read back later
    void Log(string s)
    {
        Ui(() => log.AppendText(s + "\r\n"));
        try
        {
            lock (LogLock)
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, "flashdeck-" + DateTime.Now.ToString("yyyyMMdd") + ".log"), DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
            }
        }
        catch (Exception) { }
    }

    static void CleanLogs()   // keep 30 days
    {
        try { foreach (var f in Directory.GetFiles(LogDir, "flashdeck-*.log")) if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30)) File.Delete(f); } catch (Exception) { }
    }
    void SetEnabled(bool on) { foreach (var b in btnList) b.Enabled = on; }

    bool Confirm(string word, string msg)
    {
        bool ok = Interaction.InputBox(msg + "\n\n" + string.Format(T("Type {0} to continue (capitals):", "Taip {0} untuk teruskan (huruf besar):"), word), T("Confirmation", "Pengesahan"), "") == word;
        if (!ok) { Log(string.Format(T("Cancelled: confirmation '{0}' was not entered correctly.", "Dibatalkan: pengesahan '{0}' tak dimasukkan dengan betul."), word)); Nav(PgLog); }
        return ok;
    }

    void SaveLog()
    {
        var d = new SaveFileDialog { FileName = "log-android.txt", Filter = T("Text (*.txt)|*.txt", "Teks (*.txt)|*.txt") };
        if (d.ShowDialog() == DialogResult.OK) File.WriteAllText(d.FileName, log.Text);
    }

    void OpenUrl(string url) { try { Process.Start(url); } catch (Exception ex) { MessageBox.Show(ex.Message, Cap); } }

    // ---------- files in the Rom folder ----------
    string FindRom() { return FindFile(Roms[romSel.SelectedIndex].Pattern, true); }

    string FindFile(string pattern, bool skipGapps = false)
    {
        if (pattern == null || !Directory.Exists(RomDir)) return null;
        var f = Directory.GetFiles(RomDir, pattern);
        if (skipGapps) f = Array.FindAll(f, x => !Path.GetFileName(x).ToLower().Contains("gapps"));
        if (f.Length == 0) return null;
        Array.Sort(f); // names contain dates, take the newest
        return f[f.Length - 1];
    }

    string Pick(string filter)
    {
        var d = new OpenFileDialog { Filter = filter };
        if (Directory.Exists(RomDir)) d.InitialDirectory = RomDir;
        return d.ShowDialog() == DialogResult.OK ? d.FileName : null;
    }

    void RefreshHints()
    {
        Action<Label, string> show = (l, f) =>
        {
            l.Text = f != null ? "✓ " + Path.GetFileName(f) : T("not in Rom folder (will ask you to pick a file)", "tiada dalam folder Rom (akan minta pilih fail)");
            l.ForeColor = f != null ? Color.SeaGreen : Muted;
        };
        show(hRec, FindFile("recovery.img")); show(hBoot, FindFile("recovery.img"));
        show(hRom, FindRom()); show(hGapps, FindFile("MindTheGapps*.zip"));
    }

    // ---------- run commands ----------
    string Exec(string exe, string args, string dir, bool stream, int timeoutMs = 0)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = dir ?? Pt };
        psi.EnvironmentVariables["PATH"] = Pt + ";" + psi.EnvironmentVariables["PATH"];
        var sb = new StringBuilder();
        using (var p = Process.Start(psi))
        {
            DataReceivedEventHandler h = (s, e) => { if (e.Data != null) { sb.AppendLine(e.Data); if (stream) Log(e.Data); } };
            p.OutputDataReceived += h; p.ErrorDataReceived += h;
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            // timeoutMs is for polling: fastboot can hang forever when the phone changes mode
            if (timeoutMs > 0 && !p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch (Exception) { } }
            p.WaitForExit();
        }
        return sb.ToString();
    }

    void Run(string title, Action a, bool stay = false)   // stay: keep the current page (the Apps page shows its own result line)
    {
        if (busy) return;
        busy = true; SetEnabled(false); prog.Visible = true; if (!stay) Nav(PgLog); Log("> " + title);   // show the Log tab when a task starts
        new Thread(() =>
        {
            try { a(); } catch (Exception ex) { Log(T("ERROR: ", "RALAT: ") + ex.Message); }
            busy = false; Ui(() => { SetEnabled(true); prog.Visible = false; RefreshHints(); homeLast.Text = T("Last action: ", "Tindakan terakhir: ") + title + " (" + DateTime.Now.ToString("HH:mm") + ")"; }); Log(T("Done.", "Selesai.") + "\r\n");
        }) { IsBackground = true }.Start();
    }

    bool NeedFastboot()
    {
        if (state == "fastboot") return true;
        MessageBox.Show(T("The phone must be in Fastboot mode. Press 'Reboot to Bootloader' first.", "Telefon mesti dalam mod Fastboot. Tekan 'Reboot ke Bootloader' dulu."), Cap);
        return false;
    }

    bool NeedSystem()
    {
        if (state == "device") return true;
        MessageBox.Show(T("The phone must be switched on and connected with USB debugging allowed.", "Telefon mesti dihidupkan dan disambung dengan USB debugging dibenarkan."), Cap);
        return false;
    }

    // ---------- phone detection ----------
    // returns none / device / recovery / sideload / unauthorized / fastboot; detail = text for the status card, mfr/dev = manufacturer/codename
    string Detect(out string detail, out string mfr, out string dev) { string rom; return Detect(out detail, out mfr, out dev, out rom); }

    // rom = the build flavor (e.g. "lineage_apollon-user"), used to tell a custom ROM from the stock one
    string Detect(out string detail, out string mfr, out string dev, out string rom)
    {
        string st = "none"; detail = ""; mfr = ""; dev = ""; rom = "";
        foreach (var line in Exec(Adb, "devices", null, false, 5000).Split('\n'))
        {
            var p = line.Trim().Split('\t');
            if (p.Length == 2 && (p[1] == "device" || p[1] == "recovery" || p[1] == "sideload" || p[1] == "unauthorized")) st = p[1];
        }
        if (st == "device" || st == "recovery")
        {
            // every value is labelled (name=value), so an empty property can not shift the others
            var pr = new Dictionary<string, string>();
            foreach (var l in Exec(Adb, "shell \"for p in ro.product.model ro.build.version.release ro.product.manufacturer ro.product.device ro.build.flavor ro.boot.verifiedbootstate ro.lineage.version; do echo $p=$(getprop $p); done\"", null, false, 5000).Split('\n'))
            {
                int eq = l.IndexOf('=');
                if (eq > 0) pr[l.Substring(0, eq).Trim()] = l.Substring(eq + 1).Trim();
            }
            Func<string, string> gp = k => pr.ContainsKey(k) ? pr[k] : "";
            var v = new List<string> { gp("ro.product.model"), gp("ro.build.version.release"), gp("ro.product.manufacturer"), gp("ro.product.device"), gp("ro.build.flavor") };
            lastProps = pr;
            if (v[0] != "" && v[2] != "" && v[3] != "")
            {
                mfr = v[2]; dev = v[3]; rom = v[4];
                detail = v[2] + " " + v[0] + " (" + v[3] + ")  ·  Android " + v[1];
            }
        }
        else if (st == "none" && Exec(Fb, "devices", null, false, 5000).Contains("fastboot"))
        {
            st = "fastboot";
            string v = Exec(Fb, "getvar unlocked", null, false, 5000);
            detail = v.Contains("unlocked: yes") ? "Bootloader: UNLOCKED" : v.Contains("unlocked: no") ? "Bootloader: LOCKED" : "";
            foreach (var l in Exec(Fb, "getvar product", null, false, 5000).Split('\n'))
                if (l.StartsWith("product:")) { dev = l.Substring(8).Trim(); detail += "  ·  " + l.Trim(); }
        }
        return st;
    }

    void Poll()
    {
        if (busy || polling) return;
        polling = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            string st = "none", detail = "", mfr = "", dev = "", rom = "";
            try { st = Detect(out detail, out mfr, out dev, out rom); } catch (Exception) { }
            if (!busy)
            {
                state = st;
                Ui(() =>
                {
                    ShowState(st, detail, rom);
                    if (mfr != "")
                    {
                        if (brandSrc != "manual") SetBrand(ProfileIndex(mfr), "auto");   // a brand you picked yourself is kept
                        codename = dev;
                        int cut = detail.IndexOf("  ·"); if (cut > 0) phoneName = detail.Substring(0, cut);
                        try { File.WriteAllText(LastFile, mfr + "\n" + dev + "\n" + phoneName); } catch (Exception) { }
                    }
                    else if (st == "fastboot" && dev != "") codename = dev;
                    if (mfr == "" && brandSrc == "auto") SetBrand(brandIdx, "last");   // fastboot gives no brand: it is now only the last known one
                    // follow the phone: when it enters fastboot / recovery / sideload, show the matching guide step (never presses anything)
                    if (st != lastSt)
                    {
                        lastSt = st;
                        if (follow.Checked && curPage != PgCheck && curPage != PgLog && (st == "fastboot" || st == "recovery" || st == "sideload"))
                        { Nav(PgGuide); wizScroll.ScrollControlIntoView(wizCards[curStep]); }
                    }
                });
            }
            polling = false;
        });
    }

    // name of a known custom ROM from the build flavor ("" = stock or unknown)
    static string CustomRomName(string flavor)
    {
        string f = (flavor ?? "").ToLower();
        foreach (var k in new[] { "lineage", "crdroid", "rising", "alpha", "evolution", "pixelos", "aosp", "arrow", "paranoid", "derpfest", "havoc" })
            if (f.Contains(k)) return k == "lineage" ? "LineageOS" : char.ToUpper(k[0]) + k.Substring(1);
        return "";
    }

    void ShowState(string st, string detail, string rom = "")
    {
        string cr = CustomRomName(rom);
        Color c = Color.Gray; string t = T("No phone detected", "Tiada telefon dikesan"), g = "", h = T("Next: connect the phone with a USB cable", "Seterusnya: sambung telefon dengan kabel USB"),
            i = T("Connect USB and enable USB debugging, or enter fastboot mode.", "Sambung USB + hidupkan USB debugging, atau masuk mod fastboot.");
        int step = 0;   // which guide card is highlighted
        switch (st)
        {
            case "device": c = Color.SeaGreen; g = ""; t = T("Connected (system)", "Disambung (sistem)"); i = detail;
                h = T("Next: Reboot to Bootloader (step 1)", "Seterusnya: Reboot ke Bootloader (langkah 1)");
                if (cr != "")   // already installed: do not send the user back to step 1
                { h = string.Format(T("Done: the phone already runs {0}. Open Install ROM only to install a different ROM.", "Siap: telefon sudah guna {0}. Buka Pasang ROM hanya untuk pasang ROM lain."), cr); step = 3; }
                break;
            case "recovery": c = Color.SteelBlue; g = ""; t = T("Recovery mode", "Mod Recovery"); i = T("Choose Apply update > Apply from ADB to sideload.", "Pilih Apply update > Apply from ADB untuk sideload.");
                h = T("Next: Format data, then Apply from ADB (step 2)", "Seterusnya: Format data, kemudian Apply from ADB (langkah 2)"); step = 1; break;
            case "sideload": c = Color.SteelBlue; g = ""; t = T("ADB Sideload mode - ready to receive files", "Mod ADB Sideload - sedia terima fail"); i = "";
                h = T("Next: install the ROM, then GApps (step 3)", "Seterusnya: pasang ROM, kemudian GApps (langkah 3)"); step = 2; break;
            case "fastboot": c = Color.DarkOrange; g = ""; t = T("Fastboot mode", "Mod Fastboot"); i = detail;
                h = T("Next: flash or boot the recovery image (step 1)", "Seterusnya: flash atau boot imej recovery (langkah 1)"); break;
            case "unauthorized": c = Color.Firebrick; g = ""; t = T("Not authorised", "Belum dibenarkan"); i = T("Tap Allow on the phone screen.", "Tekan Allow pada skrin telefon.");
                h = T("Next: accept the USB debugging prompt on the phone", "Seterusnya: terima prompt USB debugging pada telefon"); break;
        }
        curStep = step;
        if (st == "device" && detail != "")   // "Xiaomi M2007J3SY connected" and one line of facts
        {
            int pi = detail.IndexOf(" (");
            t = string.Format(T("{0} connected", "{0} disambung"), pi > 0 ? detail.Substring(0, pi) : detail);
            var facts = new List<string>();
            if (cr != "") { string lv = PropOf("ro.lineage.version"); facts.Add(cr == "LineageOS" && lv != "" ? "LineageOS " + lv.Split('-')[0] : cr); }
            var am = Regex.Match(detail, @"Android (\S+)"); if (am.Success) facts.Add("Android " + am.Groups[1].Value);
            string vb = PropOf("ro.boot.verifiedbootstate");
            if (vb == "orange") facts.Add(T("bootloader unlocked", "bootloader unlock")); else if (vb == "green") facts.Add(T("bootloader locked", "bootloader terkunci"));
            i = string.Join("  ·  ", facts.ToArray());
        }
        statusIcon.ForeColor = c; if (IconFont != null) statusIcon.Text = g;
        status.Text = t; info.Text = i; hint.Text = h; hintHome.Text = h;
        statusChip.Set(st == "device" ? T("Ready", "Sedia") : st == "recovery" || st == "sideload" ? T("In recovery", "Dalam recovery") : st == "fastboot" ? "Fastboot"
                       : st == "unauthorized" ? T("Allow on phone", "Benarkan pada telefon") : T("Not connected", "Tak disambung"), c);
        for (int k = 0; k < wizCards.Count; k++)
        {
            bool on = st != "none" && k == step;
            if (wizCards[k].Active != on) { wizCards[k].Active = on; wizCards[k].Invalidate(); }
        }
        // the Apps list loads itself once the phone shows up (one try per connection, so a failure does not pop up a message every poll)
        if (st != "device") appAutoTried = false;
        else if (curPage == PgApps && !appLoaded && !appAutoTried && !busy && !DemoApps) { appAutoTried = true; LoadApps(); }
    }

    // ---------- pre-flight check ----------
    static bool IsZip(string f)
    {
        using (var s = File.OpenRead(f)) return s.Length > 1000 && s.ReadByte() == 'P' && s.ReadByte() == 'K';
    }

    void AddCheck(int kind, string text)   // 0 ok, 1 warning, 2 problem
    {
        var col = kind == 0 ? Color.SeaGreen : kind == 1 ? Color.DarkOrange : Color.Firebrick;
        checkList.Controls.Add(new Label { Text = (kind == 0 ? "✓  " : kind == 1 ? "!  " : "✗  ") + text, AutoSize = true, MaximumSize = new Size(620, 0), ForeColor = col, UseMnemonic = false, Margin = new Padding(0, 3, 0, 3) });
    }

    void PreFlight()
    {
        if (busy) return;
        busy = true; SetEnabled(false); prog.Visible = true; checkList.Controls.Clear();
        new Thread(() =>
        {
            var res = new List<KeyValuePair<int, string>>();
            try { RunChecks(res); } catch (Exception ex) { res.Add(new KeyValuePair<int, string>(2, ex.Message)); }
            busy = false;
            Ui(() => { checkList.Controls.Clear(); foreach (var r in res) AddCheck(r.Key, r.Value); SetEnabled(true); prog.Visible = false; checkList.Parent.Invalidate(true); });   // repaint: clears a stray line left by the old results
        }) { IsBackground = true }.Start();
    }

    void RunChecks(List<KeyValuePair<int, string>> r)
    {
        Action<int, string> add = (k, s) => r.Add(new KeyValuePair<int, string>(k, s));
        string detail, mfr, dev; string st = Detect(out detail, out mfr, out dev);

        // phone
        if (st == "none") add(2, T("No phone detected - see 'Phone not detected?' below.", "Tiada telefon dikesan - lihat 'Telefon tak dikesan?' di bawah."));
        else if (st == "unauthorized") add(2, T("Phone not authorised - tap Allow on the phone screen.", "Telefon belum dibenarkan - tekan Allow pada skrin telefon."));
        else add(0, T("Phone detected: ", "Telefon dikesan: ") + (detail != "" ? detail : st));

        // battery and bootloader
        if (st == "device")
        {
            var m = Regex.Match(Exec(Adb, "shell dumpsys battery", null, false, 5000), @"(?m)^\s*level:\s*(\d+)");
            if (m.Success)
            {
                int lvl = int.Parse(m.Groups[1].Value);
                add(lvl >= 50 ? 0 : 1, T("Battery: ", "Bateri: ") + lvl + "%" + (lvl < 50 ? T(" - charge above 50% before flashing", " - cas melebihi 50% sebelum flash") : ""));
            }
            string lk = Exec(Adb, "shell getprop ro.boot.flash.locked", null, false, 5000).Trim();
            if (lk == "")   // custom ROMs often leave flash.locked empty; verified boot state is the fallback (orange = unlocked, green = locked)
            {
                string vb = Exec(Adb, "shell getprop ro.boot.verifiedbootstate", null, false, 5000).Trim();
                lk = vb == "orange" ? "0" : vb == "green" ? "1" : "";
            }
            if (lk == "0") add(0, T("Bootloader is unlocked.", "Bootloader sudah unlock."));
            else if (lk == "1") add(2, T("Bootloader is LOCKED - unlock it before installing a custom ROM.", "Bootloader TERKUNCI - unlock dahulu sebelum pasang ROM custom."));
            else add(1, T("Bootloader state unknown from the system (check in fastboot).", "Status bootloader tak diketahui dari sistem (semak dalam fastboot)."));
        }
        else if (st == "fastboot")
        {
            if (detail.Contains("UNLOCKED")) add(0, T("Bootloader is unlocked.", "Bootloader sudah unlock."));
            else if (detail.Contains("LOCKED")) add(2, T("Bootloader is LOCKED - unlock it before installing a custom ROM.", "Bootloader TERKUNCI - unlock dahulu sebelum pasang ROM custom."));
        }

        // files
        string romName = Roms[romSel.SelectedIndex].Name;
        string rec = FindFile("recovery.img"), rom = FindRom(), gapps = FindFile("MindTheGapps*.zip");
        add(rec != null ? 0 : 1, rec != null ? T("recovery.img found.", "recovery.img dijumpai.") : T("recovery.img not in the Rom folder.", "recovery.img tiada dalam folder Rom."));
        add(rom != null ? 0 : 1, rom != null ? T("ROM zip found: ", "Zip ROM dijumpai: ") + Path.GetFileName(rom) : string.Format(T("No {0} zip in the Rom folder.", "Tiada zip {0} dalam folder Rom."), romName));
        add(gapps != null ? 0 : 1, gapps != null ? T("GApps zip found: ", "Zip GApps dijumpai: ") + Path.GetFileName(gapps) : T("No GApps zip (optional).", "Tiada zip GApps (pilihan)."));
        foreach (var f in new[] { rom, gapps })
            if (f != null && !IsZip(f)) add(2, Path.GetFileName(f) + T(" is not a valid zip - the download may be incomplete.", " bukan zip yang sah - muat turun mungkin tak lengkap."));

        // ROM codename vs phone codename (Lineage may add a letter, e.g. apollo / apollon)
        if (rom != null && dev != "")
        {
            string pc = dev.ToLower();
            var m = Regex.Match(Path.GetFileName(rom), @"-(?:nightly|unofficial|release)-([A-Za-z0-9_]+?)(?:-signed)?\.zip$");
            if (m.Success)
            {
                string zc = m.Groups[1].Value.ToLower();
                bool ok = zc.StartsWith(pc) || pc.StartsWith(zc);
                add(ok ? 0 : 2, ok ? string.Format(T("ROM codename '{0}' matches the phone ('{1}').", "Codename ROM '{0}' sepadan dengan telefon ('{1}')."), zc, pc)
                                   : string.Format(T("This ROM is for '{0}' but the phone reports '{1}' - wrong ROM?", "ROM ini untuk '{0}' tetapi telefon lapor '{1}' - ROM salah?"), zc, pc));
            }
            else if (!Path.GetFileName(rom).ToLower().Contains(pc))   // other ROMs name their files differently: only warn
                add(1, string.Format(T("The ROM file name does not mention '{0}' - make sure it is built for this phone.", "Nama fail ROM tak menyebut '{0}' - pastikan ia dibina untuk telefon ini."), pc));
        }
        // a fresh LineageOS install needs the Android 12 stock firmware (not relevant when the phone already runs a newer ROM)
        var av = Regex.Match(detail, @"Android (\d+)");
        if (romSel.SelectedIndex == 0 && av.Success && av.Groups[1].Value != "12")
            add(1, string.Format(T("LineageOS needs the Android 12 stock firmware before a fresh install (the phone is on Android {0} now).", "LineageOS perlukan firmware stok Android 12 sebelum pemasangan baharu (telefon kini Android {0})."), av.Groups[1].Value));
        if (st == "device" && mfr != "") add(1, T("Back up your data first - flashing erases the phone.", "Backup data dahulu - flash memadam telefon."));
    }

    // ---------- apps page: list and remove (bloatware) ----------
    // never offered for removal: removing these can leave the phone unusable
    static readonly string[] ProtectedExact = { "android", "com.android.settings", "com.android.systemui", "com.android.phone", "com.android.shell", "com.android.bluetooth", "com.android.packageinstaller", "com.google.android.gms", "com.google.android.gsf" };
    static readonly string[] ProtectedPrefix = { "com.android.providers.", "com.android.inputmethod", "com.android.server.", "com.android.networkstack", "com.android.permissioncontroller", "com.android.cellbroadcast" };

    // home screen apps (launchers) found on the phone: disabling the only launcher leaves no home screen, on any brand
    static HashSet<string> HomePkgs = new HashSet<string>();

    static bool IsProtected(string p)
    {
        if (Array.IndexOf(ProtectedExact, p) >= 0 || HomePkgs.Contains(p)) return true;
        foreach (var x in ProtectedPrefix) if (p.StartsWith(x)) return true;
        return false;
    }

    static readonly Regex PkgName = new Regex("^[A-Za-z0-9_.]+$");   // package names go into a command line

    // Advice for well-known packages: kind = safe (ads/tracking), optional (a normal app), careful (some feature depends on it).
    // General community advice written from memory, not a guarantee; anything not listed is shown as "unknown".
    static readonly Dictionary<string, string[]> AppHints = BuildHints();
    static Dictionary<string, string[]> BuildHints()
    {
        var d = new Dictionary<string, string[]>();
        Action<string, string, string, string> a = (pkg, kind, en, ms) => d[pkg] = new[] { kind, en, ms };
        a("com.miui.analytics", "safe", "Xiaomi analytics (tracking)", "Analitik Xiaomi");
        a("com.miui.msa.global", "safe", "Xiaomi system ads", "Iklan sistem Xiaomi");
        a("com.xiaomi.mipicks", "safe", "GetApps store (ads)", "Kedai GetApps (iklan)");
        a("com.miui.bugreport", "safe", "Xiaomi bug reporter", "Pelapor pepijat Xiaomi");
        a("com.facebook.appmanager", "safe", "Facebook helper", "Pembantu Facebook");
        a("com.facebook.services", "safe", "Facebook services", "Perkhidmatan Facebook");
        a("com.facebook.system", "safe", "Facebook installer", "Pemasang Facebook");
        a("com.netflix.partner.activation", "safe", "Netflix activation", "Pengaktifan Netflix");
        a("com.miui.player", "optional", "Mi Music player", "Pemain muzik Mi");
        a("com.miui.videoplayer", "optional", "Mi Video player", "Pemain video Mi");
        a("com.miui.yellowpage", "optional", "Xiaomi Yellow Pages", "Yellow Pages Xiaomi");
        a("com.google.android.youtube", "optional", "YouTube", "YouTube");
        a("com.google.android.apps.youtube.music", "optional", "YouTube Music", "YouTube Music");
        a("com.google.android.videos", "optional", "Google TV (movies)", "Google TV (filem)");
        a("com.google.android.apps.photos", "optional", "Google Photos", "Google Photos");
        a("com.google.android.apps.docs", "optional", "Google Drive", "Google Drive");
        a("com.google.android.apps.maps", "optional", "Google Maps", "Google Maps");
        a("com.google.android.gm", "optional", "Gmail", "Gmail");
        a("com.google.android.apps.tachyon", "optional", "Google Meet", "Google Meet");
        a("com.android.chrome", "optional", "Chrome browser", "Pelayar Chrome");
        a("com.android.calculator2", "optional", "Calculator", "Kalkulator");
        a("org.lineageos.recorder", "optional", "Voice recorder", "Perakam suara");
        a("org.lineageos.jelly", "optional", "Jelly browser", "Pelayar Jelly");
        a("org.lineageos.etar", "optional", "Etar calendar", "Kalendar Etar");
        a("org.lineageos.aperture", "careful", "Camera app", "Aplikasi kamera");
        a("com.android.vending", "careful", "Google Play Store", "Google Play Store");
        a("com.android.contacts", "careful", "Contacts", "Kenalan");
        a("com.android.messaging", "careful", "Messages (SMS)", "Mesej (SMS)");
        a("com.android.deskclock", "careful", "Clock and alarms", "Jam dan penggera");
        a("org.lineageos.updater", "careful", "LineageOS updater", "Pengemas kini LineageOS");
        a("org.lineageos.audiofx", "careful", "Audio effects", "Kesan audio");
        return d;
    }

    // filled when the list is loaded: apps you installed yourself, and apps that show an icon in the app drawer
    static HashSet<string> UserPkgs = new HashSet<string>(), LauncherPkgs = new HashSet<string>();

    // kind of an app for the advice column: core (protected), safe, optional, careful, yours (installed by you),
    // sysicon (unlisted system app with an icon), sysbg (unlisted background part) or unknown (no extra information)
    static string AppKind(string p)
    {
        if (IsProtected(p)) return "core";
        string[] h;
        if (AppHints.TryGetValue(p, out h)) return h[0];
        if (UserPkgs.Contains(p))   // GApps parts are installed like user apps but are not your choice: be careful with them
            return p.StartsWith("com.google.") || p.StartsWith("com.android.") || p.StartsWith("android.") ? "gapps" : "yours";
        if (LauncherPkgs.Count > 0) return LauncherPkgs.Contains(p) ? "sysicon" : "sysbg";
        return "unknown";
    }

    HashSet<string> ListLauncherPkgs() { return ListActivityPkgs("LAUNCHER"); }   // the apps you see in the app drawer

    // packages that have an activity in the given category: LAUNCHER = app drawer apps, HOME = home screen apps (launchers)
    HashSet<string> ListActivityPkgs(string category)
    {
        var s = new HashSet<string>();
        foreach (var l in Exec(Adb, "shell cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category." + category, null, false, 20000).Split('\n'))
        {
            string t = l.Trim(); int i = t.IndexOf('/');
            if (i > 0 && t.IndexOf(' ') < 0 && PkgName.IsMatch(t.Substring(0, i))) s.Add(t.Substring(0, i));
        }
        return s;
    }

    // what the app is, in words (from the built-in hints); empty when we only know the package name
    static string AppDesc(string p, string kind)
    {
        string[] h; if (AppHints.TryGetValue(p, out h)) return T(h[1], h[2]);
        if (HomePkgs.Contains(p)) return T("Home screen (launcher)", "Skrin utama (launcher)");
        return kind == "core" ? T("System component", "Komponen sistem") : "";
    }

    static string KindLabel(string kind)
    {
        switch (kind)
        {
            case "safe": return T("Safe to disable", "Selamat dilumpuhkan");
            case "optional": return T("Optional app", "Aplikasi pilihan");
            case "careful": return T("Careful", "Hati-hati");
            case "core": return T("Core (protected)", "Teras (dilindungi)");
            case "yours": return T("Installed by you", "Dipasang oleh awak");
            case "gapps": return T("Google part: careful", "Bahagian Google: hati-hati");
            case "sysicon": return T("Unknown (has an icon)", "Tak diketahui (ada ikon)");
            case "sysbg": return T("Background: leave it", "Latar: biarkan");
            default: return T("Unknown", "Tak diketahui");
        }
    }

    static Color[] GroupColors { get { return new[] { Color.SeaGreen, Color.SteelBlue, Color.DarkOrange, Color.SlateBlue, Muted, Color.Sienna, Color.Firebrick }; } }

    // the count line and the Disable button follow what is ticked
    void UpdateAppBar()
    {
        int n = appList.CheckedPkgs().Count;
        btnDisable.Text = n == 1 ? T("Disable 1 app", "Lumpuhkan 1 aplikasi") : n > 1 ? string.Format(T("Disable {0} apps", "Lumpuhkan {0} aplikasi"), n) : T("Disable", "Lumpuhkan"); btnDisable.Invalidate();
        int shown = appList.AppCount;
        appCount.Text = appNote != "" ? appNote
            : (n > 0 ? string.Format(T("{0} selected · ", "{0} dipilih · "), n) : "") + (shown == 1 ? T("1 app shown", "1 aplikasi dipaparkan") : string.Format(T("{0} apps shown", "{0} aplikasi dipaparkan"), shown)) + appHiddenNote;
    }

    // a pop-up menu in the app's colours, with soft corners
    ContextMenuStrip NewMenu()
    {
        var m = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false, Font = Font, BackColor = CardBg, ForeColor = Ink, Padding = new Padding(4), Renderer = new ToolStripProfessionalRenderer(new MenuColors()) };
        m.Opened += (s, e) => { using (var p = RBtn.Round(new Rectangle(0, 0, m.Width, m.Height), 16)) m.Region = new Region(p); };
        m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
        return m;
    }

    ToolStripMenuItem MenuItem(ContextMenuStrip m, string text, Action act, Color fore)
    {
        var it = new ToolStripMenuItem(text) { ForeColor = fore, Padding = new Padding(2, 4, 2, 4) };
        it.Click += (s, e) => act(); m.Items.Add(it); return it;
    }

    void ShowAppsMenu()
    {
        var m = NewMenu();
        MenuItem(m, "      " + T("Search online", "Cari maklumat"), SearchApp, Ink);
        MenuItem(m, "      " + T("Restore by name", "Pulihkan ikut nama"), RestoreApp, Ink);
        MenuItem(m, (appShowBg ? "✓   " : "      ") + T("Show background parts", "Tunjuk bahagian latar"), () => { appShowBg = !appShowBg; FillApps(); }, Ink);
        m.Items.Add(new ToolStripSeparator());
        MenuItem(m, "      " + T("Remove ticked apps", "Buang aplikasi ditanda"), UninstallApps, DangerC);
        m.Show(appMore.PointToScreen(new Point(appMore.Width - m.GetPreferredSize(Size.Empty).Width, -m.GetPreferredSize(Size.Empty).Height - 6)));
    }

    void SearchApp()
    {
        string p = appList.Selected;
        if (p == null) { var c = appList.CheckedPkgs(); if (c.Count > 0) p = c[0]; }
        if (p == null) { MessageBox.Show(T("Click an app in the list first.", "Klik satu aplikasi dalam senarai dahulu."), Cap); return; }
        OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString("android package " + p + " what is it safe to disable"));
    }

    List<string> ListPackages(string flag)
    {
        var res = new List<string>();
        foreach (var l in Exec(Adb, "shell pm list packages " + flag, null, false, 20000).Split('\n'))
        {
            string t = l.Trim();
            if (t.StartsWith("package:") && PkgName.IsMatch(t.Substring(8))) res.Add(t.Substring(8));
        }
        res.Sort(StringComparer.OrdinalIgnoreCase);
        return res;
    }

    string AppFlag() { return appFilter.SelectedIndex == 0 ? "-3" : appFilter.SelectedIndex == 1 ? "-s" : appFilter.SelectedIndex == 3 ? "-d" : ""; }

    static string[] GroupTitles { get { return new[] {
        T("Safe to disable ({n})", "Selamat dilumpuhkan ({n})"), T("Optional apps ({n})", "Aplikasi pilihan ({n})"),
        T("Be careful ({n})", "Hati-hati ({n})"), T("Installed by you ({n})", "Dipasang oleh awak ({n})"),
        T("Unknown ({n})", "Tak diketahui ({n})"), T("Background parts: leave them ({n})", "Bahagian latar: biarkan ({n})"),
        T("Core, protected ({n})", "Teras, dilindungi ({n})") }; } }
    internal static int GroupOf(string kind)
    {
        switch (kind) { case "safe": return 0; case "optional": return 1; case "careful": case "gapps": return 2; case "yours": return 3; case "sysbg": return 5; case "core": return 6; default: return 4; }
    }

    void FillApps()
    {
        string q = appSearch.Text.Trim().ToLower();
        var keep = new HashSet<string>(appList.CheckedPkgs());   // ticks survive a search
        int hidden = 0;
        var items = new List<AppRow>[GroupTitles.Length];
        foreach (var p in appAll)
        {
            string kind = AppKind(p), desc = AppDesc(p, kind);
            if (q != "" && !p.ToLower().Contains(q) && !desc.ToLower().Contains(q)) continue;
            if (!appShowBg && kind == "sysbg") { hidden++; continue; }   // background parts: nothing a user should touch
            int gi = GroupOf(kind);
            if (items[gi] == null) items[gi] = new List<AppRow>();
            items[gi].Add(new AppRow { Pkg = p, Title = desc != "" ? desc : p, Sub = desc != "" ? p : KindLabel(kind), Locked = kind == "core", Checked = keep.Contains(p),
                                       Tag = kind == "core" ? T("Protected", "Dilindungi") : kind == "careful" || kind == "gapps" ? T("Careful", "Hati-hati") : null });
        }
        var rows = new List<AppRow>();
        for (int gi = 0; gi < items.Length; gi++)   // grouped by advice: the safest to disable first, protected last
        {
            if (items[gi] == null) continue;
            rows.Add(new AppRow { Head = GroupTitles[gi].Replace("{n}", items[gi].Count.ToString()), Dot = GroupColors[gi] });
            rows.AddRange(items[gi]);
        }
        if (rows.Count == 0 && appAll.Count > 0) appList.Empty = T("No apps match.", "Tiada aplikasi sepadan.");
        appList.SetRows(rows);
        appHiddenNote = hidden > 0 ? string.Format(T(" ({0} background parts hidden)", " ({0} bahagian latar disembunyi)"), hidden) : "";
        UpdateAppBar();
    }

    void LoadApps()
    {
        if (busy || !NeedSystem()) return;
        string flag = AppFlag();
        busy = true; SetEnabled(false); prog.Visible = true; appCount.Text = T("Loading...", "Memuatkan...");
        new Thread(() =>
        {
            List<string> pk = null; string err = null;
            try
            {
                pk = ListPackages(flag);
                UserPkgs = new HashSet<string>(flag == "-3" ? pk : ListPackages("-3"));   // what you installed yourself
                LauncherPkgs = ListLauncherPkgs();                                         // what has an icon
                HomePkgs = ListActivityPkgs("HOME");                                       // launchers: always protected
            }
            catch (Exception ex) { err = ex.Message; }
            busy = false;
            Ui(() => { SetEnabled(true); prog.Visible = false; if (pk != null) { appAll = pk; appLoaded = true; appList.Empty = T("No apps in this list.", "Tiada aplikasi dalam senarai ini."); FillApps(); } else { UpdateAppBar(); MessageBox.Show(err, Cap); } });
        }) { IsBackground = true }.Start();
    }

    void UninstallApps()
    {
        if (!NeedSystem()) return;
        var sel = appList.CheckedPkgs();
        if (sel.Count == 0) { MessageBox.Show(T("Tick the apps to remove first.", "Tandakan aplikasi yang mahu dibuang dahulu."), Cap); return; }
        var prot = sel.FindAll(IsProtected);
        if (prot.Count > 0)
        {
            MessageBox.Show(T("These apps are protected because removing them can break the phone. Untick them:\n\n", "Aplikasi ini dilindungi kerana membuangnya boleh merosakkan telefon. Nyahtanda:\n\n") + string.Join("\n", prot.ToArray()), Cap);
            return;
        }
        string shown = string.Join("\n", sel.GetRange(0, Math.Min(15, sel.Count)).ToArray()) + (sel.Count > 15 ? "\n..." : "");
        if (MessageBox.Show(string.Format(T("Remove {0} app(s) for this phone?\n\n{1}\n\nSystem apps can be restored with 'Restore an app'. Apps you installed must be installed again.",
                                            "Buang {0} aplikasi untuk telefon ini?\n\n{1}\n\nAplikasi sistem boleh dipulihkan dengan 'Pulihkan aplikasi'. Aplikasi yang awak pasang perlu dipasang semula."), sel.Count, shown),
                            T("Remove apps", "Buang aplikasi"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        string flag = AppFlag();
        Run(T("Remove apps", "Buang aplikasi"), () =>
        {
            int ok = 0; bool needRoot = false;
            foreach (var p in sel)
            {
                string o = Exec(Adb, "shell pm uninstall --user 0 " + p, null, true);
                if (o.Contains("Success")) ok++;
                else { Log(T("Failed: ", "Gagal: ") + p); if (o.Contains("only root")) needRoot = true; }
            }
            string res = string.Format(T("{0} of {1} removed.", "{0} daripada {1} dibuang."), ok, sel.Count);
            Log(res);
            if (needRoot) Log(T("This phone does not allow removing system apps without root. Use 'Disable' instead: it hides and stops the app, and 'Enable' brings it back.",
                                "Telefon ini tak benarkan membuang aplikasi sistem tanpa root. Guna 'Lumpuhkan' sebaliknya: ia menyembunyi dan menghentikan aplikasi, dan 'Hidupkan' memulihkannya."));
            if (needRoot) res += " " + T("System apps need root to remove: use Disable.", "Aplikasi sistem perlu root untuk dibuang: guna Lumpuhkan.");   // the same hint, on the Apps page itself
            var pk = ListPackages(flag);   // refresh the list
            Ui(() => { appList.ClearChecks(); appAll = pk; FillApps(); appNote = res; UpdateAppBar(); });   // ticks are cleared after the action
        }, true);
    }

    // the shared core of the Disable / Enable buttons: returns how many packages changed state
    int SetAppsState(List<string> pkgs, bool enable, bool log)
    {
        string verb = enable ? "enable" : "disable-user"; int ok = 0;
        foreach (var p in pkgs)
        {
            if (!PkgName.IsMatch(p)) continue;
            string o = Exec(Adb, "shell pm " + verb + " --user 0 " + p, null, log);
            if (o.Contains("new state")) ok++; else if (log) Log(T("Failed: ", "Gagal: ") + p);
        }
        return ok;
    }

    // Flash Deck.exe --phonetest: with a phone connected, disables Recorder, checks it, then enables it again; writes phonetest.txt
    public void PhoneSelfTest()
    {
        var r = new StringBuilder();
        Action<string, bool> chk = (n, ok) => r.AppendLine((ok ? "PASS " : "FAIL ") + n);
        const string pkg = "org.lineageos.recorder";
        try
        {
            string dd, mm, vv; string st = Detect(out dd, out mm, out vv);
            chk("phone found in system mode (" + st + ")", st == "device");
            if (st == "device")
            {
                var sysApps = ListPackages("-s");
                chk("system app list is not empty (" + sysApps.Count + ")", sysApps.Count > 0);
                chk("launcher list is not empty (" + ListLauncherPkgs().Count + ")", ListLauncherPkgs().Count > 0);
                chk("test app is present", sysApps.Contains(pkg));
                if (sysApps.Contains(pkg))
                {
                    try
                    {
                        chk("disable reports success", SetAppsState(new List<string> { pkg }, false, false) == 1);
                        chk("app appears in the Disabled list", ListPackages("-d").Contains(pkg));
                    }
                    finally   // always put the phone back
                    {
                        chk("enable reports success", SetAppsState(new List<string> { pkg }, true, false) == 1);
                    }
                    chk("app is gone from the Disabled list again", !ListPackages("-d").Contains(pkg));
                    chk("app is still installed", ListPackages("-s").Contains(pkg));
                }
            }
        }
        catch (Exception ex) { r.AppendLine("FAIL exception: " + ex.Message); }
        File.WriteAllText(Path.Combine(Root, "phonetest.txt"), r.ToString());
    }

    // disable (hide and stop) or enable the ticked apps: reversible and works without root, unlike removing system apps
    void DisableApps(bool enable)
    {
        if (!NeedSystem()) return;
        var sel = appList.CheckedPkgs();
        if (sel.Count == 0) { MessageBox.Show(T("Tick the apps first.", "Tandakan aplikasi dahulu."), Cap); return; }
        if (!enable)
        {
            var prot = sel.FindAll(IsProtected);
            if (prot.Count > 0)
            {
                MessageBox.Show(T("These apps are protected because disabling them can break the phone. Untick them:\n\n", "Aplikasi ini dilindungi kerana melumpuhkannya boleh merosakkan telefon. Nyahtanda:\n\n") + string.Join("\n", prot.ToArray()), Cap);
                return;
            }
            string shown = string.Join("\n", sel.GetRange(0, Math.Min(15, sel.Count)).ToArray()) + (sel.Count > 15 ? "\n..." : "");
            if (MessageBox.Show(string.Format(T("Disable {0} app(s)?\n\n{1}\n\nThey are hidden and stopped. 'Enable' (on the Disabled tab) brings them back.",
                                                "Lumpuhkan {0} aplikasi?\n\n{1}\n\nIa disembunyi dan dihentikan. 'Hidupkan' (pada tab Dilumpuhkan) memulihkannya."), sel.Count, shown),
                                T("Disable apps", "Lumpuhkan aplikasi"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        }
        string flag = AppFlag();
        Run(enable ? T("Enable apps", "Hidupkan aplikasi") : T("Disable apps", "Lumpuhkan aplikasi"), () =>
        {
            int ok = SetAppsState(sel, enable, true);
            string res = string.Format(enable ? T("{0} of {1} enabled.", "{0} daripada {1} dihidupkan.") : T("{0} of {1} disabled.", "{0} daripada {1} dilumpuhkan."), ok, sel.Count);
            Log(res);
            var pk = ListPackages(flag);   // refresh the list
            Ui(() => { appList.ClearChecks(); appAll = pk; FillApps(); appNote = res; UpdateAppBar(); });   // ticks are cleared after the action
        }, true);
    }

    void RestoreApp()
    {
        if (!NeedSystem()) return;
        string p = Interaction.InputBox(T("Package name to bring back (for example com.android.calculator2):", "Nama pakej yang mahu dipulihkan (contoh com.android.calculator2):"), T("Restore an app", "Pulihkan aplikasi"), "").Trim();
        if (p == "") return;
        if (!PkgName.IsMatch(p)) { MessageBox.Show(T("A package name may only contain letters, digits, dots and underscores.", "Nama pakej hanya boleh mengandungi huruf, nombor, titik dan garis bawah."), Cap); return; }
        Run(T("Restore ", "Pulihkan ") + p, () => Exec(Adb, "shell cmd package install-existing --user 0 " + p, null, true));
    }

    // ---------- unlock guide (the steps depend on the selected brand) ----------
    void BuildUnlock()
    {
        var old = new List<Control>();
        foreach (Control c in unlockPage.Controls) old.Add(c);
        foreach (var c in old) c.Dispose();
        btnList.RemoveAll(b => b.IsDisposed);

        int bi = brandIdx; var p = Prof();
        bool xiaomi = bi == 0, samsung = p.Unlock == null, code = p.Code;
        var card = NewCard(unlockPage, string.Format(T("Unlock the bootloader - {0}", "Unlock bootloader - {0}"), p.Name), 0);
        Note(card, T("Unlocking ERASES ALL DATA and can void the warranty. Back up first. Not every phone or carrier variant can be unlocked.",
                     "Unlock MEMADAM SEMUA DATA dan boleh membatalkan waranti. Backup dahulu. Tak semua telefon atau varian pengendali boleh di-unlock."));
        Note(card, p.Note);   // the brand's own notes (they used to sit in the status card)
        string[] steps;
        if (xiaomi) steps = new[] {
            T("Sign in to a Mi account on the phone and add a phone number (Settings > Mi Account).", "Log masuk akaun Mi pada telefon dan tambah nombor telefon (Settings > Mi Account)."),
            T("Turn on Developer options (tap the MIUI / HyperOS version 7 times), then Developer options > Mi Unlock status > Add account and device.", "Hidupkan Developer options (tekan versi MIUI / HyperOS 7 kali), kemudian Developer options > Mi Unlock status > Add account and device."),
            T("On the PC, download the official Mi Unlock app (button below) and run the USB driver that comes with it.", "Pada PC, muat turun Mi Unlock app rasmi (butang di bawah) dan jalankan pemacu USB yang disertakan."),
            T("Put the phone in Fastboot (switch off, hold Volume Down + Power), connect USB, then run Mi Unlock and sign in with the same Mi account.", "Letak telefon dalam Fastboot (matikan, tahan Volume Down + Power), sambung USB, kemudian jalankan Mi Unlock dan log masuk dengan akaun Mi yang sama."),
            T("Follow the app. It may ask you to wait up to 30 days; try again after the time it shows. One account can only unlock a few phones per year.", "Ikut app itu. Ia mungkin minta tunggu sehingga 30 hari; cuba lagi selepas masa yang ditunjukkan. Satu akaun hanya boleh unlock beberapa telefon setahun."),
            T("The phone is erased and restarts. Turn USB debugging back on, then open the Pre-flight check tab.", "Telefon dipadam dan dimulakan semula. Hidupkan semula USB debugging, kemudian buka tab Semakan pra-flash.") };
        else if (samsung) steps = new[] {
            T("Samsung uses Odin and Download mode, not fastboot, so Flash Deck cannot unlock it.", "Samsung guna Odin dan mod Download, bukan fastboot, jadi Flash Deck tak boleh unlock-nya."),
            T("On the phone: Developer options > OEM unlocking ON, then follow a guide made for your exact Samsung model.", "Pada telefon: Developer options > OEM unlocking ON, kemudian ikut panduan untuk model Samsung awak.") };
        else if (code) steps = new[] {
            T("Turn on Developer options, then 'OEM unlocking' and 'USB debugging'.", "Hidupkan Developer options, kemudian 'OEM unlocking' dan 'USB debugging'."),
            T("Get your unlock data from the manufacturer (the button below searches for their official page) and request an unlock code.", "Dapatkan data unlock daripada pengilang (butang di bawah mencari halaman rasmi mereka) dan minta kod unlock."),
            T("Press 'Reboot to Bootloader', then 'Unlock bootloader' and enter the code when asked.", "Tekan 'Reboot ke Bootloader', kemudian 'Unlock bootloader' dan masukkan kod bila diminta."),
            T("The phone is erased and restarts. Turn USB debugging back on, then open the Pre-flight check tab.", "Telefon dipadam dan dimulakan semula. Hidupkan semula USB debugging, kemudian buka tab Semakan pra-flash.") };
        else steps = new[] {
            T("Turn on Developer options (tap the build number 7 times).", "Hidupkan Developer options (tekan build number 7 kali)."),
            T("In Developer options turn ON 'OEM unlocking' (some phones need internet; it can be greyed out on carrier-locked phones) and 'USB debugging'.", "Dalam Developer options hidupkan 'OEM unlocking' (sesetengah telefon perlu internet; ia boleh kelabu pada telefon terkunci pengendali) dan 'USB debugging'."),
            T("Connect USB, tap Allow on the phone, then press 'Reboot to Bootloader' below.", "Sambung USB, tekan Allow pada telefon, kemudian tekan 'Reboot ke Bootloader' di bawah."),
            T("When the top card says Fastboot mode, press 'Unlock bootloader' and confirm on the phone with the volume and power keys.", "Bila kad atas tunjuk Mod Fastboot, tekan 'Unlock bootloader' dan sahkan pada telefon dengan butang volume dan kuasa."),
            T("The phone is erased and restarts. Turn USB debugging back on, then open the Pre-flight check tab.", "Telefon dipadam dan dimulakan semula. Hidupkan semula USB debugging, kemudian buka tab Semakan pra-flash.") };
        for (int i = 0; i < steps.Length; i++) Note(card, (i + 1) + ".  " + steps[i]);
        if (xiaomi) Note(card, T("Flash Deck's own 'Unlock bootloader' button usually fails on Xiaomi, so use the Mi Unlock app.", "Butang 'Unlock bootloader' Flash Deck biasanya gagal pada Xiaomi, jadi guna Mi Unlock app."));
        if (samsung) return;

        var row = ToolRow(card); row.Margin = new Padding(0, 10, 0, 0);
        if (xiaomi) row.Controls.Add(Btn(T("Open Mi Unlock page", "Buka halaman Mi Unlock"), "", () => OpenUrl("https://en.miui.com/unlock/download_en.html"), 0, 200));
        else if (code) row.Controls.Add(Btn(T("Find official unlock page", "Cari halaman unlock rasmi"), "", () => OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(p.Name + " official bootloader unlock")), 0, 220));
        row.Controls.Add(Btn(T("Reboot to Bootloader", "Reboot ke Bootloader"), "", () => Reboot("bootloader"), 0, 200));
        if (!xiaomi) row.Controls.Add(Btn(T("Unlock bootloader", "Unlock bootloader"), "", Unlock, 2, 200));
        row.Controls.Add(Btn(T("Check status", "Semak status"), "", CheckBootloader, 0, 160));
    }

    void CheckBootloader()
    {
        if (state == "none") { NoPhone(); return; }
        Run(T("Check bootloader status", "Semak status bootloader"), () =>
        {
            string r = "";
            if (state == "fastboot")
            {
                string v = Exec(Fb, "getvar unlocked", null, false, 8000);
                r = v.Contains("unlocked: yes") ? "U" : v.Contains("unlocked: no") ? "L" : "";
            }
            else if (state == "device")
            {
                string vb = Exec(Adb, "shell getprop ro.boot.verifiedbootstate", null, false, 8000).Trim(), lk = Exec(Adb, "shell getprop ro.boot.flash.locked", null, false, 8000).Trim();
                r = vb == "orange" || lk == "0" ? "U" : vb == "green" || lk == "1" ? "L" : "";
            }
            else { Log(T("Switch the phone on (USB debugging allowed) or enter fastboot to check.", "Hidupkan telefon (USB debugging dibenarkan) atau masuk fastboot untuk semak.")); return; }
            Log(r == "U" ? T("Bootloader is UNLOCKED.", "Bootloader sudah UNLOCK.") : r == "L" ? T("Bootloader is LOCKED.", "Bootloader TERKUNCI.") : T("The status could not be read (this phone may not report it).", "Status tak dapat dibaca (telefon ini mungkin tak melaporkannya)."));
        });
    }

    void RestartAdb()
    {
        Run(T("Restart ADB server", "Mula semula pelayan ADB"), () => { Exec(Adb, "kill-server", null, true); Exec(Adb, "start-server", null, true); });
    }

    // ---------- actions ----------
    void NoPhone() { MessageBox.Show(T("No phone detected.", "Tiada telefon dikesan."), Cap); }

    // partition names go straight into a fastboot command line, so allow only letters, digits and underscore
    bool ValidPart(string part)
    {
        if (Regex.IsMatch(part, "^[A-Za-z0-9_]+$")) return true;
        MessageBox.Show(T("A partition name may only contain letters, digits and underscore.", "Nama partition hanya boleh mengandungi huruf, nombor dan garis bawah."), Cap);
        return false;
    }

    // closing mid-flash can leave a multi-step flash half done
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy && MessageBox.Show(T("A task is still running. Closing now can leave the phone half-flashed. Close anyway?", "Tugasan masih berjalan. Menutup sekarang boleh membuat telefon separuh di-flash. Tutup juga?"), Cap, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    void Reboot(string target)
    {
        if (state == "none") { NoPhone(); return; }
        bool fb = state == "fastboot";
        if (fb && target == "recovery") { BootRecovery(); return; } // Xiaomi fastboot can't 'reboot recovery'; boot the image directly
        Run("Reboot " + (target == "" ? T("system", "sistem") : target), () =>
            Exec(fb ? Fb : Adb, "reboot " + target, null, true));
    }

    void FlashRecovery()
    {
        if (!NeedFastboot()) return;
        string f = FindFile("recovery.img") ?? Pick("Image (*.img)|*.img");
        if (f == null) return;
        string part = Prof() == Profiles[0] ? "recovery" : Interaction.InputBox(
            string.Format(T("Partition name (suggested for {0}: {1}; modern A/B devices may need boot or vendor_boot - follow the ROM instructions):",
                            "Nama partition (cadangan untuk {0}: {1}; peranti A/B moden mungkin boot atau vendor_boot - ikut arahan ROM):"), Prof().Name, Prof().Part),
            "Flash recovery", Prof().Part);
        if (part == "" || !ValidPart(part)) return;
        Run("Flash recovery (" + part + "): " + Path.GetFileName(f), () => Exec(Fb, "flash " + part + " \"" + f + "\"", null, true));
    }

    void BootRecovery()
    {
        if (!NeedFastboot()) return;
        string f = FindFile("recovery.img") ?? Pick("Image (*.img)|*.img");
        if (f == null) return;
        Run(T("Boot recovery (temporary, not written to the phone)", "Boot recovery (sementara, tak ditulis ke telefon)"), () => Exec(Fb, "boot \"" + f + "\"", null, true));
    }

    void FlashImg()
    {
        if (!NeedFastboot()) return;
        string f = Pick("Image (*.img)|*.img");
        if (f == null) return;
        string part = Interaction.InputBox(T("Partition name (follow the ROM instructions; usually boot or recovery):", "Nama partition (ikut arahan ROM; biasa: boot atau recovery):"), "Flash", "boot");
        if (part == "" || !ValidPart(part)) return;
        Run("Flash " + part, () => Exec(Fb, "flash " + part + " \"" + f + "\"", null, true));
    }

    // Flash several .img files one after another (partition = file name), stopping at the first failure. Only when the ROM's guide asks for it.
    void FlashMany()
    {
        if (!NeedFastboot()) return;
        var d = new OpenFileDialog { Filter = "Image (*.img)|*.img", Multiselect = true };
        if (Directory.Exists(RomDir)) d.InitialDirectory = RomDir;
        if (d.ShowDialog() != DialogResult.OK || d.FileNames.Length == 0) return;
        var files = d.FileNames;
        var plan = new StringBuilder();
        foreach (var f in files)
        {
            string part = Path.GetFileNameWithoutExtension(f);
            if (!Regex.IsMatch(part, "^[A-Za-z0-9_]+$")) { MessageBox.Show(string.Format(T("'{0}' is not a valid partition name. Rename the file to the partition (e.g. vendor_boot.img).", "'{0}' bukan nama partition yang sah. Namakan semula fail kepada partition (cth. vendor_boot.img)."), part), Cap); return; }
            plan.AppendLine("fastboot flash " + part + "   <-   " + Path.GetFileName(f));
        }
        if (MessageBox.Show(T("Flash these images in this order?", "Flash imej ini mengikut urutan ini?") + "\n\n" + plan + "\n" +
                T("The partition is taken from the file name. Only do this if the ROM's own install guide says so.", "Partition diambil daripada nama fail. Buat ini hanya jika panduan pemasangan ROM itu menyuruh."),
                T("Flash extra images", "Flash imej tambahan"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Run(T("Flash extra images", "Flash imej tambahan"), () =>
        {
            foreach (var f in files)
            {
                string part = Path.GetFileNameWithoutExtension(f);
                Log("-> " + part);
                if (Exec(Fb, "flash " + part + " \"" + f + "\"", null, true).Contains("FAILED")) { Log(T("Stopped: a flash failed.", "Berhenti: satu flash gagal.")); return; }
            }
        });
    }

    void SideloadAuto(string pattern, bool rom = false)
    {
        if (state != "sideload") { MessageBox.Show(T("On the phone: Recovery > Apply update > Apply from ADB, then try again.", "Pada telefon: Recovery > Apply update > Apply from ADB, kemudian cuba semula."), Cap); return; }
        string f = (rom ? FindRom() : FindFile(pattern)) ?? Pick("ROM/add-on (*.zip)|*.zip");
        if (f == null) return;
        if (MessageBox.Show(T("Install this file?", "Pasang fail ini?") + "\n\n" + Path.GetFileName(f), "Sideload", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Run("Sideload " + Path.GetFileName(f), () =>
        {
            Exec(Adb, "sideload \"" + f + "\"", null, true);
            Log(T("Note: stopping at 47% or 'failed to read command' is usually fine. GApps: if the phone says 'Signature verification failed', choose Yes.",
                  "Nota: berhenti pada 47% atau 'failed to read command' biasanya OK. GApps: kalau telefon tanya 'Signature verification failed', pilih Yes."));
        });
    }

    void Md5()
    {
        string f = Pick(T("All files (*.*)|*.*", "Semua fail (*.*)|*.*"));
        if (f == null) return;
        Run("MD5 " + Path.GetFileName(f), () =>
        {
            using (var m = MD5.Create())
            using (var s = File.OpenRead(f))
                Log("MD5: " + BitConverter.ToString(m.ComputeHash(s)).Replace("-", "").ToLower());
        });
    }

    void DeviceInfo()
    {
        if (state == "none") { NoPhone(); return; }
        Run(T("Device info", "Info peranti"), () =>
        {
            if (state == "fastboot") Exec(Fb, "getvar all", null, true, 15000);
            else Exec(Adb, "shell \"getprop ro.product.model; getprop ro.product.device; getprop ro.build.version.release; getprop ro.build.display.id; getprop ro.boot.flash.locked\"", null, true, 15000);
        });
    }

    void ToolVersions()
    {
        Run(T("Tool versions", "Versi alat"), () => { Exec(Adb, "version", null, true, 10000); Exec(Fb, "--version", null, true, 10000); });
    }

    // ---------- phone tools (adb, phone on) ----------
    void InstallApk()
    {
        if (!NeedSystem()) return;
        var d = new OpenFileDialog { Filter = "APK (*.apk)|*.apk" };
        if (d.ShowDialog() != DialogResult.OK) return;
        Run("Install " + Path.GetFileName(d.FileName), () => Exec(Adb, "install -r \"" + d.FileName + "\"", null, true));
    }

    void Screenshot()
    {
        if (!NeedSystem()) return;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Flash Deck");
        Directory.CreateDirectory(dir);
        string dest = Path.Combine(dir, "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
        Run(T("Screenshot", "Tangkapan skrin"), () =>
        {
            if (!Exec(Adb, "shell dumpsys power", null, false, 8000).Contains("mWakefulness=Awake"))
            { Log(T("The phone screen is off or locked - wake it up and try again.", "Skrin telefon mati atau terkunci - hidupkan skrin dan cuba lagi.")); return; }
            Exec(Adb, "shell screencap -p /sdcard/android_tools_shot.png", null, true);
            Exec(Adb, "pull /sdcard/android_tools_shot.png \"" + dest + "\"", null, true);
            Exec(Adb, "shell rm /sdcard/android_tools_shot.png", null, true);
            Log(T("Saved: ", "Disimpan: ") + dest);
            // a real 1080p screenshot is far larger than this; a tiny PNG means a black frame (secure screen or display asleep)
            if (File.Exists(dest) && new FileInfo(dest).Length < 30000)
                Log(T("Warning: the image looks blank. The screen may be off or showing protected content.", "Amaran: gambar nampak kosong. Skrin mungkin mati atau memaparkan kandungan dilindungi."));
        });
    }

    void PushFile()
    {
        if (!NeedSystem()) return;
        var d = new OpenFileDialog();
        if (d.ShowDialog() != DialogResult.OK) return;
        Run("Push " + Path.GetFileName(d.FileName), () => Exec(Adb, "push \"" + d.FileName + "\" /sdcard/Download/", null, true));
    }

    void PullFile()
    {
        if (!NeedSystem()) return;
        string src = Interaction.InputBox(T("Path on the phone (e.g. /sdcard/Download/file.zip):", "Laluan pada telefon (cth. /sdcard/Download/file.zip):"), T("Copy file from phone", "Salin fail dari telefon"), "/sdcard/Download/");
        if (src == "" || src.Contains("\"")) return;
        var d = new FolderBrowserDialog { Description = T("Save to folder", "Simpan ke folder") };
        if (d.ShowDialog() != DialogResult.OK) return;
        string dest = d.SelectedPath.TrimEnd('\\');
        Run("Pull " + src, () => Exec(Adb, "pull \"" + src + "\" \"" + dest + "\"", null, true));
    }

    void Backup()
    {
        if (!NeedSystem()) return;
        string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Flash Deck Backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if (MessageBox.Show(string.Format(T("Copy DCIM, Pictures and Download from the phone to:\n{0}\n\nThis can take a while and needs enough free disk space.",
                                            "Salin DCIM, Pictures dan Download dari telefon ke:\n{0}\n\nIni mungkin ambil masa dan perlukan ruang cakera yang cukup."), dest),
                            T("Backup files", "Backup fail"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Directory.CreateDirectory(dest);
        Run(T("Backup files", "Backup fail"), () =>
        {
            foreach (var f in new[] { "DCIM", "Pictures", "Download" }) Exec(Adb, "pull /sdcard/" + f + " \"" + dest + "\"", null, true);
            Log(T("Saved to: ", "Disimpan ke: ") + dest);
            Log(T("Not included: app data and WhatsApp chats. In WhatsApp: Settings > Chats > Chat backup.", "Tidak termasuk: data app dan chat WhatsApp. Dalam WhatsApp: Settings > Chats > Chat backup."));
        });
    }

    void Battery()
    {
        if (!NeedSystem()) return;
        Run(T("Battery info", "Maklumat bateri"), () =>
        {
            string o = Exec(Adb, "shell dumpsys battery", null, true, 10000);
            Func<string, int> num = k => { var m = Regex.Match(o, @"(?m)^\s*" + k + @":\s*(-?\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : -1; };
            int lvl = num("level"), st = num("status"), hl = num("health"), tp = num("temperature");
            if (lvl < 0) return;
            string status = st == 2 ? T("charging", "mengecas") : st == 5 ? T("full", "penuh") : st == 3 ? T("discharging", "menyahcas") : st == 4 ? T("not charging", "tak mengecas") : "?";
            string health = hl == 2 ? T("good", "baik") : hl == 3 ? T("overheating", "terlalu panas") : hl == 7 ? T("cold", "sejuk") : hl == 4 ? T("dead", "rosak") : T("check it", "semak");
            Log(string.Format(T("Summary: {0}%, {1}, {2:0.0} °C, health {3}", "Ringkasan: {0}%, {1}, {2:0.0} °C, kesihatan {3}"), lvl, status, tp / 10.0, health));
        });
    }

    // ---------- download helpers (open the browser; nothing is downloaded by this app) ----------
    void LineageDownloads()
    {
        if (codename != "")
            MessageBox.Show(string.Format(T("Detected codename: {0}\n\nSearch for this device on the page. LineageOS may list it under a slightly different name.",
                                            "Codename dikesan: {0}\n\nCari peranti ini di halaman itu. LineageOS mungkin senaraikannya dengan nama yang sedikit berbeza."), codename), Cap);
        OpenUrl("https://download.lineageos.org/");
    }

    // opens a web search for the detected phone; Flash Deck does not keep a ROM list for every model
    void FindRoms()
    {
        string q = phoneName != "" ? phoneName : codename != "" ? Prof().Name + " " + codename : "";
        if (q == "") { MessageBox.Show(T("Connect the phone once so it can be identified.", "Sambung telefon sekali supaya dapat dikenal pasti."), Cap); return; }
        OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(q + " custom ROM LineageOS"));
    }

    void StockPage()
    {
        int i = brandIdx;
        if (i == 0 && codename != "") OpenUrl("https://mirom.ezbox.idv.tw/en/phone/" + codename + "/");
        else if (i == 1) OpenUrl("https://developers.google.com/android/images");
        else MessageBox.Show(i == 0 ? T("Connect the phone once so the codename can be detected.", "Sambung telefon sekali supaya codename dapat dikesan.")
                                    : T("No stock-ROM page is known for this brand. Check the manufacturer's support site.", "Tiada halaman ROM asal diketahui untuk jenama ini. Semak laman sokongan pengilang."), Cap);
    }

    void FastbootRom(string bat, string word)
    {
        var d = new FolderBrowserDialog { Description = T("Choose the extracted fastboot ROM folder", "Pilih folder fastboot ROM yang sudah di-extract") };
        if (d.ShowDialog() != DialogResult.OK) { Log(T("Cancelled: no folder chosen.", "Dibatalkan: tiada folder dipilih.")); Nav(PgLog); return; }
        if (!File.Exists(Path.Combine(d.SelectedPath, bat))) bat = bat.Replace('_', '-'); // Pixel uses flash-all.bat
        if (!File.Exists(Path.Combine(d.SelectedPath, bat))) { MessageBox.Show(bat + T(" not found in that folder.", " tak jumpa dalam folder itu."), Cap); return; }
        if (!NeedFastboot()) return;
        if (word != null && !Confirm(word, T("All data on the phone will be erased.", "Semua data telefon akan dipadam."))) return;
        if (word == null && MessageBox.Show(string.Format(T("This will run {0} from:\n{1}\n\nOnly continue if you trust this folder.", "Ini akan menjalankan {0} dari:\n{1}\n\nTeruskan hanya jika awak percayai folder ini."), bat, d.SelectedPath), Cap, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        Run("Fastboot ROM: " + bat, () => Exec("cmd.exe", "/c " + bat, d.SelectedPath, true));
    }

    void Wipe()
    {
        if (!NeedFastboot() || !Confirm(WipeWord, T("All data on the phone will be erased.", "Semua data telefon akan dipadam."))) return;
        Run(T("Format data", "Format data"), () => Exec(Fb, "-w", null, true));
    }

    void Unlock()
    {
        var p = Prof();
        if (p.Unlock == null) { MessageBox.Show(p.Note, p.Name); return; }
        if (!NeedFastboot() || !Confirm("UNLOCK", T("Unlocking erases all data.", "Unlock memadam semua data.") + "\n\n" + p.Note)) return;
        string code = p.Code ? Interaction.InputBox(T("Enter the unlock code from the manufacturer's website:", "Masukkan kod unlock dari laman pengilang:"), T("Unlock code", "Kod unlock"), "") : "";
        if (p.Code && (code == "" || !Regex.IsMatch(code, "^[A-Za-z0-9]+$")))
        {
            if (code != "") MessageBox.Show(T("The code may only contain letters and digits.", "Kod hanya boleh mengandungi huruf dan nombor."), Cap);
            return;
        }
        string c1 = p.Unlock + (p.Code ? " " + code : "");
        Run("Unlock bootloader (" + p.Unlock + ")", () => TryBoth(c1, p.Alt));
    }

    // run the first command; if it fails or is unknown, try the alternative
    void TryBoth(string first, string alt)
    {
        string o = Exec(Fb, first, null, true);
        if (alt != null && (o.Contains("FAILED") || o.ToLower().Contains("unknown command")))
        {
            Log(T("Failed, trying alternative command: ", "Gagal, cuba arahan alternatif: ") + alt);
            Exec(Fb, alt, null, true);
        }
    }

    void Lock()
    {
        if (!NeedFastboot() || !Confirm("LOCK", T("WARNING: locking the bootloader is only safe AFTER a full flash of the manufacturer's official ROM (Xiaomi: fastboot ROM; Pixel: factory image). With a custom ROM the phone can brick.",
                                                   "AMARAN: lock bootloader hanya selamat SELEPAS flash penuh ROM rasmi pengilang (Xiaomi: fastboot ROM; Pixel: factory image). Dengan ROM custom, telefon boleh brick."))) return;
        var p = Prof();
        if (p.Lock == null) { MessageBox.Show(p.Note, p.Name); return; }
        Run("Lock bootloader (" + p.Lock + ")", () => TryBoth(p.Lock, p.LockAlt));
    }
}
