using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>界面组件工厂：统一卡片、标题、输入控件的风格。</summary>
    internal static class Ui
    {
        public static int Gap(int v) { return v; }

        public static Label Text(string text, int x, int y, float size, Color color, bool bold)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            l.BackColor = Color.White;
            l.ForeColor = color;
            l.Font = T.F(size, bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }

        public static Label Title(string text, int x, int y) { return Text(text, x, y, 13.5f, T.Text, true); }

        public static Label Sub(string text, int x, int y) { return Text(text, x, y, 8.5f, T.SubText, false); }

        /// <summary>白色圆角卡片。</summary>
        public static Panel Card(int x, int y, int w, int h)
        {
            Panel p = new Panel();
            p.Location = new Point(x, y);
            p.Size = new Size(w, h);
            p.BackColor = Color.White;
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Gfx.AA(e.Graphics);
                Gfx.FillStroke(e.Graphics, new Rectangle(0, 0, p.Width - 1, p.Height - 1), T.CardBg, T.Border, 8);
            };
            return p;
        }

        /// <summary>带左侧色条的提示条。</summary>
        public static Panel Note(int x, int y, int w, string text, Color color, int height)
        {
            if (height <= 0) height = 54;
            Panel p = new Panel();
            p.Location = new Point(x, y);
            p.Size = new Size(w, height);
            p.BackColor = Color.FromArgb(250, 251, 253);
            Color bg = Color.FromArgb(20, color);
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Gfx.AA(e.Graphics);
                Gfx.Fill(e.Graphics, new Rectangle(0, 0, p.Width - 1, p.Height - 1), bg, 8);
                using (SolidBrush b = new SolidBrush(color))
                    e.Graphics.FillRectangle(b, new Rectangle(0, 6, 4, p.Height - 13));
            };
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(14, 0);
            l.Size = new Size(w - 28, height);
            l.BackColor = Color.FromArgb(250, 251, 253);
            l.ForeColor = Color.FromArgb(70, 80, 95);
            l.Font = T.F(8.5f);
            l.TextAlign = ContentAlignment.MiddleLeft;
            p.Controls.Add(l);
            return p;
        }

        public static TextBox Input(int x, int y, int w)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, 26);
            t.Font = T.F(9f);
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Color.White;
            t.ForeColor = T.Text;
            return t;
        }

        public static TextBox Multi(int x, int y, int w, int h)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, h);
            t.Font = T.M(8.5f);
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Both;
            t.WordWrap = false;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Color.FromArgb(252, 253, 255);
            t.ForeColor = T.Text;
            t.ReadOnly = true;
            return t;
        }

        public static ComboBox Combo(int x, int y, int w)
        {
            ComboBox c = new ComboBox();
            c.Location = new Point(x, y);
            c.Size = new Size(w, 26);
            c.Font = T.F(8.5f);
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.White;
            return c;
        }

        public static CheckBox Check(string text, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Location = new Point(x, y);
            c.AutoSize = true;
            c.Font = T.F(8.5f);
            c.ForeColor = T.Text;
            c.BackColor = Color.White;
            c.FlatStyle = FlatStyle.Flat;
            return c;
        }

        public static NumericUpDown Num(int x, int y, int w, int min, int max, int val)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Size = new Size(w, 26);
            n.Font = T.F(9f);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = val;
            n.BorderStyle = BorderStyle.FixedSingle;
            return n;
        }

        public static FlatBtn Btn(string text, int x, int y, int w, EventHandler onClick)
        {
            return Btn(text, x, y, w, 34, T.Accent, onClick);
        }

        public static FlatBtn Btn(string text, int x, int y, int w, int h, Color color, EventHandler onClick, bool outline)
        {
            FlatBtn b = Btn(text, x, y, w, h, color, onClick);
            b.Outline = outline;
            return b;
        }

        public static FlatBtn Btn(string text, int x, int y, int w, int h, Color color, EventHandler onClick)
        {
            FlatBtn b = new FlatBtn();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.BaseColor = color;
            b.HoverColor = Darken(color, 0.12);
            b.DownColor = Darken(color, 0.22);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static FlatBtn Ghost(string text, int x, int y, int w, EventHandler onClick)
        {
            FlatBtn b = new FlatBtn();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 34);
            b.Outline = true;
            b.BaseColor = T.Accent;
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static ChipBtn Chip(string text, int x, int y, int w, Color color, EventHandler onClick)
        {
            ChipBtn c = new ChipBtn();
            c.Cap = text;
            c.Location = new Point(x, y);
            c.Size = new Size(w, 26);
            c.ChipColor = Color.FromArgb(28, color);
            c.ChipText = Darken(color, 0.25);
            c.HoverColor = Color.FromArgb(52, color);
            c.Tag = color;
            if (onClick != null) c.Click += onClick;
            return c;
        }

        public static Color Darken(Color c, double amount)
        {
            int r = (int)(c.R * (1 - amount));
            int g = (int)(c.G * (1 - amount));
            int b = (int)(c.B * (1 - amount));
            return Color.FromArgb(r < 0 ? 0 : r, g < 0 ? 0 : g, b < 0 ? 0 : b);
        }

        public static Color Lighten(Color c, double amount)
        {
            int r = (int)(c.R + (255 - c.R) * amount);
            int g = (int)(c.G + (255 - c.G) * amount);
            int b = (int)(c.B + (255 - c.B) * amount);
            return Color.FromArgb(r > 255 ? 255 : r, g > 255 ? 255 : g, b > 255 ? 255 : b);
        }

        /// <summary>键值行标签（用于信息网格）。</summary>
        public static Label KeyLabel(string text, int x, int y, int w)
        {
            Label l = Text(text, x, y, 9f, T.SubText, false);
            l.MaximumSize = new Size(w, 0);
            return l;
        }

        public static Label ValLabel(string text, int x, int y, int w, bool mono)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = false;
            l.Size = new Size(w, 20);
            l.BackColor = Color.White;
            l.ForeColor = T.Text;
            l.Font = mono ? T.M(9f) : T.F(9f, FontStyle.Bold);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.AutoEllipsis = true;
            return l;
        }

        public static void Separator(Panel host, int x, int y, int w)
        {
            Panel p = new Panel();
            p.Location = new Point(x, y);
            p.Size = new Size(w, 1);
            p.BackColor = T.Border;
            host.Controls.Add(p);
        }

        /// <summary>把刚创建的卡片挂到父容器上。</summary>
        public static Panel Put(Panel card, Control parent)
        {
            parent.Controls.Add(card);
            return card;
        }

        /// <summary>在卡片左上角绘制一个矢量小图标（无需外部图片资源）。</summary>
        public static void DrawIcon(Control host, int x, int y, int size, string kind, Color color)
        {
            host.Paint += delegate(object s, PaintEventArgs e)
            {
                Gfx.AA(e.Graphics);
                int cx = x + size / 2;
                int cy = y + size / 2;
                int r = size / 2 - 1;
                using (Pen p = new Pen(color, 2f))
                using (SolidBrush b = new SolidBrush(color))
                {
                    p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    switch (kind)
                    {
                        case "power":
                            e.Graphics.DrawArc(p, x + 2, y + 3, size - 4, size - 4, -60, 300);
                            e.Graphics.DrawLine(p, cx, y, cx, cy - 1);
                            break;
                        case "restart":
                            e.Graphics.DrawArc(p, x + 1, y + 1, size - 2, size - 2, 40, 280);
                            e.Graphics.FillPolygon(b, new Point[] {
                                new Point(x + size - 1, y + 2), new Point(x + size - 8, y + 1), new Point(x + size - 3, y + 9) });
                            break;
                        case "sleep":
                            e.Graphics.DrawArc(p, x + 4, y + 1, size - 4, size - 4, 90, 180);
                            e.Graphics.DrawArc(p, x + 1, y + 4, size - 6, size - 8, 270, 180);
                            break;
                        case "hourglass":
                            e.Graphics.DrawLine(p, x + 3, y + 2, x + size - 3, y + 2);
                            e.Graphics.DrawLine(p, x + 3, y + size - 2, x + size - 3, y + size - 2);
                            e.Graphics.DrawLine(p, x + 4, y + 3, x + size - 4, y + 3);
                            e.Graphics.DrawLine(p, x + 3, y + 3, cx, cy);
                            e.Graphics.DrawLine(p, x + size - 3, y + 3, cx, cy);
                            e.Graphics.DrawLine(p, x + 3, y + size - 3, cx, cy);
                            e.Graphics.DrawLine(p, x + size - 3, y + size - 3, cx, cy);
                            break;
                        case "clock":
                            e.Graphics.DrawEllipse(p, x + 1, y + 1, size - 2, size - 2);
                            e.Graphics.DrawLine(p, cx, cy, cx, cy - r + 3);
                            e.Graphics.DrawLine(p, cx, cy, cx + r - 4, cy + 2);
                            break;
                        case "watch":
                            e.Graphics.DrawEllipse(p, x + 2, y + 3, size - 4, size - 4);
                            e.Graphics.DrawEllipse(p, x + size / 2 - 3, y, 6, 4);
                            e.Graphics.DrawLine(p, cx, cy, cx + 3, cy + 3);
                            e.Graphics.DrawLine(p, cx, cy, cx - 2, cy - 3);
                            break;
                        case "globe":
                            e.Graphics.DrawEllipse(p, x + 1, y + 1, size - 2, size - 2);
                            e.Graphics.DrawLine(p, x + 1, cy, x + size - 1, cy);
                            e.Graphics.DrawEllipse(p, cx - r / 2, y + 1, r, size - 2);
                            break;
                        case "shield":
                            Point[] pts = new Point[] {
                                new Point(cx, y), new Point(x + size - 2, y + 4), new Point(x + size - 3, y + size - 5),
                                new Point(cx, y + size), new Point(x + 3, y + size - 5), new Point(x + 2, y + 4) };
                            e.Graphics.DrawPolygon(p, pts);
                            e.Graphics.DrawLine(p, cx - 4, cy, cx - 1, cy + 4);
                            e.Graphics.DrawLine(p, cx - 1, cy + 4, cx + 5, cy - 4);
                            break;
                        case "tools":
                            e.Graphics.DrawLine(p, x + 3, y + size - 3, cx + 3, cy);
                            e.Graphics.DrawEllipse(p, cx + 1, y + 1, 8, 8);
                            e.Graphics.DrawLine(p, x + size - 4, y + 3, cx - 1, cy + 2);
                            break;
                        case "code":
                            e.Graphics.DrawLine(p, x + 7, y + 4, x + 2, cy);
                            e.Graphics.DrawLine(p, x + 2, cy, x + 7, y + size - 4);
                            e.Graphics.DrawLine(p, x + size - 7, y + 4, x + size - 2, cy);
                            e.Graphics.DrawLine(p, x + size - 2, cy, x + size - 7, y + size - 4);
                            break;
                        case "list":
                            for (int i = 0; i < 3; i++)
                            {
                                int yy = y + 3 + i * 7;
                                e.Graphics.FillEllipse(b, x + 2, yy, 3, 3);
                                e.Graphics.DrawLine(p, x + 8, yy + 1, x + size - 2, yy + 1);
                            }
                            break;
                        case "info":
                            e.Graphics.DrawEllipse(p, x + 1, y + 1, size - 2, size - 2);
                            e.Graphics.FillEllipse(b, cx - 1, y + 5, 3, 3);
                            e.Graphics.DrawLine(p, cx, y + 10, cx, y + size - 5);
                            break;
                        default:
                            e.Graphics.DrawEllipse(p, x + 1, y + 1, size - 2, size - 2);
                            break;
                    }
                }
            };
        }
    }

    /// <summary>灵活的时间文本解析：22:30 / 明天 6:00 / 2025-06-01 23:00 / +30m / 90s。</summary>
    internal static class TimeParse
    {
        public static bool TryParse(string input, out DateTime target, out string error)
        {
            target = DateTime.MinValue;
            error = "";
            string s = (input == null ? "" : input.Trim());
            if (s.Length == 0) { error = "请输入时间，例如 22:30、明天 06:00 或 +45m。"; return false; }

            // 相对时间：+30m / +2h / 90s / 45
            if (s[0] == '+')
            {
                string body = s.Substring(1).Trim().ToLowerInvariant();
                double mult = 60;
                if (body.EndsWith("m")) { body = body.Substring(0, body.Length - 1); mult = 60; }
                else if (body.EndsWith("h")) { body = body.Substring(0, body.Length - 1); mult = 3600; }
                else if (body.EndsWith("s")) { body = body.Substring(0, body.Length - 1); mult = 1; }
                else if (body.EndsWith("分钟")) { body = body.Substring(0, body.Length - 2); mult = 60; }
                else if (body.EndsWith("小时")) { body = body.Substring(0, body.Length - 2); mult = 3600; }
                double v;
                if (!double.TryParse(body.Trim(), out v) || v <= 0) { error = "相对时间格式不正确，示例：+30m、+2h、+90s。"; return false; }
                target = DateTime.Now.AddSeconds(v * mult);
                return true;
            }

            // 中文相对词
            if (s.IndexOf("秒后") >= 0 || s.IndexOf("分钟后") >= 0 || s.IndexOf("小时后") >= 0)
            {
                int mult = s.IndexOf("秒后") >= 0 ? 1 : (s.IndexOf("分钟后") >= 0 ? 60 : 3600);
                string num = new string(keepDigits(s));
                double v;
                if (!double.TryParse(num, out v) || v <= 0) { error = "请写成“30分钟后”这样的格式。"; return false; }
                target = DateTime.Now.AddSeconds(v * mult);
                return true;
            }

            int dayOffset = 0;
            string body2 = s;
            if (body2.StartsWith("今天")) { dayOffset = 0; body2 = body2.Substring(2).Trim(); }
            else if (body2.StartsWith("明天")) { dayOffset = 1; body2 = body2.Substring(2).Trim(); }
            else if (body2.StartsWith("后天")) { dayOffset = 2; body2 = body2.Substring(2).Trim(); }

            DateTime dt;
            // 完整日期时间
            if (DateTime.TryParse(body2, out dt) && body2.IndexOf(":") >= 0 && body2.Length > 6)
            {
                if (body2.IndexOf("-") < 0 && body2.IndexOf("/") < 0 && body2.IndexOf("月") < 0)
                    dt = DateTime.Today.AddDays(dayOffset).Add(dt.TimeOfDay);
                if (dt <= DateTime.Now && dayOffset == 0 && body2.IndexOf("-") < 0) dt = dt.AddDays(1);
                target = dt;
                return true;
            }

            // 仅时间 HH:mm
            string t = body2.Replace("：", ":").Replace("点", ":").Replace("时", ":").TrimEnd('分');
            int colon = t.IndexOf(':');
            if (colon > 0)
            {
                int hh, mm;
                if (int.TryParse(t.Substring(0, colon).Trim(), out hh) && int.TryParse(t.Substring(colon + 1).Trim(), out mm))
                {
                    if (hh < 0 || hh > 23 || mm < 0 || mm > 59) { error = "小时需在 0-23，分钟需在 0-59 之间。"; return false; }
                    DateTime cand = DateTime.Today.AddDays(dayOffset).AddHours(hh).AddMinutes(mm);
                    if (cand <= DateTime.Now && dayOffset == 0) cand = cand.AddDays(1);
                    target = cand;
                    return true;
                }
            }

            // 纯数字：按 小时 解释（如 22 表示 22:00；小于当前小时则视为明天）
            int only;
            if (int.TryParse(t, out only) && only >= 0 && only <= 23)
            {
                DateTime cand = DateTime.Today.AddDays(dayOffset).AddHours(only);
                if (cand <= DateTime.Now && dayOffset == 0) cand = cand.AddDays(1);
                target = cand;
                return true;
            }

            error = "无法识别的时间格式。\n可用示例：\n· 22:30（今天 22:30，若已过则视为明天）\n· 明天 06:00 / 后天 08:30\n· 2025-06-01 23:00\n· +45m（45 分钟后）/ +2h / 30分钟后";
            return false;
        }

        private static char[] keepDigits(string s)
        {
            System.Collections.Generic.List<char> l = new System.Collections.Generic.List<char>();
            for (int i = 0; i < s.Length; i++)
                if (char.IsDigit(s[i]) || s[i] == '.') l.Add(s[i]);
            return l.ToArray();
        }
    }
}
