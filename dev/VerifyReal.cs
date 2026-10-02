using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using WinTool;

namespace VerifyReal
{
    /// <summary>
    /// 真实功能验证：直接调用 WinTool 的实际实现（Sys / ShutdownOps / DnsOps / Tools / Bat），
    /// 应在工作区之外运行（沙箱外），才能验证特权操作。
    /// </summary>
    internal static class R
    {
        private static StringBuilder sb = new StringBuilder();
        private static int pass, fail;
        private static string outFile;

        private static void Case(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  ::  " + detail));
            try { File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false)); } catch { }
        }

        private static string Flat(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "(空)";
            s = s.Replace("\r\n", " | ").Replace("\n", " | ").Trim();
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        private static void Main(string[] args)
        {
            outFile = args.Length > 0 ? args[0] : "verify-real.txt";
            sb.AppendLine("=== WinTool 真实功能验证 ===");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("提权: " + (Sys.Admin ? "是" : "否") + "   管理员组: " + Sys.CheckAdminGroup());
            sb.AppendLine("");

            // ---------- 1. 关机链路（真实生效） ----------
            sb.AppendLine("---- 1. 定时关机链路 ----");
            Res arm = ShutdownOps.Schedule("关机", 150, "WinTool 功能验证（将自动取消）", false, false);
            Case("安排 150 秒后关机", arm.Code == 0, "exit=" + arm.Code + " " + Flat(arm.All, 80));

            Thread.Sleep(1500);
            // 系统已有挂起关机时，再安排会返回 1190；用它反证第一次真的成功了
            Res dup = Sys.Run("shutdown.exe", "/s /t 300", 15000, null);
            Case("系统确实进入了关机倒计时（重复安排返回 1190）", dup.Code == 1190,
                "exit=" + dup.Code + " " + Flat(dup.All, 80));

            PlanItem plan = ShutdownOps.CurrentPlan();
            Case("计划已记录到配置", plan != null && plan.Remain > 100, plan == null ? "null" : ("剩余=" + plan.Remain + "s"));

            Res ab = ShutdownOps.Abort();
            Case("撤销关机计划", ab.Code == 0, "exit=" + ab.Code + " " + Flat(ab.All, 80));

            Thread.Sleep(800);
            Res after = Sys.Run("shutdown.exe", "/a", 15000, null);
            Case("撤销后系统已无挂起关机（1116）", after.Code == 1116 || after.Code == 0,
                "exit=" + after.Code + " " + Flat(after.All, 80));

            Res r2 = ShutdownOps.Schedule("重启", 120, null, true, false);
            Thread.Sleep(800);
            Res ab2 = ShutdownOps.Abort();
            Case("重启计划 安排+撤销", r2.Code == 0 && ab2.Code == 0, "安排=" + r2.Code + " 撤销=" + ab2.Code);

            // ---------- 2. 计划任务方式 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 2. schtasks 计划任务（验证日期格式修复） ----");
            Res ct = ShutdownOps.CreateTaskAt("WinToolVerifyTask", DateTime.Now.AddMinutes(45), "关机");
            Case("创建定时关机计划任务", ct.Code == 0, "exit=" + ct.Code + " " + Flat(ct.All, 120));
            if (ct.Code == 0)
            {
                Res q = Sys.Run("schtasks.exe", "/query /tn WinToolVerifyTask", 15000, null);
                Case("任务确实存在", q.Code == 0, "exit=" + q.Code);
                Res dt = ShutdownOps.DeleteTask("WinToolVerifyTask");
                Case("删除任务", dt.Code == 0, "exit=" + dt.Code);
            }

            // ---------- 3. DNS 读写（真实改动网络） ----------
            sb.AppendLine("");
            sb.AppendLine("---- 3. DNS 读写 ----");
            List<NetAdapter> ads = Net.Adapters();
            NetAdapter on = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
            if (on == null) Case("找到活动网卡", false, "无");
            else
            {
                sb.AppendLine("  网卡: " + on.Name + "  当前DNS: " + Flat(string.Join("/", DnsOps.Get(on).ToArray()), 60));
                Res set = DnsOps.Set(on, "223.5.5.5", "114.114.114.114");
                Case("把 DNS 改为 223.5.5.5 / 114.114.114.114", set.Code == 0, "exit=" + set.Code + " " + Flat(set.All, 80));

                Thread.Sleep(1500);
                List<string> now = DnsOps.Get(on);
                Case("系统里 DNS 已真的改变", now.Contains("223.5.5.5"),
                    "读回=" + Flat(string.Join("/", now.ToArray()), 60));

                int lat = Net.DnsLatency("223.5.5.5", 3000);
                Case("用新 DNS 能正常解析", lat >= 0, "解析延迟=" + lat + "ms");

                Res dnsCache = Sys.Run("ipconfig.exe", "/displaydns", 30000, null);
                Case("读取 DNS 缓存（验证编码修复）", dnsCache.Out.Length > 5000,
                    "长度=" + dnsCache.Out.Length + " 条目含RecordName=" + (dnsCache.Out.IndexOf("Record Name") >= 0 || dnsCache.Out.IndexOf("记录名称") >= 0));

                Res back = DnsOps.Set(on, "DHCP", "");
                Case("恢复自动获取 DNS", back.Code == 0, "exit=" + back.Code + " " + Flat(back.All, 80));
                Thread.Sleep(1500);
                List<string> fin = DnsOps.Get(on);
                Case("已恢复 DHCP 下发", !fin.Contains("223.5.5.5"), "读回=" + Flat(string.Join("/", fin.ToArray()), 60));

                // DHCP 幂等性：再次切换应当视为成功
                Res dh = Tools.SetDhcp(on);
                Case("切换为自动获取 IP（幂等，已启用 DHCP 应视为成功）", dh.Code == 0,
                    "exit=" + dh.Code + " " + Flat(dh.All, 80));
            }

            // ---------- 4. 注册表 / 启动项 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 4. 注册表与启动项 ----");
            string key = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";
            bool w = Sys.RegWrite(key, "WinToolVerifyStartup", "REG_SZ", "cmd.exe /c echo x");
            Case("创建测试启动项", w, "RegWrite=" + w);

            if (w)
            {
                List<Tools.StartupItem> items = Tools.StartupItems();
                Tools.StartupItem mine = null;
                for (int i = 0; i < items.Count; i++) if (items[i].Name == "WinToolVerifyStartup") { mine = items[i]; break; }
                Case("测试启动项出现在列表", mine != null, mine == null ? "未找到" : "启用=" + mine.Enabled);

                if (mine != null)
                {
                    bool dis = Tools.SetStartupEnabled(mine, false);
                    Thread.Sleep(400);
                    bool nowOff = !Tools.IsStartupEnabled("WinToolVerifyStartup", true);
                    Case("禁用启动项真的生效", dis && nowOff, "调用=" + dis + " 复检禁用=" + nowOff);

                    bool en = Tools.SetStartupEnabled(mine, true);
                    Thread.Sleep(400);
                    bool nowOn = Tools.IsStartupEnabled("WinToolVerifyStartup", true);
                    Case("重新启用生效", en && nowOn, "调用=" + en + " 复检启用=" + nowOn);
                }
                Sys.RegDelete(key, "WinToolVerifyStartup");
                Sys.RegDelete(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "WinToolVerifyStartup");
                Case("清理测试启动项", Sys.RegRead(key, "WinToolVerifyStartup") == null, "已删除");
            }

            // ---------- 5. 垃圾清理（真实删除） ----------
            sb.AppendLine("");
            sb.AppendLine("---- 5. 垃圾清理 ----");
            string probe = Path.Combine(Path.GetTempPath(), "WinToolCleanProbe");
            long made = 0;
            try
            {
                Directory.CreateDirectory(probe);
                for (int i = 0; i < 6; i++)
                {
                    File.WriteAllBytes(Path.Combine(probe, "f" + i + ".tmp"), new byte[200 * 1024]);
                    made += 200 * 1024;
                }
            }
            catch (Exception ex) { Case("创建测试垃圾文件", false, ex.Message); }
            if (made > 0)
            {
                CleanTarget t = new CleanTarget();
                t.Name = "测试目录"; t.Path = probe; t.Selected = true;
                Tools.Measure(t);
                Case("统计目录", t.Size == made && t.Files == 6, "大小=" + t.Size + " 文件=" + t.Files);

                long freed; int skipped;
                List<CleanTarget> one = new List<CleanTarget>(); one.Add(t);
                Tools.Clean(one, out freed, out skipped);
                bool empty = !Directory.Exists(probe) || Directory.GetFiles(probe, "*", SearchOption.AllDirectories).Length == 0;
                Case("清理真的删除了文件", freed > 0 && empty, "释放=" + freed + " 跳过=" + skipped + " 目录已空=" + empty);
            }

            // 系统临时目录真实清理（统计 -> 清理少量）
            List<CleanTarget> targets = Tools.CleanTargets();
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Name.IndexOf("崩溃转储") >= 0 || targets[i].Name.IndexOf("用户临时") >= 0)
                {
                    Tools.Measure(targets[i]);
                    long freed; int skipped;
                    List<CleanTarget> one = new List<CleanTarget>(); one.Add(targets[i]);
                    Tools.Clean(one, out freed, out skipped);
                    Case("清理系统目录：" + targets[i].Name, freed > 0 || skipped == 0,
                        "统计=" + Sys.FmtBytes(targets[i].Size) + " 释放=" + Sys.FmtBytes(freed) + " 跳过=" + skipped);
                    break;
                }
            }

            // ---------- 6. BAT 执行 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 6. BAT 脚本执行 ----");
            string tmpPath = Bat.TempFile(".bat");
            Case("解析出可写临时路径", !string.IsNullOrEmpty(tmpPath), "路径=" + tmpPath);

            Res bat = Bat.RunCapture("@echo off\r\nchcp 65001 >nul\r\necho BAT中文OK\r\nexit /b 7\r\n", 30000);
            Case("后台执行 BAT 并取回中文输出", bat.Out.IndexOf("BAT中文OK") >= 0, "exit=" + bat.Code + " out=" + Flat(bat.Out, 60));
            Case("BAT 退出码透传", bat.Code == 7, "exit=" + bat.Code);

            string sp = Bat.Save("WinToolVerifyScript", "@echo off\r\necho saved-ok\r\n");
            Case("另存为 .bat 并可执行", sp != null && File.Exists(sp) && Sys.Run("cmd.exe", "/c " + Sys.Q(sp), 20000, null).Out.IndexOf("saved-ok") >= 0,
                "路径=" + Flat(sp, 70));
            if (sp != null) { try { File.Delete(sp); } catch { } }

            // ---------- 7. 网络修复类 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 7. 网络修复类操作 ----");
            Res winsock = Sys.Run("netsh.exe", "winsock reset", 40000, null);
            Case("重置 Winsock", winsock.Code == 0, "exit=" + winsock.Code + " " + Flat(winsock.All, 80));

            Res flush = DnsOps.FlushCache();
            Case("刷新 DNS 缓存", flush.Code == 0, "exit=" + flush.Code);

            Res arp = Tools.ArpClear();
            Case("清空 ARP 缓存", arp.Code == 0 || Flat(arp.All, 40).IndexOf("找不到") >= 0, "exit=" + arp.Code + " " + Flat(arp.All, 60));

            Res gp = Tools.GpUpdate();
            Case("刷新组策略", gp.Code == 0, "exit=" + gp.Code + " " + Flat(gp.All, 80));

            // ---------- 8. 电源 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 8. 电源相关 ----");
            Res ult = Sys.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 25000, null);
            Case("解锁卓越性能电源计划", ult.Code == 0 || Flat(ult.All, 60).IndexOf("已存在") >= 0,
                "exit=" + ult.Code + " " + Flat(ult.All, 80));

            Res batt = Sys.Run("powercfg.exe", "/batteryreport /output " + Sys.Q(Bat.TempFile(".html")), 60000, null);
            Case("生成电池报告", batt.Code == 0, "exit=" + batt.Code + " " + Flat(batt.All, 80));

            // ---------- 9. 端口 / 进程 ----------
            sb.AppendLine("");
            sb.AppendLine("---- 9. 端口与进程 ----");
            string po = Tools.PortOwner(135);
            Case("端口占用查询有结果", po.IndexOf("PID") >= 0, Flat(po, 80));

            // ---------- 汇总 ----------
            sb.AppendLine("");
            sb.AppendLine(string.Format("=== 汇总：通过 {0}  失败 {1} ===", pass, fail));
            try { File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false)); } catch { }
            Console.WriteLine(sb.ToString());
        }
    }
}
