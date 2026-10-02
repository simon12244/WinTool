using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    internal class BatItem
    {
        public string Name = "";
        public string Cat = "";
        public string Desc = "";
        public string Code = "";
        public bool Admin;
        public string Warn = "";

        public BatItem(string name, string cat, string desc, string code)
        {
            Name = name; Cat = cat; Desc = desc; Code = code; Admin = false; Warn = "";
        }

        public BatItem(string name, string cat, string desc, string code, bool admin)
        {
            Name = name; Cat = cat; Desc = desc; Code = code; Admin = admin; Warn = "";
        }

        public BatItem(string name, string cat, string desc, string code, bool admin, string warn)
        {
            Name = name; Cat = cat; Desc = desc; Code = code; Admin = admin; Warn = warn;
        }
    }

    /// <summary>常用 BAT 脚本库：把需要多步操作的事情压成一次点击。</summary>
    internal static class BatchLib
    {
        private static string NL = "\r\n";

        public static List<BatItem> All()
        {
            List<BatItem> l = new List<BatItem>();

            // ===== 网络 =====
            l.Add(new BatItem("一键网络修复（完整）", "网络",
                "刷新 DNS、重置 Winsock/TCP-IP、清 ARP，处理“能上 QQ 打不开网页”“网页一直转圈”等常见故障。需重启生效。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 网络修复" + NL
                + "echo [1/5] 刷新 DNS 缓存..." + NL + "ipconfig /flushdns" + NL
                + "echo [2/5] 重新获取 IP..." + NL + "ipconfig /release >nul 2>nul" + NL + "ipconfig /renew" + NL
                + "echo [3/5] 清空 ARP 缓存..." + NL + "netsh interface ip delete arpcache >nul 2>nul" + NL
                + "echo [4/5] 重置 Winsock..." + NL + "netsh winsock reset" + NL
                + "echo [5/5] 重置 TCP/IP..." + NL + "netsh int ip reset" + NL
                + "echo." + NL + "echo 完成！请重启电脑使重置完全生效。" + NL + "pause", true, "会重置网络协议栈，重启后部分代理/加速器软件需要重新配置。"));

            l.Add(new BatItem("刷新 DNS 缓存", "网络", "清空本机 DNS 解析缓存，解决域名解析到旧地址的问题。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "ipconfig /flushdns" + NL + "nbtstat -R" + NL
                + "echo." + NL + "echo DNS 缓存已刷新。" + NL + "pause"));

            l.Add(new BatItem("固定 IP 示例脚本", "网络",
                "把手动设置固定 IP 的完整命令整理好，改 3 个变量即可用。默认是常见家庭网段 192.168.1.x。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 设置固定IP" + NL
                + "set IFACE=以太网" + NL + "set IP=192.168.1.88" + NL + "set MASK=255.255.255.0" + NL
                + "set GW=192.168.1.1" + NL + "set DNS1=223.5.5.5" + NL + "set DNS2=119.29.29.29" + NL
                + "echo 接口=%IFACE%  IP=%IP%  网关=%GW%" + NL
                + "netsh interface ipv4 set address name=\"%IFACE%\" static %IP% %MASK% %GW%" + NL
                + "netsh interface ipv4 set dnsservers name=\"%IFACE%\" static %DNS1% primary" + NL
                + "netsh interface ipv4 add dnsservers name=\"%IFACE%\" %DNS2% index=2" + NL
                + "echo." + NL + "echo 完成。改回自动获取：netsh interface ipv4 set address name=\"%IFACE%\" source=dhcp" + NL + "pause", true));

            l.Add(new BatItem("重置为自动获取 IP/DNS", "网络", "把所有网卡改回 DHCP 自动获取（最不容易出问题的状态）。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "for /f \"tokens=1* delims=:\" %%a in ('netsh interface ipv4 show interfaces ^| findstr /r /c:\"[0-9][0-9]*\"') do (" + NL
                + "  echo 处理接口: %%b" + NL
                + ")" + NL
                + "echo 也可以直接使用下面的命令（把 以太网 换成你的网卡名）：" + NL
                + "echo netsh interface ipv4 set address name=\"以太网\" source=dhcp" + NL
                + "echo netsh interface ipv4 set dnsservers name=\"以太网\" source=dhcp" + NL + "pause", true));

            l.Add(new BatItem("查看本机所有 IP 与端口", "网络", "一次性列出 IP 配置、DNS、路由表和监听端口。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "echo ===== IP 配置 =====& ipconfig /all" + NL
                + "echo." + NL + "echo ===== 监听端口 =====& netstat -ano | findstr LISTENING" + NL
                + "echo." + NL + "echo ===== 路由表 =====& route print -4" + NL + "pause"));

            l.Add(new BatItem("查看当前 Wi-Fi 密码", "网络", "列出本机保存过的所有 Wi-Fi 名称与明文密码。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title WiFi 密码" + NL
                + "for /f \"skip=9 tokens=1,2 delims=:\" %%i in ('netsh wlan show profiles') do (" + NL
                + "  if \"%%j\" NEQ \"\" (" + NL
                + "    echo ----------------------------------------" + NL
                + "    echo WiFi: %%j" + NL
                + "    netsh wlan show profiles name=\"%%j\" key=clear | findstr /c:\"关键内容\" /c:\"Key Content\"" + NL
                + "  )" + NL
                + ")" + NL + "pause", true));

            l.Add(new BatItem("测试网络连通性", "网络", "批量 ping 网关、公共 DNS 和常用网站，快速判断是断网还是 DNS 问题。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 网络诊断" + NL
                + "echo [1] 检查网卡 IP：" + NL + "ipconfig | findstr /c:\"IPv4\" /c:\"IP Address\"" + NL
                + "echo." + NL + "echo [2] ping 网关（看本地网络）：" + NL
                + "for /f \"tokens=2 delims=:\" %%g in ('ipconfig ^| findstr /c:\"默认网关\" /c:\"Default Gateway\"') do ping -n 2 %%g" + NL
                + "echo." + NL + "echo [3] ping 公共 DNS 223.5.5.5（看外网）：" + NL + "ping -n 2 223.5.5.5" + NL
                + "echo." + NL + "echo [4] 解析并 ping 百度（看 DNS）：" + NL + "ping -n 2 www.baidu.com" + NL
                + "echo." + NL + "echo 若 3 通 4 不通 -> DNS 问题；3 不通 -> 外网或路由问题。" + NL + "pause"));

            // ===== 系统维护 =====
            l.Add(new BatItem("系统文件完整性修复（SFC）", "维护",
                "扫描并修复被破坏的系统文件。耗时 10~40 分钟，期间不要关闭窗口。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 系统文件修复" + NL
                + "echo 即将开始扫描，预计 10-40 分钟，请不要关闭窗口。" + NL
                + "sfc /scannow" + NL + "echo." + NL + "echo 扫描结束。" + NL + "pause", true));

            l.Add(new BatItem("DISM 修复系统映像", "维护",
                "当 SFC 无法修复时报错，用 DISM 从微软服务器拉取健康文件修复。需要联网。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title DISM 修复" + NL
                + "DISM /Online /Cleanup-Image /CheckHealth" + NL
                + "echo." + NL + "DISM /Online /Cleanup-Image /ScanHealth" + NL
                + "echo." + NL + "DISM /Online /Cleanup-Image /RestoreHealth" + NL
                + "echo." + NL + "echo 完成后建议再运行一次 sfc /scannow。" + NL + "pause", true));

            l.Add(new BatItem("生成系统信息报告", "维护",
                "把系统版本、硬件、驱动、网络、已装程序等信息导出到桌面，便于排查或求助。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 系统信息报告" + NL
                + "set OUT=%USERPROFILE%\\Desktop\\系统信息报告.txt" + NL
                + "echo 正在收集，请稍等..." + NL
                + "(echo ===== 系统概览 =====& systeminfo" + NL
                + " echo." + NL + " echo ===== 已安装程序 =====& wmic product get name,version" + NL
                + " echo." + NL + " echo ===== 启动项 =====& wmic startup get caption,command" + NL
                + " echo." + NL + " echo ===== 网络配置 =====& ipconfig /all" + NL
                + ") > \"%OUT%\" 2>&1" + NL
                + "echo 报告已生成：" + NL + "echo %OUT%" + NL + "start \"\" \"%OUT%\"" + NL + "pause", true));

            l.Add(new BatItem("清理系统垃圾（安全项）", "维护",
                "只清理临时文件、日志和回收站等可安全删除的内容，不碰任何个人文件。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 垃圾清理" + NL
                + "echo 正在清理..." + NL
                + "del /f /s /q %TEMP%\\*.* >nul 2>nul" + NL
                + "del /f /s /q %WINDIR%\\Temp\\*.* >nul 2>nul" + NL
                + "del /f /s /q %WINDIR%\\Prefetch\\*.* >nul 2>nul" + NL
                + "del /f /s /q %LOCALAPPDATA%\\CrashDumps\\*.* >nul 2>nul" + NL
                + "del /f /s /q %USERPROFILE%\\AppData\\Local\\Microsoft\\Windows\\INetCache\\*.* >nul 2>nul" + NL
                + "del /f /s /q %USERPROFILE%\\AppData\\Local\\Temp\\*.* >nul 2>nul" + NL
                + "echo." + NL + "echo 清理完成（正在被占用的文件会自动跳过）。" + NL + "pause", true));

            l.Add(new BatItem("修复资源管理器卡顿（重启 explorer）", "维护",
                "任务栏/桌面卡死、图标空白时，无需重启电脑，直接重启桌面进程。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "echo 正在重启资源管理器..." + NL
                + "taskkill /f /im explorer.exe >nul 2>nul" + NL
                + "timeout /t 2 /nobreak >nul" + NL
                + "start explorer.exe" + NL
                + "echo 完成。桌面和任务栏会短暂消失后恢复。" + NL + "pause"));

            l.Add(new BatItem("重建图标缓存", "维护", "桌面图标变白、变错乱时使用。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "taskkill /f /im explorer.exe >nul 2>nul" + NL
                + "del /a /f /q %LOCALAPPDATA%\\IconCache.db >nul 2>nul" + NL
                + "del /a /f /q %LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache* >nul 2>nul" + NL
                + "del /a /f /q %LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\thumbcache* >nul 2>nul" + NL
                + "start explorer.exe" + NL
                + "echo 图标缓存已重建。" + NL + "pause"));

            l.Add(new BatItem("打开系统信息 / 设备管理器 / 服务", "维护", "一次性打开常用系统管理窗口。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "start msinfo32" + NL + "start devmgmt.msc" + NL + "start services.msc" + NL
                + "start diskmgmt.msc" + NL + "start eventvwr.msc" + NL
                + "echo." + NL + "echo 已打开：系统信息 / 设备管理器 / 服务 / 磁盘管理 / 事件查看器。" + NL
                + "echo （窗口可关闭，已打开的窗口不受影响）" + NL + "pause"));

            l.Add(new BatItem("开启 Ultimate Performance 电源计划", "维护",
                "解锁并启用 Windows 最高性能电源计划，适合台式机/插电使用。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61" + NL
                + "echo 已解锁“卓越性能”电源计划，请在 电源选项 中选择它。" + NL + "pause", true));

            l.Add(new BatItem("创建系统还原点", "维护", "动手改系统设置前的保险。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "wmic.exe /Namespace:\\\\root\\default Path SystemRestore Call CreateRestorePoint \"动手前的还原点\", 100, 7" + NL
                + "echo 请求已提交。若提示未启用，请先在 系统属性 - 系统保护 中开启。" + NL + "pause", true));

            // ===== 疑难处理 =====
            l.Add(new BatItem("关闭快速启动（解决关机/休眠异常）", "疑难",
                "关机后风扇还转、定时关机失效、休眠功能不可用时，关闭快速启动再试。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "powercfg /hibernate off" + NL
                + "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 0 /f" + NL
                + "echo 快速启动已关闭。若要恢复：powercfg /hibernate on 并把 HiberbootEnabled 改回 1" + NL + "pause", true));

            l.Add(new BatItem("开启休眠（恢复休眠功能）", "疑难", "笔记本想用休眠、或需要休眠来支持定时关机时使用。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "powercfg /hibernate on" + NL
                + "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 1 /f" + NL
                + "echo 休眠已开启，快速启动也一并开启。" + NL + "pause", true));

            l.Add(new BatItem("重置 Windows 更新组件", "疑难",
                "更新一直失败、卡在某个百分比、更新报 0x800 系列错误时使用。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 重置 Windows 更新" + NL
                + "net stop wuauserv" + NL + "net stop bits" + NL + "net stop cryptsvc" + NL
                + "ren %systemroot%\\SoftwareDistribution SoftwareDistribution.old" + NL
                + "ren %systemroot%\\System32\\catroot2 catroot2.old" + NL
                + "net start wuauserv" + NL + "net start bits" + NL + "net start cryptsvc" + NL
                + "echo." + NL + "echo 更新组件已重置，请重新检查更新。" + NL + "pause", true));

            l.Add(new BatItem("修复 Windows 应用商店", "疑难", "商店打不开、下载卡住时重置商店缓存。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "wsreset.exe" + NL + "echo 商店缓存已重置。" + NL + "pause"));

            l.Add(new BatItem("显示文件扩展名与隐藏文件", "疑难",
                "防病毒改名的常见手法就是隐藏真实扩展名，建议始终显示扩展名。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideFileExt /t REG_DWORD /d 0 /f" + NL
                + "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v Hidden /t REG_DWORD /d 1 /f" + NL
                + "echo 已显示扩展名与隐藏文件（刷新资源管理器后生效）。" + NL + "pause"));

            l.Add(new BatItem("一键打开上帝模式", "疑难", "在桌面创建包含全部系统设置入口的“上帝模式”文件夹。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "mkdir \"%USERPROFILE%\\Desktop\\上帝模式.{ED7BA470-8E54-465E-825C-99712043E01C}\" 2>nul" + NL
                + "echo 已在桌面创建“上帝模式”文件夹。" + NL + "pause"));

            l.Add(new BatItem("检查激活状态", "疑难", "查看 Windows 与 Office 的授权状态。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "cscript //nologo %windir%\\system32\\slmgr.vbs /dli" + NL
                + "echo." + NL + "echo ===== Office =====& " + NL
                + "if exist \"%ProgramFiles%\\Microsoft Office\\Office16\\ospp.vbs\" cscript //nologo \"%ProgramFiles%\\Microsoft Office\\Office16\\ospp.vbs\" /dstatus" + NL
                + "pause"));

            l.Add(new BatItem("查看磁盘 SMART 与分区健康状况", "疑难", "列出物理磁盘状态与各分区剩余空间。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "wmic diskdrive get model,status,size" + NL
                + "echo." + NL + "wmic logicaldisk get caption,volumename,size,freespace,filesystem" + NL
                + "echo." + NL + "chkdsk C: /scan" + NL + "pause", true));

            l.Add(new BatItem("禁用 Windows Defender 实时防护（临时）", "疑难",
                "仅在确有必要时临时关闭；重启后可能自动恢复。关闭期间请勿安装来路不明的程序。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "echo 【安全提示】此操作会临时降低系统防护能力，请在完成后立即重新开启。" + NL
                + "pause" + NL
                + "powershell -Command \"Set-MpPreference -DisableRealtimeMonitoring $true\"" + NL
                + "echo 已完成。恢复命令：powershell -Command \"Set-MpPreference -DisableRealtimeMonitoring $false\"" + NL + "pause", true, "会临时关闭杀毒实时防护，请谨慎使用并及时恢复。"));

            // ===== 效率 =====
            l.Add(new BatItem("打开常用开发/运维工具", "效率", "一次性打开 CMD、PowerShell、注册表、任务管理器、网络连接。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "start cmd" + NL + "start powershell" + NL + "start regedit" + NL
                + "start taskmgr" + NL + "start ncpa.cpl" + NL
                + "echo." + NL + "echo 已打开：CMD / PowerShell / 注册表编辑器 / 任务管理器 / 网络连接。" + NL + "pause"));

            l.Add(new BatItem("定时关机（示例）", "效率",
                "演示 shutdown 的几种写法，可直接改时间使用。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 定时关机示例" + NL
                + "echo 1. 3600 秒（1 小时）后关机：" + NL + "echo    shutdown /s /t 3600" + NL
                + "echo 2. 22:30 定时关机：" + NL + "echo    at 22:30 shutdown /s /f" + NL
                + "echo 3. 取消关机：" + NL + "echo    shutdown /a" + NL
                + "echo." + NL + "set /p MIN=请输入多少分钟后关机（直接回车取消）: " + NL
                + "if \"%MIN%\"==\"\" exit /b" + NL
                + "set /a SEC=%MIN%*60" + NL
                + "shutdown /s /t %SEC% /c \"定时关机已设定\"" + NL
                + "echo 已在 %MIN% 分钟后关机。取消请执行 shutdown /a" + NL + "pause"));

            l.Add(new BatItem("查看端口占用", "效率", "输入端口号，直接看到占用它的程序与 PID。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 端口占用查询" + NL
                + "set /p PORT=请输入要查询的端口号（例如 8080）: " + NL
                + "netstat -ano | findstr :%PORT%" + NL
                + "echo." + NL + "echo 上面的最后一列是 PID，可在任务管理器详细信息中查看对应程序。" + NL + "pause"));

            l.Add(new BatItem("结束占用指定端口的进程", "效率", "先查端口再强制结束，处理“端口被占用”无法启动服务。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 结束端口进程" + NL
                + "set /p PORT=请输入端口号: " + NL
                + "for /f \"tokens=5\" %%a in ('netstat -aon ^| findstr :%PORT% ^| findstr LISTENING') do (" + NL
                + "  echo 结束 PID %%a" + NL + "  taskkill /f /pid %%a" + NL
                + ")" + NL + "pause", true, "会强制结束进程，请确认该端口上的程序可以被关闭。"));

            l.Add(new BatItem("批量重命名文件（加前缀）", "效率", "把当前文件夹下所有文件加上统一前缀，重复文件自动跳过。",
                "@echo off" + NL + "chcp 65001 >nul" + NL + "title 批量重命名" + NL
                + "set /p PRE=请输入前缀（例如 2024_）: " + NL
                + "for %%f in (*.*) do (" + NL
                + "  if not \"%%f\"==\"%~nx0\" (" + NL
                + "    if not exist \"%PRE%%%f\" ren \"%%f\" \"%PRE%%%f\"" + NL
                + "  )" + NL
                + ")" + NL + "echo 完成。" + NL + "pause"));

            l.Add(new BatItem("Wi-Fi 连接质量巡检", "效率", "看信号强度、信道、速率，判断是否被邻居路由干扰。",
                "@echo off" + NL + "chcp 65001 >nul" + NL
                + "netsh wlan show interfaces" + NL
                + "echo." + NL + "echo ===== 周边无线网络（看信道拥挤程度） =====" + NL
                + "netsh wlan show networks mode=bssid" + NL + "pause"));

            return l;
        }

        public static string[] Categories(List<BatItem> items)
        {
            List<string> c = new List<string>();
            for (int i = 0; i < items.Count; i++) if (!c.Contains(items[i].Cat)) c.Add(items[i].Cat);
            return c.ToArray();
        }
    }

    /// <summary>BAT 脚本的生成与执行。</summary>
    internal static class Bat
    {
        public static string ScriptDir()
        {
            string baseDir = Path.GetDirectoryName(Cfg.Path_);
            string dir = Path.Combine(baseDir, "scripts");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, ".write_test"), "");
                return dir;
            }
            catch { }
            try
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinTool", "scripts");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { }
            return dir;
        }

        /// <summary>
        /// 找一个确实可写的临时文件路径。
        /// 部分环境（受控终端、沙箱、%TEMP% 权限受限）下系统临时目录不可写，
        /// 直接写盘会失败并导致脚本根本无法执行，因此这里按优先级回退。
        /// </summary>
        public static string TempFile(string ext)
        {
            string name = "wintool_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext;
            string[] dirs = new string[]
            {
                Path.GetTempPath(),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetDirectoryName(Cfg.Path_)
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                if (string.IsNullOrEmpty(dirs[i])) continue;
                try
                {
                    if (!Directory.Exists(dirs[i])) Directory.CreateDirectory(dirs[i]);
                    string p = Path.Combine(dirs[i], name);
                    File.AppendAllText(p, "");
                    return p;
                }
                catch { }
            }
            return Path.Combine(Path.GetTempPath(), name);
        }

        public static string Save(string fileName, string code)
        {
            try
            {
                string safe = Sanitize(fileName);
                if (!safe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) safe += ".bat";
                string p = Path.Combine(ScriptDir(), safe);
                File.WriteAllText(p, code, new UTF8Encoding(false));
                Log.Ok("已保存脚本", p);
                return p;
            }
            catch (Exception ex) { Log.Fail("保存脚本失败", ex.Message); return null; }
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "script";
            char[] bad = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                bool ok = true;
                for (int k = 0; k < bad.Length; k++) if (name[i] == bad[k]) { ok = false; break; }
                sb.Append(ok ? name[i] : '_');
            }
            return sb.ToString().Trim();
        }

        /// <summary>把脚本写入临时文件并用 cmd.exe 运行（新窗口，可看到实时输出）。</summary>
        public static void RunInWindow(string code, bool asAdmin, string title)
        {
            string p;
            try
            {
                p = TempFile(".bat");
                File.WriteAllText(p, code, new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Fail("脚本写入失败", ex.Message); return; }

            string args = "/c start \"" + (title == null ? "WinTool 脚本" : title) + "\" cmd.exe /k \"" + p + "\"";
            try
            {
                ProcessStartInfo si = new ProcessStartInfo("cmd.exe", args);
                si.UseShellExecute = true;
                if (asAdmin) si.Verb = "runas";
                Process.Start(si);
                Log.Op("执行 BAT 脚本", "新窗口运行 " + Path.GetFileName(p) + (asAdmin ? "（管理员）" : ""));
            }
            catch (Exception ex)
            {
                Log.Fail("脚本启动失败", ex.Message + "（若取消了 UAC 提示，脚本不会执行）");
            }
        }

        /// <summary>静默执行脚本并返回输出文本。</summary>
        public static Res RunCapture(string code, int timeoutMs)
        {
            Res r = new Res();
            try
            {
                string p = TempFile(".bat");
                File.WriteAllText(p, code, new UTF8Encoding(false));
                r = Sys.Run("cmd.exe", "/c " + Sys.Q(p), timeoutMs, null);
                try { File.Delete(p); } catch { }
            }
            catch (Exception ex) { r.Code = -1; r.Err = ex.Message; }
            Log.Op("执行 BAT 脚本（后台）", "返回码 " + r.Code);
            return r;
        }

        public static void OpenScriptFolder()
        {
            Sys.OpenFolder(ScriptDir(), false);
        }
    }
}
