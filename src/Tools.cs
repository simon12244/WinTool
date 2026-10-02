using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WinTool
{
    internal class CleanTarget
    {
        public string Name = "";
        public string Path = "";
        public bool Selected = true;
        public long Size;
        public int Files;
        public string Note = "";
    }

    /// <summary>常用系统工具：网络重置、垃圾清理、启动项、体检、Wi-Fi 密码等。</summary>
    internal static class Tools
    {
        // ---------- 网络修复 ----------

        public static string NetRepair(bool resetWinsock, bool resetTcpIp, bool resetFirewall)
        {
            StringBuilder sb = new StringBuilder();
            if (resetWinsock)
            {
                Res r = Sys.Run("netsh.exe", "winsock reset", 30000, null);
                sb.AppendLine("[Winsock 重置] " + (r.Code == 0 ? "成功" : "失败: " + r.All));
            }
            if (resetTcpIp)
            {
                Res r = Sys.Run("netsh.exe", "int ip reset", 30000, null);
                sb.AppendLine("[TCP/IP 重置] " + (r.Code == 0 ? "成功" : "失败: " + r.All));
            }
            if (resetFirewall)
            {
                Res r = Sys.Run("netsh.exe", "advfirewall reset", 40000, null);
                sb.AppendLine("[防火墙重置] " + (r.Code == 0 ? "成功（所有自定义入站规则已清空）" : "失败: " + r.All));
            }
            sb.AppendLine("以上网络重置操作需要【重启电脑】才会完全生效。");
            Log.Op("网络修复", sb.ToString().Replace("\r\n", " | "));
            return sb.ToString();
        }

        public static Res IpRelease() { Res r = Sys.Run("ipconfig.exe", "/release", 30000, null); Log.Op("释放 IP 地址", "ipconfig /release -> " + r.Code); return r; }
        public static Res IpRenew() { Res r = Sys.Run("ipconfig.exe", "/renew", 60000, null); Log.Op("重新获取 IP", "ipconfig /renew -> " + r.Code); return r; }

        public static Res ArpClear()
        {
            Res r = Sys.Run("netsh.exe", "interface ip delete arpcache", 20000, null);
            if (r.Code != 0) r = Sys.Run("arp.exe", "-d *", 20000, null);
            if (r.Code == 0) Log.Ok("ARP 缓存已清空", "netsh interface ip delete arpcache"); else Log.Fail("清空 ARP 缓存失败", r.All);
            return r;
        }

        public static Res GpUpdate()
        {
            Res r = Sys.Run("gpupdate.exe", "/force", 120000, null);
            if (r.Code == 0) Log.Ok("组策略已强制刷新", "gpupdate /force"); else Log.Fail("组策略刷新失败", r.All);
            return r;
        }

        public static bool SetAdapterEnabled(NetAdapter a, bool enable)
        {
            string state = enable ? "enable" : "disable";
            Res r = Sys.Run("netsh.exe", "interface set interface name=" + Sys.Q(a.Name) + " admin=" + state, 20000, null);
            if (r.Code == 0) Log.Ok("已" + (enable ? "启用" : "禁用") + "网卡", a.Name + " -> " + state);
            else Log.Fail("网卡操作失败", a.Name + " " + r.All);
            return r.Code == 0;
        }

        /// <summary>
        /// 判断 netsh 的返回是否应当视为“成功”。
        /// netsh 有已知怪癖：当接口已经启用 DHCP 时，
        /// `set address/dnsservers ... source=dhcp` 会返回 **exit=1**，
        /// 但输出是“已在此接口上启用 DHCP。”——这其实是成功。
        /// 只看退出码会把这种幂等操作误报为失败。
        /// </summary>
        public static bool NetshOk(Res r)
        {
            if (r == null) return false;
            if (r.Code == 0) return true;
            string t = r.All;
            if (string.IsNullOrEmpty(t)) return false;
            if (t.IndexOf("已在此接口上启用", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("已启用 DHCP", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("已在此接口上禁用", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("already enabled", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.IndexOf("DHCP is already enabled", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>把网卡切回 DHCP 自动获取 IP 与 DNS。</summary>
        public static Res SetDhcp(NetAdapter a)
        {
            StringBuilder log = new StringBuilder();
            Res r1 = Net.RunNat("netsh.exe", "interface ipv4 set address name=" + Sys.Q(a.Name) + " source=dhcp", 20000);
            Res r2 = Net.RunNat("netsh.exe", "interface ipv4 set dnsservers name=" + Sys.Q(a.Name) + " source=dhcp", 20000);
            bool ok = NetshOk(r1) && NetshOk(r2);
            log.Append("IP: ").Append(r1.Code).Append("/").Append(NetshOk(r1) ? "成功" : "失败")
               .Append("  DNS: ").Append(r2.Code).Append("/").Append(NetshOk(r2) ? "成功" : "失败");
            if (ok)
            {
                Log.Ok("已切换为自动获取 IP/DNS", a.Name + "  " + log.ToString());
                // 返回一个明确的成功结果，避免上层因 netsh 的 exit=1 怪癖误判
                Res okRes = new Res();
                okRes.Code = 0;
                okRes.Out = r1.Out + (r1.Out.Length > 0 && r2.Out.Length > 0 ? "\n" : "") + r2.Out;
                return okRes;
            }
            Log.Fail("切换 DHCP 失败", a.Name + "  " + log.ToString() + "  " + r1.All + " " + r2.All);
            return r1;
        }

        // ---------- 垃圾清理 ----------

        public static List<CleanTarget> CleanTargets()
        {
            List<CleanTarget> l = new List<CleanTarget>();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string temp = Path.GetTempPath();

            l.Add(Make("用户临时文件", temp, "应用程序运行时产生的临时文件，可安全清理"));
            l.Add(Make("系统临时文件", Path.Combine(win, "Temp"), "系统级临时文件，需管理员权限"));
            l.Add(Make("预读取缓存 Prefetch", Path.Combine(win, "Prefetch"), "会轻微影响程序首次启动速度，随后自动重建"));
            l.Add(Make("最近使用记录", Path.Combine(user, @"AppData\Roaming\Microsoft\Windows\Recent"), "快捷方式记录，清理后“最近使用的文件”列表清空"));
            l.Add(Make("缩略图缓存", Path.Combine(user, @"AppData\Local\Microsoft\Windows\Explorer"), "thumbcache_*.db，清理后缩略图重新生成"));
            l.Add(Make("IE/Edge 缓存", Path.Combine(user, @"AppData\Local\Microsoft\Windows\INetCache"), "网页缓存"));
            l.Add(Make("崩溃转储", Path.Combine(user, @"AppData\Local\CrashDumps"), "程序崩溃内存转储，通常很大"));
            l.Add(Make("Windows 更新缓存", Path.Combine(win, @"SoftwareDistribution\Download"), "已下载的更新包，清理可释放大量空间"));
            l.Add(Make("交付优化缓存", Path.Combine(win, @"SoftwareDistribution\DeliveryOptimization"), "更新分发缓存"));
            l.Add(Make("日志文件", Path.Combine(win, "Logs"), "系统日志目录（正在使用的文件会被跳过）"));
            l.Add(Make("字体缓存", Path.Combine(win, @"ServiceProfiles\LocalService\AppData\Local\FontCache"), "字体缓存，清理后首次打开字体较多界面会稍慢"));
            return l;
        }

        private static CleanTarget Make(string name, string path, string note)
        {
            CleanTarget t = new CleanTarget();
            t.Name = name;
            t.Path = path;
            t.Note = note;
            try { t.Selected = Directory.Exists(path); } catch { t.Selected = false; }
            return t;
        }

        /// <summary>统计单个目录中的文件数量与总大小。</summary>
        public static void Measure(CleanTarget t)
        {
            t.Size = 0; t.Files = 0;
            try
            {
                if (!Directory.Exists(t.Path)) return;
                string[] files = Directory.GetFiles(t.Path, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try { FileInfo fi = new FileInfo(files[i]); t.Size += fi.Length; t.Files++; } catch { }
                }
            }
            catch { }
        }

        /// <summary>清理选中项。freed=释放字节，skipped=被占用跳过数，denied=权限不足跳过数。</summary>
        public static string Clean(List<CleanTarget> targets, out long freed, out int skipped)
        {
            int denied;
            return Clean(targets, out freed, out skipped, out denied);
        }

        public static string Clean(List<CleanTarget> targets, out long freed, out int skipped, out int denied)
        {
            freed = 0; skipped = 0; denied = 0;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < targets.Count; i++)
            {
                if (!targets[i].Selected) continue;
                long sz = 0; int sk = 0, dn = 0;
                CleanDir(targets[i].Path, ref sz, ref sk, ref dn);
                freed += sz; skipped += sk; denied += dn;
                sb.AppendFormat("{0,-22} 释放 {1,-12} 占用跳过 {2,-5} 权限不足 {3}\r\n",
                    targets[i].Name, Sys.FmtBytes(sz), sk, dn);
            }

            // 回收站：Windows 10 用 Shell API 清空才有效（直接删 $Recycle.Bin 目录无效）
            bool rb = Sys.EmptyRecycleBin();
            sb.AppendLine(rb ? "回收站：已清空" : "回收站：清空未成功（可能仍有文件被占用）");

            Log.Op("垃圾清理", "共释放 " + Sys.FmtBytes(freed) + "，被占用跳过 " + skipped
                + "，权限不足跳过 " + denied + "，回收站" + (rb ? "已清空" : "未清空"));
            if (denied > 0 && freed == 0)
                Log.Info("清理未释放任何空间", "所有文件都因权限不足无法删除；请以管理员身份重新运行本工具");
            return sb.ToString();
        }

        private static void CleanDir(string dir, ref long size, ref int skipped, ref int denied)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        long len = new FileInfo(files[i]).Length;
                        File.SetAttributes(files[i], FileAttributes.Normal);
                        File.Delete(files[i]);
                        size += len;
                    }
                    catch (UnauthorizedAccessException) { denied++; }
                    catch (System.Security.SecurityException) { denied++; }
                    catch { skipped++; }
                }
                string[] dirs = Directory.GetDirectories(dir, "*", SearchOption.AllDirectories);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                for (int i = 0; i < dirs.Length; i++)
                {
                    try { if (Directory.GetFileSystemEntries(dirs[i]).Length == 0) Directory.Delete(dirs[i]); }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException) { denied++; }
            catch { }
        }

        // ---------- 启动项 ----------

        public class StartupItem
        {
            public string Name = "";
            public string Command = "";
            public string Location = "";
            public bool Enabled = true;
            public string RegPath = "";
            public string FilePath = "";     // 启动文件夹方式时的快捷方式路径
        }

        public static List<StartupItem> StartupItems()
        {
            List<StartupItem> list = new List<StartupItem>();
            string[] paths = new string[]
            {
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
                @"HKLM\Software\Microsoft\Windows\CurrentVersion\Run",
                @"HKLM\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
            };
            string[] locs = new string[] { "当前用户 (注册表)", "所有用户 (注册表)", "所有用户 32位 (注册表)" };

            for (int i = 0; i < paths.Length; i++)
            {
                Res r = Sys.Run("reg.exe", "query " + Sys.Q(paths[i]), 15000, null);
                if (r.Out.Length == 0) continue;
                string[] lines = r.Out.Replace("\r\n", "\n").Split('\n');
                for (int k = 0; k < lines.Length; k++)
                {
                    string ln = lines[k].Trim();
                    if (ln.Length == 0 || ln.StartsWith("HKEY") || ln.StartsWith("(")) continue;
                    string[] parts = Regex.Split(ln, @"\s{2,}");
                    if (parts.Length < 3) continue;
                    if (parts[1].IndexOf("REG_", StringComparison.OrdinalIgnoreCase) != 0) continue;
                    StartupItem it = new StartupItem();
                    it.Name = parts[0];
                    it.Command = parts[2];
                    it.Location = locs[i];
                    it.RegPath = paths[i];
                    it.Enabled = IsStartupEnabled(it.Name, true);
                    list.Add(it);
                }
            }

            string[] folders = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            };
            for (int i = 0; i < folders.Length; i++)
            {
                try
                {
                    if (!Directory.Exists(folders[i])) continue;
                    string[] files = Directory.GetFiles(folders[i]);
                    for (int k = 0; k < files.Length; k++)
                    {
                        if (Path.GetFileName(files[k]).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        StartupItem it = new StartupItem();
                        it.Name = Path.GetFileNameWithoutExtension(files[k]) + "  （快捷方式）";
                        it.Command = files[k];
                        it.Location = i == 0 ? "当前用户 (启动文件夹)" : "所有用户 (启动文件夹)";
                        it.FilePath = files[k];
                        it.Enabled = !files[k].EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                        list.Add(it);
                    }
                }
                catch { }
            }
            return list;
        }

        /// <summary>判断注册表启动项是否被任务管理器禁用（StartupApproved）。</summary>
        public static bool IsStartupEnabled(string name, bool fromRegistry)
        {
            try
            {
                string[] roots = new string[]
                {
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                    @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                    @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"
                };
                for (int i = 0; i < roots.Length; i++)
                {
                    Res r = Sys.Run("reg.exe", "query " + Sys.Q(roots[i]) + " /v " + Sys.Q(name), 8000, null);
                    if (r.Code != 0) continue;
                    // 值形如 02 00 00 00 ...（启用）或 03 00 00 00 ...（禁用）
                    Match m = Regex.Match(r.Out, @"REG_BINARY\s+([0-9a-fA-F]{2})");
                    if (m.Success)
                    {
                        int first = Convert.ToInt32(m.Groups[1].Value, 16);
                        return first == 2 || first == 6;
                    }
                }
            }
            catch { }
            return true;
        }

        public static bool SetStartupEnabled(StartupItem it, bool enable)
        {
            try
            {
                if (!string.IsNullOrEmpty(it.FilePath))
                {
                    string target = it.FilePath;
                    if (!enable)
                    {
                        File.Move(target, target + ".disabled");
                    }
                    else if (target.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(target, target.Substring(0, target.Length - 9));
                    }
                    Log.Ok((enable ? "已启用" : "已禁用") + "启动项", it.Name + "  " + it.Location);
                    return true;
                }

                string root = it.RegPath.IndexOf("HKCU", StringComparison.OrdinalIgnoreCase) == 0
                    ? @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
                    : @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                string hex = enable
                    ? "02,00,00,00,00,00,00,00,00,00,00,00"
                    : "03,00,00,00,00,00,00,00,00,00,00,00";
                Res r = Sys.Run("reg.exe", "add " + Sys.Q(root) + " /v " + Sys.Q(it.Name) + " /t REG_BINARY /d " + hex.Replace(",", "") + " /f", 10000, null);
                if (r.Code == 0)
                {
                    Log.Ok((enable ? "已启用" : "已禁用") + "启动项", it.Name + "（修改 StartupApproved 状态）");
                    return true;
                }
                Log.Fail("修改启动项失败", r.All);
            }
            catch (Exception ex) { Log.Fail("修改启动项异常", ex.Message); }
            return false;
        }

        // ---------- 系统体检 ----------

        public class CheckItem
        {
            public string Name = "";
            public string Value = "";
            public int Level;   // 0 正常 / 1 注意 / 2 建议处理
            public string Advice = "";
        }

        public static List<CheckItem> SysCheck()
        {
            List<CheckItem> l = new List<CheckItem>();

            // 系统盘剩余空间
            try
            {
                DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                double freeGB = d.AvailableFreeSpace / 1073741824.0;
                double totalGB = d.TotalSize / 1073741824.0;
                CheckItem c = new CheckItem();
                c.Name = "系统盘剩余空间";
                c.Value = string.Format("{0:0.0} GB 可用 / 共 {1:0.0} GB", freeGB, totalGB);
                if (freeGB < 10) { c.Level = 2; c.Advice = "系统盘空间不足 10GB，会明显影响更新与运行速度，建议先做一次垃圾清理。"; }
                else if (freeGB < 25) { c.Level = 1; c.Advice = "建议保持 25GB 以上可用空间。"; }
                else c.Advice = "空间充足。";
                l.Add(c);
            }
            catch { }

            // 内存占用
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        double total = Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1024.0;
                        double free = Convert.ToDouble(o["FreePhysicalMemory"]) / 1024.0;
                        double usedPct = (total - free) / total * 100;
                        CheckItem c = new CheckItem();
                        c.Name = "内存使用率";
                        c.Value = string.Format("{0:0.0}%（{1:0.0} GB / {2:0.0} GB）", usedPct, total - free, total);
                        if (usedPct > 90) { c.Level = 2; c.Advice = "内存接近耗尽，建议关闭不用的程序再操作。"; }
                        else if (usedPct > 75) { c.Level = 1; c.Advice = "内存偏高。"; }
                        else c.Advice = "正常。";
                        l.Add(c);
                    }
                }
            }
            catch { }

            // 是否管理员
            {
                CheckItem c = new CheckItem();
                c.Name = "当前权限";
                c.Value = Sys.Admin ? "管理员" : "普通用户";
                c.Level = Sys.Admin ? 0 : 1;
                c.Advice = Sys.Admin ? "可执行 DNS 修改、网络重置等操作。" : "部分功能（改 DNS、重置网络）需要管理员权限，可点左下角按钮提权。";
                l.Add(c);
            }

            // DNS 配置
            try
            {
                List<NetAdapter> ads = Net.Adapters();
                string dns = "";
                for (int i = 0; i < ads.Count; i++)
                    if (ads[i].Enabled && ads[i].IPv4.Count > 0 && ads[i].Dns.Count > 0) { dns = ads[i].Dns[0]; break; }
                CheckItem c = new CheckItem();
                c.Name = "首选 DNS";
                c.Value = dns.Length > 0 ? dns : "未读取到";
                if (dns.Length == 0) { c.Level = 1; c.Advice = "未检测到 DNS 配置，如无法上网请检查网卡设置。"; }
                else if (dns.StartsWith("8.8.") || dns.StartsWith("1.1.1.1")) { c.Level = 1; c.Advice = "当前使用海外 DNS，国内可能被干扰导致变慢，建议换成阿里/腾讯公共 DNS。"; }
                else c.Advice = "配置正常。国内推荐 223.5.5.5 或 119.29.29.29。";
                l.Add(c);
            }
            catch { }

            // 快速启动 / 休眠
            {
                CheckItem c = new CheckItem();
                bool fast = ShutdownOps.FastStartupEnabled();
                bool hib = ShutdownOps.HibernateAvailable();
                c.Name = "快速启动 / 休眠";
                c.Value = "快速启动：" + (fast ? "已开启" : "已关闭") + "    休眠：" + (hib ? "可用" : "未启用");
                c.Level = 0;
                c.Advice = "快速启动开启时，“睡眠/断电后继续”与部分关机命令的表现会受影响；若关机命令感觉没生效，可到“系统工具”中关闭快速启动再试。";
                l.Add(c);
            }

            // 待重启
            {
                CheckItem c = new CheckItem();
                bool pend = ShutdownOps.SystemShutdownPending();
                c.Name = "系统待重启状态";
                c.Value = pend ? "系统正在等待重启以完成更新" : "无";
                c.Level = pend ? 1 : 0;
                c.Advice = pend ? "存在待完成的重启，此时安排关机可能被系统更新覆盖。" : "正常。";
                l.Add(c);
            }

            // 磁盘健康
            try
            {
                int bad = 0, total = 0;
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT Status FROM Win32_DiskDrive"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        total++;
                        string st = Convert.ToString(o["Status"]);
                        if (st != "OK" && st.Length > 0) bad++;
                    }
                }
                CheckItem c = new CheckItem();
                c.Name = "磁盘状态";
                c.Value = total == 0 ? "未能读取" : (bad == 0 ? total + " 块磁盘状态正常" : bad + " 块磁盘状态异常");
                c.Level = bad > 0 ? 2 : 0;
                c.Advice = bad > 0 ? "磁盘报告异常，建议尽快备份重要数据并检测硬盘。" : "正常。";
                l.Add(c);
            }
            catch { }

            // 待处理的关机计划
            {
                PlanItem p = ShutdownOps.CurrentPlan();
                CheckItem c = new CheckItem();
                c.Name = "关机计划";
                c.Value = p == null ? "无" : (p.Kind + "，剩余 " + Sys.Fmt(p.Remain));
                c.Level = p == null ? 0 : 1;
                c.Advice = p == null ? "当前没有由本工具安排的关机。" : "如需取消，请到“定时关机”页点击“取消关机计划”。";
                l.Add(c);
            }

            return l;
        }

        // ---------- 磁盘 / 电源 / 激活 ----------

        public static List<string> Disks()
        {
            List<string> l = new List<string>();
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT Model,SerialNumber,Size,Status,MediaType FROM Win32_DiskDrive"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        long size = 0;
                        try { size = Convert.ToInt64(o["Size"]); } catch { }
                        l.Add(string.Format("{0}\r\n    容量 {1}   状态 {2}   序列号 {3}\r\n",
                            Convert.ToString(o["Model"]), Sys.FmtBytes(size),
                            Convert.ToString(o["Status"]), Convert.ToString(o["SerialNumber"])));
                    }
                }
            }
            catch { }
            return l;
        }

        public static string BatteryReport()
        {
            if (!Sys.Admin) return "生成电池报告需要管理员权限。";
            try
            {
                string outPath = Bat.TempFile(".html");
                Res r = Sys.Run("powercfg.exe", "/batteryreport /output " + Sys.Q(outPath), 60000, null);
                return r.All + "\r\n报告已生成：" + outPath;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static string SleepReport()
        {
            try
            {
                Res r = Sys.Run("powercfg.exe", "/sleepstudy", 120000, null);
                return r.All + "\r\n（若提示不支持，说明该机型不支持睡眠研究）";
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static string ActivationInfo()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                Res r = Sys.Run("cscript.exe", "//nologo " + Sys.Q(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "slmgr.vbs")) + " /dli", 60000, null);
                if (r.Out.Length > 0) sb.AppendLine(r.Out);
                else
                {
                    Res r2 = Sys.Run("wmic.exe", "path SoftwareLicensingProduct where \"PartialProductKey is not null\" get Name,LicenseStatus,PartialProductKey /format:list", 60000, null);
                    sb.AppendLine(r2.Out);
                }
            }
            catch (Exception ex) { sb.AppendLine(ex.Message); }
            return sb.ToString().Trim();
        }

        public static Res CreateRestorePoint(string desc)
        {
            string ps = "Checkpoint-Computer -Description '" + desc.Replace("'", "''") + "' -RestorePointType 'MODIFY_SETTINGS'";
            Res r = Sys.RunPS(ps, 180000);
            if (r.Code == 0) Log.Ok("已创建系统还原点", desc);
            else Log.Fail("创建还原点失败", r.All.Length > 0 ? r.All : "系统保护可能未开启（此电脑 → 属性 → 系统保护）");
            return r;
        }

        // ---------- 端口占用 ----------

        public static string PortOwner(int port)
        {
            List<NetStatRow> rows = Net.Connections(false);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Local.EndsWith(":" + port) || rows[i].Local.EndsWith(":" + port + ""))
                {
                    sb.AppendFormat("{0,-6}{1,-24}{2,-24}{3,-14}PID {4}  {5}\r\n",
                        rows[i].Proto, rows[i].Local, rows[i].Remote, rows[i].State, rows[i].Pid, rows[i].Proc);
                }
            }
            if (sb.Length == 0) return "端口 " + port + " 当前没有被占用。";
            return "端口 " + port + " 的占用情况：\r\n\r\n" + sb.ToString();
        }

        // ---------- 下载与常用链接 ----------

        public class LinkItem
        {
            public string Name = "";
            public string Url = "";
            public string Note = "";
            public LinkItem(string n, string u, string note) { Name = n; Url = u; Note = note; }
        }

        public static List<LinkItem> CommonLinks()
        {
            List<LinkItem> l = new List<LinkItem>();
            l.Add(new LinkItem("微信 Windows 版", "https://pc.weixin.qq.com/", "官方下载页"));
            l.Add(new LinkItem("QQ PC 版", "https://im.qq.com/pcqq", "官方下载页"));
            l.Add(new LinkItem("钉钉", "https://page.dingtalk.com/wow/dingtalk/act/download", "官方下载页"));
            l.Add(new LinkItem("WPS Office", "https://www.wps.cn/product/wps2019", "官方下载页"));
            l.Add(new LinkItem("360 驱动大师", "https://www.360.cn/qudongdashi/", "装驱动前建议先创建还原点"));
            l.Add(new LinkItem("火绒安全", "https://www.huorong.cn/", "轻量、无捆绑的安全软件"));
            l.Add(new LinkItem("7-Zip 官网", "https://www.7-zip.org/", "开源无广告解压工具"));
            l.Add(new LinkItem("Rufus 启动盘制作", "https://rufus.ie/zh/", "制作 Windows 安装 U 盘"));
            l.Add(new LinkItem("Windows ISO 官方下载", "https://www.microsoft.com/zh-cn/software-download/windows10", "微软官方介质创建工具"));
            l.Add(new LinkItem("清华镜像站", "https://mirrors.tuna.tsinghua.edu.cn/", "国内高速软件/系统镜像"));
            l.Add(new LinkItem("阿里云镜像站", "https://developer.aliyun.com/mirror/", "国内高速镜像"));
            l.Add(new LinkItem("腾讯软件中心", "https://pc.qq.com/", "常用软件下载"));
            return l;
        }
    }
}
