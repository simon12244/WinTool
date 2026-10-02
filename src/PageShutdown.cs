using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>页面基类：负责滚动、可见性刷新与定时刷新。</summary>
    internal abstract class Page
    {
        public Panel Host;                 // 由 MainForm 提供的可滚动容器
        public MainForm Form;
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public int ContentWidth = 900;

        public void Init(MainForm f, Panel host)
        {
            Form = f;
            Host = host;
            Build();
        }

        protected abstract void Build();

        public virtual void OnShow() { }
        public virtual void Tick() { }
    }

    // =====================================================================
    //  定时关机
    // =====================================================================
    internal class ShutdownPage : Page
    {
        public override string Title { get { return "定时关机"; } }
        public override string Subtitle
        {
            get { return "设定后即使关闭本工具也依然生效；随时可点“取消关机计划”撤销。"; }
        }

        private Label _planText;
        private Label _planSub;
        private Bar _planBar;
        private Label _stateText;
        private Label _countInput;
        private Label _schedInput;

        protected override void Build()
        {
            // 逻辑宽度：按主窗体实际可用宽度计算，保证任何 DPI / 窗口大小下都不出现横向滚动
            int W = Form.ContentLogicalWidth(Form);
            int x = 24, y = 20;
            int gap = 16;
            int tileW = (W - gap * 2) / 3;
            int tileH = 152;
            int halfW = (W - gap) / 2;
            if (halfW < 300) halfW = 300;
            if (tileW < 240) tileW = 240;

            // ---------- 六宫格快捷操作 ----------
            Panel c1 = Ui.Put(Ui.Card(x, y, tileW, tileH), Host);
            Ui.DrawIcon(c1, 18, 20, 22, "power", T.Accent);
            c1.Controls.Add(Ui.Text("立即关机", 52, 20, 11.5f, T.Text, true));
            c1.Controls.Add(Ui.Sub("强制结束正在运行的程序", 52, 42));
            ChipRow(c1, 16, 76, new string[] { "10 分钟", "30 分钟", "1 小时", "2 小时" },
                new int[] { 10, 30, 60, 120 }, "关机", "s");

            Panel c2 = Ui.Put(Ui.Card(x + tileW + gap, y, tileW, tileH), Host);
            Ui.DrawIcon(c2, 18, 20, 22, "restart", T.Info);
            c2.Controls.Add(Ui.Text("定时重启", 52, 20, 11.5f, T.Text, true));
            c2.Controls.Add(Ui.Sub("重启前会先强制保存并关闭程序", 52, 42));
            ChipRow(c2, 16, 76, new string[] { "5 分钟", "15 分钟", "30 分钟", "1 小时" },
                new int[] { 5, 15, 30, 60 }, "重启", "r");

            Panel c3 = Ui.Put(Ui.Card(x + (tileW + gap) * 2, y, tileW, tileH), Host);
            Ui.DrawIcon(c3, 18, 20, 22, "sleep", Ui.Darken(T.Accent, 0.25));
            c3.Controls.Add(Ui.Text("睡眠 / 休眠 / 锁定", 52, 20, 11.5f, T.Text, true));
            c3.Controls.Add(Ui.Sub("笔记本建议用休眠，几乎不耗电", 52, 42));
            int bx = 16;
            FlatBtn b1 = Ui.Btn("睡眠", bx, 78, (tileW - 44) / 4, 30, T.Accent, delegate { DoPower("睡眠"); });
            FlatBtn b2 = Ui.Btn("休眠", bx + (tileW - 44) / 4 + 6, 78, (tileW - 44) / 4, 30, T.Accent, delegate { DoPower("休眠"); });
            FlatBtn b3 = Ui.Btn("锁定", bx + ((tileW - 44) / 4 + 6) * 2, 78, (tileW - 44) / 4, 30, T.Accent, delegate { DoPower("锁定"); });
            FlatBtn b4 = Ui.Btn("注销", bx + ((tileW - 44) / 4 + 6) * 3, 78, (tileW - 44) / 4, 30, T.Accent, delegate { DoPower("注销"); });
            c3.Controls.Add(b1); c3.Controls.Add(b2); c3.Controls.Add(b3); c3.Controls.Add(b4);

            y += tileH + gap;

            // ---------- 倒计时关机 ----------
            Panel c4 = Ui.Put(Ui.Card(x, y, halfW, 168), Host);
            Ui.DrawIcon(c4, 18, 20, 22, "hourglass", T.Warn);
            c4.Controls.Add(Ui.Text("倒计时关机", 52, 20, 11.5f, T.Text, true));
            c4.Controls.Add(Ui.Sub("输入任意分钟数，点击开始即可", 52, 42));
            _countInput = Ui.Text("60", 20, 80, 11f, T.Text, true);
            _countInput.AutoSize = false;
            _countInput.Size = new Size(70, 26);
            _countInput.TextAlign = ContentAlignment.MiddleCenter;
            _countInput.BackColor = Color.FromArgb(243, 246, 250);
            _countInput.BorderStyle = BorderStyle.FixedSingle;
            _countInput.Tag = "num";
            c4.Controls.Add(_countInput);
            c4.Controls.Add(Ui.Text("分钟后关机", 96, 85, 9.5f, T.SubText, false));
            c4.Controls.Add(Ui.Btn("开始倒计时", 216, 76, 118, 34, T.Accent, delegate { StartCountdown(); }));
            c4.Controls.Add(Ui.Btn("取消", 342, 76, 100, 34, T.Text, delegate { Cancel(); }, true));
            c4.Controls.Add(Ui.Sub("提示：倒计时会显示在底部状态栏，双击可快速取消。", 20, 126));

            // ---------- 指定时刻关机 ----------
            Panel c5 = Ui.Put(Ui.Card(x + halfW + gap, y, halfW, 168), Host);
            Ui.DrawIcon(c5, 18, 20, 22, "clock", Ui.Darken(T.Ok, 0.1));
            c5.Controls.Add(Ui.Text("指定时刻关机", 52, 20, 11.5f, T.Text, true));
            c5.Controls.Add(Ui.Sub("支持 22:30 / 明天 06:00 / 2025-06-01 23:00", 52, 42));
            _schedInput = Ui.Text("23:00", 20, 80, 11f, T.Text, true);
            _schedInput.AutoSize = false;
            _schedInput.Size = new Size(190, 26);
            _schedInput.Font = T.M(11f);
            c5.Controls.Add(_schedInput);
            c5.Controls.Add(Ui.Btn("设定", halfW - 236, 76, 90, 34, T.Ok, delegate { StartAtTime("关机"); }));
            c5.Controls.Add(Ui.Btn("重启", halfW - 138, 76, 90, 34, T.Info, delegate { StartAtTime("重启"); }, true));
            c5.Controls.Add(Ui.Sub("到点前脚本不占用任何资源，关掉本工具也照常执行。", 20, 126));

            y += 212 + gap;

            // ---------- 进程结束关机 ----------
            Panel c6 = Ui.Put(Ui.Card(x, y, halfW, 212), Host);
            Ui.DrawIcon(c6, 18, 20, 22, "watch", T.Info);
            c6.Controls.Add(Ui.Text("程序结束后自动关机", 52, 20, 11.5f, T.Text, true));
            c6.Controls.Add(Ui.Sub("适合“等电影转码/游戏下载/渲染结束后关机”", 52, 42));

            ComboBox proc = Ui.Combo(20, 80, halfW - 240);
            proc.Items.AddRange(new object[] { "点右侧「刷新进程」" });
            proc.Tag = "proc";
            c6.Controls.Add(proc);
            FlatBtn rf = Ui.Btn("刷新进程", halfW - 212, 78, 100, 30, T.Accent, null, true);
            rf.Click += delegate { RefreshProcessList(proc); };
            c6.Controls.Add(rf);
            c6.Controls.Add(Ui.Sub("结束后执行：", 20, 124));
            ComboBox act = Ui.Combo(104, 119, 96);
            act.Items.AddRange(new object[] { "关机", "重启", "睡眠" });
            act.SelectedIndex = 0;
            act.Tag = "watchaction";
            c6.Controls.Add(act);
            FlatBtn go = Ui.Btn("开始监控", 212, 116, 104, 30, T.Accent, null, true);
            go.Click += delegate { StartWatch(proc, act); };
            c6.Controls.Add(go);
            FlatBtn stop = Ui.Btn("停止", 324, 116, 80, 30, T.Text, null, true);
            stop.Click += delegate { ShutdownOps.StopWatch(); Log.Info("已停止进程监控", ""); Form.SetStatus("已停止进程监控"); };
            c6.Controls.Add(stop);

            // ---------- 状态面板 ----------
            Panel c7 = Ui.Put(Ui.Card(x + halfW + gap, y, halfW, 212), Host);
            c7.Controls.Add(Ui.Text("当前状态", 20, 18, 11.5f, T.Text, true));
            _planText = Ui.Text("没有安排关机", 20, 44, 12.5f, T.Text, true);
            _planText.AutoSize = false;
            _planText.Size = new Size(halfW - 44, 24);
            c7.Controls.Add(_planText);
            _planSub = Ui.Sub("", 20, 72);
            _planSub.AutoSize = false;
            _planSub.Size = new Size(halfW - 34, 18);
            c7.Controls.Add(_planSub);
            _planBar = new Bar();
            _planBar.Location = new Point(20, 98);
            _planBar.Size = new Size(halfW - 34, 10);
            c7.Controls.Add(_planBar);
            _stateText = Ui.Sub("", 20, 114);
            _stateText.AutoSize = false;
            _stateText.Size = new Size(halfW - 34, 40);
            c7.Controls.Add(_stateText);

            FlatBtn cancelBtn = Ui.Btn("取消关机计划", 20, 156, 148, 32, T.Danger, delegate { Cancel(); });
            cancelBtn.Danger = true;
            c7.Controls.Add(cancelBtn);
            FlatBtn selfTest = Ui.Btn("自检", 176, 156, 68, 32, T.Text, delegate { SelfTest(); }, true);
            c7.Controls.Add(selfTest);
            FlatBtn tasks = Ui.Btn("系统计划任务", 252, 156, 132, 32, T.Text, delegate { ShowTasks(); }, true);
            c7.Controls.Add(tasks);
            FlatBtn wake = Ui.Btn("唤醒定时器", 392, 156, 106, 32, T.Text, delegate { ShowWake(); }, true);
            c7.Controls.Add(wake);

            y += 212 + 10;

            // ---------- 重要提示 ----------
            string note =
                "重要提示：" + Environment.NewLine +
                "1. 关机/重启由系统自带的 shutdown 命令执行，设定后本工具可以关闭，计划依然有效；想撤销请在本页点“取消关机计划”，或执行 shutdown /a。" + Environment.NewLine +
                "2. 强制关机（/f）会直接结束未保存的程序，可能导致未保存的内容丢失，请先手动保存正在编辑的文档。" + Environment.NewLine +
                "3. 若系统开启了“快速启动”，部分机型上定时关机可能表现异常；可到“系统工具”页关闭快速启动后重试。" + Environment.NewLine +
                "4. 睡眠需要显卡/主板驱动支持；休眠需要系统开启休眠功能（可在“系统工具”页一键开启）。" + Environment.NewLine +
                "5. 公司电脑可能被域策略锁定关机权限，此时命令会报“拒绝访问(5)”，属于正常现象。";
            Host.Controls.Add(Ui.Note(x, y, W, note, T.Warn, 120));

            Host.AutoScrollMinSize = new Size(0, y + 148);
        }

        private void ChipRow(Panel card, int x, int y, string[] labels, int[] minutes, string kind, string flag)
        {
            int bx = x;
            for (int i = 0; i < labels.Length; i++)
            {
                int mins = minutes[i];
                string label = labels[i];
                ChipBtn c = Ui.Chip(label, bx, y, 80, T.Accent, null);
                c.Click += delegate { QuickSchedule(kind, mins, label); };
                card.Controls.Add(c);
                bx += 82;
            }
        }

        private void QuickSchedule(string kind, int minutes, string label)
        {
            if (!Sys.IsAdminOrAsk(Form, "设定定时" + kind + "需要管理员权限。")) return;
            string msg = "WinTool 已安排" + kind;
            Res r = ShutdownOps.Schedule(kind, minutes * 60L, msg, true, false);
            string detail = r.Code == 0
                ? ("已在 " + label + " 后" + kind)
                : ("失败：" + firstLine(r.All));
            Form.SetStatus(detail);
            if (r.Code != 0) Alert("设定" + kind + "失败", detail + "\n\n常见原因：\n· 权限不足或被杀软/域策略拦截\n· 系统正在等待更新重启\n· 已有其他关机计划，可先执行“取消关机计划”");
            Update();
        }

        private void StartCountdown()
        {
            int mins;
            if (!int.TryParse(_countInput.Text.Trim(), out mins) || mins <= 0 || mins > 100000)
            {
                Alert("分钟数不正确", "请输入 1 ~ 100000 之间的整数分钟数。");
                return;
            }
            if (!Sys.IsAdminOrAsk(Form, "设定倒计时关机需要管理员权限。")) return;
            Res r = ShutdownOps.Schedule("关机", mins * 60L, "WinTool 倒计时关机", true, false);
            if (r.Code == 0)
            {
                Form.SetStatus("已设定：" + mins + " 分钟后自动关机");
                Alert("已设定倒计时关机",
                    mins + " 分钟后将自动关机。\n\n· 可以关闭本工具，计划依然有效\n· 想撤销：回到本页点“取消关机计划”\n· 或直接运行：shutdown /a");
            }
            else
            {
                Alert("设定失败", firstLine(r.All) + "\n\n请确认以管理员身份运行本工具。");
            }
            Update();
        }

        private void StartAtTime(string kind)
        {
            DateTime t;
            string err;
            if (!TimeParse.TryParse(_schedInput.Text, out t, out err))
            {
                Alert("时间格式不正确", err);
                return;
            }
            long sec = (long)(t - DateTime.Now).TotalSeconds;
            if (sec < 5) { Alert("时间太近", "距离该时间点不足 5 秒，请换一个时间。"); return; }
            if (sec > 60L * 60 * 24 * 30) { Alert("时间太远", "最多支持 30 天以内。"); return; }
            if (!Sys.IsAdminOrAsk(Form, "设定定时" + kind + "需要管理员权限。")) return;
            Res r = ShutdownOps.Schedule(kind, sec, "WinTool 定时" + kind, true, false);
            if (r.Code == 0)
            {
                Alert("已设定定时" + kind,
                    "将于 " + t.ToString("yyyy-MM-dd HH:mm:ss") + " " + kind + "。\n（约 " + Sys.Fmt(sec) + " 后）\n\n撤销方式：本页“取消关机计划”。");
                Form.SetStatus("已设定 " + t.ToString("MM-dd HH:mm") + " " + kind);
            }
            else Alert("设定失败", firstLine(r.All));
            Update();
        }

        private void DoPower(string kind)
        {
            if (kind != "锁定" && !Sys.IsAdminOrAsk(Form, kind + "需要管理员权限。")) return;
            if (kind == "休眠" && !ShutdownOps.HibernateAvailable())
            {
                DialogResult dr = MessageBox.Show(Form,
                    "当前系统未开启休眠功能（powercfg 报告不可用）。\n\n是否现在开启休眠后再执行？\n（会占用与内存等大的磁盘空间，约 " +
                    "数 GB）", "休眠不可用", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
                ShutdownOps.SetFastStartup(true);
                System.Threading.Thread.Sleep(1200);
            }
            ShutdownOps.Immediate(kind);
            Form.SetStatus("已执行：" + kind);
            Update();
        }

        private void Cancel()
        {
            Res r = ShutdownOps.Abort();
            if (r.Code == 0)
            {
                Form.SetStatus("已取消关机计划");
                Alert("已取消", "系统中的关机计划已撤销，电脑不会自动关机了。");
            }
            else
            {
                // 权限不足会返回“拒绝访问(5)”，要明确区分于“本来就没有计划”
                string all = r.All.Length > 0 ? r.All : "";
                bool denied = all.IndexOf("拒绝访问") >= 0 || all.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0
                           || all.IndexOf("(5)") >= 0;
                if (denied)
                {
                    if (Sys.IsAdminOrAsk(Form, "撤销关机计划被系统拒绝，需要管理员权限。"))
                    {
                        Res r2 = ShutdownOps.Abort();
                        Alert(r2.Code == 0 ? "已取消" : "仍失败",
                            r2.Code == 0 ? "系统中的关机计划已撤销。" : ("撤销仍失败：\n" + (r2.All.Length > 0 ? r2.All : "无返回信息")));
                    }
                }
                else
                {
                    Alert("无需取消", "系统当前没有处于等待中的关机计划。\n（关闭本工具不会取消已设定的关机，必须点“取消关机计划”或执行 shutdown /a）");
                }
            }
            Update();
        }

        private void SelfTest()
        {
            if (!Sys.IsAdminOrAsk(Form, "自检需要管理员权限，以验证关机命令是否可用。")) return;
            Form.SetStatus("正在自检 shutdown 命令...");
            ShutdownOps.Schedule("关机", 180, "WinTool 自检（将自动取消）", false, false);
            System.Threading.Thread.Sleep(800);
            Res a = ShutdownOps.Abort();
            string q = ShutdownOps.SystemShutdownPending() ? "检测到系统处于待重启状态。" : "未检测到待重启状态。";
            Alert("自检结果",
                "· 设置延时关机：" + "已执行（180 秒）\n" +
                "· 撤销关机计划：" + (a.Code == 0 ? "成功，命令链路正常 ✓" : "撤销返回码 " + a.Code) + "\n" +
                "· " + q + "\n\n" +
                "这说明本机的 shutdown 命令可以正常使用，定时功能可用。");
            Form.SetStatus("自检完成");
            Update();
        }

        private void ShowTasks()
        {
            Form.SetStatus("正在读取计划任务...");
            List<string> t = ShutdownOps.ScheduledShutdownTasks();
            StringBuilder sb = new StringBuilder();
            if (t.Count == 0) sb.Append("没有发现与关机相关的第三方计划任务。");
            else
            {
                sb.AppendLine("发现以下与关机相关的计划任务：\n");
                for (int i = 0; i < t.Count; i++) sb.AppendLine("· " + t[i]);
                sb.AppendLine("\n提示：如不再需要，可在“任务计划程序”中删除，或点下方按钮删除本工具创建的任务。");
            }
            sb.AppendLine();
            sb.AppendLine("本工具创建的定时任务（若使用过“计划任务方式”）：");
            sb.AppendLine("· 任务名以 WinTool 开头，可直接删除，不影响 shutdown 方式的定时关机。");
            DialogResult dr = MessageBox.Show(Form, sb.ToString(), "关机相关计划任务",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (dr == DialogResult.Yes) { ShutdownOps.DeleteAllToolTasks(); Form.SetStatus("已清理本工具创建的计划任务"); }
        }

        private void ShowWake()
        {
            string t = ShutdownOps.WakeTimers();
            Alert("唤醒定时器", t.Length > 4000 ? t.Substring(0, 4000) + "\n...(已截断)" : t);
        }

        private void RefreshProcessList(ComboBox c)
        {
            c.Items.Clear();
            List<string> names = new List<string>();
            Process[] ps = Process.GetProcesses();
            for (int i = 0; i < ps.Length; i++)
            {
                try
                {
                    string n = ps[i].ProcessName;
                    if (n.Length > 0 && !names.Contains(n)) names.Add(n);
                }
                catch { }
                finally { try { ps[i].Dispose(); } catch { } }
            }
            names.Sort();
            for (int i = 0; i < names.Count; i++) c.Items.Add(names[i]);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            Form.SetStatus("已刷新进程列表（" + names.Count + " 个）");
        }

        private void StartWatch(ComboBox proc, ComboBox act)
        {
            string name = proc.SelectedItem as string;
            if (string.IsNullOrEmpty(name) || name.StartsWith("（"))
            {
                Alert("请先选择进程", "请点“刷新进程”，然后在下拉框中选择要等待的程序。");
                return;
            }
            if (Process.GetProcessesByName(name).Length == 0)
            {
                Alert("该程序当前没有运行", "“" + name + "”现在没有在运行，无法监控它何时结束。\n请先启动该程序，再回来点“开始监控”。");
                return;
            }
            ShutdownOps.StartWatch(name, act.SelectedIndex, 720);
            Form.SetStatus("正在监控 " + name + "，结束后将执行：" + ShutdownOps.ActionName(act.SelectedIndex));
            Alert("已开始监控", "正在等待“" + name + "”结束。\n\n· 结束后将执行：" + ShutdownOps.ActionName(act.SelectedIndex) + "\n" +
                "· 最长等待 12 小时，超时自动放弃（不会执行任何操作）\n· 本工具需保持运行；可点“停止”取消监控");
            Update();
        }

        private static string firstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "（无返回信息）";
            string[] lines = s.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) if (lines[i].Trim().Length > 0) return lines[i].Trim();
            return s;
        }

        private void Alert(string title, string text)
        {
            MessageBox.Show(Form, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public override void OnShow() { Update(); }

        public override void Tick() { Update(); }

        private void Update()
        {
            if (_planText == null) return;
            PlanItem p = ShutdownOps.CurrentPlan();
            long now = Sys.Now();

            if (p == null)
            {
                _planText.Text = "没有安排关机";
                _planText.ForeColor = T.Text;
                _planSub.Text = "电脑会保持开机状态";
                _planBar.Ratio = 0;
                _planBar.BarColor = T.Border;
            }
            else
            {
                long remain = p.Remain;
                _planText.Text = p.Kind + "倒计时：" + Sys.FmtHM(remain);
                _planText.ForeColor = T.Danger;
                _planSub.Text = "计划时间 " + new DateTime(1970, 1, 1).AddSeconds(p.Deadline).ToLocalTime().ToString("MM-dd HH:mm:ss")
                    + "   ·   " + (string.IsNullOrEmpty(p.Note) ? "由 WinTool 设定" : p.Note);
                double total = p.Total <= 0 ? 1 : p.Total;
                _planBar.Ratio = 1.0 - ((double)remain / total);
                _planBar.BarColor = remain < 60 ? T.Danger : T.Accent;
            }

            StringBuilder st = new StringBuilder();
            if (ShutdownOps.IsWatching)
                st.Append("正在监控进程：" + ShutdownOps.WatchTarget + "（结束后执行 " + ShutdownOps.ActionName(0) + " 等设定动作）   ");
            if (ShutdownOps.SystemShutdownPending()) st.Append("系统提示存在待重启更新   ");
            if (st.Length == 0)
            {
                if (p == null) st.Append("当前没有活动中的关机计划。设定后底部状态栏会持续显示剩余时间。");
                else st.Append("关机命令已下发到系统，关闭本工具不会取消，请用“取消关机计划”撤销。");
            }
            _stateText.Text = st.ToString();
        }
    }

    // =====================================================================
    //  操作日志
    // =====================================================================
    internal class LogPage : Page
    {
        public override string Title { get { return "操作日志"; } }
        public override string Subtitle { get { return "本工具的每一步操作都有记录，出问题时可回看命令和返回结果。"; } }

        private ListView _list;
        private TextBox _detail;

        protected override void Build()
        {
            int W = 928, x = 24, y = 20;

            Panel card = Ui.Put(Ui.Card(x, y, W, 380), Host);
            card.Controls.Add(Ui.Text("操作记录", 20, 16, 12f, T.Text, true));
            card.Controls.Add(Ui.Sub("双击一行查看完整命令与返回内容", 110, 20));

            _list = new ListView();
            _list.Location = new Point(20, 48);
            _list.Size = new Size(560, 312);
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = false;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.Font = T.F(9f);
            _list.Columns.Add("时间", 70);
            _list.Columns.Add("类型", 60);
            _list.Columns.Add("操作", 420);
            _list.SelectedIndexChanged += delegate { ShowDetail(); };
            card.Controls.Add(_list);

            _detail = Ui.Multi(592, 48, 316, 312);
            card.Controls.Add(_detail);
            card.Controls.Add(Ui.Sub("详细内容", 592, 26));

            FlatBtn save = Ui.Btn("导出日志", 20, 372 - 40, 100, 32, T.Accent, delegate { Export(); }, true);
            card.Controls.Add(save);
            FlatBtn folder = Ui.Btn("打开日志文件夹", 128, 372 - 40, 120, 32, T.Text, delegate { Sys.OpenFolder(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Cfg.Path_), "logs"), false); }, true);
            card.Controls.Add(folder);
            FlatBtn clear = Ui.Btn("清空显示", 256, 372 - 40, 100, 32, T.Text, delegate { _list.Items.Clear(); _detail.Text = ""; }, true);
            card.Controls.Add(clear);

            y += 380 + 12;
            Host.Controls.Add(Ui.Note(x, y, W,
                "日志文件按天保存在程序目录的 logs 子文件夹中（绿色版不写系统目录），可直接复制给他人协助排查。\n" +
                "日志只记录本工具自己执行的命令与结果，不会记录你的个人文件内容。", T.Info, 66));

            Host.AutoScrollMinSize = new Size(W + 48, y + 90);
            Log.Changed += delegate { Refresh(); };
        }

        private void ShowDetail()
        {
            if (_list.SelectedItems.Count == 0) return;
            LogItem it = _list.SelectedItems[0].Tag as LogItem;
            if (it == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("时间：" + it.Time.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("类型：" + it.Kind);
            sb.AppendLine("操作：" + it.Text);
            sb.AppendLine();
            sb.AppendLine("详情：");
            sb.AppendLine(string.IsNullOrEmpty(it.Detail) ? "（无）" : it.Detail);
            _detail.Text = sb.ToString();
        }

        private void Export()
        {
            try
            {
                string p = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Cfg.Path_),
                    "wintool-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                StringBuilder sb = new StringBuilder();
                LogItem[] items = Log.Snapshot();
                for (int i = 0; i < items.Length; i++)
                {
                    sb.Append(items[i].Time.ToString("yyyy-MM-dd HH:mm:ss")).Append("  [").Append(items[i].Kind).Append("]  ").Append(items[i].Text);
                    if (!string.IsNullOrEmpty(items[i].Detail)) sb.Append("   -> ").Append(items[i].Detail);
                    sb.AppendLine();
                }
                System.IO.File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
                Log.Ok("已导出日志", p);
                Sys.OpenFolder(p, true);
            }
            catch (Exception ex) { MessageBox.Show(Form, ex.Message, "导出失败"); }
        }

        public override void OnShow() { Refresh(); }

        public void Refresh()
        {
            if (_list == null) return;
            int sel = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] : -1;
            _list.BeginUpdate();
            _list.Items.Clear();
            LogItem[] items = Log.Snapshot();
            for (int i = items.Length - 1; i >= 0; i--)
            {
                ListViewItem li = new ListViewItem(items[i].Time.ToString("HH:mm:ss"));
                li.SubItems.Add(items[i].Kind);
                li.SubItems.Add(items[i].Text);
                li.Tag = items[i];
                if (items[i].Kind == "失败") li.ForeColor = T.Danger;
                else if (items[i].Kind == "成功") li.ForeColor = T.Ok;
                else if (items[i].Kind == "提示") li.ForeColor = T.SubText;
                _list.Items.Add(li);
            }
            _list.EndUpdate();
            if (sel >= 0 && sel < _list.Items.Count) _list.Items[sel].Selected = true;
        }
    }
}
