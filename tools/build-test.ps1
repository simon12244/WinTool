# 编译功能测试台（无界面）。ASCII-only 以免 PS 5.1 解析出错。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src  = Join-Path $root "src"
$dev  = Join-Path $root "dev"
$bin  = Join-Path $root "build"
$fw   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found" }
if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }

# 与主程序完全相同的源文件（保证测的就是真实实现），入口换成 Test.cs
$names = @("Theme.cs", "Sys.cs", "UI.cs", "Net.cs", "Shutdown.cs", "Dns.cs", "Tools.cs", "Batch.cs")
$files = @()
foreach ($n in $names) { $files += (Join-Path $src $n) }
$files += (Join-Path $dev "Test.cs")
$files += (Join-Path $src "AssemblyInfo.cs")

$refs = @("System.dll", "System.Core.dll", "System.Drawing.dll",
          "System.Windows.Forms.dll", "System.Management.dll", "System.Xml.dll") |
        ForEach-Object { "/r:" + (Join-Path $fw $_) }

$out = Join-Path $bin "WinToolTest.exe"
$cscArgs = @("/nologo", "/target:exe", "/langversion:5", "/optimize+", "/utf8output", ("/out:" + $out))
$cscArgs += $refs + $files
# 测试台不需要 PageXxx，但 Sys.RunPS 需要 UI 类型；用 -define 关掉不必要的引用问题
$lines = & $csc $cscArgs 2>&1
$code = $LASTEXITCODE
foreach ($l in $lines) { Write-Host $l }
if ($code -ne 0) { throw ("test build failed: " + $code) }
Write-Host ("built: " + $out)
