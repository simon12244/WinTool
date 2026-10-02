# tools/ — 开发与验证脚本

这个目录放的是**开发和测试用的辅助脚本**，普通用户不需要用到它们。
发行版只需要 `WinTool.exe`（在 [Releases](https://github.com/simon12244/WinTool/releases) 下载）。

## 脚本说明

| 脚本 | 用途 | 需要特权 |
|---|---|---|
| `make-assets.ps1` | 生成 `src/app.ico`（多尺寸图标，含 16~256 px） | 否 |
| `build-test.ps1` | 编译无界面**功能测试台** `WinToolTest.exe` | 否 |
| `build-verify.ps1` | 编译**解析层验收台** `WinToolVerify.exe` | 否 |
| `Test.cs` | 功能测试台源码：逐项调用真实实现，输出退出码与实际效果 | — |
| `Verify.cs` | 解析层验收源码：编码识别、命令构造、DNS 库、网卡解析 | — |
| `VerifyReal.cs` | 真实功能验证源码：直接跑关机/DNS/清理等真实操作 | **是** |
| `shots-pages.ps1` | 自动点击六个页面并逐页截图，用于 UI 回归 | 否 |
| `shot.ps1` | 抓取单个窗口截图（被 `shots-pages.ps1` 使用） | 否 |
| `push-to-github.ps1` | 把仓库推送到 GitHub（令牌从环境变量读取，不落盘） | 否 |

## 常用命令

```powershell
# 1) 重新生成图标（只在改图标时用）
powershell -ExecutionPolicy Bypass -File .\tools\make-assets.ps1

# 2) 编译主程序（输出 build\WinTool.exe）
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 3) 功能测试：覆盖关机链、注册表、网络、DNS、清理、BAT、WMI 等
powershell -ExecutionPolicy Bypass -File .\tools\build-test.ps1
.\build\WinToolTest.exe .\build\testresult.txt

# 4) 解析层验收（不依赖任何特权，任何机器都能跑）
powershell -ExecutionPolicy Bypass -File .\tools\build-verify.ps1
.\build\WinToolVerify.exe .\build\dns-real.txt .\build\verify.txt

# 5) UI 回归：自动切换六个页面并截图到 build\pages\
powershell -ExecutionPolicy Bypass -File .\tools\shots-pages.ps1
```

## ⚠️ 关于测试环境的注意事项

**测试台必须放在"普通目录"里运行**（例如桌面、`D:\tools\`），并**以管理员身份运行**。

如果在受限环境（沙箱、受控终端、被安全软件拦截的目录、某些 CI 容器）里运行，
会出现下面这类**误导性结果**——看起来像程序有 bug，实际是环境拦截了写操作与特权命令：

| 现象 | 真实原因 |
|---|---|
| `shutdown /s /t` 返回 `拒绝访问(5)` | 环境拦截了关机类命令 |
| `netsh ... set` 返回 `拒绝访问` | 环境禁止修改网络配置 |
| `reg add HKCU\...` 返回 `拒绝访问` | 环境禁止写注册表 |
| 清理"释放 0 B、跳过 N 个文件" | 环境禁止删除工作区外的文件 |
| `schtasks /create` 返回 `拒绝访问` | 环境禁止创建计划任务 |

判断方法：`WinToolVerify.exe`（解析层验收）**不依赖特权**，如果它全绿而功能测试大量红，
基本可以确定是环境限制，而不是代码问题。

## `VerifyReal.cs` 的用法

因为它会**真实修改系统状态**（改 DNS、清临时文件、创建计划任务），所以单独放一个文件：

```powershell
# 编译到普通目录（不要放在受限/沙箱目录里）
$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$fw\csc.exe" /nologo /target:exe /langversion:5 /optimize+ `
    /out:"$env:USERPROFILE\Desktop\VerifyReal.exe" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll /r:System.Management.dll /r:System.Xml.dll `
    .\src\Theme.cs .\src\Sys.cs .\src\UI.cs .\src\Net.cs .\src\Shutdown.cs `
    .\src\Dns.cs .\src\Tools.cs .\src\Batch.cs .\tools\VerifyReal.cs

# 以管理员身份运行，结果写入指定文件
& "$env:USERPROFILE\Desktop\VerifyReal.exe" "$env:USERPROFILE\Desktop\verify-real.txt"
```

它会安排一个 150 秒后的关机并立即撤销，用来证明关机链路真实可用（不会留下真实计划）。
