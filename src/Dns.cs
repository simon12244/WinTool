using System;
using System.Collections.Generic;
using System.Text;

namespace WinTool
{
    internal class DnsPreset
    {
        public string Name = "";
        public string Primary = "";
        public string Secondary = "";
        public string Cat = "";       // 分类
        public string Note = "";      // 重要说明 / 风险提示
        public string Tag = "";       // 特色标签

        public DnsPreset(string name, string p, string s, string cat, string note, string tag)
        {
            Name = name; Primary = p; Secondary = s; Cat = cat; Note = note; Tag = tag;
        }

        public string Display { get { return Name + "  " + Primary + (Secondary.Length > 0 ? " / " + Secondary : ""); } }
    }

    /// <summary>国内主流公共 DNS 服务器库（含官方说明与风险提示）。</summary>
    internal static class DnsLib
    {
        public static List<DnsPreset> All()
        {
            List<DnsPreset> l = new List<DnsPreset>();

            l.Add(new DnsPreset("阿里云公共 DNS", "223.5.5.5", "223.6.6.6", "国内大厂",
                "阿里云官方公共 DNS，国内节点多、解析快，支持 IPv4/IPv6，无广告劫持，日常首选。", "推荐"));
            l.Add(new DnsPreset("阿里云公共 DNS（IPv6）", "2400:3200::1", "2400:3200:baba::1", "IPv6",
                "仅在网络已具备 IPv6 时填写，否则会导致解析失败。", ""));
            l.Add(new DnsPreset("腾讯 DNSPod", "119.29.29.29", "182.254.116.116", "国内大厂",
                "腾讯 DNSPod 公共 DNS，游戏与国内站点解析优化好，支持 DoH/DoT。", "游戏友好"));
            l.Add(new DnsPreset("百度公共 DNS", "180.76.76.76", "", "国内大厂",
                "百度官方公共 DNS，稳定性好，单一地址即可使用。", ""));
            l.Add(new DnsPreset("114DNS（纯净版）", "114.114.114.114", "114.114.115.115", "老牌公共",
                "南京信风运营的老牌公共 DNS，覆盖面广、兼容性好，全国都有节点。", "老牌稳定"));
            l.Add(new DnsPreset("114DNS（拦截恶意网站）", "114.114.114.119", "114.114.115.119", "安全防护",
                "在解析层拦截钓鱼/挂马/欺诈类网站。注意：属主动拦截，极少数正常站点可能被误判。", "拦截风险网站"));
            l.Add(new DnsPreset("114DNS（家庭防护）", "114.114.114.110", "114.114.115.110", "安全防护",
                "过滤色情等不良内容，适合家庭/儿童上网环境。", "内容过滤"));
            l.Add(new DnsPreset("360 安全 DNS", "101.226.4.6", "218.30.118.6", "安全防护",
                "360 提供的安全 DNS，拦截恶意域名。隐私敏感场景请自行评估。", "拦截恶意域名"));
            l.Add(new DnsPreset("CNNIC sDNS", "1.2.4.8", "210.2.4.8", "国内大厂",
                "中国互联网络信息中心公共 DNS，对 .cn 域名解析权威性好。", ""));
            l.Add(new DnsPreset("中国电信 DNS（广东）", "202.96.128.86", "202.96.128.166", "运营商",
                "运营商本地 DNS。解析本地资源最快，但可能存在 DNS 广告插入（NXDOMAIN 重定向）。", "本地最快"));
            l.Add(new DnsPreset("中国电信 DNS（全国通用）", "219.141.136.10", "219.141.140.10", "运营商",
                "电信通用公共地址，跨省使用效果一般，建议优先用大厂公共 DNS。", ""));
            l.Add(new DnsPreset("中国联通 DNS", "123.123.123.123", "123.123.123.124", "运营商",
                "联通公共 DNS 地址，可在联通线路上获得较低延迟。", ""));
            l.Add(new DnsPreset("中国移动 DNS", "211.136.192.6", "211.136.112.200", "运营商",
                "移动公共 DNS 地址，移动宽带用户可优先尝试。", ""));
            l.Add(new DnsPreset("教育网 DNS", "101.6.6.6", "202.112.0.33", "校园网",
                "清华 TUNA / 教育网 DNS，校园网环境下可加速学术资源与镜像站访问。", "校园网"));
            l.Add(new DnsPreset("V2EX DNS", "199.91.73.222", "178.79.131.110", "极客",
                "海外地址，国内直连可能超时，仅在需要时使用。", "谨慎"));
            l.Add(new DnsPreset("Google Public DNS", "8.8.8.8", "8.8.4.4", "海外",
                "国内网络环境下常被干扰、延迟高，一般不建议在国内使用。", "不推荐"));
            l.Add(new DnsPreset("Cloudflare DNS", "1.1.1.1", "1.0.0.1", "海外",
                "国内直连不稳定，可能出现解析超时；仅在特殊需求下使用。", "不推荐"));
            l.Add(new DnsPreset("自动获取（清除自定义 DNS）", "DHCP", "", "恢复默认",
                "恢复由路由器/运营商自动下发 DNS，这是最不容易出问题的状态。", "撤销设置"));

            return l;
        }

