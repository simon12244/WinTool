# 编译解析层验收台（无特权依赖）。ASCII-only。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src  = Join-Path $root "src"
$dev  = Join-Path $root "dev"
$bin  = Join-Path $root "build"
$fw   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) { throw "csc.exe not found" }

$names = @("Theme.cs", "Sys.cs", "UI.cs", "Net.cs", "Shutdown.cs", "Dns.cs", "Tools.cs", "Batch.cs")
$files = @()
foreach ($n in $names) { $files += (Join-Path $src $n) }
$files += (Join-Path $dev "Verify.cs")

$refs = @("System.dll", "System.Core.dll", "System.Drawing.dll",
          "System.Windows.Forms.dll", "System.Management.dll", "System.Xml.dll") |
        ForEach-Object { "/r:" + (Join-Path $fw $_) }

$out = Join-Path $bin "WinToolVerify.exe"
$cscArgs = @("/nologo", "/target:exe", "/langversion:5", "/optimize+", "/utf8output", ("/out:" + $out))
$cscArgs += $refs + $files
$lines = & $csc $cscArgs 2>&1
$code = $LASTEXITCODE
foreach ($l in $lines) { Write-Host $l }
if ($code -ne 0) { throw ("verify build failed: " + $code) }
Write-Host ("built: " + $out)
