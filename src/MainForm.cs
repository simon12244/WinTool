using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>侧边栏导航项（自绘，避免 Button 基类重复绘制文字）。</summary>
    internal class NavBtn : Control
    {
        public bool Selected;
        public string Cap = "";
        private bool _hover;
        public Color IconColor = T.SideText;

        public NavBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = T.SideBg;
            Font = T.F(10f);
            Cursor = Cursors.Hand;
            Height = Dpi.S(42);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Gfx.AA(pe.Graphics);
            using (SolidBrush bg = new SolidBrush(T.SideBg)) pe.Graphics.FillRectangle(bg, ClientRectangle);
            if (Selected) Gfx.Fill(pe.Graphics, new Rectangle(6, 3, Width - 12, Height - 6), Color.FromArgb(38, 48, 66), 8);
            else if (_hover) Gfx.Fill(pe.Graphics, new Rectangle(6, 3, Width - 12, Height - 6), Color.FromArgb(30, 38, 54), 8);

            if (Selected)
            {
                using (SolidBrush b = new SolidBrush(T.Accent))
                    pe.Graphics.FillRectangle(b, new Rectangle(0, 10, 3, Height - 20));
            }

            Color tc = Selected ? T.SideTextOn : (_hover ? Color.FromArgb(226, 232, 240) : T.SideText);
            Rectangle tr = new Rectangle(44, 0, Width - 56, Height);
            TextRenderer.DrawText(pe.Graphics, Cap, Font, tr, tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>主窗体：左侧导航 + 右侧内容 + 底部状态栏。</summary>
    internal class MainForm : Form
    {
        private const int SideW = 208;
        private const int StatusH = 34;

        private Panel _side;
        private Panel _content;
        private Panel _status;
        private Panel _header;
        private Label _statusText;
        private Label _statusPlan;
        private Label _statusClock;
        private Label _adminBadge;
        private Label _headerTitle;
        private Label _headerSub;
        private Timer _timer;
        private List<NavBtn> _navs = new List<NavBtn>();
        private Dictionary<string, Page> _pages = new Dictionary<string, Page>();
        private string _current = "";
        private int _sideW = 208;
        private int _contentLogicalW = 900;

        private static readonly string[] PageKeys = new string[] { "shutdown", "net", "dns", "tools", "batch", "log" };
        private static readonly string[] Names = new string[] { "定时关机", "IP 网络信息", "DNS 设置", "系统工具", "BAT 命令库", "操作日志" };

        public MainForm()
        {
            Text = "WinTool 系统工具箱  ·  Windows 原生桌面工具";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Dpi.Sz(1180, 800);
            MinimumSize = Dpi.Sz(900, 620);
            BackColor = T.ContentBg;
            Font = T.F(9.5f);
            DoubleBuffered = true;
            try { Icon = WinTool.Properties.IconProvider.AppIcon(); } catch { }

            FitToScreen();
            BuildSide();
            BuildContent();
            BuildStatus();
            LayoutShell();

            _pages["shutdown"] = new ShutdownPage();
            _pages["net"] = new NetPage();
            _pages["dns"] = new DnsPage();
            _pages["tools"] = new ToolsPage();
            _pages["batch"] = new BatchPage();
            _pages["log"] = new LogPage();

            ShowPage("shutdown");

            Resize += delegate { if (WindowState != FormWindowState.Minimized) { LayoutShell(); OnResized(); } };
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { OnTick(); };
            _timer.Start();

            Log.Info("WinTool 已启动",
                "版本 " + Program.Version + "   用户 " + Net.UserName + "   权限 " + (Sys.Admin ? "管理员" : "普通用户")
                + "   系统 " + Environment.OSVersion.VersionString);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode >= System.Windows.Forms.Keys.D0 && e.KeyCode <= System.Windows.Forms.Keys.D9)
                {
                    int i = e.KeyCode - System.Windows.Forms.Keys.D0 - 1;
                    if (i >= 0 && i < PageKeys.Length) { ShowPage(PageKeys[i]); e.Handled = true; }
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.F5) { RefreshPage(); e.Handled = true; }
            };
        }

        /// <summary>把默认窗口尺寸限制在当前屏幕工作区内，避免在小屏或高缩放下窗口超出屏幕。</summary>
        private void FitToScreen()
        {
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                // 默认窗口大小按设计稿 1180x800 逻辑像素，但不超出屏幕工作区
                int wantW = Dpi.S(1180);
                int wantH = Dpi.S(800);
                int w = Math.Min(wantW, wa.Width - Dpi.S(16));
                int h = Math.Min(wantH, wa.Height - Dpi.S(16));
                if (w < Dpi.S(880)) w = Math.Min(Dpi.S(880), wa.Width - Dpi.S(8));
                if (h < Dpi.S(600)) h = Math.Min(Dpi.S(600), wa.Height - Dpi.S(8));
                Size = new Size(w, h);
                Location = new Point(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2);
            }
            catch { }
        }

        /// <summary>
        /// 主区域采用绝对定位 + 锚定（不用 Dock）：
        /// 停靠控件的 z 序很容易被后续操作打乱，导致页头与内容区的布局互相覆盖。
        /// </summary>
        private void LayoutShell()
        {
            try
            {
                int sw = _side.Width;
                int sH = Dpi.S(StatusH);
                int hH = Dpi.S(76);
                int w = ClientSize.Width - sw;
                int h = ClientSize.Height - hH - sH;
                if (w < 100) w = 100;
                if (h < 100) h = 100;
                _header.Bounds = new Rectangle(sw, 0, w, hH);
                _content.Bounds = new Rectangle(sw, hH, w, h);
                _status.Bounds = new Rectangle(sw, hH + h, w, sH);
                _contentLogicalW = (int)((w - Dpi.S(64)) / Dpi.Scale);
                if (_contentLogicalW < 400) _contentLogicalW = 400;
                if (_contentLogicalW > 1600) _contentLogicalW = 1600;
            }
            catch { }
        }

        private void BuildSide()
        {
            _side = new Panel();
            _side.Dock = DockStyle.Left;
            _side.Width = Dpi.S(SideW);
            _sideW = Dpi.S(SideW);
            _side.BackColor = T.SideBg;
            _side.Paint += delegate(object s, PaintEventArgs e)
            {
                // 顶部品牌区渐变
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(
                    Dpi.R(0, 0, SideW, 96), T.SideBg2, T.SideBg, System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                    e.Graphics.FillRectangle(b, Dpi.R(0, 0, SideW, 96));
            };
            Controls.Add(_side);

            Label brand = new Label();
            brand.Text = "WinTool";
            brand.Font = T.F(17f, FontStyle.Bold);
            brand.ForeColor = Color.White;
            brand.AutoSize = true;
            brand.BackColor = T.SideBg;
            brand.Location = Dpi.P(22, 20);
            _side.Controls.Add(brand);

            Label sub = new Label();
            sub.Text = "Windows 系统工具箱";
            sub.Font = T.F(9f);
            sub.ForeColor = Color.FromArgb(138, 152, 172);
            sub.AutoSize = true;
            sub.BackColor = T.SideBg;
            sub.Location = Dpi.P(24, 52);
            _side.Controls.Add(sub);

            Label hint = new Label();
            hint.Text = "Ctrl+1~6 切换页面";
            hint.Font = T.F(8f);
            hint.ForeColor = Color.FromArgb(96, 110, 130);
            hint.AutoSize = true;
            hint.BackColor = T.SideBg;
            hint.Location = Dpi.P(24, 72);
            _side.Controls.Add(hint);

            int y = Dpi.S(112);
            for (int i = 0; i < PageKeys.Length; i++)
            {
                NavBtn b = new NavBtn();
                b.Cap = "    " + Names[i];
                b.Location = new Point(0, y);
                b.Width = Dpi.S(SideW);
                b.Tag = PageKeys[i];
                b.Click += delegate(object s, EventArgs e) { ShowPage((string)((Control)s).Tag); };
                _side.Controls.Add(b);
                _navs.Add(b);
                y += Dpi.S(44);
            }

            // 底部：权限状态
            _adminBadge = new Label();
            _adminBadge.AutoSize = false;
            _adminBadge.Size = Dpi.Sz(SideW, 30);
            _adminBadge.Location = new Point(0, _side.Height - Dpi.S(92));
            _adminBadge.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            _adminBadge.TextAlign = ContentAlignment.MiddleCenter;
            _adminBadge.Font = T.F(9f);
            _adminBadge.Cursor = Cursors.Hand;
            _adminBadge.Click += delegate
            {
                if (Sys.Admin) return;
                if (Sys.Elevate(null)) Application.Exit();
                else MessageBox.Show(this, "提权被取消，部分功能将不可用。", "提示");
            };
            _side.Controls.Add(_adminBadge);

            Label ver = new Label();
            ver.Text = "版本 " + Program.Version + "   ·   绿色便携版";
            ver.Font = T.F(8f);
            ver.ForeColor = Color.FromArgb(96, 110, 130);
            ver.AutoSize = false;
            ver.TextAlign = ContentAlignment.MiddleCenter;
            ver.Size = Dpi.Sz(SideW, 20);
            ver.Location = new Point(0, _side.Height - Dpi.S(28));
            ver.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            _side.Controls.Add(ver);

            UpdateAdminBadge();
        }

        private void UpdateAdminBadge()
        {
            if (Sys.Admin)
            {
                _adminBadge.Text = "✓  已获得管理员权限";
                _adminBadge.ForeColor = Color.FromArgb(94, 214, 142);
                _adminBadge.Cursor = Cursors.Default;
            }
            else if (Sys.CheckAdminGroup())
            {
                // 管理员账号但未提权：这是最常见的状态，必须明确提示，否则特权功能会报“拒绝访问”
                _adminBadge.Text = "⚠  当前为普通权限 · 点此提权";
                _adminBadge.ForeColor = Color.FromArgb(240, 186, 92);
                _adminBadge.Cursor = Cursors.Hand;
            }
            else
            {
                _adminBadge.Text = "✕  非管理员账号 · 部分功能不可用";
                _adminBadge.ForeColor = Color.FromArgb(200, 120, 120);
                _adminBadge.Cursor = Cursors.Default;
            }
        }

        private void BuildContent()
        {
            _header = new Panel();
            _header.Location = Dpi.P(SideW, 0);
            _header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _header.Height = Dpi.S(76);
            _header.BackColor = Color.White;
            _header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(T.Border))
                    e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };
            Controls.Add(_header);

            _headerTitle = new Label();
            _headerTitle.Font = T.F(15f, FontStyle.Bold);
            _headerTitle.ForeColor = T.Text;
            _headerTitle.AutoSize = true;
            _headerTitle.BackColor = Color.White;
            _headerTitle.Location = Dpi.P(24, 16);
            _header.Controls.Add(_headerTitle);

            _headerSub = new Label();
            _headerSub.Font = T.F(9f);
            _headerSub.ForeColor = T.SubText;
            _headerSub.AutoSize = true;
            _headerSub.BackColor = Color.White;
            _headerSub.Location = Dpi.P(26, 46);
            _header.Controls.Add(_headerSub);

            FlatBtn refresh = new FlatBtn();
            refresh.Text = "刷新 (F5)";
            refresh.Size = new Size(90, 30);
            refresh.Outline = true;
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Location = new Point(_header.Width - Dpi.S(116), Dpi.S(22));
            refresh.Click += delegate { RefreshPage(); };
            _header.Controls.Add(refresh);
            _header.Resize += delegate { refresh.Location = new Point(_header.Width - Dpi.S(116), Dpi.S(22)); };

            _content = new Panel();
            _content.Location = Dpi.P(SideW, 76);
            _content.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _content.BackColor = T.ContentBg;
            _content.AutoScroll = true;
            Controls.Add(_content);
        }

        private void BuildStatus()
        {
            _status = new Panel();
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _status.Height = StatusH;
            _status.BackColor = Color.White;
            _status.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(T.Border)) e.Graphics.DrawLine(p, 0, 0, _status.Width, 0);
                using (SolidBrush b = new SolidBrush(T.Ok)) e.Graphics.FillEllipse(b, new Rectangle(12, 12, 7, 7));
            };
            Controls.Add(_status);

            _statusText = new Label();
            _statusText.Font = T.F(9f);
            _statusText.ForeColor = T.Text;
            _statusText.AutoSize = false;
            _statusText.TextAlign = ContentAlignment.MiddleLeft;
            _statusText.Location = Dpi.P(28, 0);
            _statusText.Size = new Size(620, StatusH);
            _statusText.BackColor = Color.White;
            _statusText.Text = "就绪";
            _status.Controls.Add(_statusText);

            _statusPlan = new Label();
            _statusPlan.Font = T.M(9.5f, FontStyle.Bold);
            _statusPlan.ForeColor = T.Danger;
            _statusPlan.AutoSize = false;
            _statusPlan.TextAlign = ContentAlignment.MiddleRight;
            _statusPlan.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _statusPlan.Size = new Size(330, StatusH);
            _statusPlan.BackColor = Color.White;
            _statusPlan.Text = "";
            _status.Controls.Add(_statusPlan);

            _statusClock = new Label();
            _statusClock.Font = T.M(9.5f);
            _statusClock.ForeColor = T.SubText;
            _statusClock.AutoSize = false;
            _statusClock.TextAlign = ContentAlignment.MiddleRight;
            _statusClock.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _statusClock.Size = new Size(190, StatusH);
            _statusClock.Location = new Point(_status.Width - Dpi.S(210), 0);
            _statusClock.BackColor = Color.White;
            _status.Controls.Add(_statusClock);

            _status.Resize += delegate
            {
                _statusClock.Location = new Point(_status.Width - Dpi.S(210), 0);
                _statusPlan.Location = new Point(_status.Width - Dpi.S(560), 0);
            };

            // 停靠顺序由 z 序决定：先声明底部状态栏，再让页头占用顶部，内容区自动填充剩余空间
            _header.BringToFront();
        }

        public void ShowPage(string key)
        {
            if (!_pages.ContainsKey(key)) return;
            _current = key;
            for (int i = 0; i < _navs.Count; i++)
            {
                _navs[i].Selected = (string)_navs[i].Tag == key;
                _navs[i].Invalidate();
            }

            Page p = _pages[key];
            _headerTitle.Text = p.Title;
            _headerSub.Text = p.Subtitle;
            RebuildCurrent();
            ResetScroll();
            p.OnShow();
            SetStatus("已进入：" + p.Title + (Sys.Admin ? "" : "    ｜ 当前为普通权限，修改 DNS / 关机 / 清理等操作会提示提权"));
        }

        private Timer _resizeTimer;

        private void OnResized()
        {
            if (_resizeTimer == null)
            {
                _resizeTimer = new Timer();
                _resizeTimer.Interval = 350;
                _resizeTimer.Tick += delegate
                {
                    _resizeTimer.Stop();
                    RebuildCurrent();
                    Page p;
                    if (_pages.TryGetValue(_current, out p)) { try { p.OnShow(); } catch { } }
                };
            }
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void RefreshPage()
        {
            Page p;
            if (_pages.TryGetValue(_current, out p))
            {
                p.OnShow();
                SetStatus("已刷新：" + p.Title);
            }
        }

        public void SetStatus(string text)
        {
            if (_statusText != null) _statusText.Text = text;
        }

        /// <summary>当前内容区可用的“逻辑宽度”，页面据此排版，保证任何 DPI 下都不出现横向滚动。</summary>
        public int ContentLogicalWidth(Form f)
        {
            return _contentLogicalW;
        }

        private void RebuildCurrent()
        {
            Page p;
            if (!_pages.TryGetValue(_current, out p)) return;
            _content.SuspendLayout();
            _content.Controls.Clear();
            _content.AutoScrollMinSize = new Size(0, 0);
            _content.AutoScrollPosition = new Point(0, 0);
            p.Init(this, _content);
            ApplyScale(_content);
            _content.ResumeLayout();
            ResetScroll();
        }

        /// <summary>把内容区滚回顶部。AutoScrollPosition 需要用不同的值触发一次才会真正归零。</summary>
        private void ResetScroll()
        {
            try
            {
                _content.AutoScrollPosition = new Point(0, 1);
                _content.AutoScrollPosition = new Point(0, 0);
            }
            catch { }
        }

        /// <summary>
        /// 页面代码全部按“逻辑像素”编写，这里在任何 DPI 下统一换算一次，
        /// 保证控件尺寸与已被 GDI+ 按 DPI 放大的字形相匹配。
        /// </summary>
        private static void ApplyScale(Control root)
        {
            if (Dpi.Scale <= 1.01f) return;
            for (int i = 0; i < root.Controls.Count; i++) ScaleOne(root.Controls[i]);
        }

        private static void ScaleOne(Control c)
        {
            c.Location = Dpi.P(c.Location.X, c.Location.Y);
            c.Size = Dpi.Sz(c.Width, c.Height);
            ListView lv = c as ListView;
            if (lv != null)
                for (int i = 0; i < lv.Columns.Count; i++) lv.Columns[i].Width = Dpi.S(lv.Columns[i].Width);
            ListBox lb = c as ListBox;
            if (lb != null) { lb.ItemHeight = Dpi.S(lb.ItemHeight); lb.IntegralHeight = false; }
            if (c.HasChildren)
                for (int i = 0; i < c.Controls.Count; i++) ScaleOne(c.Controls[i]);
        }

        private void OnTick()
        {
            // 状态栏
            if (_statusClock != null) _statusClock.Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
            PlanItem plan = ShutdownOps.CurrentPlan();
            if (_statusPlan != null)
            {
                string t = "";
                if (plan != null) t = "⏻  " + plan.Kind + "  " + Sys.FmtHM(plan.Remain);
                else if (ShutdownOps.IsWatching) t = "◎  监控中：" + ShutdownOps.WatchTarget;
                _statusPlan.Text = t;
            }

            Page p;
            if (_pages.TryGetValue(_current, out p))
            {
                try { p.Tick(); }
                catch { }
            }
        }

        public void ShowTextWindow(string title, string content)
        {
            using (Form f = new Form())
            {
                f.Text = title;
                f.Size = Dpi.Sz(880, 660);
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Color.White;
                f.Font = T.F(9f);
                f.MinimizeBox = true;

                TextBox tb = Ui.Multi(12, 12, 780, 500);
                tb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                tb.Text = content == null ? "" : content;
                tb.SelectionStart = 0;
                f.Controls.Add(tb);

                FlatBtn copy = Ui.Btn("复制全部", 12, 524, 100, 32, T.Accent, null);
                copy.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
                copy.Click += delegate { Sys.Copy(tb.Text); MessageBox.Show(f, "已复制到剪贴板。", "提示"); };
                f.Controls.Add(copy);

                FlatBtn save = Ui.Btn("保存为文本", 122, 524, 120, 32, T.Text, null, true);
                save.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
                save.Click += delegate
                {
                    try
                    {
                        string p = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                            "WinTool_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                        System.IO.File.WriteAllText(p, tb.Text, Encoding.UTF8);
                        MessageBox.Show(f, "已保存到桌面：\n" + p, "完成");
                    }
                    catch (Exception ex) { MessageBox.Show(f, ex.Message, "保存失败"); }
                };
                f.Controls.Add(save);

                FlatBtn close = Ui.Btn("关闭", 692, 524, 100, 32, T.Text, null, true);
                close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                close.Click += delegate { f.Close(); };
                f.Controls.Add(close);

                f.ShowDialog(this);
            }
        }
    }
}
