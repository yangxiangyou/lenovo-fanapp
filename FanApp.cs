// FanApp v4 — in-place theme switch, DPI aware, tray-resident, background WMI
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;

[assembly: AssemblyTitle("散热模式")]
[assembly: AssemblyProduct("散热模式")]
[assembly: AssemblyDescription("联想散热/风扇控制")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace FanApp
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FanForm());
        }
    }

    public class FanForm : Form
    {
        // ---------- theme ----------
        public class Theme
        {
            public Color Bg, Card, CardHover, CardBorder, Title, Text, SubText, AccentSel, ToggleOn;
        }
        static readonly Theme Light = new Theme
        {
            Bg = Color.FromArgb(245, 246, 250),
            Card = Color.White,
            CardHover = Color.FromArgb(249, 250, 253),
            CardBorder = Color.FromArgb(225, 227, 235),
            Title = Color.FromArgb(22, 24, 30),
            Text = Color.FromArgb(35, 38, 46),
            SubText = Color.FromArgb(128, 132, 142),
            AccentSel = Color.FromArgb(0, 102, 255),
            ToggleOn = Color.FromArgb(0, 122, 255)
        };
        static readonly Theme Dark = new Theme
        {
            Bg = Color.FromArgb(30, 32, 37),
            Card = Color.FromArgb(44, 47, 55),
            CardHover = Color.FromArgb(54, 58, 67),
            CardBorder = Color.FromArgb(64, 68, 78),
            Title = Color.FromArgb(242, 244, 248),
            Text = Color.FromArgb(230, 232, 238),
            SubText = Color.FromArgb(162, 166, 176),
            AccentSel = Color.FromArgb(64, 140, 255),
            ToggleOn = Color.FromArgb(64, 150, 255)
        };
        Theme T;
        bool darkMode;
        const string THEME_KEY = @"SOFTWARE\FanApp";

        // ---------- WMI (called ONLY from worker threads) ----------
        static ManagementClass _wmi;
        static readonly object _lock = new object();

        static uint CallWmi(string method, uint? data)
        {
            lock (_lock)
            {
                try
                {
                    if (_wmi == null)
                    {
                        var scope = new ManagementScope(@"root\WMI");
                        scope.Connect();
                        _wmi = new ManagementClass(scope, new ManagementPath("LENOVO_GAMEZONE_DATA"), null);
                    }
                    foreach (ManagementObject o in _wmi.GetInstances())
                    {
                        using (o)
                        {
                            ManagementBaseObject ret;
                            if (data.HasValue)
                            {
                                var inParams = o.GetMethodParameters(method);
                                inParams["Data"] = data.Value;
                                ret = o.InvokeMethod(method, inParams, null);
                            }
                            else
                            {
                                ret = o.InvokeMethod(method, null, null);
                            }
                            if (ret == null) return 0;
                            if (ret["Data"] != null) return Convert.ToUInt32(ret["Data"]);
                            return 0;
                        }
                    }
                }
                catch { }
                return 0;
            }
        }

        // ---------- UI ----------
        System.Windows.Forms.Timer pollTimer;
        ModeCard[] modeCards = new ModeCard[3];
        Label lblTitle;
        Label[] statusVals = new Label[3];
        readonly List<Label> subLabels = new List<Label>();
        readonly List<Label> textLabels = new List<Label>();
        readonly List<Toggle> toggles = new List<Toggle>();
        Panel cards;
        LinkLabel themeLink;
        NotifyIcon tray;
        bool reallyExit = false;

        public FanForm()
        {
            darkMode = LoadThemePref();
            T = darkMode ? Dark : Light;

            Text = "散热模式";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(460, 800);
            BackColor = T.Bg;
            Point savedPos;
            if (LoadWindowPos(out savedPos) && Screen.AllScreens.Any(scr => scr.WorkingArea.Contains(savedPos)))
            {
                StartPosition = FormStartPosition.Manual;
                Location = savedPos;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
            ApplyDarkTitleBar(this, darkMode);

            BuildUi();

            pollTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            pollTimer.Tick += (s, e) => QueueStatusRefresh();
            pollTimer.Start();

            // tray icon
            tray = new NotifyIcon
            {
                Icon = this.Icon ?? SystemIcons.Application,
                Text = "散热模式",
                Visible = true
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => { reallyExit = true; Close(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; Activate(); };

            Shown += (s, e) => { QueueModeRefresh(); QueueStatusRefresh(); };

            OnFormClosingSavePos();
        }

        void OnFormClosingSavePos()
        {
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing || reallyExit)
                    SaveWindowPos();
            };
        }

        void BuildUi()
        {
            var header = new Panel { Location = new Point(0, 0), Size = new Size(460, 64), BackColor = Color.Transparent };
            lblTitle = new Label { Text = "散热模式", ForeColor = T.Title, Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold), Location = new Point(24, 16), AutoSize = true, BackColor = Color.Transparent };
            header.Controls.Add(lblTitle);
            themeLink = new LinkLabel
            {
                Text = darkMode ? "浅色模式" : "深色模式",
                LinkColor = T.SubText,
                ActiveLinkColor = T.AccentSel,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Font = new Font("Microsoft YaHei UI", 9.5f),
                Location = new Point(370, 24),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            themeLink.Click += (s, e) => ToggleTheme();
            header.Controls.Add(themeLink);
            Controls.Add(header);

            Controls.Add(MakeSectionLabel("性能模式", 74));

            cards = new Panel { Location = new Point(20, 104), Size = new Size(420, 150), BackColor = Color.Transparent };
            string[] names = { "安静", "均衡", "野兽" };
            string[] descs = { "低噪音低功耗\n适合日常办公", "平衡噪音与性能\n适合日常使用", "全力性能释放\n适合游戏重载" };
            Color[] accents = { Color.FromArgb(0, 150, 255), Color.FromArgb(0, 180, 120), Color.FromArgb(255, 96, 72) };
            for (int i = 0; i < 3; i++)
            {
                var mc = new ModeCard(i, names[i], descs[i], accents[i], T);
                mc.Click += OnModeClick;
                mc.Location = new Point(i * 142, 0);
                mc.Size = new Size(136, 150);
                cards.Controls.Add(mc);
                modeCards[i] = mc;
            }
            Controls.Add(cards);

            Controls.Add(MakeSectionLabel("实时状态", 272));

            var status = new Panel { Location = new Point(20, 300), Size = new Size(420, 126), BackColor = Color.Transparent };
            MakeStatusRow(status, 0, "CPU 温度", 0);
            MakeStatusRow(status, 42, "风扇转速", 1);
            MakeStatusRow(status, 84, "当前模式", 2);
            Controls.Add(status);

            Controls.Add(MakeSectionLabel("功能开关", 442));

            int y = 472;
            AddFeatureRow(y, "禁用 Win 键", "打游戏时锁定键盘左下角的 Win 键，防误触弹出开始菜单", "SetWinKeyStatus", "GetWinKeyStatus", WmiProbe("IsSupportDisableWinKey") == 1); y += 52;
            AddFeatureRow(y, "禁用触控板", "外接鼠标时锁定触控板，打字时手掌误碰不再乱点", "SetTPStatus", "GetTPStatus", WmiProbe("IsSupportDisableTP") == 1); y += 52;
            AddFeatureRow(y, "键盘灯", "开关键盘背光；部分机型灯效走管家私有接口，可能无效", "SetKeyboardLight", "GetKeyboardLight", WmiProbe("IsSupportLightingFeature") == 2); y += 52;
            AddFeatureRow(y, "G-Sync（自适应刷新率）", "屏幕刷新率跟着帧率走，减少画面撕裂", "SetGSyncStatus", "GetGSyncStatus", WmiProbe("IsSupportGSync") > 0); y += 52;
            AddFeatureRow(y, "极速散热", "风扇短暂全力运转，快速降温", "SetFanCooling", "GetFanCoolingStatus", WmiProbe("IsSupportFanCooling") == 1); y += 52;
            AddFeatureRow(y, "水冷", "外接水冷模块开关（仅带水冷模块的机型）", "SetWaterCoolingStatus", "GetWaterCoolingStatus", WmiProbe("IsSupportWaterCooling") == 1); y += 52;
            if (y + 10 > ClientSize.Height)
                ClientSize = new Size(460, y + 20);
        }

        void AddFeatureRow(int y, string name, string desc, string setMethod, string getMethod, bool supported)
        {
            var nameL = new Label { Text = name, ForeColor = T.Text, Font = new Font("Microsoft YaHei UI", 10.5f), Location = new Point(24, y + 2), AutoSize = true, BackColor = Color.Transparent };
            var descL = new Label { Text = supported ? desc : desc + "（本机不支持，已置灰）", ForeColor = T.SubText, Font = new Font("Microsoft YaHei UI", 8.25f), Location = new Point(24, y + 27), AutoSize = true, BackColor = Color.Transparent };
            textLabels.Add(nameL);
            subLabels.Add(descL);
            Controls.Add(nameL);
            Controls.Add(descL);
            var t = MakeToggle(y, setMethod, getMethod);
            if (!supported) { t.Enabled = false; }
            toggles.Add(t);
            Controls.Add(t);
        }

        Label MakeSectionLabel(string text, int y)
        {
            var l = new Label { Text = text, ForeColor = T.SubText, Font = new Font("Microsoft YaHei UI", 9f), Location = new Point(24, y), AutoSize = true, BackColor = Color.Transparent };
            subLabels.Add(l);
            return l;
        }

        Panel MakeStatusRow(Panel parent, int y, string name, int idx)
        {
            var nameL = new Label { Text = name, ForeColor = T.SubText, Font = new Font("Microsoft YaHei UI", 10.5f), Location = new Point(4, y), Size = new Size(120, 32), AutoSize = false, BackColor = Color.Transparent };
            var valL = new Label { Text = "…", ForeColor = T.Text, Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold), Location = new Point(130, y), Size = new Size(280, 32), AutoSize = false, BackColor = Color.Transparent };
            subLabels.Add(nameL);
            textLabels.Add(valL);
            statusVals[idx] = valL;
            parent.Controls.Add(nameL);
            parent.Controls.Add(valL);
            return parent;
        }

        Toggle MakeToggle(int y, string setMethod, string getMethod)
        {
            var t = new Toggle(T) { Location = new Point(386, y + 2), Size = new Size(50, 28) };
            ThreadPool.QueueUserWorkItem(_ =>
            {
                uint v = CallWmi(getMethod, null);
                BeginInvoke((Action)(() => { t.Checked = v == 1; }));
            });
            t.Toggled += v =>
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    CallWmi(setMethod, v ? 1u : 0u);
                    Thread.Sleep(200);
                    uint real = CallWmi(getMethod, null);
                    BeginInvoke((Action)(() =>
                    {
                        t.Checked = real == 1;
                        if (real != (v ? 1u : 0u)) t.Enabled = false;
                    }));
                });
            };
            return t;
        }

        uint WmiProbe(string method)
        {
            return CallWmi(method, null);
        }

        void OnModeClick(object sender, EventArgs e)
        {
            var mc = (ModeCard)sender;
            uint mode = (uint)(mc.Index + 1);
            for (int i = 0; i < 3; i++) modeCards[i].Selected = (i == mc.Index);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                CallWmi("SetSmartFanMode", mode);
                Thread.Sleep(200);
                BeginInvoke((Action)(() => { QueueModeRefresh(); QueueStatusRefresh(); }));
            });
        }

        void QueueModeRefresh()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                uint m = CallWmi("GetSmartFanMode", null);
                string name = m == 1 ? "安静" : m == 3 ? "野兽" : m == 2 ? "均衡" : "未知";
                uint t = CallWmi("GetIRTemp", null);
                uint f1 = CallWmi("GetFan1Speed", null);
                uint f2 = CallWmi("GetFan2Speed", null);
                BeginInvoke((Action)(() =>
                {
                    for (int i = 0; i < 3; i++) modeCards[i].Selected = (m == (uint)(i + 1));
                    statusVals[2].Text = name;
                    statusVals[0].Text = t + " °C";
                    statusVals[1].Text = f1 + " / " + f2 + " RPM";
                }));
            });
        }

        void QueueStatusRefresh()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                uint t = CallWmi("GetIRTemp", null);
                uint f1 = CallWmi("GetFan1Speed", null);
                uint f2 = CallWmi("GetFan2Speed", null);
                BeginInvoke((Action)(() =>
                {
                    statusVals[0].Text = t + " °C";
                    statusVals[1].Text = f1 + " / " + f2 + " RPM";
                }));
            });
        }

        // ---------- close to tray ----------
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && !reallyExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            if (reallyExit && tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
        }

        // ---------- theme switching (in-place) ----------
        void ToggleTheme()
        {
            darkMode = !darkMode;
            SaveThemePref(darkMode);
            T = darkMode ? Dark : Light;
            ApplyDarkTitleBar(this, darkMode);
            BackColor = T.Bg;

            lblTitle.ForeColor = T.Title;
            themeLink.Text = darkMode ? "浅色模式" : "深色模式";
            themeLink.LinkColor = T.SubText;

            foreach (var l in textLabels) l.ForeColor = T.Text;
            foreach (var l in subLabels) l.ForeColor = T.SubText;
            foreach (var t in toggles) t.SetTheme(T);
            foreach (var mc in modeCards) mc.SetTheme(T);

            Invalidate(true);
        }

        static bool LoadThemePref()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(THEME_KEY))
                return k != null && Convert.ToInt32(k.GetValue("Dark", 0)) == 1;
        }
        static void SaveThemePref(bool dark)
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(THEME_KEY))
                k.SetValue("Dark", dark ? 1 : 0);
        }

        bool LoadWindowPos(out Point pos)
        {
            pos = default(Point);
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(THEME_KEY))
            {
                if (k == null) return false;
                object x = k.GetValue("PosX"), y = k.GetValue("PosY");
                if (x == null || y == null) return false;
                pos = new Point(Convert.ToInt32(x), Convert.ToInt32(y));
                return true;
            }
        }
        void SaveWindowPos()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(THEME_KEY))
            {
                k.SetValue("PosX", Location.X);
                k.SetValue("PosY", Location.Y);
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        static void ApplyDarkTitleBar(Form f, bool dark)
        {
            try
            {
                int on = dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref on, 4);
                DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            }
            catch { }
        }
    }

    // ---------- mode card ----------
    public class ModeCard : Panel
    {
        public int Index { get; private set; }
        bool _selected, _hover;
        Color _accent;
        FanForm.Theme T;
        Label _nameLabel, _descLabel;
        IconBadge _badge;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                _nameLabel.ForeColor = _selected ? _accent : T.Text;
                BackColor = T.Card;
                Invalidate();
            }
        }

        public ModeCard(int idx, string name, string desc, Color accent, FanForm.Theme t)
        {
            Index = idx;
            _accent = accent;
            T = t;
            BackColor = t.Card;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            var badge = new IconBadge(accent, idx) { Location = new Point(16, 18), Size = new Size(34, 34), BackColor = t.Card };
            Controls.Add(badge);

            _nameLabel = new Label { Text = name, Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold), ForeColor = t.Text, Location = new Point(16, 62), AutoSize = true, BackColor = Color.Transparent };
            _descLabel = new Label { Text = desc, Font = new Font("Microsoft YaHei UI", 8.25f), ForeColor = t.SubText, Location = new Point(16, 96), Size = new Size(108, 48), BackColor = Color.Transparent };
            Controls.Add(_nameLabel);
            Controls.Add(_descLabel);
        }

        public void SetTheme(FanForm.Theme t)
        {
            T = t;
            BackColor = T.Card;
            _nameLabel.ForeColor = _selected ? _accent : T.Text;
            _descLabel.ForeColor = T.SubText;
            foreach (Control c in Controls) c.BackColor = T.Card;
            Invalidate(true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            BackColor = _hover && !_selected ? T.CardHover : T.Card;
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(_selected ? _accent : T.CardBorder, _selected ? 2f : 1f))
            using (var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
                g.DrawPath(p, path);
            if (_selected)
            {
                using (var b = new SolidBrush(_accent))
                    g.FillRectangle(b, 12, Height - 6, Width - 24, 3);
            }
        }

        static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // ---------- icon badge ----------
    public class IconBadge : Control
    {
        Color _accent;
        int _idx;
        public IconBadge(Color accent, int idx)
        {
            _accent = accent; _idx = idx;
            DoubleBuffered = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Color.FromArgb(28, _accent)))
                g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
            using (var p = new Pen(_accent, 2f))
            {
                if (_idx == 0)
                {
                    g.DrawArc(p, 8, 10, 18, 14, 150, 200);
                    g.DrawArc(p, 11, 16, 12, 8, 150, 200);
                }
                else if (_idx == 1)
                {
                    g.DrawLine(p, 10, 17, 24, 17);
                    g.DrawLine(p, 13, 12, 21, 12);
                    g.DrawLine(p, 13, 22, 21, 22);
                }
                else
                {
                    g.DrawLines(p, new[] { new Point(19, 6), new Point(12, 18), new Point(17, 18), new Point(15, 28) });
                }
            }
        }
    }

    // ---------- iOS-style toggle ----------
    public class Toggle : Control
    {
        bool _checked;
        float _anim;
        System.Windows.Forms.Timer _animTimer;
        FanForm.Theme T;
        public event Action<bool> Toggled;

        public bool Checked
        {
            get { return _checked; }
            set { _checked = value; Invalidate(); Animate(); }
        }

        public Toggle(FanForm.Theme t)
        {
            T = t;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            _anim = 0f;
            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += (s, e) =>
            {
                float target = _checked ? 1f : 0f;
                _anim += Math.Sign(target - _anim) * 0.2f;
                if (Math.Abs(target - _anim) < 0.02f) { _anim = target; _animTimer.Stop(); }
                Invalidate();
            };
        }

        public void SetTheme(FanForm.Theme t)
        {
            T = t;
            Invalidate();
        }

        void Animate()
        {
            if (!_animTimer.Enabled) _animTimer.Start();
        }

        protected override void OnClick(EventArgs e)
        {
            _checked = !_checked;
            _animTimer.Start();
            base.OnClick(e);
            if (Toggled != null) Toggled(_checked);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            Color off = Color.FromArgb(190, 194, 202);
            Color bg = _anim > 0.5f ? T.ToggleOn : off;
            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, 0, 0, Width - 1, Height - 1);
            float r = Height - 8;
            float x = 4 + (Width - r - 8) * _anim;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, x, 4, r, r);
        }
    }
}
