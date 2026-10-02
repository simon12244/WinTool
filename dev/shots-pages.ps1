# Launch WinTool, click through every nav item, and capture each page (ASCII-only).
param(
    [string]$Exe = "",
    [string]$OutDir = "",
    [int]$WaitMs = 7000,
    [int]$AfterClickMs = 2600
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ($Exe -eq "") { $Exe = Join-Path $root "build\WinTool.exe" }
if ($OutDir -eq "") { $OutDir = Join-Path $root "build\pages" }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class M {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

[void][M]::SetProcessDPIAware()

foreach ($proc in Get-Process WinTool -ErrorAction SilentlyContinue) { $proc.Kill(); Start-Sleep -Milliseconds 400 }

$p = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Milliseconds $WaitMs
$p.Refresh()
if ($p.HasExited) { throw ("process exited early, code " + $p.ExitCode) }

$h = $p.MainWindowHandle
[void][M]::ShowWindow($h, 9)
[void][M]::SetForegroundWindow($h)
[void][M]::SetActiveWindow($h)
Start-Sleep -Milliseconds 600

$pt = New-Object M+POINT
[void][M]::ClientToScreen($h, [ref]$pt)
$baseX = $pt.X
$baseY = $pt.Y   # client origin on screen

$rc = New-Object M+RECT
[void][M]::GetClientRect($h, [ref]$rc)
$cw = $rc.R
$ch = $rc.B
Write-Host ("client {0}x{1} at {2},{3}" -f $cw, $ch, $baseX, $baseY)

function Shot([string]$name) {
    $rc2 = New-Object M+RECT
    [void][M]::GetClientRect($h, [ref]$rc2)
    $w = $rc2.R; $ht = $rc2.B
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($baseX, $baseY, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
    $bmp.Save((Join-Path $OutDir ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host ("saved " + $name + " " + $w + "x" + $ht)
}

# nav rows: y = Dpi.S(112) + i*Dpi.S(44) + Dpi.S(21) ; x center = Dpi.S(120)
# derive scale from a known metric so it works on any DPI
$scale = [M]::GetSystemMetrics(0) / 1366.0
if ($scale -lt 1) { $scale = 1 }
# Better: read the app's own scaling by using the sidebar width ratio is unavailable here.
# The VM reports 200% scaling; compute from screen vs logical primary width.
$scale = [double]$cw / 1180.0
if ($scale -le 0) { $scale = 1 }
Write-Host ("inferred scale {0}" -f $scale)

$navX = [int](120 * $scale)
$names = @("1-shutdown", "2-net", "3-dns", "4-tools", "5-batch", "6-log")
for ($i = 0; $i -lt 6; $i++) {
    $navY = [int]((112 + $i * 44 + 21) * $scale)
    [M]::Click($baseX + $navX, $baseY + $navY)
    Start-Sleep -Milliseconds $AfterClickMs
    Shot $names[$i]
}

Write-Host ("pid=" + $p.Id)
