using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinTool
{
    /// <summary>命令执行结果。</summary>
    internal class Res
    {
        public int Code;
        public string Out = "";
        public string Err = "";
        public bool TimedOut;
        public string All { get { return ((Out ?? "") + "\n" + (Err ?? "")).Trim(); } }
    }

    /// <summary>配置持久化（绿色便携：优先写在 exe 同目录，不可写则退回 AppData）。</summary>
    internal static class Cfg
    {
        private static string _path;
        private static Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Path_
        {
            get
            {
                if (_path == null)
                {
                    string dir = null;
                    try
                    {
                        string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                        string probe = System.IO.Path.Combine(exeDir, "wintool.ini");
                        File.AppendAllText(probe, "");
                        dir = exeDir;
                    }
                    catch { dir = null; }
                    if (dir == null)
                    {
                        dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinTool");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    }
                    _path = System.IO.Path.Combine(dir, "wintool.ini");
                }
                return _path;
            }
        }

        public static void Load()
        {
            _map.Clear();
            try
            {
                if (!File.Exists(Path_)) return;
                foreach (string line in File.ReadAllLines(Path_, Encoding.UTF8))
                {
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    _map[line.Substring(0, i).Trim()] = line.Substring(i + 1);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# WinTool 配置（可安全删除，程序会自动重建）");
                foreach (KeyValuePair<string, string> kv in _map)
                    sb.AppendLine(kv.Key + "=" + kv.Value);
                File.WriteAllText(Path_, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static string Get(string key, string def)
        {
            string v;
            if (_map.TryGetValue(key, out v)) return v;
            return def;
        }

        public static void Set(string key, string val)
        {
            _map[key] = val == null ? "" : val.Replace("\r", " ").Replace("\n", " ");
            Save();
        }

        public static long GetLong(string key, long def)
        {
            long v;
            if (long.TryParse(Get(key, ""), out v)) return v;
            return def;
        }

        public static void SetLong(string key, long val) { Set(key, val.ToString()); }
    }

    internal class LogItem
    {
        public DateTime Time;
        public string Kind;      // 操作 / 成功 / 失败 / 提示
        public string Text;
        public string Detail;
    }

    /// <summary>操作日志：内存 + 落盘（logs\wintool-yyyyMMdd.log）。</summary>
    internal static class Log
    {
        private static readonly List<LogItem> _items = new List<LogItem>();
        private static readonly object _lock = new object();
        public static event EventHandler Changed;

        public static LogItem[] Snapshot()
        {
            lock (_lock) return _items.ToArray();
        }

        public static void Add(string kind, string text, string detail)
        {
            LogItem it = new LogItem();
            it.Time = DateTime.Now;
            it.Kind = kind;
            it.Text = text;
            it.Detail = detail;
            lock (_lock)
            {
                _items.Add(it);
                if (_items.Count > 800) _items.RemoveAt(0);
            }
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Cfg.Path_), "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string f = System.IO.Path.Combine(dir, "wintool-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                StringBuilder sb = new StringBuilder();
                sb.Append(it.Time.ToString("HH:mm:ss")).Append("  [").Append(kind).Append("]  ").Append(text);
                if (!string.IsNullOrEmpty(detail)) sb.Append("   -> ").Append(detail.Replace("\r\n", " | ").Replace("\n", " | "));
                File.AppendAllText(f, sb.ToString() + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            EventHandler h = Changed;
            if (h != null) { try { h(null, EventArgs.Empty); } catch { } }
        }

        public static void Op(string text, string detail) { Add("操作", text, detail); }
        public static void Ok(string text, string detail) { Add("成功", text, detail); }
        public static void Fail(string text, string detail) { Add("失败", text, detail); }
        public static void Info(string text, string detail) { Add("提示", text, detail); }
    }

    /// <summary>与 Windows 环境深度绑定的底层能力集合。</summary>
    internal static class Sys
    {
        public static readonly bool IsChinese = IsZh();

        /// <summary>
        /// 是否已真正提权（令牌已提升）。
        /// 注意：不能用 IsInRole(Administrator) —— 在 UAC 下，管理员账号的普通进程
        /// 依然属于 Administrators 组，但令牌是受限的，执行 netsh/shutdown/reg 等
        /// 特权操作会返回“拒绝访问(5)”。必须查 TokenElevation 才准确。
        /// </summary>
        public static readonly bool Admin = CheckElevated();

        private static bool IsZh()
        {
            try { return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"; }
            catch { return true; }
        }

        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);

        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int tokenInfoClass,
            out TOKEN_ELEVATION info, int infoLength, out int returnLength);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct TOKEN_ELEVATION { public int TokenIsElevated; }

        private const int TOKEN_QUERY = 0x0008;
        private const int TokenElevationClass = 20;

        /// <summary>令牌是否已提升。失败时退回组成员判断（老系统兼容）。</summary>
        public static bool CheckElevated()
        {
            try
            {
                IntPtr token;
                if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out token))
                {
                    try
                    {
                        TOKEN_ELEVATION info;
                        int ret;
                        if (GetTokenInformation(token, TokenElevationClass, out info,
                                System.Runtime.InteropServices.Marshal.SizeOf(typeof(TOKEN_ELEVATION)), out ret))
                            return info.TokenIsElevated != 0;
                    }
                    finally { CloseHandle(token); }
                }
            }
            catch { }
            return CheckAdminGroup();
        }

        /// <summary>仅判断是否为管理员组成员（不代表已提权）。</summary>
        public static bool CheckAdminGroup()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        /// <summary>是管理员账号但没提权（此时需要提示用户提权）。</summary>
        public static bool NeedsElevation
        {
            get { return !Admin && CheckAdminGroup(); }
        }

        /// <summary>为什么权限不足的可读说明。</summary>
        public static string AdminReason
        {
            get
            {
                if (Admin) return "已获得管理员权限";
                if (CheckAdminGroup()) return "当前以普通权限运行（可提权）";
                return "当前账号不是管理员";
            }
        }

        public static string Text(string zh, string en) { return IsChinese ? zh : en; }

        // ---------- 输出解码 ----------

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetConsoleOutputCP();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetACP();

        private static Encoding _oemEnc, _ansiEnc;

        /// <summary>
        /// 系统 OEM 代码页编码。cmd 内建工具（netsh/ipconfig/netstat/arp/shutdown/reg）
        /// 的中文输出按此代码页，**不能**用控制台输出代码页：
        /// 在部分环境下 GetConsoleOutputCP() 会是 65001，而 cmd 工具仍按 936 输出，
        /// 用 65001 解码会把中文全变成乱码。
        /// </summary>
        public static Encoding Oem
        {
            get
            {
                if (_oemEnc == null)
                {
                    int cp = 0;
                    // 优先级：OEM 代码页 > ANSI 代码页 > 控制台输出代码页
                    try { cp = (int)GetOEMCP(); } catch { }
                    if (cp == 0) { try { cp = (int)GetACP(); } catch { } }
                    if (cp == 0) { try { cp = (int)GetConsoleOutputCP(); } catch { } }
                    if (cp == 0) cp = 936;
                    try { _oemEnc = Encoding.GetEncoding(cp); }
                    catch { try { _oemEnc = Encoding.GetEncoding(936); } catch { _oemEnc = Encoding.Default; } }
                }
                return _oemEnc;
            }
        }

        /// <summary>系统 ANSI 代码页编码。</summary>
        public static Encoding Ansi
        {
            get
            {
                if (_ansiEnc == null)
                {
                    int cp = 0;
                    try { cp = (int)GetACP(); } catch { }
                    if (cp == 0) cp = 936;
                    try { _ansiEnc = Encoding.GetEncoding(cp); }
                    catch { _ansiEnc = Oem; }
                }
                return _ansiEnc;
            }
        }

        private static int CountReplacement(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\uFFFD') n++;
            return n;
        }

        /// <summary>字节序列是否是合法的 UTF-8（严格校验）。</summary>
        public static bool IsValidUtf8(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            bool hasHigh = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b < 0x80) continue;
                hasHigh = true;
                int extra;
                if ((b & 0xE0) == 0xC0) extra = 1;
                else if ((b & 0xF0) == 0xE0) extra = 2;
                else if ((b & 0xF8) == 0xF0) extra = 3;
                else return false;                       // 非法首字节（含 0x80-0xBF 孤立续字节）
                if (i + extra >= bytes.Length) return false;
                for (int k = 1; k <= extra; k++)
                    if ((bytes[i + k] & 0xC0) != 0x80) return false;
                i += extra;
            }
            return hasHigh;
        }

        /// <summary>
        /// 把命令输出的原始字节解码为文本。
        /// 判定依据：先做严格 UTF-8 校验——真的是 UTF-8 才按 UTF-8 解，
        /// 否则按系统 OEM/ANSI 代码页（简体中文为 936）解。
        /// 不能用“替换字符多少”判断：GBK 几乎能把任意字节对吃成字，产生乱码却没有替换字符。
        /// </summary>
        public static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            if (IsValidUtf8(bytes))
            {
                try { return new UTF8Encoding(false, false).GetString(bytes); } catch { }
            }
            try { return Oem.GetString(bytes); }
            catch { }
            try { return new UTF8Encoding(false, false).GetString(bytes); }
            catch { return ""; }
        }

        // ---------- 进程执行 ----------

        public static Res Run(string exe, string args) { return Run(exe, args, 30000, null); }

        public static Res Run(string exe, string args, int timeoutMs, string workDir)
        {
            Res r = new Res();
            Process p = new Process();
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(exe, args);
                si.UseShellExecute = false;
                si.CreateNoWindow = true;
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
                if (!string.IsNullOrEmpty(workDir)) si.WorkingDirectory = workDir;
                p.StartInfo = si;
                p.Start();
            }
            catch (Exception ex)
            {
                r.Code = -1; r.Err = ex.Message;
                return r;
            }

            // 逐字节读取再按代码页解码：
            // 若让 .NET 按固定编码逐行读，遇到不匹配的字节会丢内容（实测 ipconfig 输出曾整段丢失）。
            byte[] outBytes = null, errBytes = null;
            try
            {
                System.Threading.Tasks.Task<byte[]> to = ReadAllAsync(p.StandardOutput.BaseStream);
                System.Threading.Tasks.Task<byte[]> te = ReadAllAsync(p.StandardError.BaseStream);
                if (!p.WaitForExit(timeoutMs))
                {
                    r.TimedOut = true;
                    try { p.Kill(); } catch { }
                }
                try { p.WaitForExit(3000); } catch { }
                if (!to.Wait(4000)) outBytes = new byte[0]; else outBytes = to.Result;
                if (!te.Wait(2000)) errBytes = new byte[0]; else errBytes = te.Result;
            }
            catch
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
            }
            try { r.Code = p.ExitCode; } catch { r.Code = -1; }
            p.Dispose();
            r.Out = DecodeText(outBytes).Trim();
            r.Err = DecodeText(errBytes).Trim();
            return r;
        }

        private static System.Threading.Tasks.Task<byte[]> ReadAllAsync(Stream s)
        {
            return System.Threading.Tasks.Task.Factory.StartNew<byte[]>(delegate()
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[8192];
                    try
                    {
                        int n;
                        while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    }
                    catch { }
                    return ms.ToArray();
                }
            });
        }

        /// <summary>运行 PowerShell 脚本（结果按 UTF-8 解码）。</summary>
        public static Res RunPS(string script) { return RunPS(script, 60000); }

        public static Res RunPS(string script, int timeoutMs)
        {
            Res r = new Res();
            try
            {
                // -EncodedCommand 用 UTF-16LE Base64 传递，彻底避开脚本文件编码与代码页问题
                string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string args = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + b64;
                r = Run("powershell.exe", args, timeoutMs, null);
            }
            catch (Exception ex) { r.Code = -1; r.Err = ex.Message; }
            return r;
        }

        /// <summary>以隐藏窗口后台执行，不等待结果（用于后续用 shutdown /a 撤销的场景）。</summary>
        public static bool RunHidden(string exe, string args)
        {
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(exe, args);
                si.UseShellExecute = false;
                si.CreateNoWindow = true;
                Process.Start(si);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Shell 引用参数（处理空格与引号）。</summary>
        public static string Q(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            bool need = s.IndexOf(' ') >= 0 || s.IndexOf('\t') >= 0 || s.IndexOf('"') >= 0;
            if (!need) return s;
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        // ---------- 提权 ----------

        /// <summary>尝试以管理员身份重启自身，并带上指定参数。成功返回 true。</summary>
        public static bool Elevate(string extraArgs)
        {
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(Application.ExecutablePath);
                si.UseShellExecute = true;
                si.Verb = "runas";
                if (!string.IsNullOrEmpty(extraArgs)) si.Arguments = extraArgs;
                Process.Start(si);
                return true;
            }
            catch { return false; }
        }

        public static bool IsAdminOrAsk(Form owner, string whyText)
        {
            if (Admin) return true;

            string tail = CheckAdminGroup()
                ? "\n\n是否现在以管理员身份重新启动本工具？\n（重新启动后当前未执行完的倒计时关机设置不会丢失）"
                : "\n\n当前登录账号不是管理员，该功能无法使用。\n请改用管理员账号登录，或让管理员为你开放权限。";
            if (!CheckAdminGroup())
            {
                MessageBox.Show(owner, whyText + tail, "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            DialogResult dr = MessageBox.Show(owner,
                "该功能需要管理员权限。\n\n" + whyText + tail,
                "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes)
            {
                if (Elevate(null)) { Application.Exit(); return false; }
                MessageBox.Show(owner, "提权被取消或失败，该功能无法继续。", "已取消", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return false;
        }

        /// <summary>在 UI 线程上安全地询问权限（后台线程取数据后调用本方法不会崩）。</summary>
        public static bool IsAdminOrAskSafe(Form owner, string whyText)
        {
            if (Admin) return true;
            if (owner == null) return false;
            if (!owner.IsHandleCreated) return IsAdminOrAsk(owner, whyText);
            if (owner.InvokeRequired)
            {
                bool result = false;
                try { owner.Invoke((MethodInvoker)delegate { result = IsAdminOrAsk(owner, whyText); }); }
                catch { return false; }
                return result;
            }
            return IsAdminOrAsk(owner, whyText);
        }

        // ---------- 注册表 ----------

        public static string RegRead(string path, string name)
        {
            Res r = Run("reg.exe", "query " + Q(path) + " /v " + Q(name), 10000, null);
            if (r.Code != 0) return null;
            string[] lines = r.Out.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i].Trim();
                if (ln.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = System.Text.RegularExpressions.Regex.Split(ln, @"\s{2,}");
                    if (parts.Length >= 3) return parts[parts.Length - 1].Trim();
                    if (parts.Length == 2) return "";
                }
            }
            return null;
        }

        public static bool RegWrite(string path, string name, string type, string value)
        {
            Res r = Run("reg.exe", "add " + Q(path) + " /v " + Q(name) + " /t " + type + " /d " + Q(value) + " /f", 10000, null);
            return r.Code == 0;
        }

        public static bool RegDelete(string path, string name)
        {
            Res r = Run("reg.exe", "delete " + Q(path) + " /v " + Q(name) + " /f", 10000, null);
            return r.Code == 0;
        }

        // ---------- 时间/格式化 ----------

        public static long Now() { return DateTime.Now.Ticks / TimeSpan.TicksPerSecond; }

        public static string Fmt(long seconds)
        {
            if (seconds < 0) seconds = 0;
            long h = seconds / 3600;
            long m = (seconds % 3600) / 60;
            long s = seconds % 60;
            if (h > 0) return string.Format("{0} 小时 {1} 分 {2} 秒", h, m, s);
            if (m > 0) return string.Format("{0} 分 {1} 秒", m, s);
            return string.Format("{0} 秒", s);
        }

        public static string FmtHM(long seconds)
        {
            if (seconds < 0) seconds = 0;
            return string.Format("{0:00}:{1:00}:{2:00}", seconds / 3600, (seconds % 3600) / 60, seconds % 60);
        }

        public static string FmtBytes(long b)
        {
            string[] u = new string[] { "B", "KB", "MB", "GB", "TB" };
            double v = b;
            int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return string.Format("{0:0.##} {1}", v, u[i]);
        }

        /// <summary>把 8 位十六进制掩码转成点分十进制。</summary>
        public static string MaskToDotted(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "";
            if (hex.IndexOf('.') >= 0) return hex;
            hex = hex.Trim();
            if (hex.Length != 8) return hex;
            try
            {
                uint v = Convert.ToUInt32(hex, 16);
                return string.Format("{0}.{1}.{2}.{3}", (v >> 24) & 0xFF, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
            }
            catch { return hex; }
        }

        public static string PrefixFromMask(string mask)
        {
            try
            {
                string[] p = mask.Split('.');
                if (p.Length != 4) return "";
                uint v = 0;
                for (int i = 0; i < 4; i++) v = (v << 8) | uint.Parse(p[i]);
                int n = 0;
                for (int i = 31; i >= 0; i--) { if ((v & (1u << i)) != 0) n++; else break; }
                return "/" + n;
            }
            catch { return ""; }
        }

        public static bool IsPrivateIP(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            if (ip.StartsWith("10.")) return true;
            if (ip.StartsWith("192.168.")) return true;
            if (ip.StartsWith("127.")) return true;
            if (ip.StartsWith("169.254.")) return true;
            if (ip.StartsWith("172."))
            {
                string[] p = ip.Split('.');
                if (p.Length > 1)
                {
                    int n;
                    if (int.TryParse(p[1], out n) && n >= 16 && n <= 31) return true;
                }
            }
            return false;
        }

        public static bool IsValidIP(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string[] p = s.Split('.');
            if (p.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                int n;
                if (!int.TryParse(p[i], out n)) return false;
                if (n < 0 || n > 255) return false;
                if (p[i].Length > 1 && p[i][0] == '0') return false;
            }
            return true;
        }

        public static bool IsValidIPv6(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.IndexOf(':') < 0) return false;
            try
            {
                System.Net.IPAddress a;
                return System.Net.IPAddress.TryParse(s, out a)
                    && a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            }
            catch { return false; }
        }

        /// <summary>DNS 地址是否可用于本工具的 DNS 设置（IPv4 或 IPv6 均可）。</summary>
        public static bool IsValidDns(string s)
        {
            return IsValidIP(s) || IsValidIPv6(s);
        }

        /// <summary>
        /// schtasks /sd 要求的日期写法。
        /// 必须是 yyyy/MM/dd（或 yyyy-MM-dd）：实测传 MM/dd/yyyy 会报
        /// “无效开始日期(日期格式应该是 yyyy/mm/dd)”，且年月日不能补零错误（如 00002026）。
        /// </summary>
        public static string TaskDate(DateTime t)
        {
            return t.ToString("yyyy'/'MM'/'dd");
        }

        public static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(url);
                si.UseShellExecute = true;
                Process.Start(si);
            }
            catch { }
        }

        public static void OpenPath(string path)
        {
            try
            {
                ProcessStartInfo si = new ProcessStartInfo(path);
                si.UseShellExecute = true;
                Process.Start(si);
            }
            catch { }
        }

        public static void OpenFolder(string path, bool selectFile)
        {
            try
            {
                if (selectFile) Process.Start("explorer.exe", "/select," + Q(path));
                else Process.Start("explorer.exe", Q(path));
            }
            catch { }
        }

        public static void Copy(string text)
        {
            try { Clipboard.SetText(text ?? ""); } catch { }
        }

        // ---------- 回收站 ----------

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        /// <summary>
        /// 清空回收站。Windows 10 下直接删 $Recycle.Bin 目录是无效的，
        /// 必须走 Shell API。返回是否成功。
        /// </summary>
        public static bool EmptyRecycleBin()
        {
            try
            {
                // 只用“整个系统”这一种调用；传盘符会返回 0x8000FFFF（实测）
                int hr = SHEmptyRecycleBin(IntPtr.Zero, null,
                    SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (hr == 0) return true;                  // S_OK
                if (hr == 1) return true;                  // S_FALSE：回收站本来就是空的
                // 明确失败时记录真实 HRESULT，便于排查（不静默吞掉）
                Log.Info("清空回收站未成功", "SHEmptyRecycleBin 返回 0x" + hr.ToString("X8"));
            }
            catch (Exception ex)
            {
                Log.Info("清空回收站异常", ex.Message);
            }
            return false;
        }
    }
}
