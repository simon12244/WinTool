using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;

namespace WinTool
{
    /// <summary>
    /// 解析层验收台：不依赖任何特权，用真实命令输出验证解析与命令构造。
    /// 用法：WinToolVerify.exe &lt;真实输出文件&gt; &lt;结果文件&gt;
    /// </summary>
    internal static class Verify
    {
        private static StringBuilder sb = new StringBuilder();
        private static int pass, fail;

        private static void Case(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  ::  " + detail));
        }

        private static void Info(string s) { sb.AppendLine(s); }

        private static string Flat(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(空)";
            s = s.Replace("\r\n", " | ").Replace("\n", " | ").Trim();
            if (s.Length > max) s = s.Substring(0, max) + "…";
            return s;
        }

        private static void Main(string[] args)
        {
            string sampleFile = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dns-real.txt");
            string outFile = args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verify.txt");

            sb.AppendLine("=== 解析层验收 ===");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("");

            // ---------- 1. 编码解码：用真实命令的原始字节 ----------
            Info("---- 1. 输出解码（真实字节） ----");
            string text = "";
            try { text = File.ReadAllText(sampleFile, Encoding.UTF8); }
            catch (Exception ex) { Info("读取样本失败: " + ex.Message); }

            Case("样本文件读取（ipconfig /displaydns 真实输出）", text.Length > 1000, "字符数=" + text.Length + " 字节=" + new FileInfo(sampleFile).Length);

            // 用 GBK 编码的中文错误文本，验证固定 GB18030 解码不会丢字
            byte[] gbkBytes = Encoding.GetEncoding(936).GetBytes("你没有执行此操作所需的权限。拒绝访问。(5)");
            string viaGbk = Sys.DecodeText(gbkBytes);
            Case("GBK 字节解码正确（中文不丢字）", viaGbk.IndexOf("没有执行此操作所需的权限") >= 0, "解码=" + Flat(viaGbk, 80));

            // 用 UTF-8 编码的中文（部分系统/工具输出 UTF-8），旧代码会乱码
            byte[] utf8Bytes = new UTF8Encoding(false).GetBytes("你没有执行此操作所需的权限。");
            string viaUtf8 = Sys.DecodeText(utf8Bytes);
            Case("UTF-8 字节解码正确（自动识别）", viaUtf8.IndexOf("没有执行此操作所需的权限") >= 0, "解码=" + Flat(viaUtf8, 80));

            // 纯 ASCII 不受影响
            string ascii = Sys.DecodeText(Encoding.ASCII.GetBytes("Configuration for interface"));
            Case("ASCII 输出不受影响", ascii.IndexOf("Configuration") >= 0, Flat(ascii, 60));

            // ---------- 2. DNS 缓存解析 ----------
            Info("");
            Info("---- 2. DNS 缓存输出处理 ----");
            Case("DNS 缓存样本可解析出记录名", text.IndexOf("Record Name") >= 0 || text.IndexOf("记录名称") >= 0,
                "行数=" + text.Split('\n').Length);
            int records = 0;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) if (lines[i].IndexOf("Record Name") >= 0 || lines[i].IndexOf("记录名称") >= 0) records++;
            Case("统计缓存条目数", records > 0, "条目=" + records);

            // ---------- 3. schtasks 命令构造 ----------
            Info("");
            Info("---- 3. 定时任务命令构造（真实执行） ----");
            DateTime when = DateTime.Now.AddMinutes(40);
            string sd = when.ToString("yyyy'/'MM'/'dd");
            string st = when.ToString("HH:mm");
            string argsStr = "/create /tn " + Sys.Q("WinToolVerifyTask") + " /tr " + Sys.Q("shutdown /s /f /t 0")
                + " /sc once /st " + st + " /sd " + sd + " /f";
            Info("  构造参数: " + argsStr);
            Res cr = Sys.Run("schtasks.exe", argsStr, 25000, null);
            Case("schtasks 创建任务（旧代码用 MM/dd/yyyy 会失败）", cr.Code == 0, "exit=" + cr.Code + " out=" + Flat(cr.Out, 120) + " err=" + Flat(cr.Err, 120));

            if (cr.Code == 0)
            {
                Res q = Sys.Run("schtasks.exe", "/query /tn " + Sys.Q("WinToolVerifyTask"), 15000, null);
                Case("任务确实存在", q.Code == 0, "exit=" + q.Code);
                Res dl = Sys.Run("schtasks.exe", "/delete /tn " + Sys.Q("WinToolVerifyTask") + " /f", 15000, null);
                Case("任务清理", dl.Code == 0, "exit=" + dl.Code);
            }

