using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    // =====================================================================
    //  系统工具
    // =====================================================================
    internal class ToolsPage : Page
    {
        public override string Title { get { return "系统工具"; } }
        public override string Subtitle { get { return "把需要翻好几层菜单、敲好几条命令的事，做成一次点击。"; } }

        private CheckBox _ckWinsock, _ckTcp, _ckFw;
        private ListView _cleanList;
        private List<CleanTarget> _targets;

        private ListView _startup;
        private List<Tools.StartupItem> _startupItems = new List<Tools.StartupItem>();
        private ListView _check;
        private ComboBox _wifiProfile;
        private TextBox _wifiOut;
        private TextBox _portIn;
        private TextBox _toolOut;
        private Label _cleanTotal;

        protected override void Build()
        {
            int W = 928, x = 24, y = 20;

            // ================= 网络修复 =================
            Panel net = Ui.Put(Ui.Card(x, y, W, 176), Host);
            Ui.DrawIcon(net, 18, 22, 22, "shield", T.Info);
            net.Controls.Add(Ui.Text("网络故障修复", 52, 22, 11.5f, T.Text, true));
            net.Controls.Add(Ui.Sub("适用于“能上 QQ 打不开网页”“Wi-Fi 显示已连接但不能上网”“网页一直转圈”等", 52, 44));

            _ckWinsock = Ui.Check("重置 Winsock 目录（修代理/加速器残留）", 22, 76);
            _ckWinsock.Checked = true;
            _ckTcp = Ui.Check("重置 TCP/IP 协议栈", 22, 102);
            _ckTcp.Checked = true;
            _ckFw = Ui.Check("重置防火墙规则（会清空自定义规则）", 22, 128);
            net.Controls.Add(_ckWinsock); net.Controls.Add(_ckTcp); net.Controls.Add(_ckFw);

            net.Controls.Add(MkBtn("执行所选修复", 430, 72, 122, 30, delegate { NetRepair(); }, false));
            net.Controls.Add(MkBtn("刷新 DNS 缓存", 560, 72, 122, 30, delegate { FlushDns(); }, true));
            net.Controls.Add(MkBtn("清空 ARP 缓存", 690, 72, 122, 30, delegate { ArpClear(); }, true));
            net.Controls.Add(MkBtn("刷新组策略", 820, 72, 98, 30, delegate { GpUpdate(); }, true));
            net.Controls.Add(MkBtn("释放 IP", 430, 110, 122, 30, delegate { IpRelease(); }, true));
            net.Controls.Add(MkBtn("重新获取 IP", 560, 110, 122, 30, delegate { IpRenew(); }, true));
            net.Controls.Add(MkBtn("切换为自动获取", 690, 110, 122, 30, delegate { DhcpAll(); }, true));
            net.Controls.Add(MkBtn("打开网络连接", 820, 110, 98, 30, delegate { Sys.OpenPath("ncpa.cpl"); }, true));

            y += 176 + 12;

            // ================= 垃圾清理 =================
            Panel clean = Ui.Put(Ui.Card(x, y, W, 250), Host);
            clean.Controls.Add(Ui.Text("系统垃圾清理", 20, 16, 12f, T.Text, true));
            clean.Controls.Add(Ui.Sub("只清理系统与程序的临时文件，不涉及任何个人文档", 150, 20));

            _cleanList = new ListView();
            _cleanList.Location = new Point(20, 48);
            _cleanList.Size = new Size(560, 150);
            _cleanList.View = View.Details;
            _cleanList.CheckBoxes = true;
            _cleanList.FullRowSelect = true;
            _cleanList.GridLines = true;
            _cleanList.Font = T.F(9f);
            _cleanList.Columns.Add("清理项", 170);
            _cleanList.Columns.Add("大小", 90);
            _cleanList.Columns.Add("文件数", 70);
            _cleanList.Columns.Add("说明", 220);
            _cleanList.ItemChecked += delegate { SumClean(); };
            clean.Controls.Add(_cleanList);

            _cleanTotal = Ui.Text("正在统计…", 600, 50, 10f, T.SubText, false);
            _cleanTotal.AutoSize = false;
            _cleanTotal.Size = new Size(300, 22);
            clean.Controls.Add(_cleanTotal);
            clean.Controls.Add(MkBtn("重新统计", 600, 80, 120, 32, delegate { UpdateCleanSizes(); }, true));
            clean.Controls.Add(MkBtn("全选", 600, 120, 60, 32, delegate { CheckAllClean(true); }, true));
            clean.Controls.Add(MkBtn("全不选", 668, 120, 70, 32, delegate { CheckAllClean(false); }, true));

            FlatBtn doClean = Ui.Btn("开始清理", 600, 160, 120, 32, T.Accent, delegate { DoClean(); });
            clean.Controls.Add(doClean);
            FlatBtn openTemp = Ui.Btn("打开临时目录", 730, 160, 130, 32, T.Text, delegate { Sys.OpenPath(Path.GetTempPath()); }, true);
            clean.Controls.Add(openTemp);
            clean.Controls.Add(Ui.Sub("清理时正在被占用的文件会自动跳过，属于正常现象。", 20, 204));

            y += 250 + 12;

            // ================= 启动项 =================
            Panel su = Ui.Put(Ui.Card(x, y, W, 250), Host);
            su.Controls.Add(Ui.Text("开机启动项管理", 20, 16, 12f, T.Text, true));
            su.Controls.Add(Ui.Sub("开机变慢的主因之一。禁用只是不自动启动，不会卸载软件", 150, 20));

            _startup = new ListView();
            _startup.Location = new Point(20, 48);
            _startup.Size = new Size(888, 150);
            _startup.View = View.Details;
            _startup.FullRowSelect = true;
            _startup.GridLines = true;
            _startup.Font = T.F(9f);
            _startup.Columns.Add("名称", 210);
            _startup.Columns.Add("状态", 70);
            _startup.Columns.Add("位置", 150);
            _startup.Columns.Add("命令 / 路径", 450);
            su.Controls.Add(_startup);

            su.Controls.Add(MkBtn("刷新列表", 20, 208, 100, 30, delegate { LoadStartup(); }, true));
            su.Controls.Add(MkBtn("禁用选中", 130, 208, 100, 30, delegate { ToggleStartup(false); }, true));
            su.Controls.Add(MkBtn("启用选中", 240, 208, 100, 30, delegate { ToggleStartup(true); }, true));
            su.Controls.Add(MkBtn("打开启动文件夹", 350, 208, 130, 30, delegate { Sys.OpenPath(Environment.GetFolderPath(Environment.SpecialFolder.Startup)); }, true));
            su.Controls.Add(MkBtn("打开任务管理器", 490, 208, 130, 30, delegate { Sys.RunHidden("taskmgr.exe", ""); }, true));

            y += 250 + 12;

            // ================= 系统体检 =================
            Panel chk = Ui.Put(Ui.Card(x, y, W, 262), Host);
            chk.Controls.Add(Ui.Text("一键系统体检", 20, 16, 12f, T.Text, true));
            chk.Controls.Add(Ui.Sub("检查磁盘、内存、DNS、权限、关机状态等常见问题", 130, 20));
            FlatBtn run = Ui.Btn("开始体检", 820, 12, 88, 30, T.Accent, delegate { RunCheck(); });
            chk.Controls.Add(run);

            _check = new ListView();
            _check.Location = new Point(20, 48);
            _check.Size = new Size(888, 200);
            _check.View = View.Details;
            _check.FullRowSelect = true;
            _check.GridLines = true;
            _check.Font = T.F(9f);
            _check.Columns.Add("检查项", 150);
            _check.Columns.Add("结果", 300);
            _check.Columns.Add("建议", 430);
            chk.Controls.Add(_check);

            y += 262 + 12;

            // ================= Wi-Fi 密码 =================
            Panel wifi = Ui.Put(Ui.Card(x, y, W, 214), Host);
            wifi.Controls.Add(Ui.Text("查看已保存的 Wi-Fi 密码", 20, 16, 12f, T.Text, true));
            wifi.Controls.Add(Ui.Sub("需要管理员权限；只在本机显示，不会上传任何数据", 210, 20));
            _wifiProfile = Ui.Combo(20, 50, 300);
            wifi.Controls.Add(_wifiProfile);
            wifi.Controls.Add(MkBtn("读取密码", 330, 48, 110, 30, delegate { ShowWifiPwd(); }, false));
            wifi.Controls.Add(MkBtn("显示全部", 450, 48, 100, 30, delegate { ShowAllWifi(); }, true));
            wifi.Controls.Add(MkBtn("刷新列表", 560, 48, 100, 30, delegate { LoadWifi(); }, true));
            _wifiOut = Ui.Multi(20, 88, 888, 108);
            wifi.Controls.Add(_wifiOut);

            y += 214 + 12;

            // ================= 端口占用 =================
            Panel port = Ui.Put(Ui.Card(x, y, W, 190), Host);
            port.Controls.Add(Ui.Text("端口占用查询", 20, 16, 12f, T.Text, true));
            port.Controls.Add(Ui.Sub("装软件提示“端口被占用”、服务起不来时用", 130, 20));
            port.Controls.Add(Ui.Sub("端口号", 20, 56));
            _portIn = Ui.Input(70, 52, 100);
            _portIn.Text = "8080";
            port.Controls.Add(_portIn);
            port.Controls.Add(MkBtn("查询", 182, 50, 90, 30, delegate { QueryPort(); }, false));
            port.Controls.Add(MkBtn("结束占用进程", 282, 50, 130, 30, delegate { KillPort(); }, true));
            _toolOut = Ui.Multi(20, 90, 888, 88);
            port.Controls.Add(_toolOut);

            y += 190 + 12;

            // ================= 系统报告与维护 =================
            Panel util = Ui.Put(Ui.Card(x, y, W, 200), Host);
            util.Controls.Add(Ui.Text("系统报告与维护", 20, 16, 12f, T.Text, true));
            util.Controls.Add(Ui.Sub("生成报告、检查授权、查看磁盘与供电信息", 150, 20));

            util.Controls.Add(MkBtn("生成系统信息报告", 20, 52, 150, 32, delegate { SysInfoReport(); }, false));
            util.Controls.Add(MkBtn("检查激活状态", 180, 52, 130, 32, delegate { Activation(); }, true));
            util.Controls.Add(MkBtn("电池健康报告", 320, 52, 126, 32, delegate { Battery(); }, true));
            util.Controls.Add(MkBtn("磁盘信息", 460, 52, 110, 32, delegate { DiskInfo(); }, true));
            util.Controls.Add(MkBtn("创建还原点", 580, 52, 120, 32, delegate { RestorePoint(); }, true));
            util.Controls.Add(MkBtn("下载常用软件", 710, 52, 130, 32, delegate { Links(); }, false));

            util.Controls.Add(MkBtn("关闭快速启动", 20, 94, 130, 32, delegate { FastStartup(false); }, true));
            util.Controls.Add(MkBtn("开启休眠", 160, 94, 110, 32, delegate { Hibernate(true); }, true));
            util.Controls.Add(MkBtn("关闭休眠", 280, 94, 110, 32, delegate { Hibernate(false); }, true));
            util.Controls.Add(MkBtn("开启卓越性能", 400, 94, 130, 32, delegate { Ultimate(); }, true));
            util.Controls.Add(MkBtn("清空 DNS 缓存", 540, 94, 130, 32, delegate { FlushDns(); }, true));
            util.Controls.Add(MkBtn("打开控制面板", 680, 94, 130, 32, delegate { Sys.OpenPath("control"); }, true));

            util.Controls.Add(Ui.Sub("提示：关闭快速启动可以解决部分机型“定时关机不生效”“睡眠后自动唤醒”的怪问题。", 20, 140));
            util.Controls.Add(Ui.Sub("修改电源相关设置需要管理员权限，程序会自动提示提权。", 20, 162));

            y += 200 + 14;
            Host.AutoScrollMinSize = new Size(W + 48, y + 10);
        }

        private FlatBtn MkBtn(string text, int x, int y, int w, int h, EventHandler handler, bool outline)
        {
            FlatBtn b = Ui.Btn(text, x, y, w, h, T.Accent, null, outline);
            b.Click += handler;
            return b;
        }
        public override void OnShow()
        {
            if (_cleanList.Items.Count == 0) InitCleanList();
            if (_startup.Items.Count == 0) LoadStartup();
            if (_check.Items.Count == 0) RunCheck();
            if (_wifiProfile.Items.Count == 0) LoadWifi();
        }

        // ---------------- 网络修复 ----------------

        private void NetRepair()
        {
            if (!Sys.IsAdminOrAsk(Form, "网络重置（Winsock / TCP-IP / 防火墙）需要管理员权限。")) return;
            if (!_ckWinsock.Checked && !_ckTcp.Checked && !_ckFw.Checked) { Info("请至少勾选一项。"); return; }
            DialogResult dr = MessageBox.Show(Form,
                "即将执行：\n" +
                (_ckWinsock.Checked ? "· 重置 Winsock 目录\n" : "") +
                (_ckTcp.Checked ? "· 重置 TCP/IP 协议栈\n" : "") +
                (_ckFw.Checked ? "· 重置防火墙规则（自定义规则将全部清空）\n" : "") +
                "\n注意：\n· 重置后需要【重启电脑】才完全生效\n· 部分代理、加速器、虚拟网卡软件需要重新配置\n\n确定继续吗？",
                "确认网络重置", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
            Form.SetStatus("正在执行网络重置，请稍候…");
            string r = Tools.NetRepair(_ckWinsock.Checked, _ckTcp.Checked, _ckFw.Checked);
            Form.SetStatus("网络重置完成，请重启电脑");
            Info(r + "\n\n请重启电脑使重置生效。");
        }

        private void FlushDns()
        {
            Res r = DnsOps.FlushCache();
            Info(r.Code == 0 ? "DNS 缓存已刷新。" : ("刷新失败：\n" + r.All));
        }

        private void ArpClear()
        {
            if (!Sys.IsAdminOrAsk(Form, "清空 ARP 缓存需要管理员权限。")) return;
            Res r = Tools.ArpClear();
            Info(r.Code == 0 ? "ARP 缓存已清空。" : ("操作结果：\n" + r.All));
        }

        private void GpUpdate()
        {
            Form.SetStatus("正在刷新组策略…");
            Res r = Tools.GpUpdate();
            Info(r.Code == 0 ? "组策略已强制刷新。" : ("执行结果：\n" + r.All));
            Form.SetStatus("组策略刷新完成");
        }

        private void IpRelease()
        {
            Form.SetStatus("正在释放 IP…");
            Res r = Tools.IpRelease();
            Info(r.Code == 0 ? "已释放当前 IP 地址。" : ("执行结果：\n" + r.All));
        }

        private void IpRenew()
        {
            Form.SetStatus("正在重新获取 IP…");
            Res r = Tools.IpRenew();
            Info(r.Code == 0 ? "已重新获取 IP 地址。" : ("执行结果：\n" + r.All));
            Form.SetStatus("IP 已更新");
        }

        private void DhcpAll()
        {
            if (!Sys.IsAdminOrAsk(Form, "切换为自动获取 IP 需要管理员权限。")) return;
            List<NetAdapter> ads = Net.Adapters();
            NetAdapter target = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { target = ads[i]; break; }
            if (target == null) { Info("没有找到活动的网卡。"); return; }
            DialogResult dr = MessageBox.Show(Form, "将把活动网卡【" + target.Name + "】切换为 DHCP 自动获取 IP 与 DNS。\n\n确定吗？",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
            Res r = Tools.SetDhcp(target);
            Info(r.Code == 0 ? "已切换为自动获取。若仍未联网，请拔插网线或重启路由器。" : ("切换失败：\n" + r.All));
        }

        // ---------------- 垃圾清理 ----------------

        private void InitCleanList()
        {
            _targets = Tools.CleanTargets();
            if (_targets == null || _targets.Count == 0) return;
            _cleanList.BeginUpdate();
            _cleanList.Items.Clear();
            for (int i = 0; i < _targets.Count; i++)
            {
                ListViewItem li = new ListViewItem(_targets[i].Name);
                li.SubItems.Add("—");
                li.SubItems.Add("—");
                li.SubItems.Add(_targets[i].Note);
                li.Checked = _targets[i].Selected;
                li.Tag = _targets[i];
                _cleanList.Items.Add(li);
            }
            _cleanList.EndUpdate();
            UpdateCleanSizes();
        }

        private void CheckAllClean(bool on)
        {
            for (int i = 0; i < _cleanList.Items.Count; i++) _cleanList.Items[i].Checked = on;
        }

        private void UpdateCleanSizes()
        {
            _cleanTotal.Text = "正在统计文件大小…";
            Form.SetStatus("正在统计垃圾文件…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                try
                {
                    for (int i = 0; i < _targets.Count; i++) Tools.Measure(_targets[i]);
                }
                catch { }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        long total = 0; int files = 0;
                        for (int i = 0; i < _targets.Count; i++)
                        {
                            if (i < _cleanList.Items.Count)
                            {
                                ListViewItem li = _cleanList.Items[i];
                                li.SubItems[1].Text = Sys.FmtBytes(_targets[i].Size);
                                li.SubItems[2].Text = _targets[i].Files.ToString();
                                if (!Directory.Exists(_targets[i].Path)) li.SubItems[1].Text = "不存在";
                            }
                            total += _targets[i].Size; files += _targets[i].Files;
                        }
                        _cleanTotal.Text = "可清理约 " + Sys.FmtBytes(total) + "（" + files + " 个文件）";
                        SumClean();
                        Form.SetStatus("统计完成");
                    });
                }
                catch { }
            });
        }

        private void SumClean()
        {
            long sel = 0;
            for (int i = 0; i < _cleanList.Items.Count; i++)
                if (_cleanList.Items[i].Checked && _cleanList.Items[i].Tag is CleanTarget) sel += ((CleanTarget)_cleanList.Items[i].Tag).Size;
            _cleanTotal.Text = "已选中约 " + Sys.FmtBytes(sel);
        }

        private void DoClean()
        {
            List<CleanTarget> sel = new List<CleanTarget>();
            for (int i = 0; i < _cleanList.Items.Count; i++)
            {
                CleanTarget t = _cleanList.Items[i].Tag as CleanTarget;
                if (t == null) continue;
                t.Selected = _cleanList.Items[i].Checked;
                if (t.Selected) sel.Add(t);
            }
            if (sel.Count == 0) { Info("请至少勾选一个清理项。"); return; }
            DialogResult dr = MessageBox.Show(Form,
                "即将清理以下内容：\n\n· " + JoinNames(sel) + "\n\n" +
                "只删除临时文件、日志与缓存，不会动你的文档、照片、桌面文件。\n" +
                "正在被程序占用的文件会自动跳过。\n\n确定开始吗？",
                "确认清理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
            Form.SetStatus("正在清理…");
            long freed; int skipped, denied;
            string detail = Tools.Clean(sel, out freed, out skipped, out denied);
            Form.SetStatus("清理完成，共释放 " + Sys.FmtBytes(freed));
            UpdateCleanSizes();
            string extra = "";
            if (freed == 0 && denied > 0)
                extra = "\n\n注意：所有文件都因【权限不足】未能删除。\n请点左下角以管理员身份重启本工具后重试。";
            else if (denied > 0)
                extra = "\n\n另有 " + denied + " 个文件因权限不足被跳过（系统目录需要管理员权限）。";
            MessageBox.Show(Form,
                "清理完成！\n\n共释放空间：" + Sys.FmtBytes(freed)
                + "\n被占用跳过：" + skipped + " 个\n权限不足跳过：" + denied + " 个" + extra
                + "\n\n" + detail,
                "清理结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string JoinNames(List<CleanTarget> l)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < l.Count; i++)
            {
                if (i > 0) sb.Append("、");
                sb.Append(l[i].Name);
            }
            return sb.ToString();
        }

        // ---------------- 启动项 ----------------

        private void LoadStartup()
        {
            Form.SetStatus("正在读取启动项…");
            _startupItems = Tools.StartupItems();
            _startup.BeginUpdate();
            _startup.Items.Clear();
            for (int i = 0; i < _startupItems.Count; i++)
            {
                Tools.StartupItem it = _startupItems[i];
                ListViewItem li = new ListViewItem(it.Name);
                li.SubItems.Add(it.Enabled ? "已启用" : "已禁用");
                li.SubItems.Add(it.Location);
                li.SubItems.Add(it.Command);
                li.Tag = it;
                li.ForeColor = it.Enabled ? T.Text : T.SubText;
                _startup.Items.Add(li);
            }
            _startup.EndUpdate();
            Form.SetStatus("共 " + _startupItems.Count + " 个启动项");
        }

        private void ToggleStartup(bool enable)
        {
            if (_startup.SelectedItems.Count == 0) { Info("请先选中一行。"); return; }
            Tools.StartupItem it = _startup.SelectedItems[0].Tag as Tools.StartupItem;
            if (it == null) return;
            if (!it.Location.StartsWith("当前用户") && !Sys.IsAdminOrAsk(Form, "修改所有用户的启动项需要管理员权限。")) return;
            if (Tools.SetStartupEnabled(it, enable)) LoadStartup();
            else Info("修改失败。可尝试用“打开任务管理器 → 启动”页手动切换。");
        }

        // ---------------- 体检 ----------------

        private void RunCheck()
        {
            Form.SetStatus("正在体检…");
            List<Tools.CheckItem> items = Tools.SysCheck();
            _check.BeginUpdate();
            _check.Items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                ListViewItem li = new ListViewItem(items[i].Name);
                li.SubItems.Add(items[i].Value);
                li.SubItems.Add(items[i].Advice);
                if (items[i].Level == 2) { li.ForeColor = T.Danger; li.SubItems[1].Text = "⚠ " + items[i].Value; }
                else if (items[i].Level == 1) li.ForeColor = T.Warn;
                else li.ForeColor = T.Ok;
                _check.Items.Add(li);
            }
            _check.EndUpdate();
            Form.SetStatus("体检完成（" + items.Count + " 项）");
        }

        // ---------------- Wi-Fi ----------------

        private void LoadWifi()
        {
            if (!Sys.Admin) { _wifiOut.Text = "读取 Wi-Fi 密码需要管理员权限。\r\n请点主界面左下角的“提权”按钮，或以管理员身份重新运行本工具。"; }
            List<string> ps = Net.WifiProfiles();
            _wifiProfile.Items.Clear();
            for (int i = 0; i < ps.Count; i++) _wifiProfile.Items.Add(ps[i]);
            if (_wifiProfile.Items.Count > 0) _wifiProfile.SelectedIndex = 0;
            if (ps.Count == 0) _wifiOut.Text = "本机没有保存的无线网络配置（可能是有线电脑，或从未连过 Wi-Fi）。";
        }

        private void ShowWifiPwd()
        {
            if (!Sys.IsAdminOrAsk(Form, "读取 Wi-Fi 明文密码需要管理员权限。")) return;
            if (_wifiProfile.SelectedItem == null) { Info("请先选择一个 Wi-Fi 名称。"); return; }
            string name = _wifiProfile.SelectedItem.ToString();
            string pwd = Net.WifiPassword(name);
            _wifiOut.Text = pwd.Length > 0
                ? ("Wi-Fi：" + name + "\r\n密码：" + pwd + "\r\n\r\n（已读取到明文密码，可点右键复制）")
                : ("Wi-Fi：" + name + "\r\n未读取到密码。\r\n\r\n可能原因：该配置使用证书/企业认证，或密码存储在域账号中。");
            _wifiOut.SelectionStart = 0;
        }

        private void ShowAllWifi()
        {
            if (!Sys.IsAdminOrAsk(Form, "读取 Wi-Fi 明文密码需要管理员权限。")) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _wifiProfile.Items.Count; i++)
            {
                string name = _wifiProfile.Items[i].ToString();
                string pwd = Net.WifiPassword(name);
                sb.AppendFormat("{0,-28}{1}\r\n", name, pwd.Length > 0 ? pwd : "（未读取到，可能非密码认证）");
            }
            _wifiOut.Text = sb.Length > 0 ? sb.ToString() : "没有可读取的 Wi-Fi 配置。";
            _wifiOut.SelectionStart = 0;
        }

        // ---------------- 端口 ----------------

        private void QueryPort()
        {
            int port;
            if (!int.TryParse(_portIn.Text.Trim(), out port) || port < 1 || port > 65535) { Info("请输入 1-65535 之间的端口号。"); return; }
            Form.SetStatus("正在查询端口 " + port + "…");
            _toolOut.Text = Tools.PortOwner(port);
            _toolOut.SelectionStart = 0;
            Form.SetStatus("端口查询完成");
        }

        private void KillPort()
        {
            if (!Sys.IsAdminOrAsk(Form, "结束进程需要管理员权限。")) return;
            int port;
            if (!int.TryParse(_portIn.Text.Trim(), out port) || port < 1 || port > 65535) { Info("请输入 1-65535 之间的端口号。"); return; }
            string info = Tools.PortOwner(port);
            _toolOut.Text = info;
            DialogResult dr = MessageBox.Show(Form,
                info + "\n\n是否强制结束以上占用该端口的进程？\n（未保存的数据会丢失）",
                "确认结束进程", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
            List<NetStatRow> rows = Net.Connections(false);
            List<int> pids = new List<int>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Local.EndsWith(":" + port) && !pids.Contains(rows[i].Pid) && rows[i].Pid > 4) pids.Add(rows[i].Pid);
            if (pids.Count == 0) { Info("没有找到可结束的进程。"); return; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pids.Count; i++)
            {
                Res r = Sys.Run("taskkill.exe", "/f /pid " + pids[i], 15000, null);
                sb.AppendLine("PID " + pids[i] + " -> " + (r.Code == 0 ? "已结束" : "失败：" + r.All));
                if (r.Code == 0) Log.Ok("已结束占用端口的进程", "PID " + pids[i] + "（端口 " + port + "）");
            }
            _toolOut.Text = sb.ToString();
            Info(sb.ToString());
        }

        // ---------------- 报告与维护 ----------------

        private void SysInfoReport()
        {
            Form.SetStatus("正在生成系统信息报告…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "系统信息报告.txt");
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("===== 系统信息报告 =====");
                    sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("计算机名：" + Net.HostName + "    用户：" + Net.UserName);
                    sb.AppendLine("系统：" + Net.OsName);
                    sb.AppendLine();
                    sb.AppendLine("===== 网卡 =====");
                    List<NetAdapter> ads = Net.Adapters();
                    for (int i = 0; i < ads.Count; i++)
                    {
                        sb.AppendLine("【" + ads[i].Name + "】" + ads[i].Desc);
                        sb.AppendLine("  IPv4: " + string.Join(" ", ads[i].IPv4.ToArray()) + "  掩码: " + (ads[i].Masks.Count > 0 ? ads[i].Masks[0] : "") + "  网关: " + (ads[i].Gateways.Count > 0 ? ads[i].Gateways[0] : ""));
                        sb.AppendLine("  DNS: " + string.Join(" ", ads[i].Dns.ToArray()) + "   MAC: " + ads[i].Mac);
                    }
                    sb.AppendLine();
                    sb.AppendLine("===== IP 配置全文 =====");
                    sb.AppendLine(Net.RunNat("ipconfig.exe", "/all", 30000).Out);
                    sb.AppendLine("===== 路由表 =====");
                    List<string> routes = Net.RouteTable();
                    for (int i = 0; i < routes.Count; i++) sb.AppendLine(routes[i]);
                    sb.AppendLine();
                    sb.AppendLine("===== 开机启动项 =====");
                    List<Tools.StartupItem> sits = Tools.StartupItems();
                    for (int i = 0; i < sits.Count; i++) sb.AppendLine((sits[i].Enabled ? "[启用] " : "[禁用] ") + sits[i].Name + "  " + sits[i].Command);
                    sb.AppendLine();
                    sb.AppendLine("===== 体检结果 =====");
                    List<Tools.CheckItem> cis = Tools.SysCheck();
                    for (int i = 0; i < cis.Count; i++) sb.AppendLine(cis[i].Name + "：" + cis[i].Value + "    " + cis[i].Advice);
                    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                    Log.Ok("已生成系统信息报告", path);
                    try { Form.BeginInvoke((MethodInvoker)delegate { Form.SetStatus("报告已生成：" + path); Sys.OpenFolder(path, true); }); }
                    catch { }
                }
                catch (Exception ex)
                {
                    Log.Fail("生成报告失败", ex.Message);
                    try { Form.BeginInvoke((MethodInvoker)delegate { Info("生成失败：" + ex.Message); }); }
                    catch { }
                }
            });
        }

        private void Activation()
        {
            Form.SetStatus("正在检查激活状态…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                string t = "";
                try { t = Tools.ActivationInfo(); } catch (Exception ex) { t = ex.Message; }
                try { Form.BeginInvoke((MethodInvoker)delegate { Form.ShowTextWindow("Windows / Office 激活状态", t); Form.SetStatus("激活状态已读取"); }); }
                catch { }
            });
        }

        private void Battery()
        {
            if (!Sys.IsAdminOrAsk(Form, "生成电池报告需要管理员权限。")) return;
            if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\powercfg.exe"))) return;
            Form.SetStatus("正在生成电池报告…");
            string t = Tools.BatteryReport();
            Form.SetStatus("电池报告已生成");
            string p = FindBatteryReport();
            if (File.Exists(p))
            {
                Sys.OpenPath(p);
                Info("电池报告已生成并在浏览器中打开：\n" + p);
            }
            else Info("生成结果：\n" + t + "\n\n（台式机通常没有电池，会提示不支持）");
        }

        /// <summary>找出刚生成的电池报告（可能在临时目录或回退目录）。</summary>
        private static string FindBatteryReport()
        {
            string[] dirs = new string[]
            {
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetDirectoryName(Cfg.Path_)
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (string.IsNullOrEmpty(dirs[i]) || !Directory.Exists(dirs[i])) continue;
                    string[] files = Directory.GetFiles(dirs[i], "wintool_*.html");
                    if (files.Length > 0)
                    {
                        Array.Sort(files);
                        return files[files.Length - 1];
                    }
                }
                catch { }
            }
            return Path.Combine(Path.GetTempPath(), "wintool-battery.html");
        }

        private void DiskInfo()
        {
            List<string> d = Tools.Disks();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("物理磁盘：");
            sb.AppendLine();
            for (int i = 0; i < d.Count; i++) sb.AppendLine(d[i]);
            sb.AppendLine("分区使用情况：");
            sb.AppendLine();
            try
            {
                DriveInfo[] ds = DriveInfo.GetDrives();
                for (int i = 0; i < ds.Length; i++)
                {
                    try
                    {
                        if (ds[i].DriveType != DriveType.Fixed && ds[i].DriveType != DriveType.Removable) continue;
                        if (!ds[i].IsReady) { sb.AppendLine(ds[i].Name + "  （未就绪）"); continue; }
                        double usedPct = 100.0 * (ds[i].TotalSize - ds[i].AvailableFreeSpace) / ds[i].TotalSize;
                        sb.AppendFormat("{0,-6}{1,-14} 已用 {2,7}  可用 {3,7}  ({4:0.0}%)  格式 {5}\r\n",
                            ds[i].Name, ds[i].DriveType == DriveType.Removable ? "可移动" : "本地磁盘",
                            Sys.FmtBytes(ds[i].TotalSize - ds[i].AvailableFreeSpace),
                            Sys.FmtBytes(ds[i].AvailableFreeSpace), usedPct, ds[i].DriveFormat);
                    }
                    catch { }
                }
            }
            catch { }
            Form.ShowTextWindow("磁盘信息", sb.ToString());
        }

        private void RestorePoint()
        {
            if (!Sys.IsAdminOrAsk(Form, "创建系统还原点需要管理员权限。")) return;
            DialogResult dr = MessageBox.Show(Form,
                "即将创建一个系统还原点。\n\n· 创建过程可能需要 1-3 分钟\n· 需要系统已开启“系统保护”（否则会失败）\n· 这是动手改系统设置前的推荐保险\n\n确定创建吗？",
                "创建还原点", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
            Form.SetStatus("正在创建还原点，请稍候…");
            Res r = Tools.CreateRestorePoint("WinTool 手动还原点 " + DateTime.Now.ToString("MM-dd HH:mm"));
            Form.SetStatus(r.Code == 0 ? "还原点已创建" : "还原点创建失败");
            Info(r.Code == 0
                ? "系统还原点已创建成功。\n\n如需回滚：控制面板 → 恢复 → 打开系统还原。"
                : ("创建失败：\n" + r.All + "\n\n常见原因：系统保护未开启。\n开启方法：此电脑 → 右键属性 → 系统保护 → 配置 → 启用系统保护。"));
        }

        private void Links()
        {
            List<Tools.LinkItem> items = Tools.CommonLinks();
            using (Form f = new Form())
            {
                f.Text = "常用软件与资源下载";
                f.Size = new Size(560, 520);
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Color.White;
                f.Font = T.F(9.5f);
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;

                int ly = 16;
                for (int i = 0; i < items.Count; i++)
                {
                    Tools.LinkItem it = items[i];
                    Label l = Ui.Text(it.Name, 20, ly + 4, 10f, T.Text, true);
                    f.Controls.Add(l);
                    Label n = Ui.Sub(it.Note, 220, ly + 5);
                    f.Controls.Add(n);
                    FlatBtn b = Ui.Btn("打开", 440, ly, 80, 26, T.Accent, null, true);
                    string url = it.Url;
                    b.Click += delegate { Sys.OpenUrl(url); };
                    f.Controls.Add(b);
                    ly += 34;
                }
                FlatBtn ok = Ui.Btn("关闭", 440, ly + 6, 80, 28, T.Text, null, true);
                ok.Click += delegate { f.Close(); };
                f.Controls.Add(ok);
                f.ShowDialog(Form);
            }
        }

        private void FastStartup(bool on)
        {
            if (!Sys.IsAdminOrAsk(Form, "修改快速启动设置需要管理员权限。")) return;
            DialogResult dr = MessageBox.Show(Form,
                "关闭“快速启动”可以解决：\n· 定时关机命令偶尔不生效\n· 关机后风扇/指示灯仍亮\n· 睡眠、休眠行为异常\n\n代价：开机速度会略慢一点点（通常 1-3 秒）。\n\n确定关闭吗？",
                "关闭快速启动", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
            ShutdownOps.SetFastStartup(false);
            Info("已关闭快速启动。\n\n如需恢复，请点“开启休眠”按钮同时会把快速启动开回来。");
            Form.SetStatus("已关闭快速启动");
        }

        private void Hibernate(bool on)
        {
            if (!Sys.IsAdminOrAsk(Form, "修改休眠设置需要管理员权限。")) return;
            ShutdownOps.SetFastStartup(on);
            Info(on
                ? "已开启休眠功能（同时开启快速启动）。\n\n现在可以在“定时关机”页使用“休眠”。"
                : "已关闭休眠功能（同时关闭快速启动）。\n\n注意：关闭后无法使用休眠，睡眠也可能受影响。");
            Form.SetStatus(on ? "已开启休眠" : "已关闭休眠");
        }

        private void Ultimate()
        {
            if (!Sys.IsAdminOrAsk(Form, "解锁电源计划需要管理员权限。")) return;
            Res r = Sys.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 20000, null);
            Info(r.Code == 0
                ? "已解锁“卓越性能”电源计划。\n\n请到 控制面板 → 电源选项 中手动选中它。\n（多见于台式机；笔记本可能不显示该计划）"
                : ("操作结果：\n" + r.All));
        }

        private void Info(string text)
        {
            MessageBox.Show(Form, text, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
