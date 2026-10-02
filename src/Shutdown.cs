using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>当前生效中的定时任务（关机/重启/休眠/进程监控）。</summary>
    internal class PlanItem
    {
        public string Kind = "";          // 关机 / 重启 / 休眠 / 注销
        public long Deadline;             // Unix 秒
        public long Total;                // 总秒数（用于进度条）
        public string Note = "";
        public string Origin = "本工具";  // 本工具 / 系统已有

        public long Remain { get { long r = Deadline - Sys.Now(); return r < 0 ? 0 : r; } }
    }

    /// <summary>关机/电源相关的全部动作，直接驱动 shutdown.exe / powercfg / rundll32。</summary>
    internal static class ShutdownOps
    {
        public const string PLAN_KEY = "plan_deadline";
        public const string PLAN_KIND = "plan_kind";
        public const string PLAN_TOTAL = "plan_total";
        public const string PLAN_NOTE = "plan_note";

        // ---------- 系统级查询 ----------

        [DllImport("user32.dll")]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReserved);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitiateSystemShutdownEx(string machine, string message,
            uint timeout, bool force, bool reboot, uint reason);

        private static volatile bool _inited;
        private static bool _seShutdown;

        /// <summary>授予当前进程 SE_SHUTDOWN_NAME 特权，以便调用底层关机 API。</summary>
        private static bool EnsurePrivilege()
        {
            if (_inited) return _seShutdown;
            _inited = true;
            try
            {
                IntPtr hToken;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken)) return false;
                try
                {
                    LUID luid;
                    if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out luid)) return false;
                    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                    tp.PrivilegeCount = 1;
                    tp.Luid = luid;
                    tp.Attributes = SE_PRIVILEGE_ENABLED;
                    int retLen;
                    _seShutdown = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, out retLen);
                    if (Marshal.GetLastWin32Error() != 0) _seShutdown = false;
                }
                finally { CloseHandle(hToken); }
            }
            catch { _seShutdown = false; }
            return _seShutdown;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public int PrivilegeCount; public LUID Luid; public int Attributes; }

        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_QUERY = 0x0008;
        private const int SE_PRIVILEGE_ENABLED = 0x0002;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr handle, int access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string system, string name, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState,
            int bufferLength, IntPtr prevState, out int retLen);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>系统是否已存在挂起的关机（含策略/更新推送的关机）。</summary>
        public static bool SystemShutdownPending()
        {
            try
            {
                // 计划任务方式（本工具用 shutdown.exe，会生成此键）
                Res r = Sys.Run("reg.exe", "query \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequired\"", 8000, null);
                if (r.Code == 0 && r.Out.Length > 0) return true;
            }
            catch { }
            try
            {
                using (System.Management.ManagementObjectSearcher s = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        // 仅作为辅助信号
                        object b = o["RebootRequired"];
                        if (b != null && !(b is bool) && Convert.ToBoolean(b)) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>读取系统里由 shutdown.exe 记录的“计划关机”信息（注册表 PendingFileRenameOperations 无关）。</summary>
        public static bool ShutdownInProgress()
        {
            try
            {
                uint flags = 0;
                // GetSystemMetrics(0x2000) == SM_SHUTTINGDOWN
                flags = (uint)GetSystemMetrics(0x2000);
                return flags != 0;
            }
            catch { return false; }
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        /// <summary>供电能力：是否支持休眠 / 快速启动。</summary>
        public static bool HibernateAvailable()
        {
            try
            {
                Res r = Sys.Run("powercfg.exe", "/a", 15000, null);
                string t = r.Out;
                if (t.IndexOf("休眠", StringComparison.Ordinal) >= 0 && t.IndexOf("尚未启用", StringComparison.Ordinal) >= 0) return false;
                if (t.IndexOf("Hibernate", StringComparison.OrdinalIgnoreCase) >= 0
                    && t.IndexOf("not available", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                if (t.IndexOf("休眠", StringComparison.Ordinal) >= 0) return true;
                if (t.IndexOf("Hibernate", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            catch { }
            return false;
        }

        public static bool FastStartupEnabled()
        {
            string v = Sys.RegRead(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");
            return v != null && v.Trim() == "1";
        }

        /// <summary>关闭 / 开启快速启动（Win10+）。</summary>
        public static Res SetFastStartup(bool on)
        {
            Sys.RegWrite(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", "REG_DWORD", on ? "1" : "0");
            return Sys.Run("powercfg.exe", on ? "/hibernate on" : "/hibernate off", 20000, null);
        }

        /// <summary>唤醒定时器：查看哪些设备会唤醒电脑。</summary>
        public static string WakeTimers()
        {
            Res r = Sys.Run("powercfg.exe", "/waketimers", 15000, null);
            if (r.Out.Length == 0 && r.Err.Length == 0) return "当前没有活动的唤醒定时器。";
            return r.All;
        }

        /// <summary>查询系统中所有 shutdown.exe 类型的计划任务（第三方或系统设置的定时关机）。</summary>
        public static List<string> ScheduledShutdownTasks()
        {
            List<string> list = new List<string>();
            try
            {
                Res r = Sys.Run("schtasks.exe", "/query /fo LIST /v", 40000, null);
                if (r.Out.Length == 0) return list;
                string[] blocks = r.Out.Replace("\r\n", "\n").Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < blocks.Length; i++)
                {
                    string b = blocks[i];
                    bool isShutdown = b.IndexOf("shutdown", StringComparison.OrdinalIgnoreCase) >= 0
                                   || b.IndexOf("关机", StringComparison.Ordinal) >= 0;
                    if (!isShutdown) continue;
                    string name = Pick(b, "任务名", "TaskName");
                    string next = Pick(b, "下次运行时间", "Next Run Time");
                    if (name.Length == 0) continue;
                    if (name.IndexOf("\\Microsoft\\", StringComparison.OrdinalIgnoreCase) == 0) continue;
                    list.Add(name + (next.Length > 0 ? "    下次运行: " + next : ""));
                }
            }
            catch { }
            return list;
        }

        private static string Pick(string block, string zh, string en)
        {
            string[] lines = block.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int c = lines[i].IndexOf(':');
                if (c <= 0) continue;
                string key = lines[i].Substring(0, c).Trim();
                if (string.Equals(key, zh, StringComparison.OrdinalIgnoreCase) || string.Equals(key, en, StringComparison.OrdinalIgnoreCase))
                    return lines[i].Substring(c + 1).Trim();
            }
            return "";
        }

        // ---------- 核心动作 ----------

        /// <summary>挂起关机/重启计划。seconds 为延迟秒数。</summary>
        public static Res Schedule(string kind, long seconds, string message, bool force, bool firmware)
        {
            if (seconds < 0) seconds = 0;
            StringBuilder a = new StringBuilder();
            if (kind == "重启") a.Append("/r");
            else if (kind == "休眠") a.Append("/h");
            else if (kind == "注销") a.Append("/l");
            else a.Append("/s");
            if (seconds > 0) a.Append(" /t ").Append(seconds);
            else a.Append(" /t 0");
            if (force && kind != "注销" && kind != "休眠") a.Append(" /f");
            if (firmware && (kind == "关机" || kind == "重启")) a.Append(" /fw");
            if (!string.IsNullOrEmpty(message) && kind != "休眠" && kind != "注销")
                a.Append(" /c ").Append(Sys.Q(message));

            Res r = Sys.Run("shutdown.exe", a.ToString(), 20000, null);
            if (r.Code == 0)
            {
                if (kind == "关机" || kind == "重启")
                {
                    Cfg.SetLong(PLAN_KEY, Sys.Now() + seconds);
                    Cfg.SetLong(PLAN_TOTAL, seconds);
                    Cfg.Set(PLAN_KIND, kind);
                    Cfg.Set(PLAN_NOTE, string.IsNullOrEmpty(message) ? ("由 WinTool 设定，延迟 " + Sys.Fmt(seconds)) : message);
                }
                Log.Ok("已安排" + kind, "shutdown.exe " + a.ToString() + (seconds > 0 ? "（" + Sys.Fmt(seconds) + "后执行）" : "（立即执行）"));
            }
            else
            {
                Log.Fail("安排" + kind + "失败", r.All);
            }
            return r;
        }

        /// <summary>撤销所有挂起的关机/重启计划。</summary>
        public static Res Abort()
        {
            Res r = Sys.Run("shutdown.exe", "/a", 15000, null);
            ClearPlan();
            if (r.Code == 0) Log.Ok("已取消关机计划", "shutdown /a 执行成功");
            else Log.Add("提示", "取消关机计划", r.All.Length > 0 ? r.All : "系统当前没有已安排的关机（返回码 " + r.Code + "）");
            return r;
        }

        public static void ClearPlan()
        {
            Cfg.SetLong(PLAN_KEY, 0);
            Cfg.SetLong(PLAN_TOTAL, 0);
            Cfg.Set(PLAN_KIND, "");
            Cfg.Set(PLAN_NOTE, "");
        }

        /// <summary>当前由本工具挂起的计划（无则返回 null）。</summary>
        public static PlanItem CurrentPlan()
        {
            long dl = Cfg.GetLong(PLAN_KEY, 0);
            if (dl <= 0) return null;
            long now = Sys.Now();
            if (dl <= now)
            {
                // 已过时间点，可能是被取消或已执行
                if (dl + 120 < now) { ClearPlan(); return null; }
            }
            PlanItem p = new PlanItem();
            p.Kind = Cfg.Get(PLAN_KIND, "关机");
            p.Deadline = dl;
            p.Total = Cfg.GetLong(PLAN_TOTAL, 60);
            p.Note = Cfg.Get(PLAN_NOTE, "");
            p.Origin = "本工具";
            return p;
        }

        /// <summary>立即动作（不走 shutdown.exe 延迟队列的更轻量通道）。</summary>
        public static void Immediate(string kind)
        {
            if (kind == "锁定")
            {
                try { LockWorkStation(); Log.Ok("已锁定工作站", "LockWorkStation()"); }
                catch (Exception ex) { Log.Fail("锁定失败", ex.Message); }
                return;
            }
            if (kind == "休眠")
            {
                Res r = Sys.Run("shutdown.exe", "/h", 15000, null);
                if (r.Code != 0) r = Sys.Run("rundll32.exe", "powrprof.dll,SetSuspendState 1,1,0", 15000, null);
                if (r.Code == 0) Log.Ok("已进入休眠", "shutdown /h"); else Log.Fail("休眠失败", r.All);
                return;
            }
            if (kind == "睡眠")
            {
                Res r = Sys.Run("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", 15000, null);
                if (r.Code == 0) Log.Ok("已进入睡眠", "SetSuspendState");
                else Log.Fail("睡眠失败", r.All + "  （若系统已关闭休眠且开启了快速启动，睡眠可能不可用）");
                return;
            }
            Schedule(kind, 0, null, true, false);
        }

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        // ---------- 进程监控关机 ----------

        private static Thread _watchThread;
        private static volatile bool _watching;
        private static volatile string _watchTarget = "";
        private static volatile int _watchAction;
        private static volatile int _watchMaxMinutes;

        public static bool IsWatching { get { return _watching; } }
        public static string WatchTarget { get { return _watchTarget; } }

        /// <summary>监控指定进程，退出后执行动作。action: 0 关机 / 1 重启 / 2 睡眠。</summary>
        public static void StartWatch(string processName, int action, int maxMinutes)
        {
            StopWatch();
            _watchTarget = processName;
            _watchAction = action;
            _watchMaxMinutes = maxMinutes;
            _watching = true;
            _watchThread = new Thread(WatchLoop);
            _watchThread.IsBackground = true;
            _watchThread.Start();
            Log.Op("已启动进程监控", "等待 " + processName + " 结束后执行：" + ActionName(action)
                + (maxMinutes > 0 ? "（最长等待 " + maxMinutes + " 分钟，超时自动放弃）" : "（不设超时）"));
        }

        public static string ActionName(int action)
        {
            if (action == 1) return "重启";
            if (action == 2) return "睡眠";
            return "关机";
        }

        public static void StopWatch()
        {
            if (_watchThread != null)
            {
                _watching = false;
                try { _watchThread.Abort(); } catch { }
                _watchThread = null;
            }
        }

        private static void WatchLoop()
        {
            DateTime start = DateTime.Now;
            bool seen = false;
            while (_watching)
            {
                Thread.Sleep(2000);
                if (!_watching) return;
                bool running = Process.GetProcessesByName(_watchTarget).Length > 0;
                if (running) seen = true;
                if (seen && !running)
                {
                    _watching = false;
                    Log.Ok("监控的进程已退出", _watchTarget + " 已结束，执行" + ActionName(_watchAction));
                    if (_watchAction == 2) Immediate("睡眠");
                    else Schedule(ActionName(_watchAction), 30, "由 WinTool 进程监控触发", true, false);
                    return;
                }
                if (_watchMaxMinutes > 0 && (DateTime.Now - start).TotalMinutes > _watchMaxMinutes)
                {
                    _watching = false;
                    Log.Info("进程监控已超时放弃", "等待 " + _watchTarget + " 超过 " + _watchMaxMinutes + " 分钟，未执行任何电源操作");
                    return;
                }
            }
        }

        // ---------- 任务计划方式（更稳的定时关机，替代方案） ----------

        /// <summary>用 schtasks 创建一次性定时关机任务（即使本工具退出也生效）。</summary>
        public static Res CreateTaskAt(string name, DateTime when, string kind)
        {
            string cmd = kind == "重启" ? "shutdown /r /f /t 0" : "shutdown /s /f /t 0";
            // /sd 必须使用当前区域设置的短日期格式，否则中文系统会报“无效开始日期”
            string args = string.Format("/create /tn {0} /tr {1} /sc once /st {2} /sd {3} /f",
                Sys.Q(name), Sys.Q(cmd), when.ToString("HH:mm"), Sys.TaskDate(when));
            Res r = Sys.Run("schtasks.exe", args, 25000, null);
            if (r.Code == 0) Log.Ok("已创建系统计划任务", name + " -> " + when.ToString("yyyy-MM-dd HH:mm") + "  " + cmd);
            else Log.Fail("创建计划任务失败", r.All + "  （命令：" + args + "）");
            return r;
        }

        public static Res DeleteTask(string name)
        {
            Res r = Sys.Run("schtasks.exe", "/delete /tn " + Sys.Q(name) + " /f", 20000, null);
            if (r.Code == 0) Log.Ok("已删除计划任务", name);
            return r;
        }

        public static Res DeleteAllToolTasks()
        {
            Res r = Sys.Run("schtasks.exe", "/delete /tn \"WinTool*\" /f", 20000, null);
            Log.Ok("已清理本工具创建的计划任务", "schtasks /delete /tn WinTool*");
            return r;
        }
    }
}
