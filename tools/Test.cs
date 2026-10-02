using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>
    /// 无界面功能测试台：逐项调用 WinTool 的真实底层实现，输出退出码与真实效果。
    /// 用法：WinToolTest.exe [输出文件]
    /// </summary>
    internal static class Test
    {
        private static StringBuilder _sb = new StringBuilder();
        private static int _pass, _fail, _skip;

        private static void Line(string s) { _sb.AppendLine(s); Console.WriteLine(s); }

        private static void Case(string name, bool ok, string detail)
        {
            if (ok) _pass++; else _fail++;
            Line((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  ::  " + detail));
        }

        private static void Skip(string name, string why)
        {
            _skip++;
            Line("[SKIP] " + name + "  ::  " + why);
        }

        private static string Flat(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(空)";
            s = s.Replace("\r\n", " | ").Replace("\n", " | ").Trim();
            if (s.Length > 200) s = s.Substring(0, 200) + "…";
            return s;
        }

        private static string Res1(Res r)
        {
            return "exit=" + r.Code + (r.TimedOut ? "(超时)" : "") + " out=" + Flat(r.Out) + " err=" + Flat(r.Err);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                Cfg.Load();
                Line("=== WinTool 功能测试台 ===");
                Line("时间   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Line("用户   : " + Environment.UserName + "   管理员=" + Sys.Admin);
                Line("系统   : " + Net.OsName);
                Line("配置   : " + Cfg.Path_);
                Line("");

                T1_Shell();
                T2_Registry();
                T3_Shutdown();
                T4_Power();
                T5_Net();
                T6_Dns();
                T7_Startup();
                T8_Clean();
                T9_Batch();
                T10_Wmi();
                T11_Misc();

                Line("");
                Line(string.Format("=== 汇总：通过 {0}  失败 {1}  跳过 {2} ===", _pass, _fail, _skip));
            }
            catch (Exception ex)
            {
                Line("测试台异常: " + ex);
            }

            string outFile = args.Length > 0 ? args[0]
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testresult.txt");
            try { File.WriteAllText(outFile, _sb.ToString(), new UTF8Encoding(false)); }
            catch { }
        }

        // ---------- 1. 命令执行层 ----------
        private static void T1_Shell()
        {
            Line("---- 1. 命令执行引擎 ----");
            Res r;

            r = Sys.Run("cmd.exe", "/c echo hello", 10000, null);
            Case("cmd 基本执行与输出捕获", r.Code == 0 && r.Out.IndexOf("hello") >= 0, Res1(r));

            r = Sys.Run("cmd.exe", "/c chcp 65001>nul & echo 中文测试", 10000, null);
            Case("cmd 中文输出编码", r.Code == 0 && r.Out.IndexOf("中文测试") >= 0, Res1(r));

            r = Sys.RunPS("Write-Output ('PS中文-' + (3*7))", 30000);
            Case("PowerShell 桥接与中文", r.Code == 0 && r.Out.IndexOf("PS中文-21") >= 0, Res1(r));

            r = Sys.Run("ipconfig.exe", "/all", 20000, null);
            Case("ipconfig 中文解析", r.Code == 0 && r.Out.Length > 200 && r.Out.IndexOf("IPv4") >= 0, "len=" + r.Out.Length + " hasIPv4=" + (r.Out.IndexOf("IPv4") >= 0));

            r = Net.RunNat("netsh.exe", "interface ipv4 show dnsservers");
            Case("netsh 输出读取", r.Code == 0 && r.Out.Length > 20, "exit=" + r.Code + " len=" + r.Out.Length);

            r = Sys.Run("cmd.exe", "/c ping -n 1 -w 800 127.0.0.1", 15000, null);
            Case("子进程超时/等待逻辑", r.Code == 0 && !r.TimedOut, Res1(r));
        }

        // ---------- 2. 注册表 ----------
        private static void T2_Registry()
        {
            Line("");
            Line("---- 2. 注册表读写 ----");
            string key = @"HKCU\Software\WinToolTest";

            bool w = Sys.RegWrite(key, "Str", "REG_SZ", "中文值-abc");
            Case("reg 写入字符串", w, "RegWrite=" + w);
            string got = Sys.RegRead(key, "Str");
            Case("reg 读回字符串", got == "中文值-abc", "读到=" + Flat(got));

            bool w2 = Sys.RegWrite(key, "Num", "REG_DWORD", "1234");
            string got2 = Sys.RegRead(key, "Num");
            Case("reg 写入并读回 DWORD", w2 && got2 == "0x4d2", "读到=" + Flat(got2));

            bool d = Sys.RegDelete(key, "Str") && Sys.RegDelete(key, "Num");
            Case("reg 删除值", d, "RegDelete=" + d);
            Sys.Run("reg.exe", "delete " + Sys.Q(key) + " /f", 10000, null);

            string hk = Sys.RegRead(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
            Case("读取 HKLM 现有值", !string.IsNullOrEmpty(hk), "ProductName=" + Flat(hk));

            // StartupApproved 写入（启动项启用/禁用依赖它）
            string sa = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            bool w3 = Sys.RegWrite(sa, "WinToolTestItem", "REG_BINARY", "030000000000000000000000");
            string g3 = Sys.RegRead(sa, "WinToolTestItem");
            Case("写入 StartupApproved 二进制(禁用启动项用)", w3 && g3 != null, "退出=" + w3 + " 读回=" + Flat(g3));
            Sys.RegDelete(sa, "WinToolTestItem");
        }

        // ---------- 3. 关机链 ----------
        private static void T3_Shutdown()
        {
            Line("");
            Line("---- 3. 关机计划链 ----");

            Res a = ShutdownOps.Schedule("关机", 150, "test", false, false);
            Case("安排延时关机 shutdown /s /t", a.Code == 0, Res1(a));

            Thread.Sleep(800);
            PlanItem p = ShutdownOps.CurrentPlan();
            Case("关机计划写入配置并可读回", p != null && p.Remain > 100, p == null ? "计划为 null" : ("剩余=" + p.Remain + "s 类型=" + p.Kind));

            Res b = ShutdownOps.Abort();
            Case("撤销关机 shutdown /a", b.Code == 0, Res1(b));

            Thread.Sleep(300);
            Case("撤销后计划已清空", ShutdownOps.CurrentPlan() == null, "CurrentPlan=" + (ShutdownOps.CurrentPlan() == null ? "null" : "仍存在"));

            // 再次安排并撤销，验证可重复
            Res c = ShutdownOps.Schedule("重启", 120, null, true, false);
            Thread.Sleep(600);
            Res d = ShutdownOps.Abort();
            Case("重启计划安排+撤销可重复", c.Code == 0 && d.Code == 0, "安排=" + c.Code + " 撤销=" + d.Code);

            string tasks = "";
            try
            {
                List<string> t = ShutdownOps.ScheduledShutdownTasks();
                tasks = "共 " + t.Count + " 条";
                Case("读取系统关机相关计划任务", true, tasks);
            }
            catch (Exception ex) { Case("读取系统关机相关计划任务", false, ex.Message); }

            Res st = ShutdownOps.CreateTaskAt("WinToolTestTask", DateTime.Now.AddMinutes(30), "关机");
            Case("schtasks 创建定时任务", st.Code == 0, Res1(st));
            Res dt = ShutdownOps.DeleteTask("WinToolTestTask");
            Case("schtasks 删除任务", dt.Code == 0, Res1(dt));
        }

        // ---------- 4. 电源 ----------
        private static void T4_Power()
        {
            Line("");
            Line("---- 4. 电源能力 ----");
            try
            {
                bool hib = ShutdownOps.HibernateAvailable();
                Case("检测休眠可用性", true, "可用=" + hib);
            }
            catch (Exception ex) { Case("检测休眠可用性", false, ex.Message); }

            try
            {
                bool fs = ShutdownOps.FastStartupEnabled();
                Case("读取快速启动状态", true, "已开启=" + fs);
            }
            catch (Exception ex) { Case("读取快速启动状态", false, ex.Message); }

            Res r = Sys.Run("powercfg.exe", "/a", 20000, null);
            Case("powercfg /a 可用", r.Code == 0, "exit=" + r.Code + " out=" + Flat(r.Out));

            Res r2 = Sys.Run("powercfg.exe", "/waketimers", 20000, null);
            Case("powercfg /waketimers 可用", !r2.TimedOut && (r2.Out.Length > 0 || r2.Err.Length > 0), Res1(r2));

            Res r3 = Sys.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 20000, null);
            Case("解锁卓越性能电源计划", r3.Code == 0 || r3.All.IndexOf("已存在") >= 0 || r3.All.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0, Res1(r3));
        }

        // ---------- 5. 网络信息 ----------
        private static void T5_Net()
        {
            Line("");
            Line("---- 5. 网络信息解析 ----");
            List<NetAdapter> ads = Net.Adapters();
            Case("枚举网卡 (WMI)", ads.Count > 0, "数量=" + ads.Count);

            NetAdapter on = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
            Case("找到活动网卡", on != null, on == null ? "无" : (on.Name + " " + on.Cidr + " 网关=" + on.PrimaryGateway + " DNS=" + string.Join("/", on.Dns.ToArray()) + " MAC=" + on.Mac));

            if (on != null)
            {
                Case("网卡掩码转点分十进制", on.Masks.Count > 0 && on.Masks[0].Split('.').Length == 4, on.Masks.Count > 0 ? on.Masks[0] : "无掩码");
                Case("CIDR 前缀换算", on.Cidr.IndexOf("/") > 0, on.Cidr);
            }

            try
            {
                WifiInfo w = Net.Wifi();
                Case("Wi-Fi 状态读取", true, "已连接=" + w.On + " SSID=" + Flat(w.SSID) + " 信号=" + Flat(w.SignalPercent));
            }
            catch (Exception ex) { Case("Wi-Fi 状态读取", false, ex.Message); }

            List<string> routes = Net.RouteTable();
            Case("路由表读取", routes.Count > 1, "行数=" + routes.Count + " 首行=" + (routes.Count > 0 ? Flat(routes[1]) : ""));

            List<string> arp = Net.ArpTable();
            Case("ARP 表读取", arp.Count > 0, "行数=" + arp.Count);

            List<NetStatRow> conns = Net.Connections(true);
            Case("活动连接读取 (netstat)", true, "已建立连接=" + conns.Count);

            List<NetStatRow> listen = Net.Connections(false);
            int listening = 0;
            for (int i = 0; i < listen.Count; i++) if (listen[i].Proto == "TCP" && (listen[i].State == "LISTENING" || listen[i].State == "侦听")) listening++;
            Case("监听端口读取", listening > 0, "监听中=" + listening);

            string dns = Net.LocalDnsServer();
            Case("取当前首选 DNS", !string.IsNullOrEmpty(dns), "DNS=" + Flat(dns));

            string outIp = Net.OutboundIP();
            Case("取本机出口 IP (UDP)", Sys.IsValidIP(outIp), "出口=" + Flat(outIp));

            string loc = "";
            string pub = Net.PublicIP(out loc);
            Case("公网 IP 查询 (需外网)", Sys.IsValidIP(pub), "IP=" + Flat(pub) + " 归属=" + Flat(loc));

            int ms = Net.Ping("223.5.5.5", 3000);
            Case("ping 阿里 DNS", ms >= 0, "延迟=" + ms + "ms");

            int ms2 = Net.Ping("www.baidu.com", 3000);
            Case("ping 域名 (需 DNS 正常)", ms2 >= 0, "延迟=" + ms2 + "ms");

            Dictionary<string, int> lat = Net.TestDnsLatency(new string[] { "223.5.5.5", "114.114.114.114", "119.29.29.29" }, 2500);
            Case("DNS 真实解析测速", lat.Count == 3, "223.5.5.5=" + Get(lat, "223.5.5.5") + "ms  114=" + Get(lat, "114.114.114.114") + "ms  119=" + Get(lat, "119.29.29.29") + "ms");
        }

        private static string Get(Dictionary<string, int> d, string k)
        {
            int v;
            return d.TryGetValue(k, out v) ? v.ToString() : "?";
        }

        // ---------- 6. DNS 操作 ----------
        private static void T6_Dns()
        {
            Line("");
            Line("---- 6. DNS 读写（会真实改动网络设置！） ----");
            List<NetAdapter> ads = Net.Adapters();
            NetAdapter on = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
            if (on == null) { Skip("DNS 全部用例", "没有找到活动网卡"); return; }

            List<string> before = DnsOps.Get(on);
            Case("读取网卡当前 DNS", true, "网卡=" + on.Name + " 当前=" + (before.Count == 0 ? "自动(DHCP)" : string.Join("/", before.ToArray())));

            Res r = DnsOps.Set(on, "223.5.5.5", "223.6.6.6");
            Case("设置 DNS 为 223.5.5.5", r.Code == 0, Res1(r));

            Thread.Sleep(900);
            List<string> after = DnsOps.Get(on);
            bool applied = after.Contains("223.5.5.5");
            Case("DNS 是否真的写入系统", applied, "设置后读回=" + (after.Count == 0 ? "空" : string.Join("/", after.ToArray())));

            if (applied)
            {
                int m = Net.Ping("223.5.5.5", 2000);
                Case("改 DNS 后网络仍可达", m >= 0, "ping 223.5.5.5 = " + m + "ms");
            }

            Res r2 = DnsOps.Set(on, "DHCP", "");
            Case("恢复自动获取 DNS", r2.Code == 0, Res1(r2));

            Thread.Sleep(900);
            List<string> restored = DnsOps.Get(on);
            Case("已恢复为 DHCP 下发", !restored.Contains("223.5.5.5"), "读回=" + (restored.Count == 0 ? "自动(DHCP，符合预期)" : string.Join("/", restored.ToArray())));

            Res r3 = DnsOps.FlushCache();
            Case("刷新 DNS 缓存", r3.Code == 0, Res1(r3));

            string cache = DnsOps.ShowCache();
            Case("读取 DNS 缓存内容", cache.Length > 50, "长度=" + cache.Length);

            // 预设库完整性
            List<DnsPreset> lib = DnsLib.All();
            int bad = 0;
            for (int i = 0; i < lib.Count; i++)
                if (lib[i].Primary != "DHCP" && !Sys.IsValidIP(lib[i].Primary)) bad++;
            Case("DNS 预设库地址格式全部合法", bad == 0, "共 " + lib.Count + " 条，非法 " + bad + " 条");
        }

        // ---------- 7. 启动项 ----------
        private static void T7_Startup()
        {
            Line("");
            Line("---- 7. 启动项 ----");
            List<Tools.StartupItem> items = Tools.StartupItems();
            Case("枚举启动项", items.Count > 0, "共 " + items.Count + " 项");

            if (items.Count > 0)
            {
                Tools.StartupItem it = items[0];
                Case("启动项字段解析", it.Name.Length > 0 && it.Location.Length > 0,
                    "样例: " + it.Name + " / " + it.Location + " / 启用=" + it.Enabled + " / " + Flat(it.Command));
            }

            // 自建一个启动项，验证 禁用->启用->清理 的完整链路
            string key = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";
            bool w = Sys.RegWrite(key, "WinToolTestStartup", "REG_SZ", "cmd.exe /c echo test");
            Case("创建测试启动项", w, "RegWrite=" + w);

            List<Tools.StartupItem> items2 = Tools.StartupItems();
            Tools.StartupItem mine = null;
            for (int i = 0; i < items2.Count; i++) if (items2[i].Name == "WinToolTestStartup") { mine = items2[i]; break; }
            Case("新启动项出现在列表中", mine != null, mine == null ? "未找到" : ("启用状态=" + mine.Enabled));

            if (mine != null)
            {
                bool dis = Tools.SetStartupEnabled(mine, false);
                Thread.Sleep(300);
                bool nowDisabled = !Tools.IsStartupEnabled("WinToolTestStartup", true);
                Case("禁用启动项真的生效", dis && nowDisabled, "调用=" + dis + " 复检IsEnabled=" + (!nowDisabled));

                bool en = Tools.SetStartupEnabled(mine, true);
                Thread.Sleep(300);
                bool nowEnabled = Tools.IsStartupEnabled("WinToolTestStartup", true);
                Case("重新启用启动项生效", en && nowEnabled, "调用=" + en + " 复检IsEnabled=" + nowEnabled);
            }

            Sys.RegDelete(key, "WinToolTestStartup");
            Sys.RegDelete(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "WinToolTestStartup");
            Case("清理测试启动项", Sys.RegRead(key, "WinToolTestStartup") == null, "已删除");
        }

        // ---------- 8. 垃圾清理 ----------
        private static void T8_Clean()
        {
            Line("");
            Line("---- 8. 垃圾清理 ----");
            List<CleanTarget> targets = Tools.CleanTargets();
            Case("枚举清理项", targets.Count >= 5, "共 " + targets.Count + " 项");

            // 造一个真实的临时文件，确认统计与删除都能命中
            string probeDir = Path.Combine(Path.GetTempPath(), "WinToolCleanProbe");
            long probeSize = 0;
            try
            {
                Directory.CreateDirectory(probeDir);
                for (int i = 0; i < 5; i++)
                {
                    string f = Path.Combine(probeDir, "p" + i + ".tmp");
                    File.WriteAllBytes(f, new byte[100 * 1024]);
                    probeSize += 100 * 1024;
                }
            }
            catch (Exception ex) { Case("创建测试垃圾文件", false, ex.Message); }

            if (probeSize > 0)
            {
                CleanTarget t = new CleanTarget();
                t.Name = "测试临时目录";
                t.Path = probeDir;
                t.Selected = true;
                Tools.Measure(t);
                Case("统计目录大小与文件数", t.Size == probeSize && t.Files == 5, "大小=" + t.Size + "(期望" + probeSize + ") 文件数=" + t.Files);

                long freed; int skipped;
                List<CleanTarget> one = new List<CleanTarget>();
                one.Add(t);
                Tools.Clean(one, out freed, out skipped);
                bool gone = !Directory.Exists(probeDir) || Directory.GetFiles(probeDir, "*", SearchOption.AllDirectories).Length == 0;
                Case("清理真的删除了文件", freed > 0 && gone, "释放=" + freed + " 跳过=" + skipped + " 目录已空=" + gone);
            }

            // 真实统计系统临时目录（只统计不删除）
            CleanTarget real = null;
            for (int i = 0; i < targets.Count; i++) if (targets[i].Name.IndexOf("用户临时") >= 0) real = targets[i];
            if (real != null)
            {
                Stopwatch sw = Stopwatch.StartNew();
                Tools.Measure(real);
                sw.Stop();
                Case("统计用户临时目录（真实数据）", real.Files >= 0, "文件=" + real.Files + " 大小=" + Sys.FmtBytes(real.Size) + " 耗时=" + sw.ElapsedMilliseconds + "ms");
            }
        }

        // ---------- 9. BAT ----------
        private static void T9_Batch()
        {
            Line("");
            Line("---- 9. BAT 脚本 ----");
            List<BatItem> items = BatchLib.All();
            Case("脚本库加载", items.Count > 0, "共 " + items.Count + " 个脚本");

            int empty = 0, badAdmin = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.IsNullOrEmpty(items[i].Code) || items[i].Code.Length < 10) empty++;
                if (items[i].Name.Length == 0 || items[i].Desc.Length == 0) badAdmin++;
            }
            Case("脚本内容完整性", empty == 0 && badAdmin == 0, "空脚本=" + empty + " 缺描述=" + badAdmin);

            Res r = Bat.RunCapture("@echo off\r\nchcp 65001 >nul\r\necho BAT中文测试\r\necho 退出码校验\r\nexit /b 7\r\n", 30000);
            Case("后台执行 BAT 并取回输出", r.Out.IndexOf("BAT中文测试") >= 0, Res1(r));
            Case("BAT 退出码透传", r.Code == 7, "exit=" + r.Code);

            string p = Bat.Save("WinToolTestScript", "@echo off\r\necho saved\r\n");
            Case("另存为 .bat", p != null && File.Exists(p), "路径=" + Flat(p));
            if (p != null)
            {
                Res r2 = Sys.Run("cmd.exe", "/c " + Sys.Q(p), 20000, null);
                Case("另存的脚本可执行", r2.Out.IndexOf("saved") >= 0, Res1(r2));
                try { File.Delete(p); } catch { }
            }
            Case("脚本目录存在", Directory.Exists(Bat.ScriptDir()), Bat.ScriptDir());
        }

        // ---------- 10. WMI ----------
        private static void T10_Wmi()
        {
            Line("");
            Line("---- 10. WMI / 硬件信息 ----");
            try
            {
                List<string> disks = Tools.Disks();
                Case("读取物理磁盘", disks.Count >= 0, "共 " + disks.Count + " 块" + (disks.Count > 0 ? "  " + Flat(disks[0]) : ""));
            }
            catch (Exception ex) { Case("读取物理磁盘", false, ex.Message); }

            try
            {
                Res r = Sys.Run("powercfg.exe", "/batteryreport /output " + Sys.Q(Path.Combine(Path.GetTempPath(), "wt-batt.html")), 60000, null);
                string f = Path.Combine(Path.GetTempPath(), "wt-batt.html");
                Case("生成电池报告", File.Exists(f), "exit=" + r.Code + " 文件存在=" + File.Exists(f) + " " + Flat(r.All));
            }
            catch (Exception ex) { Case("生成电池报告", false, ex.Message); }

            try
            {
                string act = Tools.ActivationInfo();
                Case("读取激活状态", act.Length > 10, Flat(act));
            }
            catch (Exception ex) { Case("读取激活状态", false, ex.Message); }

            try
            {
                List<Tools.CheckItem> ck = Tools.SysCheck();
                int warn = 0;
                for (int i = 0; i < ck.Count; i++) if (ck[i].Level > 0) warn++;
                Case("系统体检执行", ck.Count >= 5, "共 " + ck.Count + " 项，异常/注意 " + warn + " 项");
            }
            catch (Exception ex) { Case("系统体检执行", false, ex.Message); }
        }

        // ---------- 11. 其他 ----------
        private static void T11_Misc()
        {
            Line("");
            Line("---- 11. 端口 / 进程 / 工具 ----");

            string po = Tools.PortOwner(135);
            Case("端口占用查询", po.Length > 10, Flat(po));

            string bad = Tools.PortOwner(65000);
            Case("未占用端口有明确提示", bad.IndexOf("没有") >= 0 || bad.IndexOf("未被") >= 0, Flat(bad));

            Case("进程名解析", Net.ProcName(4) == "System", "PID4=" + Net.ProcName(4));

            List<NetAdapter> ads = Net.Adapters();
            NetAdapter on = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
            if (on != null)
            {
                Res r = Tools.SetDhcp(on);
                Case("切换为自动获取 IP/DNS", r.Code == 0, Res1(r));
                Thread.Sleep(1200);
                List<NetAdapter> ads2 = Net.Adapters();
                NetAdapter on2 = null;
                for (int i = 0; i < ads2.Count; i++) if (ads2[i].Name == on.Name) { on2 = ads2[i]; break; }
                Case("切换后网卡仍有 IP（未断网）", on2 != null && on2.IPv4.Count > 0,
                    on2 == null ? "网卡消失" : ("IP=" + on2.PrimaryIPv4 + " DHCP=" + on2.Dhcp));
            }

            Res arp = Tools.ArpClear();
            Case("清空 ARP 缓存", arp.Code == 0, Res1(arp));

            Res gp = Tools.GpUpdate();
            Case("刷新组策略 gpupdate", gp.Code == 0, Res1(gp));

            string rep = Path.Combine(Path.GetTempPath(), "wt-report.txt");
            try
            {
                File.WriteAllText(rep, "test", Encoding.UTF8);
                Case("文件写入测试", File.Exists(rep), rep);
                File.Delete(rep);
            }
            catch (Exception ex) { Case("文件写入测试", false, ex.Message); }
        }
    }
}
