using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinTool
{
    namespace Properties
    {
        /// <summary>程序图标：启动时用 GDI+ 现场绘制后转成 Icon，无需外部资源文件。</summary>
        internal static class IconProvider
        {
            private static Icon _cached;

            public static Icon AppIcon()
            {
                if (_cached != null) return _cached;
                try { _cached = Render(64).ToIcon(); }
                catch { try { _cached = SystemIcons.Application; } catch { } }
                return _cached;
            }

            public static Bitmap Render(int size)
            {
                Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.Transparent);

                    float pad = size * 0.04f;
                    RectangleF r = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);

                    using (GraphicsPath path = RoundedRect(r, size * 0.24f))
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new Rectangle(0, 0, size, size),
                        Color.FromArgb(58, 140, 246), Color.FromArgb(22, 78, 180), 55f))
                    {
                        g.FillPath(b, path);
                    }

                    // 内部高光
                    using (GraphicsPath path = RoundedRect(new RectangleF(r.X + size * 0.06f, r.Y + size * 0.05f,
                        r.Width - size * 0.12f, r.Height * 0.42f), size * 0.16f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
                        g.FillPath(b, path);

                    // 扳手/工具符号：斜置的“W”形折线，简洁可辨识
                    using (Pen p = new Pen(Color.White, size * 0.10f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        float x0 = size * 0.24f, x1 = size * 0.39f, x2 = size * 0.50f, x3 = size * 0.61f, x4 = size * 0.76f;
                        float yt = size * 0.42f, yb = size * 0.74f;
                        g.DrawLines(p, new PointF[] {
                            new PointF(x0, yt), new PointF(x1, yb), new PointF(x2, yt + size * 0.10f),
                            new PointF(x3, yb), new PointF(x4, yt) });
                    }
                }
                return bmp;
            }

            private static GraphicsPath RoundedRect(RectangleF r, float radius)
            {
                GraphicsPath p = new GraphicsPath();
                float d = radius * 2;
                if (d > r.Width) d = r.Width;
                if (d > r.Height) d = r.Height;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }

            /// <summary>把 Bitmap 转成单尺寸 Icon（利用 Icon.FromHandle + 克隆，避免句柄泄漏）。</summary>
            public static Icon ToIcon(this Bitmap bmp)
            {
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    DestroyIcon(h);
                }
            }

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            private static extern bool DestroyIcon(IntPtr handle);
        }
    }

    internal static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Thread.GetDomain().UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Report(e.ExceptionObject as Exception, "后台线程");
            };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                Report(e.Exception, "界面线程");
            };

            Cfg.Load();

            // 无界面自检：用于自动化验证关机计划链路（不依赖鼠标操作）
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--selftest") { SelfTest(); return; }
            }

            // 单实例提示（不影响 shutdown 计划，仅避免重复操作造成混乱）
            bool created;
            using (Mutex m = new Mutex(true, "WinTool_SingleInstance_9F2A", out created))
            {
                if (!created)
                {
                    DialogResult dr = MessageBox.Show(
                        "WinTool 已经在运行中。\n\n同时开两个窗口容易重复下发关机命令，建议只保留一个。\n\n仍要继续打开新窗口吗？",
                        "WinTool 已在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dr != DialogResult.Yes) return;
                }

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Report(ex, "启动");
                    MessageBox.Show("程序发生异常：\n\n" + ex.Message, "WinTool 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 无界面自检：安排一个 180 秒后的关机再立即撤销，用于确认 shutdown 链路可用。
        /// 绝不留下任何真实的关机计划。
        /// </summary>
        private static void SelfTest()
        {
            StringBuilder sb = new StringBuilder();
            bool admin = Sys.Admin;
            sb.AppendLine("WinTool 自检 " + Version);
            sb.AppendLine("管理员权限 : " + (admin ? "是" : "否"));
            sb.AppendLine("配置文件   : " + Cfg.Path_);

            Res arm = ShutdownOps.Schedule("关机", 180, "WinTool 自检（将自动取消）", false, false);
            sb.AppendLine("安排关机   : " + (arm.Code == 0 ? "成功 (exit 0)" : "失败 (exit " + arm.Code + ") " + arm.All));
            System.Threading.Thread.Sleep(700);

            Res dis = ShutdownOps.Abort();
            sb.AppendLine("撤销计划   : " + (dis.Code == 0 ? "成功 (exit 0)" : "失败 (exit " + dis.Code + ") " + dis.All));
            sb.AppendLine("计划残留   : " + (ShutdownOps.CurrentPlan() == null ? "无" : "仍存在（异常！）"));
            sb.AppendLine("休眠可用   : " + (ShutdownOps.HibernateAvailable() ? "是" : "否"));
            sb.AppendLine("快速启动   : " + (ShutdownOps.FastStartupEnabled() ? "已开启" : "已关闭"));

            System.Collections.Generic.List<NetAdapter> ads = Net.Adapters();
            sb.AppendLine("网卡数量   : " + ads.Count);
            for (int i = 0; i < ads.Count && i < 4; i++)
                sb.AppendLine("  · " + ads[i].Name + "  " + ads[i].Cidr + "  网关 " + ads[i].PrimaryGateway
                    + "  DNS " + string.Join("/", ads[i].Dns.ToArray()));

            sb.AppendLine("DNS 库条数 : " + DnsLib.All().Count);
            sb.AppendLine("BAT 脚本数 : " + BatchLib.All().Count);

            string outFile = Path.Combine(Path.GetDirectoryName(Cfg.Path_), "selftest.txt");
            try { File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false)); }
            catch { }
            Console.WriteLine(sb.ToString());
        }

        private static void Report(Exception ex, string where)        {
            if (ex == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("位置：" + where);
            sb.AppendLine("异常：" + ex.GetType().FullName);
            sb.AppendLine("消息：" + ex.Message);
            sb.AppendLine();
            sb.AppendLine(ex.StackTrace);
            try { Log.Fail("程序异常（" + where + "）", ex.Message); } catch { }
            try
            {
                string dir = Path.GetDirectoryName(Cfg.Path_);
                File.AppendAllText(Path.Combine(dir, "error.log"), sb.ToString() + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
