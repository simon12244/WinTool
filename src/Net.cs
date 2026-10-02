using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace WinTool
{
    internal class NetAdapter
    {
        public string Name = "";
        public string Desc = "";
        public string Mac = "";
        public List<string> IPv4 = new List<string>();
        public List<string> IPv6 = new List<string>();
        public List<string> Masks = new List<string>();
        public List<string> Gateways = new List<string>();
        public List<string> Dns = new List<string>();
        public bool Dhcp;
        public string DhcpServer = "";
        public bool Enabled = true;
        public long Speed;
        public string NetType = "";
        public bool IsWifi;

        public string PrimaryIPv4 { get { return IPv4.Count > 0 ? IPv4[0] : ""; } }
        public string PrimaryMask { get { return Masks.Count > 0 ? Masks[0] : ""; } }
        public string PrimaryGateway { get { return Gateways.Count > 0 ? Gateways[0] : ""; } }

        public string Cidr
        {
            get
            {
                if (IPv4.Count == 0) return "";
                return PrimaryIPv4 + Sys.PrefixFromMask(PrimaryMask);
            }
        }
    }

    internal class WifiInfo
    {
        public bool On;
        public string SSID = "";
        public string BSSID = "";
        public string Band = "";
        public string Channel = "";
        public string RadioType = "";
        public string SignalPercent = "";
        public string RxRate = "";
        public string TxRate = "";
        public string State = "";
    }

    internal class NetStatRow
    {
        public string Proto = "";
        public string Local = "";
        public string Remote = "";
        public string State = "";
        public int Pid;
        public string Proc = "";
    }

    internal static class Net
    {
        /// <summary>
        /// ipconfig / netsh / netstat / arp 等系统自带工具的调用入口。
        /// 统一走 Sys.Run：逐字节读取后按代码页（GBK/UTF-8）自动解码，
        /// 避免中文错误信息变乱码、或输出被整段丢弃。
        /// </summary>
        public static Res RunNat(string exe, string args)
        {
            return Sys.Run(exe, args, 40000, null);
        }

        public static Res RunNat(string exe, string args, int timeoutMs)
        {
            return Sys.Run(exe, args, timeoutMs, null);
        }

        // ---------- 主机信息 ----------

        public static string HostName
        {
            get { try { return Dns.GetHostName(); } catch { return Environment.MachineName; } }
        }

        public static string UserName
        {
            get
            {
                try
                {
                    string u = Environment.UserName;
                    string d = Environment.UserDomainName;
                    if (string.IsNullOrEmpty(d) || d == HostName) return u;
                    return d + "\\" + u;
                }
                catch { return ""; }
            }
        }

        public static string OsName
        {
            get
            {
                try
                {
                    using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Caption,Version,BuildNumber FROM Win32_OperatingSystem"))
                    {
                        foreach (ManagementObject o in s.Get())
                        {
                            return Convert.ToString(o["Caption"]) + "  (Build " + Convert.ToString(o["BuildNumber"]) + ")";
                        }
                    }
                }
                catch { }
                return Environment.OSVersion.VersionString;
            }
        }

        /// <summary>公共 DNS 解析得到的本机出口 IP（不依赖第三方网页）。</summary>
        public static string OutboundIP()
        {
            try
            {
                using (System.Net.Sockets.UdpClient u = new System.Net.Sockets.UdpClient())
                {
                    u.Connect("223.5.5.5", 53);
                    IPEndPoint ep = (IPEndPoint)u.Client.LocalEndPoint;
                    return ep.Address.ToString();
                }
            }
            catch { return ""; }
        }

        // ---------- 网卡信息（WMI，可靠且结构化） ----------

        public static List<NetAdapter> Adapters()
        {
            List<NetAdapter> list = new List<NetAdapter>();
            Dictionary<string, NetAdapter> byIndex = new Dictionary<string, NetAdapter>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Index,Description,MACAddress,NetConnectionID,NetEnabled,Speed,NetConnectionStatus,PNPDeviceID FROM Win32_NetworkAdapter"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        try
                        {
                            string mac = Convert.ToString(o["MACAddress"]);
                            string desc = Convert.ToString(o["Description"]);
                            if (string.IsNullOrEmpty(mac)) continue;
                            NetAdapter a = new NetAdapter();
                            a.Name = Convert.ToString(o["NetConnectionID"]);
                            if (string.IsNullOrEmpty(a.Name)) a.Name = desc;
                            a.Desc = desc;
                            a.Mac = mac;
                            object sp = o["Speed"];
                            if (sp != null) { try { a.Speed = Convert.ToInt64(sp); } catch { } }
                            object en = o["NetEnabled"];
                            a.Enabled = en == null ? true : Convert.ToBoolean(en);
                            a.NetType = ClassifyType(desc, a.Name);
                            a.IsWifi = desc.IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0
                                    || desc.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0
                                    || desc.IndexOf("WLAN", StringComparison.OrdinalIgnoreCase) >= 0
                                    || desc.IndexOf("无线") >= 0;
                            string idx = Convert.ToString(o["Index"]);
                            if (!string.IsNullOrEmpty(idx)) byIndex[idx] = a;
                            list.Add(a);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Index,IPAddress,IPSubnet,DefaultIPGateway,DHCPEnabled,DHCPServer,DNSServerSearchOrder,DNSDomain FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        try
                        {
                            string idx = Convert.ToString(o["Index"]);
                            NetAdapter a;
                            if (idx == null || !byIndex.TryGetValue(idx, out a)) continue;
                            a.IPv4.Clear(); a.IPv6.Clear(); a.Masks.Clear(); a.Gateways.Clear(); a.Dns.Clear();

                            string[] ip = o["IPAddress"] as string[];
                            string[] mask = o["IPSubnet"] as string[];
                            if (ip != null)
                            {
                                for (int i = 0; i < ip.Length; i++)
                                {
                                    if (Sys.IsValidIP(ip[i])) a.IPv4.Add(ip[i]);
                                    else if (Sys.IsValidIPv6(ip[i])) a.IPv6.Add(ip[i]);
                                }
                            }
                            if (mask != null)
                                for (int i = 0; i < mask.Length; i++) a.Masks.Add(Sys.MaskToDotted(mask[i]));
                            string[] gw = o["DefaultIPGateway"] as string[];
                            if (gw != null) for (int i = 0; i < gw.Length; i++) if (Sys.IsValidIP(gw[i])) a.Gateways.Add(gw[i]);
                            object dh = o["DHCPEnabled"];
                            a.Dhcp = dh != null && Convert.ToBoolean(dh);
                            a.DhcpServer = Convert.ToString(o["DHCPServer"]);
                            string[] dns = o["DNSServerSearchOrder"] as string[];
                            if (dns != null) for (int i = 0; i < dns.Length; i++) if (!string.IsNullOrEmpty(dns[i])) a.Dns.Add(dns[i]);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 用 netsh 补齐 DNS（WMI 偶尔为空）
            try
            {
                Res r = RunNat("netsh.exe", "interface ipv4 show dnsservers");
                if (r.Code == 0) ParseNetshDns(r.Out, list);
            }
            catch { }

            list.Sort(delegate(NetAdapter a, NetAdapter b)
            {
                int pa = Rank(a), pb = Rank(b);
                if (pa != pb) return pa.CompareTo(pb);
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
            return list;
        }

        private static int Rank(NetAdapter a)
        {
            bool realIP = a.IPv4.Count > 0 && !a.IPv4[0].StartsWith("169.254.");
            if (realIP && a.Enabled) return 0;
            if (a.Enabled) return 1;
            if (realIP) return 2;
            return 3;
        }

        private static string ClassifyType(string desc, string name)
        {
            string d = (desc + " " + name).ToLowerInvariant();
            if (d.IndexOf("wireless") >= 0 || d.IndexOf("wi-fi") >= 0 || d.IndexOf("wlan") >= 0 || d.IndexOf("无线") >= 0) return "无线";
            if (d.IndexOf("bluetooth") >= 0 || d.IndexOf("蓝牙") >= 0) return "蓝牙";
            if (d.IndexOf("vmware") >= 0 || d.IndexOf("virtualbox") >= 0 || d.IndexOf("hyper-v") >= 0
                || d.IndexOf("virtual") >= 0 || d.IndexOf("vethernet") >= 0) return "虚拟";
            if (d.IndexOf("tap") >= 0 || d.IndexOf("tun") >= 0 || d.IndexOf("vpn") >= 0 || d.IndexOf("wintun") >= 0) return "VPN";
            if (d.IndexOf("loopback") >= 0 || d.IndexOf("环回") >= 0) return "环回";
            if (d.IndexOf("ethernet") >= 0 || d.IndexOf("以太网") >= 0 || d.IndexOf("gbe") >= 0
                || d.IndexOf("realtek") >= 0 || d.IndexOf("intel") >= 0) return "有线";
            return "其他";
        }

        /// <summary>解析 "netsh interface ipv4 show dnsservers" 输出。</summary>
        public static void ParseNetshDns(string text, List<NetAdapter> adapters)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            NetAdapter cur = null;
            List<string> found = new List<string>();
            List<string> pending = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.Length == 0) continue;
                bool isHeader = Regex.IsMatch(ln, @"^(配置|Configuration)\s+(DNS|用于接口|for interface)", RegexOptions.IgnoreCase)
                             || Regex.IsMatch(ln, @"的\s*DNS\s*服务器", RegexOptions.IgnoreCase)
                             || Regex.IsMatch(ln, @"DNS servers? (configured|for)", RegexOptions.IgnoreCase);
                if (isHeader)
                {
                    if (cur != null) ApplyDns(cur, found);
                    cur = null; found = new List<string>();
                    string nm = ExtractQuoted(ln);
                    if (!string.IsNullOrEmpty(nm))
                    {
                        for (int k = 0; k < adapters.Count; k++)
                            if (string.Equals(adapters[k].Name, nm, StringComparison.OrdinalIgnoreCase)) { cur = adapters[k]; break; }
                        if (cur == null)
                            for (int k = 0; k < adapters.Count; k++)
                                if (adapters[k].Desc.IndexOf(nm, StringComparison.OrdinalIgnoreCase) >= 0) { cur = adapters[k]; break; }
                    }
                    continue;
                }
                if (ln.StartsWith("-")) continue;
                if (ln.IndexOf(":") > 0)
                {
                    string val = ln.Substring(ln.IndexOf(':') + 1).Trim();
                    if (Regex.IsMatch(ln, @"^\s*(DNS|DNS 服务器|服务器)", RegexOptions.IgnoreCase) && Sys.IsValidIP(val)) found.Add(val);
                    if (Sys.IsValidIP(val) && ln.IndexOf(":") > 0 && ln.IndexOf(".") > 0 && !Regex.IsMatch(ln, @"^\s*(注册|Register|附加|Append|搜索|Search)"))
                    {
                        if (!found.Contains(val)) found.Add(val);
                    }
                }
            }
            if (cur != null) ApplyDns(cur, found);
        }

        private static void ApplyDns(NetAdapter a, List<string> found)
        {
            if (a == null || found.Count == 0 || a.Dns.Count > 0) return;
            for (int i = 0; i < found.Count; i++) a.Dns.Add(found[i]);
        }

        public static string ExtractQuoted(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int a = s.IndexOf('"');
            if (a < 0) return "";
            int b = s.IndexOf('"', a + 1);
            if (b < 0) return "";
            return s.Substring(a + 1, b - a - 1);
        }

        // ---------- 路由 / ARP / 端口 ----------

        public static List<string> RouteTable()
        {
            List<string> rows = new List<string>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                    "SELECT Destination,Mask,NextHop,InterfaceIndex,Metric1 FROM Win32_IP4RouteTable"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        try
                        {
                            string dest = Convert.ToString(o["Destination"]);
                            string mask = Convert.ToString(o["Mask"]);
                            string hop = Convert.ToString(o["NextHop"]);
                            string met = Convert.ToString(o["Metric1"]);
                            if (dest == "127.0.0.0" || dest == "224.0.0.0" || dest == "255.255.255.255") continue;
                            string prefix = Sys.PrefixFromMask(mask);
                            if (dest == "0.0.0.0") { dest = "默认路由"; prefix = ""; }
                            rows.Add(string.Format("{0,-16}{1,-6}{2,-16}{3,-8}{4}",
                                dest, prefix, hop, met, Convert.ToString(o["InterfaceIndex"])));
                        }
                        catch { }
                    }
                }
            }
            catch { }
            rows.Sort();
            rows.Insert(0, string.Format("{0,-16}{1,-6}{2,-16}{3,-8}{4}", "目标网络", "前缀", "网关/下一跳", "跃点", "接口"));
            return rows;
        }

        public static List<string> ArpTable()
        {
            List<string> rows = new List<string>();
            Res r = RunNat("arp.exe", "-a");
            if (r.Code == 0 || r.Out.Length > 0)
            {
                string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln.StartsWith("接口") || ln.StartsWith("Interface") || ln.StartsWith("Internet")) continue;
                    if (Regex.IsMatch(ln, @"\d+\.\d+\.\d+\.\d+\s+([0-9a-f]{2}-){5}[0-9a-f]{2}", RegexOptions.IgnoreCase))
                        rows.Add(Regex.Replace(ln, @"\s{2,}", "   "));
                }
            }
            rows.Insert(0, "     Internet 地址          物理地址              类型");
            return rows;
        }

        private static Dictionary<int, string> _procCache = new Dictionary<int, string>();

        public static string ProcName(int pid)
        {
            if (pid == 0) return "System Idle";
            if (pid == 4) return "System";
            string v;
            if (_procCache.TryGetValue(pid, out v)) return v;
            try
            {
                Process p = Process.GetProcessById(pid);
                v = p.ProcessName;
                p.Dispose();
            }
            catch { v = "-"; }
            _procCache[pid] = v;
            return v;
        }

        public static List<NetStatRow> Connections(bool onlyEstablished)
        {
            List<NetStatRow> list = new List<NetStatRow>();
            Res r = RunNat("netstat.exe", "-ano");
            if (r.Out.Length == 0) return list;
            string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.Length == 0) continue;
                if (!(ln.StartsWith("TCP") || ln.StartsWith("UDP"))) continue;
                string[] parts = Regex.Split(ln, @"\s+");
                if (parts.Length < 4) continue;
                NetStatRow row = new NetStatRow();
                row.Proto = parts[0];
                row.Local = parts[1];
                row.Remote = parts.Length >= 4 ? parts[2] : "";
                row.State = parts.Length >= 5 ? parts[3] : (parts.Length == 4 ? "" : "");
                string pidStr = parts[parts.Length - 1];
                int pid;
                if (int.TryParse(pidStr, out pid)) { row.Pid = pid; row.Proc = ProcName(pid); }
                if (parts.Length == 4) { row.Remote = "*:*"; row.State = parts[2]; }
                if (onlyEstablished && !(row.State == "ESTABLISHED" || row.State == "已建立")) continue;
                list.Add(row);
            }
            return list;
        }

        // ---------- Wi-Fi ----------

        public static WifiInfo Wifi()
        {
            WifiInfo w = new WifiInfo();
            Res r = RunNat("netsh.exe", "wlan show interfaces");
            if (r.Out.Length == 0) return w;
            string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                int c = ln.IndexOf(':');
                if (c <= 0) continue;
                string key = ln.Substring(0, c).Trim();
                string val = ln.Substring(c + 1).Trim();
                if (key == "SSID") w.SSID = val;
                else if (key == "BSSID") w.BSSID = val;
                else if (key.IndexOf("信号") >= 0 || key.Equals("Signal", StringComparison.OrdinalIgnoreCase)) w.SignalPercent = val;
                else if (key.IndexOf("无线电类型") >= 0 || key.Equals("Radio type", StringComparison.OrdinalIgnoreCase)) w.RadioType = val;
                else if (key.IndexOf("信道") >= 0 || key.Equals("Channel", StringComparison.OrdinalIgnoreCase)) w.Channel = val;
                else if (key.IndexOf("频带") >= 0 || key.Equals("Band", StringComparison.OrdinalIgnoreCase)) w.Band = val;
                else if (key.IndexOf("接收速率") >= 0 || key.Equals("Receive rate", StringComparison.OrdinalIgnoreCase)) w.RxRate = val;
                else if (key.IndexOf("传输速率") >= 0 || key.Equals("Transmit rate", StringComparison.OrdinalIgnoreCase)) w.TxRate = val;
                else if (key.IndexOf("状态") >= 0 || key.Equals("State", StringComparison.OrdinalIgnoreCase)) w.State = val;
            }
            w.On = w.SSID.Length > 0;
            return w;
        }

        public static List<string> WifiProfiles()
        {
            List<string> list = new List<string>();
            Res r = RunNat("netsh.exe", "wlan show profiles");
            string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                int c = ln.IndexOf(':');
                if (c <= 0) continue;
                string key = ln.Substring(0, c).Trim();
                if (key.IndexOf("所有用户配置文件") >= 0 || key.IndexOf("All User Profile") >= 0)
                {
                    string v = ln.Substring(c + 1).Trim();
                    if (v.Length > 0) list.Add(v);
                }
            }
            return list;
        }

        /// <summary>读取某个 Wi-Fi 配置文件的明文密码（需管理员）。</summary>
        public static string WifiPassword(string profile)
        {
            Res r = RunNat("netsh.exe", "wlan show profile name=" + Sys.Q(profile) + " key=clear");
            string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                int c = ln.IndexOf(':');
                if (c <= 0) continue;
                string key = ln.Substring(0, c).Trim();
                if (key.IndexOf("关键内容") >= 0 || key.IndexOf("Key Content") >= 0)
                    return ln.Substring(c + 1).Trim();
            }
            return "";
        }

        // ---------- 公网 IP ----------

        /// <summary>查询公网出口 IP 与归属地（多源容错，仅用国内可直连接口）。</summary>
        public static string PublicIP(out string location)
        {
            location = "";
            string[] urls = new string[]
            {
                "http://ip.3322.net/",
                "http://myip.ipip.net/",
                "http://ip.taobao.com/outGetIpInfo?ip=&accessKey=alibaba-inc",
                "https://api.ip.sb/ip"
            };
            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    System.Net.WebRequest req = System.Net.WebRequest.Create(urls[i]);
                    req.Timeout = 5000;
                    req.Method = "GET";
                    using (System.Net.WebResponse resp = req.GetResponse())
                    using (System.IO.Stream st = resp.GetResponseStream())
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(st, Encoding.UTF8))
                    {
                        string body = sr.ReadToEnd().Trim();
                        if (body.Length == 0) continue;
                        if (urls[i].IndexOf("ipip") >= 0)
                        {
                            // 形如: 当前 IP：1.2.3.4  来自于：中国 广东 深圳 电信
                            string ip = ExtractFirstIP(body);
                            if (ip.Length == 0) continue;
                            int p = body.IndexOf("来自于");
                            location = p >= 0 ? body.Substring(p + 3).Trim() : body;
                            return ip;
                        }
                        if (urls[i].IndexOf("outGetIpInfo") >= 0)
                        {
                            string ip = MatchJson(body, "ip");
                            if (ip.Length == 0 || ip == "null") continue;
                            string region = MatchJson(body, "region") + " " + MatchJson(body, "city") + " " + MatchJson(body, "isp");
                            location = region.Trim();
                            return ip;
                        }
                        string first = ExtractFirstIP(body);
                        if (first.Length > 0)
                        {
                            if (body.IndexOf("china") >= 0 || body.IndexOf("China") >= 0) location = body;
                            return first;
                        }
                    }
                }
                catch { }
            }
            return "";
        }

        private static string MatchJson(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"?([^\",}]*)");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        private static string ExtractFirstIP(string s)
        {
            Match m = Regex.Match(s, @"\b((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\b");
            if (!m.Success) return "";
            string ip = m.Value;
            if (ip.StartsWith("0.") || ip.StartsWith("127.")) return "";
            return ip;
        }

        // ---------- 连通性 ----------

        /// <summary>Ping 一个主机，返回平均延迟（毫秒），失败返回 -1。</summary>
        public static int Ping(string host, int timeoutMs)
        {
            try
            {
                using (System.Net.NetworkInformation.Ping p = new System.Net.NetworkInformation.Ping())
                {
                    System.Net.NetworkInformation.PingReply r = p.Send(host, timeoutMs);
                    if (r.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        if (r.RoundtripTime > 0) return (int)r.RoundtripTime;
                        return 1;
                    }
                }
            }
            catch { }
            return -1;
        }

        /// <summary>批量测试 DNS 服务器延迟（并行），返回 ip -> 毫秒(-1 失败)。</summary>
        public static Dictionary<string, int> TestDnsLatency(string[] servers, int timeoutMs)
        {
            Dictionary<string, int> res = new Dictionary<string, int>();
            List<System.Threading.Thread> threads = new List<System.Threading.Thread>();
            object gate = new object();
            for (int i = 0; i < servers.Length; i++)
            {
                string ip = servers[i];
                System.Threading.Thread t = new System.Threading.Thread(delegate()
                {
                    int ms = DnsLatency(ip, timeoutMs);
                    lock (gate) res[ip] = ms;
                });
                t.IsBackground = true;
                threads.Add(t);
                t.Start();
            }
            for (int i = 0; i < threads.Count; i++) { try { threads[i].Join(timeoutMs + 3000); } catch { } }
            return res;
        }

        /// <summary>向指定 DNS 发一次真实解析请求并计时。</summary>
        public static int DnsLatency(string server, int timeoutMs)
        {
            try
            {
                byte[] query = BuildDnsQuery("www.baidu.com");
                using (System.Net.Sockets.UdpClient u = new System.Net.Sockets.UdpClient())
                {
                    u.Client.ReceiveTimeout = timeoutMs;
                    u.Connect(server, 53);
                    Stopwatch sw = Stopwatch.StartNew();
                    u.Send(query, query.Length);
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = u.Receive(ref ep);
                    sw.Stop();
                    if (resp == null || resp.Length < 12) return -1;
                    if ((resp[3] & 0x0F) != 0) return -1; // RCODE != 0
                    return (int)sw.ElapsedMilliseconds;
                }
            }
            catch { return -1; }
        }

        private static byte[] BuildDnsQuery(string name)
        {
            List<byte> b = new List<byte>();
            int id = new Random().Next(1, 65535);
            b.Add((byte)(id >> 8)); b.Add((byte)(id & 0xFF));
            b.Add(0x01); b.Add(0x00);            // 标准查询，递归期望
            b.Add(0x00); b.Add(0x01);            // QDCOUNT
            b.Add(0x00); b.Add(0x00);            // ANCOUNT
            b.Add(0x00); b.Add(0x00);            // NSCOUNT
            b.Add(0x00); b.Add(0x00);            // ARCOUNT
            string[] labels = name.Split('.');
            for (int i = 0; i < labels.Length; i++)
            {
                byte[] lb = Encoding.ASCII.GetBytes(labels[i]);
                b.Add((byte)lb.Length);
                b.AddRange(lb);
            }
            b.Add(0x00);
            b.Add(0x00); b.Add(0x01);            // QTYPE = A
            b.Add(0x00); b.Add(0x01);            // QCLASS = IN
            return b.ToArray();
        }

        public static string LocalDnsServer()
        {
            try
            {
                List<NetAdapter> list = Adapters();
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Enabled && list[i].IPv4.Count > 0 && list[i].Dns.Count > 0) return list[i].Dns[0];
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Dns.Count > 0) return list[i].Dns[0];
            }
            catch { }
            return "";
        }
    }
}
