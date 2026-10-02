using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>统一配色。浅色卡片式布局 + 右侧深色导航栏。</summary>
    internal static class T
    {
        // 基础色
        public static readonly Color SideBg = Color.FromArgb(24, 30, 42);
        public static readonly Color SideBg2 = Color.FromArgb(16, 21, 30);
        public static readonly Color SideText = Color.FromArgb(178, 189, 205);
        public static readonly Color SideTextOn = Color.White;
        public static readonly Color Accent = Color.FromArgb(37, 118, 232);
        public static readonly Color AccentDark = Color.FromArgb(26, 92, 190);
        public static readonly Color ContentBg = Color.FromArgb(246, 248, 251);
        public static readonly Color CardBg = Color.White;
        public static readonly Color Border = Color.FromArgb(224, 229, 236);
        public static readonly Color Text = Color.FromArgb(29, 36, 48);
        public static readonly Color SubText = Color.FromArgb(118, 130, 148);
        public static readonly Color Ok = Color.FromArgb(26, 158, 92);
        public static readonly Color Warn = Color.FromArgb(226, 138, 16);
        public static readonly Color Danger = Color.FromArgb(212, 62, 62);
        public static readonly Color Info = Color.FromArgb(40, 122, 200);
        public static readonly Color Chip = Color.FromArgb(238, 243, 250);

        public const string FontName = "Microsoft YaHei UI";
        public const string MonoName = "Consolas";

        public static Font F(float size) { return F(size, FontStyle.Regular); }
        public static Font F(float size, FontStyle style)
        {
            try { return new Font(FontName, size, style); }
            catch { return new Font(FontFamily.GenericSansSerif, size, style); }
        }
        public static Font M(float size) { return M(size, FontStyle.Regular); }
        public static Font M(float size, FontStyle style)
        {
            try { return new Font(MonoName, size, style); }
            catch { return new Font(FontFamily.GenericMonospace, size, style); }
        }
    }

    /// <summary>
    /// 高 DPI 下字体已随系统缩放，控件尺寸必须同步放大，否则文字会被挤出控件。
    /// 统一在这里做 逻辑像素 -> 实际像素 的换算。
    /// </summary>
    internal static class Dpi
    {
        private static float _scale = 0f;

        public static float Scale
        {
            get
            {
                if (_scale <= 0f)
                {
                    float s = 1f;
                    // 用一个临时窗体获取真实系统 DPI：
                    // Graphics.FromHwnd(IntPtr.Zero) 反映的是虚拟屏幕 DPI，不一定是系统缩放值。
                    try
                    {
                        using (Form probe = new Form())
                        {
                            probe.ShowInTaskbar = false;
                            probe.FormBorderStyle = FormBorderStyle.None;
                            probe.StartPosition = FormStartPosition.Manual;
                            probe.Location = new Point(-3000, -3000);
                            probe.Size = new Size(1, 1);
                            IntPtr h = probe.Handle;
                            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(h))
                                s = g.DpiX / 96f;
                        }
                    }
                    catch { s = 1f; }
                    if (s < 0.5f || s > 6f) s = 1f;
                    _scale = s;
                }
                return _scale;
            }
        }

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        public static Point P(int x, int y) { return new Point(S(x), S(y)); }
        public static Size Sz(int w, int h) { return new Size(S(w), S(h)); }

        /// <summary>逻辑矩形 -> 实际像素矩形。</summary>
        public static Rectangle R(int x, int y, int w, int h)
        {
            return new Rectangle(S(x), S(y), S(w), S(h));
        }
    }

    internal static class Gfx
    {
        public static void AA(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, Rectangle r, Color c, int radius)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, Rectangle r, Color c, int radius)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(c, 1f)) g.DrawPath(pen, p);
        }

        public static void FillStroke(Graphics g, Rectangle r, Color fill, Color stroke, int radius)
        {
            Fill(g, r, fill, radius);
            Stroke(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), stroke, radius);
        }

        /// <summary>截断过长文本并加省略号。</summary>
        public static string Ellipsis(Graphics g, string text, Font f, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (g.MeasureString(text, f).Width <= maxWidth) return text;
            for (int len = text.Length - 1; len > 0; len--)
            {
                string s = text.Substring(0, len) + "…";
                if (g.MeasureString(s, f).Width <= maxWidth) return s;
            }
            return "…";
        }
    }

    /// <summary>扁平圆角主按钮，带悬停/按下反馈。</summary>
    internal class FlatBtn : Button
    {
        public Color BaseColor = T.Accent;
        public Color HoverColor = T.AccentDark;
        public Color DownColor = Color.FromArgb(18, 70, 150);
        public Color TextColor = Color.White;
        public int Radius = 6;
        public bool Outline = false;
        public bool Danger = false;
        private bool _hover, _down;

        public FlatBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.White;
            Font = T.F(9f);
            Cursor = Cursors.Hand;
            Size = new Size(110, 34);
        }

        public void Style(Color baseColor) { BaseColor = baseColor; }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Gfx.AA(pe.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            if (!Enabled) fill = Color.FromArgb(226, 230, 236);
            else if (Outline) fill = _down ? Color.FromArgb(232, 238, 246) : (_hover ? Color.FromArgb(242, 246, 252) : Color.White);
            else if (Danger) fill = _down ? Color.FromArgb(170, 40, 40) : (_hover ? Color.FromArgb(190, 48, 48) : T.Danger);
            else fill = _down ? DownColor : (_hover ? HoverColor : BaseColor);

            if (Outline)
            {
                Color bc = _hover ? BaseColor : T.Border;
                Gfx.FillStroke(pe.Graphics, r, fill, bc, Radius);
            }
            else Gfx.Fill(pe.Graphics, r, fill, Radius);

            Color tc = !Enabled ? Color.FromArgb(150, 158, 170) : (Outline ? (_hover ? BaseColor : T.Text) : TextColor);
            TextRenderer.DrawText(pe.Graphics, Text, Font, r, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>小号圆角标签按钮（如 10分钟 / 114DNS）。同样纯自绘，避免 Button 基类重复绘制文字。</summary>
    internal class ChipBtn : Control
    {
        public Color ChipColor = T.Chip;
        public Color ChipText = T.Text;
        public Color HoverColor = Color.FromArgb(222, 233, 248);
        public string Cap = "";
        private bool _hover;
        public int Radius = 13;

        public ChipBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
            Font = T.F(9f);
            Cursor = Cursors.Hand;
            Size = new Size(80, 27);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Gfx.AA(pe.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Gfx.Fill(pe.Graphics, r, _hover ? HoverColor : ChipColor, Radius);
            TextRenderer.DrawText(pe.Graphics, Cap, Font, r, ChipText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>内嵌的进度/剩余时间条。</summary>
    internal class Bar : Control
    {
        public double Ratio = 0;
        public Color BarColor = T.Accent;
        public int Radius = 8;

        public Bar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Height = 10;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Gfx.AA(pe.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Gfx.Fill(pe.Graphics, r, Color.FromArgb(232, 237, 244), Radius);
            double ratio = Ratio;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            int w = (int)(r.Width * ratio);
            if (w < 2) return;
            Rectangle rf = new Rectangle(0, 0, w, r.Height);
            if (rf.Width < Radius * 2) { using (SolidBrush b = new SolidBrush(BarColor)) pe.Graphics.FillRectangle(b, rf); }
            else Gfx.Fill(pe.Graphics, rf, BarColor, Radius);
        }
    }
}
