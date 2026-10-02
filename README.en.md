# WinTool — Native Windows System Toolbox

**Pure native Windows desktop tool · single portable executable · zero dependencies**

[![Release](https://img.shields.io/github/v/release/simon12244/WinTool?style=flat-square&label=release)](https://github.com/simon12244/WinTool/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/simon12244/WinTool/total?style=flat-square&label=downloads)](https://github.com/simon12244/WinTool/releases)
[![License](https://img.shields.io/github/license/simon12244/WinTool?style=flat-square&label=license)](LICENSE)
[![Size](https://img.shields.io/badge/size-232%20KB-2ea44f?style=flat-square)](#download)
[![Platform](https://img.shields.io/badge/Windows-7%20SP1%20%7C%208.1%20%7C%2010%20%7C%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](#system-requirements)

[简体中文](README.md) · **English**

[Download](#download) · [Screenshots](#screenshots) · [Features](#features) · [Getting started](#quick-start) · [Privileges](#-note-1-most-features-need-administrator-privileges) · [Build](#building-from-source)

---

A **pure native Windows desktop tool** (C# WinForms — not an HTML/Electron wrapper) that turns
tasks which normally require a command line or digging through several settings panels into
a single click. It drives the built-in
`shutdown.exe` / `netsh` / `powercfg` / `schtasks` / WMI / registry directly.

| | |
|---|---|
| **Size** | Single `WinTool.exe`, **232 KB** |
| **Dependencies** | **None.** No third-party libraries, controls or image assets; the icon is drawn at runtime with GDI+ |
| **Installation** | Not required. Portable — just double-click, no registry writes |
| **Runtime** | Only .NET Framework 4.x, which ships with Windows |
| **OS** | Windows 7 SP1 / 8.1 / 10 / 11 |
| **Build** | Only the bundled `csc.exe` — **no .NET SDK or Visual Studio needed** |

---

## Download

| File | Size | Description |
|---|---|---|
| **[WinTool.exe](https://github.com/simon12244/WinTool/releases/latest/download/WinTool.exe)** | 232 KB | Single portable executable, just double-click |
| [WinTool-v1.0.0-src.zip](https://github.com/simon12244/WinTool/releases/latest/download/WinTool-v1.0.0-src.zip) | 296 KB | Full source plus build scripts |

More versions on the [Releases page](https://github.com/simon12244/WinTool/releases).

### System requirements

- Windows 7 SP1 / 8.1 / 10 / 11 (x86 / x64)
- .NET Framework 4.x — **ships with Windows 7 SP1 and later, no separate install needed**
- No network required; online features (public IP lookup, DNS benchmark) degrade gracefully when offline

---

## Screenshots

### Scheduled shutdown
![Scheduled shutdown](docs/screenshots/shutdown.png)

### IP & network information
![Network information](docs/screenshots/network.png)

### DNS settings
![DNS settings](docs/screenshots/dns.png)

<details>
<summary><b>Show the other three (System tools / BAT library / Log)</b></summary>

### System tools
![System tools](docs/screenshots/tools.png)

### BAT script library
![BAT script library](docs/screenshots/batch.png)

### Operation log
![Operation log](docs/screenshots/log.png)

</details>

---

## Features

<details open>
<summary><b>Scheduled shutdown</b> — the plan survives closing the tool</summary>

| Feature | Description |
|---|---|
| Quick shutdown | One-click 10 min / 30 min / 1 h / 2 h, force-closes unresponsive apps |
| Countdown shutdown | Any number of minutes |
| Shutdown at a specific time | Accepts `22:30`, `tomorrow 06:00`, `2025-06-01 23:00`, `+45m`, `in 30 minutes` |
| Scheduled restart | Same time formats; saves and closes apps first |
| Sleep / Hibernate / Lock / Sign out | Prompts and offers to enable hibernation if unavailable |
| Shutdown after a program exits | Wait for a game download, video transcode or render to finish (12 h cap) |
| Live countdown | Persistent remaining time in the status bar |
| Cancel | One click, or use `shutdown /a` |
| Self-test | Verifies that the `shutdown` command chain works on this machine |
| Scheduled tasks / wake timers | Diagnose "my PC wakes up at night" problems |

</details>

<details>
<summary><b>IP &amp; network information</b> — no more ipconfig</summary>

- Local overview: hostname, current user, Wi-Fi status, gateway, primary DNS
- Public egress IP with location (multi-endpoint fallback)
- Per-adapter detail: IPv4/IPv6, subnet mask, CIDR, default gateway, DNS, DHCP state and server, MAC, link speed
- One-click: ping gateway latency, ping public targets, copy all info, switch to automatic IP, enable/disable adapter
- Active connections / listening ports / IPv4 route table / ARP cache — all with **process name and PID**

</details>

<details>
<summary><b>DNS settings</b> — 18 curated Chinese public DNS providers</summary>

- View and change primary/secondary DNS on any adapter; takes effect immediately, no reboot
- **Built-in curated library**, grouped by category with notes:
  - Major providers: Alibaba `223.5.5.5`, Tencent DNSPod `119.29.29.29`, Baidu `180.76.76.76`, CNNIC sDNS `1.2.4.8`
  - Established: 114DNS plain `114.114.114.114`
  - Security: 114DNS malware-blocking `114.114.114.119`, family filter `114.114.114.110`, 360 Safe DNS
  - ISPs: China Telecom / Unicom / Mobile (lowest local latency, but some regions inject DNS ads)
  - Campus: CERNET `101.6.6.6`
  - Overseas (explicitly marked as not recommended): Google `8.8.8.8`, Cloudflare `1.1.1.1`
- **Real resolution benchmark**: sends actual DNS query packets to each server concurrently and times them,
  instead of parsing `nslookup` text
- Manual DNS entry with validation, flush cache, view cache contents, restore automatic

</details>

<details>
<summary><b>System tools</b> — junk cleanup, startup items, health check</summary>

- Network repair: reset Winsock / TCP-IP / firewall (with risk confirmation), flush DNS, clear ARP, refresh Group Policy
- IP management: release, renew, switch to automatic
- Junk cleanup: 11 safe targets (user/system temp, Prefetch, thumbnail cache, crash dumps, update cache…),
  **measures sizes first**, reports "in use" vs "access denied" separately, **never touches personal files**
- Startup item management: registry + Startup folder, enable/disable
- One-click health check: disk space, memory, privileges, DNS config, fast startup/hibernation,
  pending reboot, disk health, shutdown plan
- Wi-Fi password viewer, port occupancy lookup with process kill
- System info report export, activation status, battery report, disk info, restore point creation
- Official download links for common software, fast startup toggle, hibernation toggle,
  unlock Ultimate Performance power plan

</details>

<details>
<summary><b>BAT script library</b> — 31 ready-made scripts</summary>

Five categories (network / maintenance / troubleshooting / productivity); single click to view,
double click to run.

- Every script documents its purpose and risks; admin scripts elevate and ask for extra confirmation
- Run in a new window (live output), run as admin, run in background and capture output, save as `.bat`, copy
- Custom BAT editor: write any batch file, run it or save it to `scripts\`
- Examples: one-click network repair, Wi-Fi password export, SFC integrity repair, DISM repair,
  reset Windows Update components, disable fast startup, rebuild icon cache, God Mode,
  port occupancy lookup and kill, batch rename…

</details>

<details>
<summary><b>Operation log</b> — everything is traceable</summary>

- Every action records time / type / action / **full command and real return code**
- Written per day to `logs\`, exportable, folder can be opened
- Send the log to someone else to help diagnose a problem

</details>

---

## Quick start

1. Put `WinTool.exe` anywhere (a USB stick works), then **right-click → Run as administrator**
2. Navigate with the sidebar or the shortcuts `Ctrl+1` … `Ctrl+6`; press `F5` to refresh
3. Confirm the bottom-left shows `✓ Administrator privileges active`

```
┌─────────────────┬──────────────────────────────────────┐
│  WinTool        │  Scheduled shutdown                  │
│  Windows toolbox│  The plan survives closing this app… │
│                 │                                      │
│ ▸ Shutdown      │  ┌──────────┐ ┌──────────┐ ┌───────┐ │
│   IP & network  │  │ Shutdown │ │ Restart  │ │Sleep… │ │
│   DNS settings  │  │ 10/30/60 │ │ 5/15/30  │ │       │ │
│   System tools  │  └──────────┘ └──────────┘ └───────┘ │
│   BAT library   │  ┌─────────────────┐ ┌────────────┐ │
│   Operation log │  │ Countdown        │ │ Status     │ │
│                 │  └─────────────────┘ └────────────┘ │
│ ✓ Admin active  │                                      │
│ version 1.0.0   │  Ready                   2026-10-01  │
└─────────────────┴──────────────────────────────────────┘
```

### Portable by design

- Configuration lives in `wintool.ini` next to the exe (falls back to `%APPDATA%\WinTool\` if not writable)
- Scripts go to `scripts\`, logs to `logs\`
- Move the whole folder to another machine and your settings and logs come along

---

## ⚠ Note #1: most features need administrator privileges

This is the most common pitfall. Under UAC, **a program launched by double-click runs with normal
privileges even if the account is an administrator**. In that state:

- Scheduled shutdown / restart / cancel → `Access is denied (5)`
- Changing DNS, resetting the network (Winsock / TCP-IP), enabling/disabling adapters → `Access is denied`
- Disabling startup items, clearing ARP, killing processes, battery report, restore point → `Access is denied`

The bottom-left of the sidebar always shows the current state:

| Display | Meaning | Action |
|---|---|---|
| `✓ Administrator privileges active` | Everything works | Nothing to do |
| `⚠ Running with normal privileges · click to elevate` | You are an admin, but the app is not elevated | **Click it** and let it restart |
| `✕ Not an administrator account` | The account itself is not an admin | Sign in with an admin account |

You can also right-click `WinTool.exe` → **Run as administrator**. When you use a feature that needs
elevation, the app asks whether to elevate — **it will not fail silently**.

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

### Other important notes

1. **Shutdown/restart runs through the built-in `shutdown` command.** Once set, you can close this tool
   and the plan still fires. To cancel you must click "Cancel shutdown plan" (or run `shutdown /a`) —
   **simply closing the window does not cancel it**.
2. **Forced shutdown (`/f`) terminates unsaved programs.** Save your documents first.
3. On some machines, having "Fast Startup" enabled makes scheduled shutdown behave oddly. Turn it off on
   the System Tools page and retry.
4. Sleep needs graphics/chipset driver support; hibernation must be enabled in Windows (one click on the
   System Tools page; it uses disk space roughly equal to RAM).
5. Corporate/school machines may lock down shutdown rights via domain policy; the command then returns
   `Access is denied (5)`, which is expected.
6. **Changing DNS only affects name resolution speed, not bandwidth.** If the network breaks after a
   change, immediately click "Restore automatic".
7. Overseas DNS (`8.8.8.8` / `1.1.1.1`) is frequently interfered with inside mainland China; the tool
   warns about this — don't set it unless you need to.
8. After a network reset (Winsock / TCP-IP) **a reboot is required** for it to fully take effect, and some
   proxy / accelerator / virtual-adapter software must be reconfigured.
9. Junk cleanup only removes temp files and caches. In rare cases a running program may complain about a
   deleted temp file; restarting that program fixes it.
10. This tool needs permission to change network and power settings — **only use an executable from a
    trusted source**.

---

## Building from source

No .NET SDK or Visual Studio needed — the compiler shipped with Windows is enough.

```powershell
# 1) Generate icon assets (only needed the first time, or after changing the icon)
powershell -ExecutionPolicy Bypass -File .\tools\make-assets.ps1

# 2) Build (outputs build\WinTool.exe)
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` locates `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and compiles with
`/target:winexe /langversion:5 /optimize+ /win32icon /win32manifest` into a single executable.

### Source layout

```
WinTool/
├── src/                        Source code
│   ├── Theme.cs                Palette, fonts, DPI conversion, custom-drawn buttons/chips/progress bar
│   ├── UI.cs                   Widget factories (cards, notes, inputs, buttons) + time text parsing
│   ├── Sys.cs                  Command engine (encoding detection/timeout/redirect), elevation, registry, config, logging, recycle bin
│   ├── Net.cs                  WMI/ipconfig/netsh/netstat/arp parsing, DNS latency benchmark
│   ├── Shutdown.cs             shutdown/powercfg/schtasks drivers, plan tracking, process-watch shutdown
│   ├── Dns.cs                  Curated DNS library, DNS read/write and cache operations
│   ├── Tools.cs                Network repair, junk cleanup, startup items, health check, disks/battery/activation
│   ├── Batch.cs                BAT script library and execution engine
│   ├── PageShutdown.cs         Scheduled shutdown page
│   ├── PageNet.cs              IP & network page
│   ├── PageDns.cs              DNS settings page
│   ├── PageTools.cs            System tools page
│   ├── PageBatch.cs            BAT library page
│   ├── MainForm.cs             Shell: sidebar navigation, header, status bar, DPI scaling, timers
│   ├── Program.cs              Entry point, runtime icon rendering, global exception capture
│   ├── AssemblyInfo.cs         Assembly metadata
│   ├── app.manifest            DPI awareness, supported OS list, asInvoker
│   ├── app.ico                 Icon (generated by tools/make-assets.ps1)
│   └── App.config              Runtime configuration
├── tools/                      Development and test scripts (see tools/README.md)
├── docs/screenshots/           Screenshots used in this README
├── build.ps1                   Build script
├── cleanup.ps1                 Removes temporary artifacts
├── LICENSE                     MIT
└── CHANGELOG.md                Version history
```

### Technical highlights

- **Zero third-party dependencies.** No libraries, controls or image assets; the icon is drawn at runtime
  with GDI+, and the whole UI is custom-painted.
- **DNS benchmarking implements the DNS wire protocol directly** instead of parsing `nslookup` text.
- **Privilege detection uses `TokenElevation`, not group membership.** Under UAC, "member of
  Administrators" does not mean elevated; checking group membership alone makes the app think it is
  elevated, fail silently and report "Access is denied".
- **Command output encoding is auto-detected.** Built-in console tools (`netsh` / `ipconfig` / `netstat` /
  `reg` / `shutdown`) emit text in the system OEM code page, while `GetConsoleOutputCP()` may report 65001
  in some environments — decoding with the console code page garbles all Chinese text. The implementation
  validates UTF-8 strictly and falls back to the OEM code page.
- **Output is read byte-by-byte and decoded afterwards** instead of using `StandardOutputEncoding` line
  reads, which can silently drop content when the encoding doesn't match.
- All slow work (WMI, network queries, cleanup measurement) runs on background threads — the UI never blocks.
- Layout is converted by the system DPI scale so text never overlaps or gets clipped at high DPI.
- Temp files, the script directory and the battery report all have **writable-path fallbacks**.
- A global exception handler writes `error.log`; the app never dies silently.

### Testing

```powershell
# Functional tests: shutdown chain, registry, network, DNS, cleanup, BAT, WMI
powershell -ExecutionPolicy Bypass -File .\tools\build-test.ps1
.\build\WinToolTest.exe .\build\testresult.txt

# Parsing-layer acceptance: encoding detection, command construction, DNS library, adapter parsing
# (needs no privileges)
powershell -ExecutionPolicy Bypass -File .\tools\build-verify.ps1
.\build\WinToolVerify.exe .\build\dns-real.txt .\build\verify.txt
```

`WinTool.exe --selftest` also performs a headless self-check: it schedules a shutdown 180 seconds out and
immediately cancels it, confirming that the local `shutdown` chain works (it never leaves a real plan behind).

See [tools/README.md](tools/README.md) for details — including how to tell **environment restrictions**
apart from real bugs.

---

## FAQ

<details>
<summary><b>Double-click does nothing / missing .NET?</b></summary>

This tool needs .NET Framework 4.x, which **ships with Windows 7 SP1 and later**.
On a very old Windows 7 install that only has .NET 3.5, install .NET Framework 4.8 once from Microsoft —
after that every similar app will work.
</details>

<details>
<summary><b>Antivirus flags it?</b></summary>

This tool changes network settings, runs shutdown commands and deletes temp files — behaviour that
overlaps with some malware heuristics, so false positives happen.

- It **uploads nothing**; only "public IP lookup" and "DNS benchmark" touch the network
- The full source is public (see `src/`), so you can compile it yourself
- If unsure, build your own copy with `build.ps1`
</details>

<details>
<summary><b>Why must I click "Cancel shutdown plan" instead of just closing the window?</b></summary>

Because the plan is handed to Windows' own `shutdown` service; this tool only issues the command.
That is exactly why it keeps working after you close the tool. To cancel:

- Return to the Scheduled shutdown page and click "Cancel shutdown plan"
- Or press `Win+R`, type `shutdown /a`, and hit Enter
</details>

<details>
<summary><b>The scheduled shutdown didn't fire on time?</b></summary>

Check in order:

1. Look at "Current status" on the Scheduled shutdown page and the status bar — is the countdown still running?
2. On some machines "Fast Startup" interferes with shutdown commands. Disable it on the System Tools page.
3. A pending Windows update reboot can override your plan.
4. Click "Self-test" to confirm the `shutdown` chain works on this machine.
5. Corporate machines may restrict shutdown via domain policy — the command then returns `Access is denied (5)`.
</details>

<details>
<summary><b>Public IP lookup fails?</b></summary>

It relies on external endpoints (several are tried in order). With no internet access, or if the
endpoints are blocked, it reports failure. Other features are unaffected.
</details>

---

## Contributing

Issues and pull requests are welcome. Before submitting code, please:

1. Confirm `build.ps1` compiles with no errors and no warnings
2. Run the parsing-layer acceptance suite (`tools/build-verify.ps1`)
3. Describe what you changed and how you verified it

---

## License

[MIT License](LICENSE) — free to use, modify and redistribute.

> **Disclaimer**: This tool really does shut down, restart, reset network settings and delete files.
> Make sure you intend the action before confirming, especially for **forced shutdown**,
> **network reset** and **killing processes**. The user is responsible for any data loss caused by
> misoperation.
