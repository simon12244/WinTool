using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    // =====================================================================
    //  IP 网络信息
    // =====================================================================
    internal class NetPage : Page
    {
        public override string Title { get { return "IP 网络信息"; } }
        public override string Subtitle { get { return "本机 / 网卡 / 公网信息一屏看完，不用再敲 ipconfig。"; } }

        private ListBox _list;
        private TextBox _detail;
        private Label _wifiVal, _dnsVal, _pubVal, _pubLoc, _gwVal;
        private ComboBox _viewKind;
        private TextBox _viewBox;
        private List<NetAdapter> _adapters = new List<NetAdapter>();
        private bool _loading;

        protected override void Build()
        {
            int W = 928, x = 24, y = 20;

            // ---- 顶部三张速览卡 ----
            Panel s1 = Ui.Put(Ui.Card(x, y, 300, 108), Host);
            s1.Controls.Add(Ui.Text("本机", 18, 14, 10.5f, T.SubText, true));
            AddKv(s1, "计算机名", Net.HostName, 18, 40);
            AddKv(s1, "当前用户", Net.UserName, 18, 66);

            Panel s2 = Ui.Put(Ui.Card(x + 314, y, 300, 108), Host);
            s2.Controls.Add(Ui.Text("无线网络", 18, 14, 10.5f, T.SubText, true));
            _wifiVal = Ui.ValLabel("读取中…", 18, 40, 264, true);
            s2.Controls.Add(_wifiVal);
            _gwVal = Ui.ValLabel("", 18, 66, 264, false);
            s2.Controls.Add(_gwVal);

            Panel s3 = Ui.Put(Ui.Card(x + 628, y, 300, 108), Host);
            s3.Controls.Add(Ui.Text("当前首选 DNS", 18, 14, 10.5f, T.SubText, true));
            _dnsVal = Ui.ValLabel("读取中…", 18, 40, 264, true);
            s3.Controls.Add(_dnsVal);
            FlatBtn dnsBtn = Ui.Btn("去修改 DNS", 18, 68, 100, 28, T.Accent, delegate { Form.ShowPage("dns"); }, true);
            s3.Controls.Add(dnsBtn);

            y += 108 + 12;

            // ---- 公网信息 ----
            Panel pub = Ui.Put(Ui.Card(x, y, W, 84), Host);
            Ui.DrawIcon(pub, 18, 28, 22, "globe", T.Info);
            pub.Controls.Add(Ui.Text("公网出口", 52, 22, 11.5f, T.Text, true));
            _pubVal = Ui.ValLabel("点击右侧按钮查询", 52, 48, 300, true);
            _pubVal.Font = T.M(11f, FontStyle.Bold);
            pub.Controls.Add(_pubVal);
            _pubLoc = Ui.Sub("", 370, 50);
            _pubLoc.AutoSize = false;
            _pubLoc.Size = new Size(380, 18);
            pub.Controls.Add(_pubLoc);
            FlatBtn q = Ui.Btn("查询公网 IP", 780, 26, 128, 34, T.Accent, null);
            q.Click += delegate { QueryPublic(); };
            pub.Controls.Add(q);

            y += 84 + 12;

            // ---- 网卡列表 + 详情 ----
            Panel card = Ui.Put(Ui.Card(x, y, W, 430), Host);
            card.Controls.Add(Ui.Text("网卡明细", 20, 16, 12f, T.Text, true));
            card.Controls.Add(Ui.Sub("选中左侧任一网卡查看完整信息", 110, 20));
            FlatBtn re = Ui.Btn("刷新", 828, 14, 80, 30, T.Accent, null, true);
            re.Click += delegate { Load(false); };
            card.Controls.Add(re);

            _list = new ListBox();
            _list.Location = new Point(20, 52);
            _list.Size = new Size(250, 300);
            _list.Font = T.F(9f);
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.IntegralHeight = false;
            _list.SelectedIndexChanged += delegate { ShowDetail(); };
            card.Controls.Add(_list);

            _detail = Ui.Multi(282, 52, 626, 300);
            card.Controls.Add(_detail);

            int by = 362;
            card.Controls.Add(MkBtn("测试网关延迟", 20, by, delegate { Action(delegate { return PingGateway(); }); }, false));
            card.Controls.Add(MkBtn("复制全部信息", 138, by, delegate { Action(delegate { CopyAll(); return null; }); }, false));
            card.Controls.Add(MkBtn("切换为自动获取 IP", 256, by, delegate { Action(delegate { return Dhcp(); }); }, true));
            card.Controls.Add(MkBtn("启用 / 禁用网卡", 402, by, delegate { Action(delegate { return ToggleAdapter(); }); }, true));
            card.Controls.Add(MkBtn("ping 公网测试", 548, by, delegate { Action(delegate { return PingPublic(); }); }, false));

            y += 430 + 12;

            // ---- 活动连接 / 路由 / ARP ----
            Panel act = Ui.Put(Ui.Card(x, y, W, 320), Host);
            act.Controls.Add(Ui.Text("活动连接与路由", 20, 16, 12f, T.Text, true));
            _viewKind = Ui.Combo(600, 14, 180);
            _viewKind.Items.AddRange(new object[] { "已建立的外网连接", "全部监听端口", "IPv4 路由表", "ARP 缓存表", "所有连接与端口" });
            _viewKind.SelectedIndex = 0;
            _viewKind.SelectedIndexChanged += delegate { Action(delegate { return LoadView(); }); };
            act.Controls.Add(_viewKind);
            FlatBtn vb = Ui.Btn("刷新", 790, 14, 118, 30, T.Accent, null, true);
            vb.Click += delegate { Action(delegate { return LoadView(); }); };
            act.Controls.Add(vb);

            _viewBox = Ui.Multi(20, 52, 888, 250);
            act.Controls.Add(_viewBox);

            y += 320 + 12;
            Host.AutoScrollMinSize = new Size(W + 48, y + 10);
        }

        private FlatBtn MkBtn(string text, int x, int y, EventHandler handler, bool outline)
        {
            FlatBtn b = Ui.Btn(text, x, y, 138, 32, T.Accent, null, outline);
            b.Click += handler;
            return b;
        }

        private void AddKv(Panel card, string k, string v, int x, int y)
        {
            Label key = Ui.Text(k, x, y - 1, 8.5f, T.SubText, false);
            card.Controls.Add(key);
            Label val = Ui.ValLabel(v, x + 66, y - 3, 190, false);
            card.Controls.Add(val);
        }

        /// <summary>把一个耗时动作放到后台线程执行，避免界面卡死。</summary>
        private void Action(Func<string> work)
        {
            if (_loading) return;
            _loading = true;
            Form.SetStatus("正在读取…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                string result = null;
                try { result = work(); }
                catch (Exception ex) { result = "读取失败：" + ex.Message; }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        _loading = false;
                        if (!string.IsNullOrEmpty(result)) { _viewBox.Text = result; Form.SetStatus("已刷新（" + DateTime.Now.ToString("HH:mm:ss") + "）"); }
                        else Form.SetStatus("完成");
                    });
                }
                catch { }
            });
        }

        private string CurrentAdapterName()
        {
            return _list.SelectedItem == null ? "" : _list.SelectedItem.ToString();
        }

        private NetAdapter CurrentAdapter()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _adapters.Count) return null;
            return _adapters[i];
        }

        public override void OnShow() { Load(true); }

        private void Load(bool withPublic)
        {
            if (_loading) return;
            _loading = true;
            Form.SetStatus("正在读取网络信息…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                List<NetAdapter> ads = new List<NetAdapter>();
                WifiInfo wifi = new WifiInfo();
                string dns = "";
                string err = null;
                try
                {
                    ads = Net.Adapters();
                    wifi = Net.Wifi();
                    dns = Net.LocalDnsServer();
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        _loading = false;
                        _adapters = ads;
                        _list.BeginUpdate();
                        _list.Items.Clear();
                        for (int i = 0; i < ads.Count; i++)
                        {
                            string tag = ads[i].Enabled ? "" : "（已禁用）";
                            string ip = ads[i].PrimaryIPv4.Length > 0 ? "  " + ads[i].PrimaryIPv4 : "";
                            _list.Items.Add(ads[i].Name + tag + ip);
                        }
                        _list.EndUpdate();
                        if (_list.Items.Count > 0)
                        {
                            int pick = 0;
                            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { pick = i; break; }
                            _list.SelectedIndex = pick;
                        }

                        if (wifi.On)
                            _wifiVal.Text = "已连接 " + wifi.SSID + "  " + wifi.SignalPercent;
                        else
                            _wifiVal.Text = "未连接无线网络";
                        _gwVal.Text = "";

                        NetAdapter on = null;
                        for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
                        _gwVal.Text = on == null ? "未检测到活动网卡" : ("网关 " + (on.PrimaryGateway.Length > 0 ? on.PrimaryGateway : "无") + "   接口 " + on.Cidr);
                        _dnsVal.Text = dns.Length > 0 ? dns : "未读取到";

                        if (err != null) Form.SetStatus("读取部分信息失败：" + err);
                        else Form.SetStatus("网络信息已更新（" + DateTime.Now.ToString("HH:mm:ss") + "）");
                    });
                }
                catch { }
            });

            if (withPublic) QueryPublic();
            Action(delegate { return LoadView(); });
        }

        private string LoadView()
        {
            int k = _viewKind == null ? 0 : _viewKind.SelectedIndex;
            StringBuilder sb = new StringBuilder();
            if (k == 0 || k == 4)
            {
                List<NetStatRow> rows = Net.Connections(k == 0);
                if (k == 0) sb.AppendLine("外部已建立的连接（去掉本机回环）：");
                else sb.AppendLine("全部 TCP/UDP 连接与监听端口：");
                sb.AppendLine();
                sb.AppendFormat("{0,-6}{1,-24}{2,-24}{3,-14}{4}\r\n", "协议", "本机地址", "远程地址", "状态", "进程 (PID)");
                sb.AppendLine(new string('-', 96));
                int n = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (k == 0 && (rows[i].Remote.StartsWith("127.") || rows[i].Remote.StartsWith("0.0.0.0") || rows[i].Remote == "*:*")) continue;
                    sb.AppendFormat("{0,-6}{1,-24}{2,-24}{3,-14}{4}\r\n",
                        rows[i].Proto, rows[i].Local, rows[i].Remote, rows[i].State, rows[i].Proc + " (" + rows[i].Pid + ")");
                    if (++n > 300) { sb.AppendLine("...(仅显示前 300 条)"); break; }
                }
                if (n == 0) sb.AppendLine("没有符合条件的连接。");
            }
            else if (k == 1)
            {
                List<NetStatRow> rows = Net.Connections(false);
                sb.AppendLine("本机正在监听的端口（LISTENING）以及 UDP 端口：");
                sb.AppendLine();
                sb.AppendFormat("{0,-6}{1,-24}{2,-14}{3}\r\n", "协议", "监听地址", "状态", "进程 (PID)");
                sb.AppendLine(new string('-', 88));
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Proto == "TCP" && !(rows[i].State == "LISTENING" || rows[i].State == "侦听")) continue;
                    sb.AppendFormat("{0,-6}{1,-24}{2,-14}{3}\r\n",
                        rows[i].Proto, rows[i].Local, rows[i].State, rows[i].Proc + " (" + rows[i].Pid + ")");
                }
            }
            else if (k == 2)
            {
                List<string> rows = Net.RouteTable();
                sb.AppendLine("IPv4 路由表：");
                sb.AppendLine();
                for (int i = 0; i < rows.Count; i++) sb.AppendLine(rows[i]);
            }
            else if (k == 3)
            {
                List<string> rows = Net.ArpTable();
                sb.AppendLine("ARP 缓存（局域网内与本机通信过的设备）：");
                sb.AppendLine();
                for (int i = 0; i < rows.Count; i++) sb.AppendLine(rows[i]);
            }
            return sb.ToString();
        }

        private void ShowDetail()
        {
            NetAdapter a = CurrentAdapter();
            if (a == null) { _detail.Text = "请选择左侧的网卡。"; return; }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【" + a.Name + "】 " + (a.Enabled ? "已启用" : "已禁用") + "   " + a.NetType);
            sb.AppendLine(a.Desc);
            sb.AppendLine(new string('-', 74));
            sb.AppendLine("IPv4 地址      : " + (a.IPv4.Count > 0 ? string.Join("  ", a.IPv4.ToArray()) : "（无）"));
            sb.AppendLine("子网掩码       : " + (a.Masks.Count > 0 ? a.Masks[0] : "（无）"));
            sb.AppendLine("网段           : " + (a.Cidr.Length > 0 ? a.Cidr : "（无）"));
            sb.AppendLine("默认网关       : " + (a.Gateways.Count > 0 ? string.Join("  ", a.Gateways.ToArray()) : "（无）"));
            sb.AppendLine("DNS 服务器     : " + (a.Dns.Count > 0 ? string.Join("  ", a.Dns.ToArray()) : "（未设置或自动）"));
            sb.AppendLine("DHCP           : " + (a.Dhcp ? ("已启用，服务器 " + (a.DhcpServer.Length > 0 ? a.DhcpServer : "未知")) : "未启用（手动配置/静态 IP）"));
            sb.AppendLine("MAC 地址       : " + a.Mac);
            sb.AppendLine("连接速度       : " + (a.Speed > 0 ? FormatSpeed(a.Speed) : "未知"));
            if (a.IPv6.Count > 0)
            {
                sb.AppendLine("IPv6 地址      :");
                for (int i = 0; i < a.IPv6.Count; i++) sb.AppendLine("                 " + a.IPv6[i]);
            }
            sb.AppendLine();
            sb.AppendLine("常见问题提示：");
            if (a.IPv4.Count > 0 && a.IPv4[0].StartsWith("169.254."))
            {
                sb.AppendLine("· 当前是 169.254.x.x 自动专用地址，说明没有从路由器拿到 IP。");
                sb.AppendLine("  请检查网线、路由器 DHCP，或点“切换为自动获取 IP”后重试。");
            }
            if (a.Dns.Count == 0)
                sb.AppendLine("· 未读取到 DNS，若无法上网请到“DNS 设置”页设置公共 DNS。");
            if (!a.Dhcp && a.IPv4.Count > 0)
                sb.AppendLine("· 当前为静态 IP，若更换了网络环境可能无法上网，可切换为自动获取。");
            if (a.IsWifi)
                sb.AppendLine("· 这是无线网卡，信号弱时优先靠近路由器或改用 5GHz 频段。");
            if (a.NetType == "虚拟" || a.NetType == "VPN")
                sb.AppendLine("· 这是虚拟/VPN 网卡，改动它可能影响虚拟机或代理软件。");
            _detail.Text = sb.ToString();
            _detail.SelectionStart = 0;
        }

        private static string FormatSpeed(long bps)
        {
            if (bps >= 1000000000) return (bps / 1000000000.0).ToString("0.##") + " Gbps";
            if (bps >= 1000000) return (bps / 1000000.0).ToString("0.##") + " Mbps";
            if (bps >= 1000) return (bps / 1000.0).ToString("0.##") + " Kbps";
            return bps + " bps";
        }

        private string PingGateway()
        {
            NetAdapter a = CurrentAdapter();
            if (a == null) return "请先选择网卡。";
            if (a.PrimaryGateway.Length == 0) return "该网卡没有默认网关。";
            int ms = Net.Ping(a.PrimaryGateway, 2000);
            if (ms < 0) return "ping 网关 " + a.PrimaryGateway + " 失败。\n可能原因：网关未响应、网线/无线未真正连通、被防火墙拦截。";
            return "ping 网关 " + a.PrimaryGateway + " 成功，延迟约 " + ms + " ms。\n" +
                (ms <= 5 ? "延迟很低，本地局域网正常。" : "延迟偏高，可能是无线信号弱或网络拥塞。");
        }

        private string PingPublic()
        {
            StringBuilder sb = new StringBuilder();
            string[] targets = new string[] { "223.5.5.5", "114.114.114.114", "www.baidu.com" };
            string[] names = new string[] { "阿里 DNS", "114 DNS", "百度（含域名解析）" };
            for (int i = 0; i < targets.Length; i++)
            {
                int ms = Net.Ping(targets[i], 3000);
                sb.AppendFormat("{0,-16}{1}\r\n", names[i], ms < 0 ? "不通 ✗" : ("通，延迟 " + ms + " ms ✓"));
            }
            sb.AppendLine();
            sb.AppendLine("判断方法：");
            sb.AppendLine("· 网关通 + 公网 IP 不通 => 路由器/宽带出口问题");
            sb.AppendLine("· 公网 IP 通 + 域名不通 => 典型 DNS 故障，去“DNS 设置”页换公共 DNS");
            return sb.ToString();
        }

        private string Dhcp()
        {
            NetAdapter a = CurrentAdapter();
            if (a == null) return "请先选择网卡。";
            if (!Sys.IsAdminOrAskSafe(Form, "修改 IP 获取方式需要管理员权限。")) return null;
            Res r = Tools.SetDhcp(a);
            return r.Code == 0
                ? "已把【" + a.Name + "】切换为自动获取 IP 和 DNS。\n若仍未获得地址，请拔插网线或重启路由器。"
                : "切换失败：\n" + r.All;
        }

        private string ToggleAdapter()
        {
            NetAdapter a = CurrentAdapter();
            if (a == null) return "请先选择网卡。";
            if (!Sys.IsAdminOrAskSafe(Form, "启用/禁用网卡需要管理员权限。")) return null;
            DialogResult dr = MessageBox.Show(Form,
                (a.Enabled ? "确定要【禁用】网卡“" + a.Name + "”吗？\n\n禁用后该网卡会立即断网；如果是唯一的网卡，你将暂时失去网络连接（本机操作不受影响，随时可再启用）。"
                           : "确定要【启用】网卡“" + a.Name + "”吗？"),
                "确认操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return null;
            bool ok = Tools.SetAdapterEnabled(a, !a.Enabled);
            System.Threading.Thread.Sleep(1500);
            Load(false);
            return ok ? ("已" + (a.Enabled ? "禁用" : "启用") + "网卡：" + a.Name + "\n列表已刷新。") : "操作失败，请确认管理员权限。";
        }

        private void CopyAll()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _adapters.Count; i++)
            {
                NetAdapter a = _adapters[i];
                sb.AppendLine("【" + a.Name + "】" + a.Desc);
                sb.AppendLine("IPv4: " + string.Join("  ", a.IPv4.ToArray()) + "  掩码: " + (a.Masks.Count > 0 ? a.Masks[0] : "") + "  网关: " + (a.Gateways.Count > 0 ? a.Gateways[0] : ""));
                sb.AppendLine("DNS: " + string.Join("  ", a.Dns.ToArray()) + "   MAC: " + a.Mac);
                sb.AppendLine();
            }
            Sys.Copy(sb.ToString());
            Form.SetStatus("已复制全部网卡信息到剪贴板");
        }

        private void QueryPublic()
        {
            Form.SetStatus("正在查询公网 IP…");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                string loc = "";
                string ip = "";
                try { ip = Net.PublicIP(out loc); }
                catch { }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        if (ip.Length == 0)
                        {
                            _pubVal.Text = "查询失败";
                            _pubLoc.Text = "可能无外网连接，或接口被拦截；可稍后重试。";
                        }
                        else
                        {
                            _pubVal.Text = ip;
                            _pubLoc.Text = loc.Length > 0 ? loc : "（未返回归属地信息）";
                            Form.SetStatus("公网 IP：" + ip);
                        }
                    });
                }
                catch { }
            });
        }
    }
}
