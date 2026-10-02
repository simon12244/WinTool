# WinTool build script (native C# WinForms, no .NET SDK required).
# Compiles with the csc.exe shipped inside .NET Framework 4.x, producing a single portable exe.
# NOTE: keep this file ASCII-only - Windows PowerShell 5.1 reads .ps1 as ANSI.
param(
    [string]$Configuration = "Release",
    [switch]$Probe
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root "src"
$bin  = Join-Path $root "build"
$fw   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"

if (-not (Test-Path $csc)) {
    $fw  = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
    $csc = Join-Path $fw "csc.exe"
}
if (-not (Test-Path $csc)) { throw "csc.exe not found - .NET Framework 4.x is required" }

if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }

# Make sure no previous instance is locking the output file.
Get-Process WinTool -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
Start-Sleep -Milliseconds 300

if ($Probe) {
    $names = @("Theme.cs", "Sys.cs", "Probe.cs")
    $out   = Join-Path $bin "probe.exe"
} else {
    $names = @("Theme.cs", "Sys.cs", "UI.cs", "Net.cs", "Shutdown.cs", "Dns.cs", "Tools.cs",
               "Batch.cs", "PageShutdown.cs", "PageNet.cs", "PageDns.cs", "PageTools.cs",
               "PageBatch.cs", "MainForm.cs", "AssemblyInfo.cs", "Program.cs")
    $out   = Join-Path $bin "WinTool.exe"
}

$files = @()
foreach ($n in $names) {
    $p = Join-Path $src $n
    if (Test-Path $p) { $files += $p }
}

$refs = @("System.dll", "System.Core.dll", "System.Drawing.dll",
          "System.Windows.Forms.dll", "System.Management.dll", "System.Xml.dll") |
        ForEach-Object { "/r:" + (Join-Path $fw $_) }

$cscArgs = @("/nologo", "/langversion:5", "/optimize+", "/warn:4", "/utf8output", ("/out:" + $out))
if ($Probe) { $cscArgs += "/target:exe" } else { $cscArgs += "/target:winexe"; $cscArgs += "/platform:anycpu" }
$cscArgs += $refs
$cscArgs += $files

if (-not $Probe) {
    $manifest = Join-Path $src "app.manifest"
    if (Test-Path $manifest) { $cscArgs += "/win32manifest:" + $manifest }
    $ico = Join-Path $src "app.ico"
    if (Test-Path $ico) { $cscArgs += "/win32icon:" + $ico }
    $res = Join-Path $src "assets.resources"
    if (Test-Path $res) { $cscArgs += "/resource:" + $res }
}

Write-Host ("csc    : " + $csc)
Write-Host ("sources: " + $files.Count)
Write-Host ("output : " + $out)

$lines = & $csc $cscArgs 2>&1
$code = $LASTEXITCODE
foreach ($l in $lines) { Write-Host $l }
if ($code -ne 0) { throw ("compile failed, exit code " + $code) }

$fi = Get-Item $out
Write-Host ("OK: {0}  {1:N0} bytes ({2:N1} KB)" -f $fi.Name, $fi.Length, ($fi.Length / 1KB))
