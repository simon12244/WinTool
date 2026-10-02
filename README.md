<div align="center">

# WinTool 系统工具箱

**纯原生 Windows 桌面工具 · 单文件绿色版 · 零依赖**

[![Release](https://img.shields.io/github/v/release/simon12244/WinTool?style=flat-square&label=版本)](https://github.com/simon12244/WinTool/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/simon12244/WinTool/total?style=flat-square&label=下载)](https://github.com/simon12244/WinTool/releases)
[![License](https://img.shields.io/github/license/simon12244/WinTool?style=flat-square&label=许可)](LICENSE)
[![Size](https://img.shields.io/badge/体积-232%20KB-2ea44f?style=flat-square)](#下载)
[![Platform](https://img.shields.io/badge/Windows-7%20SP1%20%7C%208.1%20%7C%2010%20%7C%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](#系统要求)
[![C%23](https://img.shields.io/badge/C%23-WinForms-239120?style=flat-square&logo=csharp&logoColor=white)](src)

[**简体中文**](README.md) · [English](README.en.md)

[下载](#下载) · [截图](#截图) · [功能](#功能) · [快速开始](#快速开始) · [权限说明](#-头号提示大部分功能需要管理员权限) · [编译](#从源码编译) · [常见问题](#常见问题)

</div>

---

用 C# WinForms 写的**纯原生** Windows 桌面工具，把平时需要敲命令行、翻好几层设置面板才能完成的事，
做成一次点击。**不是 HTML/Electron 套壳**，直接驱动系统自带的
`shutdown.exe` / `netsh` / `powercfg` / `schtasks` / WMI / 注册表。

| | |
|---|---|
| **体积** | 单文件 `WinTool.exe`，**232 KB** |
| **依赖** | **零**。不使用任何第三方库、控件、图片资源；图标由 GDI+ 代码现场绘制 |
| **安装** | 不需要。绿色便携版，双击即用，不写注册表 |
| **运行时** | 只需 Windows 自带的 .NET Framework 4.x |
| **系统** | Windows 7 SP1 / 8.1 / 10 / 11 |
| **编译** | 只需 Windows 自带的 `csc.exe`，**不需要装 .NET SDK 或 Visual Studio** |

---

## 截图

### 定时关机
![定时关机](docs/screenshots/shutdown.png)

### IP 网络信息
![IP 网络信息](docs/screenshots/network.png)

### DNS 设置
![DNS 设置](docs/screenshots/dns.png)

<details>
<summary><b>展开查看其余三张（系统工具 / BAT 命令库 / 操作日志）</b></summary>

### 系统工具
![系统工具](docs/screenshots/tools.png)

### BAT 命令库
![BAT 命令库](docs/screenshots/batch.png)

### 操作日志
![操作日志](docs/screenshots/log.png)

</details>

---

## 下载

| 文件 | 大小 | 说明 |
|---|---|---|
| **[WinTool.exe](https://github.com/simon12244/WinTool/releases/latest/download/WinTool.exe)** | 232 KB | 单文件绿色版，双击即用 |
| [WinTool-v1.0.0-src.zip](https://github.com/simon12244/WinTool/releases/latest/download/WinTool-v1.0.0-src.zip) | 296 KB | 完整源码 + 构建脚本 |

更多版本见 [Releases](https://github.com/simon12244/WinTool/releases)。

### 系统要求

- Windows 7 SP1 / 8.1 / 10 / 11（x86 / x64）
- .NET Framework 4.x —— **Windows 7 SP1 及以上系统自带，无需额外安装**
- 无网络要求；公网 IP 查询、DNS 测速等联网功能不可用时会自动降级

---

## 功能

<details open>
<summary><b>定时关机</b> — 关掉本工具计划依然生效</summary>

| 功能 | 说明 |
|---|---|
| 快捷关机 | 10 分钟 / 30 分钟 / 1 小时 / 2 小时 一键设定，强制结束未响应程序 |
| 倒计时关机 | 输入任意分钟数 |
| 指定时刻关机 | 支持 `22:30`、`明天 06:00`、`2025-06-01 23:00`、`+45m`、`30分钟后` |
| 定时重启 | 同样的时间写法，重启前强制保存并关闭程序 |
| 睡眠 / 休眠 / 锁定 / 注销 | 休眠不可用时提示并支持一键开启 |
| 程序结束后自动关机 | 等游戏下载、视频转码、渲染跑完再关（最长等 12 小时，超时自动放弃） |
| 实时倒计时 | 底部状态栏常驻显示剩余时间 |
| 取消关机计划 | 一键撤销；也可用系统命令 `shutdown /a` |
| 自检 | 自动验证本机关机命令链路是否可用 |
| 查看系统计划任务 / 唤醒定时器 | 排查“电脑半夜自己开机”等问题 |

</details>

<details>
<summary><b>IP 网络信息</b> — 不用再敲 ipconfig</summary>

- 本机速览：计算机名、当前用户、无线连接状况、网关、首选 DNS
- 公网出口 IP + 归属地（多接口容错）
- 网卡明细：IPv4/IPv6、子网掩码、CIDR 网段、默认网关、DNS、DHCP 状态与服务器、MAC、连接速率
- 一键：测试网关延迟、ping 公网、复制全部信息、切换为自动获取 IP、启用/禁用网卡
- 活动连接 / 监听端口 / IPv4 路由表 / ARP 缓存表，均带**进程名与 PID**

</details>

<details>
<summary><b>DNS 设置</b> — 内置 18 条国内公共 DNS</summary>

- 查看与修改任意网卡的首选/备用 DNS，立即生效，无需重启
- **内置国内主流公共 DNS 库**（按类别分组，含重要提示）：
  - 国内大厂：阿里云 `223.5.5.5`、腾讯 DNSPod `119.29.29.29`、百度 `180.76.76.76`、CNNIC sDNS `1.2.4.8`
  - 老牌公共：114DNS 纯净版 `114.114.114.114`
  - 安全防护：114DNS 恶意网站拦截版 `114.114.114.119`、家庭防护版 `114.114.114.110`、360 安全 DNS
  - 运营商：电信 / 联通 / 移动（本地最快，但个别地区有 DNS 广告插入）
  - 校园网：教育网 DNS `101.6.6.6`
  - 海外（明确标注不推荐）：Google `8.8.8.8`、Cloudflare `1.1.1.1`
- **真实解析测速**：并发向每个 DNS 发送真实 DNS 查询报文并计时，而不是解析 `nslookup` 文本
- 手动指定 DNS（含格式校验）、刷新 DNS 缓存、查看缓存内容、一键恢复自动获取

</details>

<details>
<summary><b>系统工具</b> — 11 项垃圾清理 + 一键体检</summary>

- 网络故障修复：重置 Winsock / TCP-IP / 防火墙（带风险确认）、刷新 DNS、清空 ARP、刷新组策略
- IP 管理：释放 IP、重新获取 IP、切换为自动获取
- 系统垃圾清理：11 个安全清理项（用户/系统临时文件、Prefetch、缩略图缓存、崩溃转储、更新缓存…），
  **先统计大小再清理**，被占用与权限不足分别统计，**不触碰任何个人文件**
- 开机启动项管理：注册表 + 启动文件夹，支持启用/禁用
- 一键系统体检：磁盘空间、内存、权限、DNS、快速启动/休眠、待重启、磁盘健康、关机计划
- Wi-Fi 密码查看（单项 / 全部）、端口占用查询 + 结束占用进程
- 系统信息报告导出、激活状态检查、电池健康报告、磁盘信息、创建还原点
- 常用软件官方下载入口、关闭快速启动、开启/关闭休眠、解锁卓越性能电源计划

</details>

<details>
<summary><b>BAT 命令库</b> — 31 个现成脚本</summary>

分 5 类（网络 / 维护 / 疑难 / 效率），单击看内容、双击直接运行。

- 每个脚本都写清了用途与风险；管理员类型的脚本自动提权并额外弹风险确认
- 支持：直接运行（新窗口看实时输出）、以管理员运行、后台运行并取回结果、另存为 `.bat`、复制
- 自定义 BAT 编辑器：随便写批处理，运行或保存到 `scripts\` 目录
- 脚本示例：一键网络修复、Wi-Fi 密码导出、系统文件完整性修复、DISM 修复、重置 Windows 更新组件、
  关闭快速启动、重建图标缓存、上帝模式、端口占用查询与结束进程、批量重命名…

</details>

<details>
<summary><b>操作日志</b> — 出问题能查</summary>

- 每一步操作都记录：时间 / 类型 / 操作 / **完整命令与真实返回码**
- 按天落盘到程序目录 `logs\`，可导出、可打开文件夹
- 出问题时把日志发给别人即可协助排查

</details>

---

## 快速开始

1. 把 `WinTool.exe` 放到任意目录（U 盘也行），**右键 → 以管理员身份运行**
2. 左侧栏是功能区导航，快捷键 `Ctrl+1` ~ `Ctrl+6` 切换，`F5` 刷新当前页
3. 确认左下角显示 `✓ 已获得管理员权限`

```
┌─────────────────┬──────────────────────────────────────┐
│  WinTool        │  定时关机                            │
│  Windows 系统工具箱 │  设定后即使关闭本工具也依然生效…      │
│                 │                                      │
│ ▸ 定时关机       │  ┌──────────┐ ┌──────────┐ ┌───────┐ │
│   IP 网络信息    │  │ 立即关机  │ │ 定时重启  │ │睡眠…  │ │
│   DNS 设置      │  │ 10/30/60 │ │ 5/15/30  │ │       │ │
│   系统工具       │  └──────────┘ └──────────┘ └───────┘ │
│   BAT 命令库     │  ┌─────────────────┐ ┌────────────┐ │
│   操作日志       │  │ 倒计时关机       │ │ 当前状态    │ │
│                 │  └─────────────────┘ └────────────┘ │
│ ✓ 已获得管理员权限 │                                      │
│ 版本 1.0.0       │  已进入：定时关机        2026-10-01  │
└─────────────────┴──────────────────────────────────────┘
```

### 便携版说明

- 配置写在 `WinTool.exe` 同目录的 `wintool.ini`（不可写时自动退到 `%APPDATA%\WinTool\`）
- 脚本保存在同目录 `scripts\`，日志在 `logs\`
- 换电脑只需整个文件夹复制过去，配置和日志跟着走

---

## ⚠ 头号提示：大部分功能需要管理员权限

这是最容易踩的坑。Windows 的 UAC 机制下，**管理员账号双击运行的程序默认也是"普通权限"**，此时：

- 定时关机 / 重启 / 取消关机计划 → 报 `拒绝访问(5)`
- 修改 DNS、重置网络（Winsock / TCP-IP）、启用禁用网卡 → 报 `拒绝访问`
- 禁用启动项、清空 ARP、结束进程、生成电池报告、创建还原点 → 报 `拒绝访问`

**本工具左侧栏底部会明确显示当前权限状态**：

| 显示 | 含义 | 建议 |
|---|---|---|
| `✓ 已获得管理员权限` | 全部功能可用 | 无需操作 |
| `⚠ 当前为普通权限 · 点此提权` | 你是管理员，但程序未提权 | **点它一下**，重启后全功能可用 |
| `✕ 非管理员账号 · 部分功能不可用` | 账号本身不是管理员 | 用管理员账号登录 |

也可以直接：右键 `WinTool.exe` → **以管理员身份运行**。
你点需要权限的功能时，程序也会主动弹窗询问是否提权，**不会静默失败**。

### 哪些功能需要管理员

| 不需要管理员 | 需要管理员 |
|---|---|
| IP 网络信息全部（网卡/公网/路由/ARP/端口/连接） | 定时关机、重启、取消关机计划 |
| DNS 延迟测速、查看当前 DNS | 修改 DNS、恢复自动获取 |
| 系统体检、查看启动项、磁盘信息（只读） | 禁用/启用启动项 |
| 端口占用查询 | 结束占用进程 |
| 系统信息报告导出、激活状态查看 | 网络重置、清空 ARP、刷新组策略、释放/续订 IP |
| 常用软件下载入口 | 垃圾清理（系统级临时文件）、Wi-Fi 密码、电池报告、还原点、快速启动/休眠开关 |
| BAT 脚本：查看、另存、普通脚本运行 | BAT 脚本中标为“管理员”的脚本、自定义脚本的管理员运行 |

### 其他重要提示

1. **关机/重启由系统自带 `shutdown` 命令执行**，设定后本工具可以关闭，计划依然有效；
   要撤销必须回本页点「取消关机计划」，或执行 `shutdown /a`，**直接关掉窗口是不会取消的**。
2. **强制关机（`/f`）会直接结束未保存的程序**，可能丢失未保存内容，操作前请先手动保存文档。
3. 部分机型开启「快速启动」后，定时关机表现异常；可在「系统工具」页关闭快速启动后重试。
4. 睡眠需要显卡/主板驱动支持；休眠需要系统已开启休眠功能（可在「系统工具」一键开启，会占用与内存相当的磁盘空间）。
5. 公司/学校电脑可能被域策略锁定关机权限，此时命令返回 `拒绝访问(5)`，属正常现象。
6. **换 DNS 只影响域名解析速度，不会提升带宽。** 如果改完无法上网，请立刻点「恢复自动获取」。
7. 海外 DNS（`8.8.8.8` / `1.1.1.1`）在国内常被干扰，程序会额外警告，非必要不要设置。
8. 网络重置（Winsock / TCP-IP）后**需要重启电脑才完全生效**，且部分代理、加速器、虚拟网卡软件需要重新配置。
9. 垃圾清理只删除临时文件与缓存，但若你正在运行的程序临时文件被清理，极少数程序可能报错，重启该程序即可。
10. 本工具需要修改系统网络/电源设置的权限，**请从官方渠道获取 exe**，不要使用来路不明的同名文件。

---

## 从源码编译

不需要安装 .NET SDK 或 Visual Studio，用 Windows 自带的编译器即可。

```powershell
# 1) 生成图标资源（仅首次或改图标时需要）
powershell -ExecutionPolicy Bypass -File .\tools\make-assets.ps1

# 2) 编译，输出 build\WinTool.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

`build.ps1` 会自动查找 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，
并以 `/target:winexe /langversion:5 /optimize+ /win32icon /win32manifest` 编译出单文件 exe。

### 项目结构

```
WinTool/
├── src/                        源码
│   ├── Theme.cs                配色、字体、DPI 换算、自绘圆角按钮/标签/进度条
│   ├── UI.cs                   界面组件工厂（卡片、提示条、输入框、按钮）+ 时间文本解析
│   ├── Sys.cs                  命令执行引擎（编码识别/超时/重定向）、提权、注册表、配置、日志、回收站
│   ├── Net.cs                  WMI/ipconfig/netsh/netstat/arp 解析，DNS 延迟实测
│   ├── Shutdown.cs             shutdown/powercfg/schtasks 驱动、关机计划跟踪、进程监控关机
│   ├── Dns.cs                  国内公共 DNS 库、DNS 读写与缓存操作
│   ├── Tools.cs                网络修复、垃圾清理、启动项、体检、磁盘/电池/激活、常用链接
│   ├── Batch.cs                BAT 脚本库与脚本执行引擎
│   ├── PageShutdown.cs         定时关机页
│   ├── PageNet.cs              IP 网络信息页
│   ├── PageDns.cs              DNS 设置页
│   ├── PageTools.cs            系统工具页
│   ├── PageBatch.cs            BAT 命令库页
│   ├── MainForm.cs             主窗体：侧栏导航、页头、状态栏、DPI 缩放、定时刷新
│   ├── Program.cs              入口、程序图标现场绘制、全局异常捕获
│   ├── AssemblyInfo.cs         程序集信息
│   ├── app.manifest            DPI 感知、Windows 版本声明、asInvoker（需要时才提权）
│   ├── app.ico                 图标（由 tools/make-assets.ps1 生成）
│   └── App.config              运行时配置
├── tools/                      开发与测试脚本（见 tools/README.md）
├── docs/screenshots/           README 用截图
├── build.ps1                   编译脚本
├── cleanup.ps1                 清理临时产物
├── LICENSE                     MIT
└── CHANGELOG.md                版本变更记录
```

### 技术要点

- **零第三方依赖**：不使用任何库、控件或图片资源，图标由 GDI+ 代码现场绘制，界面全部自绘
- **自己实现 DNS 协议报文**用于测速，不依赖 `nslookup` 文本解析
- **权限判定用 `TokenElevation` 而非组成员**：UAC 下"属于 Administrators 组"不等于已提权，
  只查组成员会让程序误判为管理员、静默失败并报"拒绝访问"
- **命令输出自动识别编码**：cmd 内建工具（`netsh`/`ipconfig`/`netstat`/`reg`/`shutdown`）按系统
  OEM 代码页输出，而部分环境下 `GetConsoleOutputCP()` 返回 65001，用错编码会让中文全变乱码；
  实现改为严格校验 UTF-8，不合法则按 OEM 代码页解码
- **逐字节读取命令输出后再解码**，不用 `StandardOutputEncoding` 逐行读，避免编码不匹配时整段内容丢失
- 所有耗时操作（WMI、网络查询、清理统计）都在后台线程执行，界面不卡
- 高 DPI 下按系统缩放比例换算布局，避免文字重叠或截断
- 临时文件、脚本目录、电池报告都有**可写路径回退**，受限环境下也能工作
- 全局异常捕获写入 `error.log`，程序不会静默崩溃

### 测试

```powershell
# 功能测试：覆盖关机链、注册表、网络、DNS、清理、BAT、WMI
powershell -ExecutionPolicy Bypass -File .\tools\build-test.ps1
.\build\WinToolTest.exe .\build\testresult.txt

# 解析层验收：编码识别、命令构造、DNS 库、网卡解析（不需要任何特权）
powershell -ExecutionPolicy Bypass -File .\tools\build-verify.ps1
.\build\WinToolVerify.exe .\build\dns-real.txt .\build\verify.txt
```

`WinTool.exe --selftest` 还会做一次无界面自检：安排一个 180 秒后的关机再立即撤销，
用来确认本机 `shutdown` 链路是否可用（不会留下真实关机计划）。

详见 [tools/README.md](tools/README.md)。

---

## 常见问题

<details>
<summary><b>双击没反应 / 提示缺少 .NET？</b></summary>

本工具需要 .NET Framework 4.x。Windows 7 SP1 及以上系统**默认自带**。
如果你在 Windows 7 上没装过任何 .NET 更新，可能只有 .NET 3.5，
此时去微软官网装一次 .NET Framework 4.8 即可（一次性，之后所有同类程序都能用）。
</details>

<details>
<summary><b>杀毒软件报毒？</b></summary>

本工具会修改网络配置、执行关机命令、删除临时文件，这些行为与某些恶意软件的特征重合，
因此可能被启发式引擎误报。

- 程序**不联网上传任何数据**，只有「查询公网 IP」和「DNS 测速」会主动访问网络
- 源码完全公开（见 `src/`），可自行编译
- 如果不放心，用 `build.ps1` 自己编译一份再运行
</details>

<details>
<summary><b>为什么设置定时关机后要点「取消关机计划」才能取消，关掉窗口不行？</b></summary>

因为计划是交给 Windows 自己的 `shutdown` 服务执行的，本工具只是下发命令。
这也正是它"关掉工具计划依然生效"的原因。想取消：

- 回到「定时关机」页点「取消关机计划」
- 或按 `Win+R` 输入 `shutdown /a` 回车
</details>

<details>
<summary><b>定时关机没有在预期时间执行？</b></summary>

按顺序排查：

1. 看「定时关机」页的「当前状态」和底部状态栏，确认计划还在（剩余时间在跳）
2. 部分机型开启「快速启动」会影响关机命令，去「系统工具」关闭快速启动后重试
3. 系统可能有待完成的更新重启，会覆盖你的关机计划
4. 点「自检」按钮，确认本机 `shutdown` 链路是否可用
5. 公司电脑可能被域策略限制，此时命令会返回 `拒绝访问(5)`
</details>

<details>
<summary><b>公网 IP 显示查询失败？</b></summary>

公网 IP 查询依赖外部接口（会依次尝试多个）。如果本机无法访问外网、
或者接口被拦截，就会显示失败。这不影响其它功能。
</details>

<details>
<summary><b>能和 Windows 自带的「任务计划程序」配合吗？</b></summary>

可以。「定时关机」页的「系统计划任务」按钮会列出系统中与关机相关的计划任务，
方便你排查冲突；本工具用 `shutdown.exe` 方式下发的计划不会出现在那里
（它在系统的关机队列里）。
</details>

---

## 贡献

欢迎提交 [Issue](https://github.com/simon12244/WinTool/issues) 和 Pull Request。
提交代码前请：

1. 用 `build.ps1` 确认能编译通过（无 error、无 warning）
2. 跑一遍 `tools/build-verify.ps1` 的解析层验收
3. 说明改动的功能和验证方式

---

## 许可证

[MIT License](LICENSE) —— 可自由使用、修改、分发。

> **免责声明**：本工具会真实执行关机、重启、网络重置、文件清理等系统操作。
> 请在使用前确认自己的意图，尤其是「强制关机」「重置网络」「结束进程」三类操作。
> 因误操作造成的数据丢失需由使用者自行承担。
