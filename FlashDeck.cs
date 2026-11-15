// Flash Deck - GUI for adb/fastboot. Build with build.bat (C# 5, no install needed).
// Language (English default / Bahasa Melayu) and theme (light / dark) are saved in lang.txt / theme.txt.
// Dev switch: start with --page=N (0 guide, 1 tools, 2 check, 3 unlock, 4 log) to open a specific page.
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
        using (var p = Round(new Rectangle(0, 0, Width - 1, Height - 1), 12))
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
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new Pen(Active ? App.Accent : App.Border, Active ? 2f : 1f))
            e.Graphics.DrawRectangle(pen, Active ? 1 : 0, Active ? 1 : 0, Width - (Active ? 3 : 1), Height - (Active ? 3 : 1));
    }
}

class Box : Panel   // bordered panel (status card)
{
    public Box() { BackColor = App.CardBg; ForeColor = App.Ink; }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var pen = new Pen(App.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
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
        using (var p = RBtn.Round(new Rectangle(0, 0, Width - 1, Height - 1), 14))
        using (var br = new LinearGradientBrush(ClientRectangle, Color.FromArgb(59, 130, 246), Color.FromArgb(99, 102, 241), 45f))
            g.FillPath(br, p);
        if (App.IconFont != null)
            TextRenderer.DrawText(g, "", App.IconFont, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
        PageBg = Dark ? Color.FromArgb(24, 31, 46) : Color.FromArgb(241, 245, 249),
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
    Profile Prof() { return Profiles[brand.SelectedIndex]; }

    static readonly string Pt = Path.Combine(Root, "platform-tools");
    static readonly string RomDir = Path.Combine(Root, "Rom");
    static readonly string Adb = Path.Combine(Pt, "adb.exe");
    static readonly string Fb = Path.Combine(Pt, "fastboot.exe");
    public const string Cap = "Flash Deck", Version = "1.0.1";
    static Size WorkArea;   // usable screen size; --wa=WxH overrides it for testing small screens
    const int PgGuide = 0, PgTools = 1, PgCheck = 2, PgUnlock = 3, PgLog = 4;
    static int StartPage = 0;

    Label statusIcon, status, info, hint, hRec, hBoot, hRom, hGapps;
    TextBox log;
    ProgressBar prog;
    FlowLayoutPanel wizPage, toolPage, checkPage, checkList, unlockPage;   // content flows
    Panel wizScroll, toolScroll, checkScroll, unlockScroll, logPage;       // scrolling containers around them
    RBtn navWiz, navTool, navCheck, navUnlock, navLog;
    CheckBox follow;
    ComboBox brand;   // index into Profiles
    Label brandNote, romNote;
    ComboBox romSel;   // index into Roms
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
            if (a.StartsWith("--page=")) int.TryParse(a.Substring(7), out StartPage);
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
        chk("brand xiaomi", ProfileIndex("Xiaomi") == 0);
        chk("brand google", ProfileIndex("Google") == 1);
        chk("brand unknown falls back to Other", ProfileIndex("Acme") == Profiles.Length - 1);
        chk("samsung has no unlock command", Profiles[ProfileIndex("samsung")].Unlock == null);
        chk("lock commands derive from unlock", Profiles[0].Lock == "oem lock" && Profiles[1].LockAlt == "oem lock");
        File.WriteAllText(Path.Combine(Root, "selftest.txt"), r.ToString());
    }

    App()
    {
        Text = Cap;
        Width = Math.Min(1000, WorkArea.Width - 20); Height = Math.Min(900, WorkArea.Height - 20);   // fit small screens (e.g. 1366x768 laptops)
        MinimumSize = new Size(Math.Min(880, Width), Math.Min(600, Height));
        Font = new Font("Segoe UI", 9.5f); StartPosition = FormStartPosition.CenterScreen; BackColor = PageBg; ForeColor = Ink;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        // --- sidebar ---
        var side = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = SideBg };
        side.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Border });   // thin divider
        side.Controls.Add(new Logo { Left = 16, Top = 18 });
        side.Controls.Add(new Label { Text = Cap, Left = 64, Top = 18, AutoSize = true, ForeColor = SideTitle, Font = new Font("Segoe UI", 12.5f, FontStyle.Bold) });
        side.Controls.Add(new Label { Text = "adb · fastboot · v" + Version, Left = 66, Top = 42, AutoSize = true, ForeColor = SideMuted, Font = new Font("Segoe UI", 8.5f) });
        navWiz = NavBtn(T("ROM Guide", "Panduan ROM"), "", 100, PgGuide);
        navTool = NavBtn(T("Tools", "Alat"), "", 148, PgTools);
        navCheck = NavBtn(T("Pre-flight check", "Semakan pra-flash"), "", 196, PgCheck);
        navUnlock = NavBtn(T("Unlock guide", "Panduan unlock"), "", 244, PgUnlock);
        navLog = NavBtn("Log", "", 292, PgLog);
        side.Controls.AddRange(new Control[] { navWiz, navTool, navCheck, navUnlock, navLog });
        follow = new CheckBox { Text = T("Auto-follow phone", "Ikut telefon auto"), Checked = true, AutoSize = true, Left = 16, ForeColor = SideText, BackColor = SideBg };
        var themeBtn = new RBtn { Text = Dark ? T("Light mode", "Mod cerah") : T("Dark mode", "Mod gelap"), Glyph = Dark ? "" : "", Left = 12, Width = 176, Height = 40, LeftText = true, Shift = SideShift, Fill = SideBg, Edge = SideEdge, Fore = SideText, GlyphColor = SideMuted };
        themeBtn.Click += (s, e) => SwitchSetting(ThemeFile, Dark ? "light" : "dark");
        var langBtn = new RBtn { Text = T("Bahasa Melayu", "English"), Glyph = "", Left = 12, Width = 176, Height = 40, LeftText = true, Shift = SideShift, Fill = SideBg, Edge = SideEdge, Fore = SideText, GlyphColor = SideMuted };
        langBtn.Click += (s, e) => SwitchSetting(LangFile, Lang == "ms" ? "en" : "ms");
        side.Controls.AddRange(new Control[] { follow, themeBtn, langBtn });
        side.Resize += (s, e) => { langBtn.Top = side.Height - 56; themeBtn.Top = side.Height - 102; follow.Top = side.Height - 138; };

        // --- content ---
        var content = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(16) };

        var statusCard = new Box { Dock = DockStyle.Top, Height = 136 };
        statusIcon = new Label { Left = 16, Top = 14, Width = 40, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gray,
            Font = IconFont != null ? new Font(IconFont.FontFamily, 22f) : new Font("Segoe UI", 22f), Text = IconFont != null ? "" : "●" };
        status = new Label { Left = 64, Top = 14, AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Ink };
        info = new Label { Left = 66, Top = 44, AutoSize = true, ForeColor = Muted };
        hint = new Label { Left = 66, Top = 72, AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        statusCard.Controls.AddRange(new Control[] { statusIcon, status, info, hint });
        brand = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Top = 36, Width = 240 };
        if (Dark) { brand.FlatStyle = FlatStyle.Flat; brand.BackColor = CardBg; brand.ForeColor = Ink; }
        foreach (var p in Profiles) brand.Items.Add(p.Name);
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
        brand.SelectedIndex = start;
        var brandLbl = new Label { Text = T("Phone brand (auto-detect)", "Jenama telefon (auto-detect)"), Top = 14, AutoSize = true, ForeColor = Muted };
        brandNote = new Label { Left = 66, Top = 102, AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Muted, Font = new Font("Segoe UI", 9f) };
        brand.SelectedIndexChanged += (s, e) => { brandNote.Text = Prof().Note; BuildUnlock(); };
        brandNote.Text = Prof().Note;
        statusCard.Controls.Add(brandLbl); statusCard.Controls.Add(brand); statusCard.Controls.Add(brandNote);
        statusCard.Resize += (s, e) => { brand.Left = statusCard.Width - 260; brandLbl.Left = brand.Left + 2; }; // always on the right

        wizPage = NewPage(out wizScroll); toolPage = NewPage(out toolScroll); checkPage = NewPage(out checkScroll); unlockPage = NewPage(out unlockScroll);
        var pages = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
        pages.Controls.Add(wizScroll); pages.Controls.Add(toolScroll); pages.Controls.Add(checkScroll); pages.Controls.Add(unlockScroll);

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
        var a = NewCard(wizPage, T("Enter Fastboot and recovery", "Masuk Fastboot dan recovery"), 1);
        Row(a, T("Reboot to Bootloader", "Reboot ke Bootloader"), "", () => Reboot("bootloader"), false);
        hRec = Row(a, "Flash recovery.img", "", FlashRecovery, true);
        hBoot = Row(a, T("Boot recovery temporarily", "Boot recovery sementara"), "", BootRecovery, false);
        Row(a, T("Flash extra images (optional)", "Flash imej tambahan (pilihan)"), "", FlashMany, false);
        Note(a, T("Files are looked up in the 'Rom' folder. Use 'Boot temporarily' if Volume Up + Power does not enter recovery.",
                  "Fail dicari dalam folder 'Rom'. Guna 'Boot sementara' kalau Volume Up + Power tak masuk recovery."));
        var b = NewCard(wizPage, T("On the phone (in recovery)", "Pada telefon (dalam recovery)"), 2);
        Note(b, T("Factory Reset > Format data, then Apply update > Apply from ADB.", "Factory Reset > Format data, kemudian Apply update > Apply from ADB."));
        var c = NewCard(wizPage, T("Install ROM, then GApps", "Pasang ROM, kemudian GApps"), 3);
        var selRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        selRow.Controls.Add(new Label { Text = "ROM:", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 6, 8, 0) });
        romSel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        if (Dark) { romSel.FlatStyle = FlatStyle.Flat; romSel.BackColor = CardBg; romSel.ForeColor = Ink; }
        foreach (var ro in Roms) romSel.Items.Add(ro.Name);
        int rs = 0;
        try { if (File.Exists(RomFile)) int.TryParse(File.ReadAllText(RomFile).Trim(), out rs); } catch (Exception) { }
        romSel.SelectedIndex = rs >= 0 && rs < Roms.Length ? rs : 0;
        selRow.Controls.Add(romSel); c.Controls.Add(selRow);
        romNote = new Label { AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Muted, UseMnemonic = false, Margin = new Padding(0, 0, 0, 6), Text = Roms[romSel.SelectedIndex].Note };
        c.Controls.Add(romNote);
        romSel.SelectedIndexChanged += (s, e) =>
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
        Tile(r7, T("Find ROMs for this phone", "Cari ROM untuk telefon ini"), T("Web search for your model", "Carian web untuk model awak"), "", FindRoms, false);
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

        BuildUnlock();
        Nav(StartPage);
        ShowState("none", "");
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (s, e) => Poll();
        timer.Start();
        Shown += (s, e) => { CleanLogs(); Log("Flash Deck v" + Version + " started"); RefreshHints(); Poll(); };
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
        scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg };
        scroll.Controls.Add(flow);
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
        wizScroll.Visible = i == PgGuide; toolScroll.Visible = i == PgTools; checkScroll.Visible = i == PgCheck; unlockScroll.Visible = i == PgUnlock; logPage.Visible = i == PgLog;
        wizScroll.AutoScrollPosition = new Point(0, 0); toolScroll.AutoScrollPosition = new Point(0, 0); checkScroll.AutoScrollPosition = new Point(0, 0);
        var navs = new[] { navWiz, navTool, navCheck, navUnlock, navLog };
        for (int k = 0; k < navs.Length; k++)
        {
            bool on = k == i;
            navs[k].Fill = on ? Accent : SideBg; navs[k].Edge = navs[k].Fill; navs[k].Fore = on ? Color.White : SideText;
            navs[k].GlyphColor = on ? Color.White : SideMuted; navs[k].Invalidate();
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
        return row;
    }

    FlowLayoutPanel ToolCard(Control page, string title) { return ToolRow(NewCard(page, title, 0)); }

    void Note(Control p, string s) { p.Controls.Add(new Label { Text = s, AutoSize = true, MaximumSize = new Size(640, 0), ForeColor = Muted, UseMnemonic = false, Margin = new Padding(0, 4, 0, 2) }); }

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
        r.Controls.Add(Btn(text, glyph, act, primary ? 1 : 0, 230)); r.Controls.Add(h); p.Controls.Add(r);
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

    void Run(string title, Action a)
    {
        if (busy) return;
        busy = true; SetEnabled(false); prog.Visible = true; Nav(PgLog); Log("> " + title);   // show the Log tab when a task starts
        new Thread(() =>
        {
            try { a(); } catch (Exception ex) { Log(T("ERROR: ", "RALAT: ") + ex.Message); }
            busy = false; Ui(() => { SetEnabled(true); prog.Visible = false; RefreshHints(); }); Log(T("Done.", "Selesai.") + "\r\n");
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
            var v = new List<string>();
            foreach (var l in Exec(Adb, "shell \"getprop ro.product.model; getprop ro.build.version.release; getprop ro.product.manufacturer; getprop ro.product.device; getprop ro.build.flavor\"", null, false, 5000).Split('\n'))
                if (l.Trim() != "") v.Add(l.Trim());
            if (v.Count >= 4)
            {
                mfr = v[2]; dev = v[3]; if (v.Count >= 5) rom = v[4];
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
                        brand.SelectedIndex = ProfileIndex(mfr); codename = dev;
                        int cut = detail.IndexOf("  ·"); if (cut > 0) phoneName = detail.Substring(0, cut);
                        try { File.WriteAllText(LastFile, mfr + "\n" + dev + "\n" + phoneName); } catch (Exception) { }
                    }
                    else if (st == "fastboot" && dev != "") codename = dev;
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
                { h = string.Format(T("Done: the phone already runs {0}. Use step 1 only to install a different ROM.", "Siap: telefon sudah guna {0}. Guna langkah 1 hanya untuk pasang ROM lain."), cr); step = 3; }
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
        statusIcon.ForeColor = c; if (IconFont != null) statusIcon.Text = g;
        status.Text = t; info.Text = i; hint.Text = h;
        for (int k = 0; k < wizCards.Count; k++)
        {
            bool on = st != "none" && k == step;
            if (wizCards[k].Active != on) { wizCards[k].Active = on; wizCards[k].Invalidate(); }
        }
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

    // ---------- unlock guide (the steps depend on the selected brand) ----------
    void BuildUnlock()
    {
        var old = new List<Control>();
        foreach (Control c in unlockPage.Controls) old.Add(c);
        foreach (var c in old) c.Dispose();
        btnList.RemoveAll(b => b.IsDisposed);

        int bi = brand.SelectedIndex; var p = Prof();
        bool xiaomi = bi == 0, samsung = p.Unlock == null, code = p.Code;
        var card = NewCard(unlockPage, string.Format(T("Unlock the bootloader - {0}", "Unlock bootloader - {0}"), p.Name), 0);
        Note(card, T("Unlocking ERASES ALL DATA and can void the warranty. Back up first. Not every phone or carrier variant can be unlocked.",
                     "Unlock MEMADAM SEMUA DATA dan boleh membatalkan waranti. Backup dahulu. Tak semua telefon atau varian pengendali boleh di-unlock."));
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
        int i = brand.SelectedIndex;
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
