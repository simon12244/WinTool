using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    // =====================================================================
    //  DNS 设置
    // =====================================================================
    internal class DnsPage : Page
    {
        public override string Title { get { return "DNS 设置"; } }
        public override string Subtitle { get { return "一键切换国内公共 DNS，支持真实解析测速，随时可恢复自动获取。"; } }

        private ComboBox _adapter;
        private ListBox _current;
        private ListView _presets;
        private Label _status;
        private TextBox _p1, _p2;
        private List<NetAdapter> _adapters = new List<NetAdapter>();
        private List<DnsPreset> _all = new List<DnsPreset>();
        private Dictionary<string, int> _latency = new Dictionary<string, int>();
        private ComboBox _filter;
        private bool _testing;

        protected override void Build()
        {
            int W = 928, x = 24, y = 20;

            // ---------- 当前状态 ----------
            Panel cur = Ui.Put(Ui.Card(x, y, W, 168), Host);
            Ui.DrawIcon(cur, 18, 22, 22, "globe", T.Accent);
            cur.Controls.Add(Ui.Text("当前 DNS 配置", 52, 22, 11.5f, T.Text, true));

            cur.Controls.Add(Ui.Sub("网卡", 20, 58));
            _adapter = Ui.Combo(60, 52, 300);
            _adapter.SelectedIndexChanged += delegate { RefreshCurrent(); LoadManual(); };
            cur.Controls.Add(_adapter);

            FlatBtn re = Ui.Btn("重新读取", 372, 50, 96, 30, T.Accent, null, true);
            re.Click += delegate { LoadAdapters(); };
            cur.Controls.Add(re);

            FlatBtn flush = Ui.Btn("刷新 DNS 缓存", 478, 50, 130, 30, T.Accent, delegate { Flush(); });
            cur.Controls.Add(flush);
            FlatBtn cache = Ui.Btn("查看缓存", 616, 50, 96, 30, T.Text, delegate { ShowCache(); }, true);
            cur.Controls.Add(cache);
            FlatBtn dh = Ui.Btn("恢复自动获取", 720, 50, 130, 30, T.Text, delegate { RestoreDhcp(); }, true);
            cur.Controls.Add(dh);

            _current = new ListBox();
            _current.Location = new Point(20, 92);
            _current.Size = new Size(300, 62);
            _current.Font = T.M(10f);
            _current.BorderStyle = BorderStyle.FixedSingle;
            _current.IntegralHeight = false;
            cur.Controls.Add(_current);

            _status = Ui.Sub("", 336, 94);
            _status.AutoSize = false;
            _status.Size = new Size(574, 58);
            cur.Controls.Add(_status);

            y += 168 + 12;

            // ---------- 预设列表 ----------
            Panel pl = Ui.Put(Ui.Card(x, y, W, 400), Host);
            pl.Controls.Add(Ui.Text("国内公共 DNS 推荐库", 20, 16, 12f, T.Text, true));
            pl.Controls.Add(Ui.Sub("双击一行即可应用到所选网卡 · 测速结果为真实解析请求的往返延迟", 20, 40));

            _filter = Ui.Combo(660, 14, 150);
            _filter.Items.Add("全部类别");
            string[] cats = DnsLib.Categories();
            for (int i = 0; i < cats.Length; i++) _filter.Items.Add(cats[i]);
            _filter.SelectedIndex = 0;
            _filter.SelectedIndexChanged += delegate { FillPresets(); };
            pl.Controls.Add(_filter);

            FlatBtn speed = Ui.Btn("测速全部", 818, 12, 90, 30, T.Warn, delegate { TestAll(); });
            pl.Controls.Add(speed);

            _presets = new ListView();
            _presets.Location = new Point(20, 64);
            _presets.Size = new Size(888, 268);
            _presets.View = View.Details;
            _presets.FullRowSelect = true;
            _presets.MultiSelect = false;
            _presets.GridLines = true;
            _presets.Font = T.F(9f);
            _presets.Columns.Add("名称", 170);
            _presets.Columns.Add("首选 DNS", 130);
            _presets.Columns.Add("备用 DNS", 130);
            _presets.Columns.Add("类别", 80);
            _presets.Columns.Add("延迟", 70);
            _presets.Columns.Add("说明", 290);
            _presets.DoubleClick += delegate { ApplySelected(); };
            pl.Controls.Add(_presets);

            FlatBtn ap = Ui.Btn("应用到所选网卡", 20, 342, 150, 34, T.Accent, delegate { ApplySelected(); });
            pl.Controls.Add(ap);
            FlatBtn ad = Ui.Btn("以管理员身份重新启动", 180, 342, 200, 34, T.Text, delegate { ElevateNow(); }, true);
            pl.Controls.Add(ad);
            pl.Controls.Add(Ui.Sub("应用后立即生效，无需重启；如浏览器仍访问旧地址，请再点“刷新 DNS 缓存”。", 392, 350));

            y += 400 + 12;

            // ---------- 手动设置 ----------
            Panel man = Ui.Put(Ui.Card(x, y, W, 118), Host);
            man.Controls.Add(Ui.Text("手动指定 DNS", 20, 16, 11.5f, T.Text, true));
            man.Controls.Add(Ui.Sub("首选", 20, 58));
            _p1 = Ui.Input(56, 54, 150);
            man.Controls.Add(_p1);
            man.Controls.Add(Ui.Sub("备用（可留空）", 222, 58));
            _p2 = Ui.Input(310, 54, 150);
            man.Controls.Add(_p2);
            FlatBtn go = Ui.Btn("应用", 476, 52, 90, 30, T.Accent, delegate { ApplyManual(); });
            man.Controls.Add(go);
            man.Controls.Add(Ui.Sub("填写前请确认地址正确；IPv6 地址请填写在网卡的 IPv6 属性中。", 20, 88));

            y += 118 + 12;

            // ---------- 提示 ----------
            string note =
                "关于 DNS 的重要提示：" + Environment.NewLine +
                "1. 换 DNS 只影响域名解析速度，不会改变你的宽带带宽；网页“感觉很慢”时，往往确实是 DNS 拖后腿。" + Environment.NewLine +
                "2. 国内日常使用推荐：阿里 223.5.5.5 / 腾讯 119.29.29.29 / 百度 180.76.76.76，稳定且无广告劫持。" + Environment.NewLine +
                "3. 运营商 DNS（电信/联通/移动）通常本地延迟最低，但个别地区存在 DNS 广告插入（输错网址跳转到广告页）。" + Environment.NewLine +
                "4. 8.8.8.8、1.1.1.1 等海外 DNS 在国内常被干扰，可能出现解析超时，非必要不要设置。" + Environment.NewLine +
                "5. 设置后如果完全无法上网，请立即点“恢复自动获取”回到最初的 DHCP 状态。";
            Host.Controls.Add(Ui.Note(x, y, W, note, T.Info, 116));

            Host.AutoScrollMinSize = new Size(W + 48, y + 130);

            _all = DnsLib.All();
            FillPresets();
        }

        private void FillPresets()
        {
            _presets.BeginUpdate();
            _presets.Items.Clear();
            string cat = _filter == null || _filter.SelectedIndex <= 0 ? null : _filter.SelectedItem.ToString();
            for (int i = 0; i < _all.Count; i++)
            {
                if (cat != null && _all[i].Cat != cat) continue;
                ListViewItem li = new ListViewItem(_all[i].Name);
                li.SubItems.Add(_all[i].Primary);
                li.SubItems.Add(_all[i].Secondary.Length > 0 ? _all[i].Secondary : "—");
                li.SubItems.Add(_all[i].Cat);
                int ms;
                li.SubItems.Add(_latency.TryGetValue(_all[i].Primary, out ms) ? (ms < 0 ? "超时" : ms + " ms") : "未测");
                li.SubItems.Add(_all[i].Note);
                li.Tag = _all[i];
                if (_all[i].Tag == "推荐") li.ForeColor = T.Ok;
                else if (_all[i].Tag == "不推荐" || _all[i].Tag == "谨慎") li.ForeColor = T.Danger;
                else if (_all[i].Primary == "DHCP") li.ForeColor = T.SubText;
                _presets.Items.Add(li);
            }
            _presets.EndUpdate();
        }

        public override void OnShow()
        {
            LoadAdapters();
        }

        private void LoadAdapters()
        {
            Form.SetStatus("正在读取网卡与 DNS…");
            try
            {
                _adapters = Net.Adapters();
                _adapter.Items.Clear();
                for (int i = 0; i < _adapters.Count; i++)
                {
                    string t = _adapters[i].Name + (_adapters[i].Enabled ? "" : "（已禁用）");
                    _adapter.Items.Add(t);
                }
                int pick = 0;
                for (int i = 0; i < _adapters.Count; i++)
                    if (_adapters[i].Enabled && _adapters[i].IPv4.Count > 0) { pick = i; break; }
                if (_adapter.Items.Count > 0) _adapter.SelectedIndex = pick;
                RefreshCurrent();
                LoadManual();
                Form.SetStatus("已读取 " + _adapters.Count + " 个网卡");
            }
            catch (Exception ex) { Form.SetStatus("读取失败：" + ex.Message); }
        }

        private NetAdapter Current()
        {
            int i = _adapter.SelectedIndex;
            if (i < 0 || i >= _adapters.Count) return null;
            return _adapters[i];
        }

        private void RefreshCurrent()
        {
            NetAdapter a = Current();
            _current.Items.Clear();
            if (a == null) { _status.Text = "请选择一个网卡。"; return; }
            List<string> dns = DnsOps.Get(a);
            if (dns.Count == 0)
            {
                _current.Items.Add("自动获取（DHCP 下发）");
            }
            else
            {
                for (int i = 0; i < dns.Count; i++)
                    _current.Items.Add((i == 0 ? "首选：" : "备用：") + dns[i]);
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("网卡：" + a.Name + "   " + (a.Enabled ? "已启用" : "已禁用"));
            sb.AppendLine("当前 IP：" + (a.PrimaryIPv4.Length > 0 ? a.Cidr : "（无）") + "    网关：" + (a.PrimaryGateway.Length > 0 ? a.PrimaryGateway : "（无）"));
            sb.AppendLine(dns.Count == 0
                ? "状态：DNS 由路由器自动下发。想手动指定，请从下方列表选择后点“应用”。"
                : "状态：已手动指定 DNS。若解析异常，可点“恢复自动获取”。");
            _status.Text = sb.ToString();
        }

        private void LoadManual()
        {
            NetAdapter a = Current();
            if (a == null) { _p1.Text = ""; _p2.Text = ""; return; }
            List<string> dns = DnsOps.Get(a);
            _p1.Text = dns.Count > 0 ? dns[0] : "";
            _p2.Text = dns.Count > 1 ? dns[1] : "";
        }

        private void ApplySelected()
        {
            if (_presets.SelectedItems.Count == 0) { Warn("请先在列表中选中一行 DNS。"); return; }
            DnsPreset p = _presets.SelectedItems[0].Tag as DnsPreset;
            NetAdapter a = Current();
            if (a == null) { Warn("请先选择一个网卡。"); return; }
            if (p.Primary == "DHCP") { RestoreDhcp(); return; }
            if (!Sys.IsAdminOrAsk(Form, "修改 DNS 需要管理员权限。")) return;

            if (p.Note.IndexOf("海外") >= 0 || p.Cat == "海外")
            {
                DialogResult dr = MessageBox.Show(Form,
                    "【重要提示】你选择的是海外 DNS：" + p.Name + "\n\n" +
                    "国内网络环境下这类 DNS 常被干扰或超时，可能导致网页打不开、解析变慢。\n" +
                    "如果没有特殊需求，建议改用 阿里 223.5.5.5 或 腾讯 119.29.29.29。\n\n确定仍要应用吗？",
                    "确认使用海外 DNS", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
            }

            ConfirmAndApply(a, p.Primary, p.Secondary, p.Name);
        }

        private void ConfirmAndApply(NetAdapter a, string p1, string p2, string label)
        {
            string msg = "即将把网卡【" + a.Name + "】的 DNS 修改为：\n\n" +
                "首选：" + p1 + "\n" +
                (string.IsNullOrEmpty(p2) ? "" : "备用：" + p2 + "\n") +
                "\n· 修改立即生效，无需重启\n· 会同时刷新一次 DNS 缓存\n· 如果改完无法上网，请点“恢复自动获取”\n\n确定继续吗？";
            DialogResult dr = MessageBox.Show(Form, msg, "确认修改 DNS（" + label + "）",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            Form.SetStatus("正在应用 DNS…");
            Res r = DnsOps.Set(a, p1, p2);
            if (r.Code == 0)
            {
                Form.SetStatus("DNS 已修改为 " + label);
                RefreshCurrent();
                LoadManual();
                MessageBox.Show(Form,
                    "已成功把【" + a.Name + "】的 DNS 设置为 " + label + "。\n\n" +
                    "建议立即验证：打开浏览器访问一个网站；\n若打不开，请回来点“恢复自动获取”。",
                    "修改成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(Form,
                    "修改 DNS 失败：\n\n" + Trim(r.All) + "\n\n" +
                    "常见原因：\n· 未以管理员身份运行（请点“以管理员身份重新启动”）\n· 网卡名称不匹配或被安全软件拦截\n· 公司/学校的组策略禁止修改网络设置",
                    "修改失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyManual()
        {
            NetAdapter a = Current();
            if (a == null) { Warn("请先选择一个网卡。"); return; }
            string p1 = _p1.Text.Trim();
            string p2 = _p2.Text.Trim();
            if (!Sys.IsValidIP(p1)) { Warn("首选 DNS 不是合法的 IPv4 地址。\n例如：223.5.5.5"); return; }
            if (p2.Length > 0 && !Sys.IsValidIP(p2)) { Warn("备用 DNS 不是合法的 IPv4 地址，可留空。"); return; }
            if (p2 == p1) p2 = "";
            if (!Sys.IsAdminOrAsk(Form, "修改 DNS 需要管理员权限。")) return;
            ConfirmAndApply(a, p1, p2, "手动指定");
        }

        private void RestoreDhcp()
        {
            NetAdapter a = Current();
            if (a == null) { Warn("请先选择一个网卡。"); return; }
            if (!Sys.IsAdminOrAsk(Form, "恢复自动获取 DNS 需要管理员权限。")) return;
            DialogResult dr = MessageBox.Show(Form,
                "将把网卡【" + a.Name + "】的 DNS 恢复为“自动获取（由路由器下发）”。\n\n这是最稳妥的状态，确定吗？",
                "恢复自动获取", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;
            Res r = DnsOps.Set(a, "DHCP", "");
            if (r.Code == 0)
            {
                Form.SetStatus("已恢复自动获取 DNS");
                RefreshCurrent();
                LoadManual();
            }
            else MessageBox.Show(Form, "恢复失败：\n\n" + Trim(r.All), "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Flush()
        {
            Res r = DnsOps.FlushCache();
            if (r.Code == 0) MessageBox.Show(Form, "DNS 解析缓存已刷新。\n\n之前访问过但打不开的网站，现在可以再试一次。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else MessageBox.Show(Form, Trim(r.All), "刷新失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowCache()
        {
            Form.SetStatus("正在读取 DNS 缓存…");
            string t = DnsOps.ShowCache();
            if (t.Length > 40000) t = t.Substring(0, 40000) + "\r\n...(内容过长已截断)";
            Form.ShowTextWindow("DNS 解析缓存内容", t);
            Form.SetStatus("DNS 缓存已读取");
        }

        private void ElevateNow()
        {
            if (Sys.Admin) { MessageBox.Show(Form, "当前已经是管理员权限。", "提示"); return; }
            if (Sys.Elevate(null)) Application.Exit();
            else MessageBox.Show(Form, "提权被取消。", "提示");
        }

        private void TestAll()
        {
            if (_testing) { Warn("正在测速中，请稍候…"); return; }
            _testing = true;
            Form.SetStatus("正在并行测试各 DNS 延迟（约 3 秒）…");
            string[] servers = new string[0];
            List<string> s = new List<string>();
            for (int i = 0; i < _all.Count; i++)
            {
                // IPv6 预设不参与 IPv4 延迟测速（本机多半没有 IPv6 出口）
                if (_all[i].Cat == "IPv6") continue;
                if (Sys.IsValidIP(_all[i].Primary) && !s.Contains(_all[i].Primary)) s.Add(_all[i].Primary);
            }
            servers = s.ToArray();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                Dictionary<string, int> res = null;
                try { res = Net.TestDnsLatency(servers, 2500); }
                catch { res = new Dictionary<string, int>(); }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        _latency = res;
                        _testing = false;
                        FillPresets();
                        int best = -1; string bestName = "";
                        foreach (KeyValuePair<string, int> kv in res)
                        {
                            if (kv.Value < 0) continue;
                            if (best < 0 || kv.Value < best) { best = kv.Value; bestName = kv.Key; }
                        }
                        Form.SetStatus(best < 0 ? "测速完成：全部超时（可能无外网连接）" : ("测速完成：最快 " + bestName + "  " + best + " ms"));
                    });
                }
                catch { }
            });
        }

        private void Warn(string text)
        {
            MessageBox.Show(Form, text, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "（无返回信息）";
            s = s.Trim();
            if (s.Length > 800) s = s.Substring(0, 800) + "\n...(已截断)";
            return s;
        }
    }
}