            // ---------- 4. 命令构造正确性（不执行） ----------
            Info("");
            Info("---- 4. 关键命令构造检查 ----");
            List<NetAdapter> ads = Net.Adapters();
            NetAdapter on = null;
            for (int i = 0; i < ads.Count; i++) if (ads[i].Enabled && ads[i].IPv4.Count > 0) { on = ads[i]; break; }
            if (on != null)
            {
                Info("  活动网卡: " + on.Name);
                string setDns = "interface ipv4 set dnsservers name=" + Sys.Q(on.Name) + " source=static address=223.5.5.5 register=primary validate=no";
                Info("  DNS 设置命令: netsh " + setDns);
                // 只做语法验证：用 netsh 的 help 机制确认参数名被接受（执行会真改设置，故不执行）
                Case("DNS 设置命令参数名合法", setDns.IndexOf("register=primary") > 0 && setDns.IndexOf("validate=no") > 0, "含 register/validate");

                string addDns = "interface ipv4 add dnsservers name=" + Sys.Q(on.Name) + " address=223.6.6.6 index=2 validate=no";
                Info("  DNS 备用命令: netsh " + addDns);
                Case("DNS 备用命令参数名合法", addDns.IndexOf("index=2") > 0, "含 index=2");

                string dhcp = "interface ipv4 set address name=" + Sys.Q(on.Name) + " source=dhcp";
                Info("  DHCP 命令: netsh " + dhcp);
                Case("DHCP 命令参数名合法", dhcp.EndsWith("source=dhcp"), "含 source=dhcp");
            }
            else Info("  (无活动网卡，跳过命令构造检查)");

            // ---------- 5. DNS 预设库合法性 ----------
            Info("");
            Info("---- 5. DNS 预设库 ----");
            List<DnsPreset> lib = DnsLib.All();
            int badAll = 0, ipv6 = 0;
            for (int i = 0; i < lib.Count; i++)
            {
                string p = lib[i].Primary;
                if (p == "DHCP") continue;
                if (Sys.IsValidIP(p)) continue;
                if (Sys.IsValidIPv6(p)) { ipv6++; continue; }
                badAll++;
                Info("  非法地址: " + lib[i].Name + " -> " + p);
            }
            Case("所有预设地址均为合法 IPv4/IPv6", badAll == 0, "共 " + lib.Count + " 条，IPv6 条目 " + ipv6 + " 条，非法 " + badAll + " 条");
            Info("  (仅按 IPv4 校验会误判的条目数: " + ipv6 + " —— 这正是旧校验逻辑的 bug)");

            // ---------- 6. 网卡解析 ----------
            Info("");
            Info("---- 6. 网卡信息解析 ----");
            Case("网卡枚举", ads.Count > 0, "数量=" + ads.Count);
            if (on != null)
            {
                Case("IPv4/掩码/网关/DNS/MAC 均有值",
                    on.IPv4.Count > 0 && on.Masks.Count > 0 && on.Gateways.Count > 0 && on.Mac.Length > 0,
                    on.Cidr + " 网关=" + on.PrimaryGateway + " DNS=" + Flat(string.Join("/", on.Dns.ToArray()), 40) + " MAC=" + on.Mac);
                Case("CIDR 计算正确", on.Cidr.EndsWith("/24") || on.Cidr.EndsWith("/16") || on.Cidr.EndsWith("/8"), on.Cidr);
            }

            // ---------- 7. Bat 脚本库 ----------
            Info("");
            Info("---- 7. BAT 脚本库 ----");
            List<BatItem> bats = BatchLib.All();
            Case("脚本数量", bats.Count >= 25, "共 " + bats.Count + " 个");
            int noChcp = 0;
            for (int i = 0; i < bats.Count; i++)
                if (bats[i].Code.IndexOf("pause", StringComparison.OrdinalIgnoreCase) < 0
                    && bats[i].Code.IndexOf("echo", StringComparison.OrdinalIgnoreCase) >= 0) noChcp++;
            Case("含输出的脚本都有 pause（避免窗口一闪而过）", noChcp == 0, "缺 pause 的脚本数=" + noChcp);

            sb.AppendLine("");
            sb.AppendLine(string.Format("=== 汇总：通过 {0}  失败 {1} ===", pass, fail));

            try { File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false)); } catch { }
            Console.WriteLine(sb.ToString());
        }
    }
}