        public static string[] Categories()
        {
            List<string> c = new List<string>();
            List<DnsPreset> all = All();
            for (int i = 0; i < all.Count; i++) if (!c.Contains(all[i].Cat)) c.Add(all[i].Cat);
            return c.ToArray();
        }
    }

    /// <summary>DNS 查询与修改（netsh / WMI），以及缓存操作。</summary>
    internal static class DnsOps
    {
        /// <summary>读取某网卡当前的 DNS 服务器。</summary>
        public static List<string> Get(NetAdapter a)
        {
            List<string> l = new List<string>();
            if (a == null) return l;
            Res r = Net.RunNat("netsh.exe", "interface ipv4 show dnsservers name=" + Sys.Q(a.Name));
            string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                int c = ln.IndexOf(':');
                if (c <= 0) continue;
                string val = ln.Substring(c + 1).Trim();
                if (Sys.IsValidIP(val) && !l.Contains(val)) l.Add(val);
            }
            if (l.Count == 0)
            {
                NetAdapter fresh = null;
                List<NetAdapter> all = Net.Adapters();
                for (int i = 0; i < all.Count; i++) if (all[i].Name == a.Name) { fresh = all[i]; break; }
                if (fresh != null) l.AddRange(fresh.Dns);
            }
            return l;
        }

        /// <summary>设置静态 DNS。primary 为空或 "DHCP" 时恢复自动获取。</summary>
        public static Res Set(NetAdapter a, string primary, string secondary)
        {
            if (a == null) { Res e = new Res(); e.Code = -1; e.Err = "未选择网卡"; return e; }
            StringBuilder log = new StringBuilder();
            Res r;

            if (string.IsNullOrEmpty(primary) || primary == "DHCP")
            {
                r = Run("interface ipv4 set dnsservers name=" + Sys.Q(a.Name) + " source=dhcp");
                log.AppendLine("netsh interface ipv4 set dnsservers name=" + Sys.Q(a.Name) + " source=dhcp -> " + r.Code
                    + "（" + (Tools.NetshOk(r) ? "成功" : "失败") + "）");
                if (Tools.NetshOk(r) && r.Code != 0)
                {
                    // netsh 在已经是 DHCP 时会返回 exit=1，但语义上就是成功，这里归一化
                    Res ok = new Res();
                    ok.Code = 0;
                    ok.Out = r.Out;
                    r = ok;
                }
                FlushCache();
            }
            else
            {
                r = Run("interface ipv4 set dnsservers name=" + Sys.Q(a.Name) + " source=static address=" + primary + " register=primary validate=no");
                log.AppendLine("设置首选 " + primary + " -> " + r.Code);
                if (r.Code != 0) return Fail(a, primary, r, log);
                if (!string.IsNullOrEmpty(secondary))
                {
                    Res r2 = Run("interface ipv4 add dnsservers name=" + Sys.Q(a.Name) + " address=" + secondary + " index=2 validate=no");
                    log.AppendLine("设置备用 " + secondary + " -> " + r2.Code);
                }
                FlushCache();
            }

            if (r.Code == 0)
                Log.Ok("已修改 DNS", a.Name + "  ->  " + (primary == "DHCP" ? "自动获取(DHCP)" : primary + (string.IsNullOrEmpty(secondary) ? "" : " / " + secondary)));
            else
                Log.Fail("修改 DNS 失败", r.All + "\n" + log.ToString());
            return r;
        }

        private static Res Fail(NetAdapter a, string primary, Res r, StringBuilder log)
        {
            Log.Fail("设置 DNS 失败", "网卡：" + a.Name + " 地址：" + primary + "\n" + r.All);
            return r;
        }

        private static Res Run(string args)
        {
            return Net.RunNat("netsh.exe", args, 20000);
        }

        /// <summary>刷新 DNS 解析缓存。</summary>
        public static Res FlushCache()
        {
            Res r = Sys.Run("ipconfig.exe", "/flushdns", 20000, null);
            if (r.Code == 0)
            {
                // 顺带清一次 NetBIOS 名称缓存
                Sys.Run("nbtstat.exe", "-R", 10000, null);
                Log.Ok("DNS 缓存已刷新", "ipconfig /flushdns");
            }
            else Log.Fail("刷新 DNS 缓存失败", r.All);
            return r;
        }

        /// <summary>查看 DNS 缓存内容。</summary>
        public static string ShowCache()
        {
            Res r = Net.RunNat("ipconfig.exe", "/displaydns", 30000);
            return r.Out;
        }

        /// <summary>把一个网卡的 DNS 改为指定预设。</summary>
        public static Res ApplyPreset(NetAdapter a, DnsPreset p)
        {
            if (p.Primary == "DHCP") return Set(a, "DHCP", "");
            return Set(a, p.Primary, p.Secondary);
        }
    }
}
