# WinTool — Native Windows System Toolbox v1.0.0

[简体中文](README.md) | **English**

A **pure native Windows desktop tool** (C# WinForms — not an HTML/Electron wrapper) that turns
tasks which normally require a command line or digging through several settings panels into
a single click.

- **Single portable executable**: `WinTool.exe` (~230 KB). Just double-click — no installation,
  no registry writes, no runtime to install.
- **Deeply tied to the OS**: drives `shutdown.exe` / `netsh` / `powercfg` / `schtasks` / WMI / the registry directly.
- **Compatibility**: Windows 7 SP1 / 8.1 / 10 / 11 (needs .NET Framework 4.x, which ships with Windows).
- **Download**: [latest release](https://github.com/simon12244/WinTool/releases/latest)

---

## 1. Features

### 1.1 Scheduled shutdown
| Feature | Description |
|---|---|
| Quick shutdown | One-click 10 min / 30 min / 1 h / 2 h, force-closes unresponsive apps |
| Countdown shutdown | Any number of minutes |
| Shutdown at a specific time | Accepts `22:30`, `tomorrow 06:00`, `2025-06-01 23:00`, `+45m`, `in 30 minutes` |
| Scheduled restart | Same time formats; saves and closes apps first |
| Sleep / Hibernate / Lock / Sign out | Prompts and offers to enable hibernation if unavailable |
| Shutdown after a program exits | Wait for a game download, video transcode or render to finish (12 h cap, gives up on timeout) |
| Live countdown | Persistent remaining time in the status bar; the plan **survives closing the tool** |
| Cancel | One click, or use `shutdown /a` |
| Self-test | Verifies that the `shutdown` command chain works on this machine |

### 1.2 IP & network information
- Local overview: hostname, current user, Wi-Fi status, gateway, primary DNS
- Public egress IP (multi-endpoint fallback)
- Per-adapter detail: IPv4/IPv6, subnet mask, CIDR, default gateway, DNS, DHCP state and server, MAC, link speed
- One-click: ping gateway latency, ping public targets, copy all info, switch to automatic IP, enable/disable adapter
- Active connections / listening ports / IPv4 route table / ARP cache — all with process name and PID

### 1.3 DNS settings
- View and change primary/secondary DNS on any adapter; takes effect immediately, no reboot
- **Built-in curated library of Chinese public DNS providers**, grouped by category with notes:
  - Major providers: Alibaba `223.5.5.5`, Tencent DNSPod `119.29.29.29`, Baidu `180.76.76.76`, CNNIC sDNS `1.2.4.8`
  - Established: 114DNS plain `114.114.114.114`
  - Security: 114DNS malware-blocking `114.114.114.119`, family filter `114.114.114.110`, 360 Safe DNS
  - ISPs: China Telecom / Unicom / Mobile (lowest local latency, but some regions inject DNS ads)
  - Campus: CERNET `101.6.6.6`
  - Overseas (explicitly marked as not recommended): Google `8.8.8.8`, Cloudflare `1.1.1.1`
- **Real resolution benchmark**: sends actual DNS query packets to each server concurrently and times them
- Manual DNS entry with validation, flush cache, view cache contents, restore automatic

### 1.4 System tools
- Network repair: reset Winsock / TCP-IP / firewall (with risk confirmation), flush DNS, clear ARP, refresh Group Policy
- IP management: release, renew, switch to automatic
- Junk cleanup: 11 safe targets (user/system temp, Prefetch, thumbnail cache, crash dumps, update cache…),
  measures sizes first, skips in-use files, **never touches personal files**; reports "in use" vs "access denied" separately
- Startup item management: registry + Startup folder, enable/disable
- One-click health check: disk space, memory usage, privileges, DNS config, fast startup/hibernation, pending reboot, disk health, shutdown plan
- Wi-Fi password viewer (single / all, requires admin)
- Port occupancy lookup + kill the owning process
- System info report export, activation status, battery report, disk info, restore point creation
- Official download links for common software, toggle fast startup, enable/disable hibernation, unlock Ultimate Performance power plan

### 1.5 BAT script library
- **31 ready-made scripts** in 5 categories (network / maintenance / troubleshooting / productivity);
  single click to view, double click to run
- Every script documents its purpose and risks; admin scripts elevate and ask for extra confirmation
- Run in a new window (live output), run as admin, run in background and capture output, save as `.bat`, copy
- Custom BAT editor: write any batch file, run it or save it to `scripts\`

### 1.6 Operation log
- Every action is recorded (time / type / action / full command and return code)
- Written per day to `logs\`, exportable, folder can be opened
- Send the log to someone else to help diagnose a problem

---

## 2. Getting started

1. Put `WinTool.exe` anywhere (a USB stick works), double-click it.
2. Navigate with the sidebar or the shortcuts `Ctrl+1` … `Ctrl+6`. Press `F5` to refresh the current page.
3. On first launch the bottom-left shows the privilege state:
   - `✓ Administrator privileges active` — everything works
   - `⚠ Running with normal privileges · click to elevate` — **click it**, or right-click the exe → Run as administrator
   - `✕ Not an administrator account` — some features are unavailable

### Portable by design
- Configuration lives in `wintool.ini` next to the exe (falls back to `%APPDATA%\WinTool\` if not writable)
- Scripts go to `scripts\`, logs to `logs\`
- Move the whole folder to another machine and your settings and logs come along

---

## 3. Important notes (please read)

### ⚠ Note #1: most features need administrator privileges

This is the most common pitfall. Under UAC, **a program launched by double-click runs with normal
privileges even if the account is an administrator**. In that state:

- Scheduled shutdown / restart / cancel → `Access is denied (5)`
- Changing DNS, resetting the network (Winsock / TCP-IP), enabling/disabling adapters → `Access is denied`
- Disabling startup items, clearing ARP, killing processes, battery report, restore point → `Access is denied`

The bottom-left of the sidebar always shows the current state. The tool also asks whether to elevate
when you use a feature that needs it — it will not fail silently.

### Which features need administrator privileges

| No admin required | Admin required |
|---|---|
| All of IP & network info (adapters / public IP / routes / ARP / ports / connections) | Scheduled shutdown, restart, cancel shutdown plan |
| DNS latency benchmark, view current DNS | Change DNS, restore automatic DNS |
| Health check, view startup items, disk info (read-only) | Enable/disable startup items |
| Port occupancy lookup | Kill the process occupying a port |
| System report export, activation status | Network reset, clear ARP, refresh Group Policy, release/renew IP |
| Download links for common software | Junk cleanup (system temp), Wi-Fi passwords, battery report, restore point, fast startup / hibernation toggles |
| BAT scripts: view, save as, run normal scripts | BAT scripts marked "admin", running custom scripts as admin |

### Other notes

1. **Shutdown/restart runs through the built-in `shutdown` command.** Once set, you can close this tool and
   the plan still fires. To cancel you must click "Cancel shutdown plan" (or run `shutdown /a`) —
   **simply closing the window does not cancel it**.
2. **Forced shutdown (`/f`) terminates unsaved programs.** Save your documents first.
3. On some machines, having "Fast Startup" enabled makes scheduled shutdown behave oddly. Turn it off on the
   System Tools page and retry.
4. Sleep needs graphics/chipset driver support; hibernation must be enabled in Windows (one click on the
   System Tools page; it uses disk space roughly equal to RAM).
5. Corporate/school machines may lock down shutdown rights via domain policy; the command then returns
   "Access is denied (5)", which is expected.
6. **Changing DNS only affects name resolution speed, not bandwidth.** If the network breaks after a change,
   immediately click "Restore automatic".
7. Overseas DNS (`8.8.8.8` / `1.1.1.1`) is frequently interfered with inside mainland China; the tool warns
   about this — don't set it unless you need to.
8. After a network reset (Winsock / TCP-IP) **a reboot is required** for it to fully take effect, and some
   proxy / accelerator / virtual-adapter software must be reconfigured.
9. Junk cleanup only removes temp files and caches. In rare cases a running program may complain about a
   deleted temp file; restarting that program fixes it.
10. This tool needs permission to change network and power settings — **only use an executable from a
    trusted source**.

---

## 4. Building from source

No .NET SDK or Visual Studio needed — the compiler shipped with Windows is enough.

```powershell
# 1) Generate icon assets (only needed the first time or after changing the icon)
powershell -ExecutionPolicy Bypass -File .\dev\make-assets.ps1

# 2) Build (outputs build\WinTool.exe)
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` locates `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and compiles with
`/target:winexe /langversion:5 /optimize+ /win32icon /win32manifest` into a single executable.

### Source layout

| File | Purpose |
|---|---|
| `src/Theme.cs` | Palette, fonts, DPI conversion, custom-drawn rounded buttons / chips / progress bar |
| `src/UI.cs` | Widget factories (cards, notes, inputs, buttons) + natural-language time parsing |
| `src/Sys.cs` | Command engine (encoding detection / timeout / redirect), elevation, registry, config, logging, recycle bin |
| `src/Net.cs` | WMI / ipconfig / netsh / netstat / arp parsing, DNS latency benchmark (hand-built DNS packets) |
| `src/Shutdown.cs` | shutdown / powercfg / schtasks drivers, plan tracking, process-watch shutdown, privilege setup |
| `src/Dns.cs` | Curated DNS library (with notes and risk labels), DNS read/write and cache operations |
| `src/Tools.cs` | Network repair, junk cleanup, startup items, health check, disks/battery/activation, links |
| `src/Batch.cs` | BAT script library and execution engine |
| `src/Page*.cs` | The five feature pages plus the log page |
| `src/MainForm.cs` | Shell: sidebar navigation, header, status bar, DPI scaling, timers |
| `src/Program.cs` | Entry point, on-the-fly icon rendering, global exception capture |
| `src/app.manifest` | DPI awareness, supported OS list, `asInvoker` (elevates only when needed) |

### Technical highlights

- **No third-party libraries, controls or image assets.** The icon is drawn at runtime with GDI+; the whole
  UI is custom-painted.
- **DNS benchmarking implements the DNS wire protocol directly** instead of parsing `nslookup` text.
- **Privilege detection uses `TokenElevation`, not group membership.** Under UAC, "member of Administrators"
  does not mean elevated; checking group membership alone makes the app think it is elevated, fail silently
  and report "Access is denied".
- **Command output encoding is auto-detected.** Built-in console tools (`netsh` / `ipconfig` / `netstat` /
  `reg` / `shutdown`) emit text in the system OEM code page, while `GetConsoleOutputCP()` may report 65001
  in some environments — decoding with the console code page garbles all Chinese text. The implementation
  validates UTF-8 strictly and falls back to the OEM code page.
- **Output is read byte-by-byte and decoded afterwards** instead of using `StandardOutputEncoding` line reads,
  which can silently drop content when the encoding doesn't match.
- All slow work (WMI, network queries, cleanup measurement) runs on background threads — the UI never blocks.
- Layout is converted by the system DPI scale so text never overlaps or gets clipped at high DPI.
- Temp files, the script directory and the battery report all have **writable-path fallbacks**, so the tool
  still works in restricted environments.
- A global exception handler writes `error.log`; the app never dies silently.

### Development / test tools (`dev/`)

| Script | Purpose |
|---|---|
| `dev/build-test.ps1` | Builds the headless functional test harness `WinToolTest.exe` |
| `dev/build-verify.ps1` | Builds the parsing-layer harness `WinToolVerify.exe` (needs no privileges) |
| `dev/VerifyReal.cs` | Real functionality verification against the actual implementation |
| `dev/shots-pages.ps1` | Auto-clicks all six pages and screenshots each, for UI regression |
| `dev/make-assets.ps1` | Regenerates `app.ico` |

```powershell
# Functional tests (prints exit codes and real effects)
powershell -ExecutionPolicy Bypass -File .\dev\build-test.ps1
.\build\WinToolTest.exe .\build\testresult.txt

# Parsing-layer acceptance (encoding detection, command construction, DNS library, adapter parsing)
powershell -ExecutionPolicy Bypass -File .\dev\build-verify.ps1
.\build\WinToolVerify.exe .\build\dns-real.txt .\build\verify.txt
```

`WinTool.exe --selftest` also performs a headless self-check: it schedules a shutdown 180 seconds out and
immediately cancels it, confirming that the local `shutdown` chain works (it never leaves a real plan behind).

---

## 5. Disclaimer

This tool really does shut down, restart, reset network settings and delete files. Make sure you intend the
action before confirming, especially for **forced shutdown**, **network reset** and **killing processes**.
The user is responsible for any data loss caused by misoperation.
