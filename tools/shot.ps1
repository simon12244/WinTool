# Bring WinTool to front and capture its window rect exactly (ASCII-only).
param(
    [string]$Exe = "",
    [string]$Out = "",
    [int]$WaitMs = 7000
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ($Exe -eq "") { $Exe = Join-Path $root "build\WinTool.exe" }
if ($Out -eq "") { $Out = Join-Path $root "build\shot.png" }

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Make this PowerShell host DPI aware, otherwise GetClientRect returns virtualized (halved) values.
Add-Type @"
using System.Runtime.InteropServices;
public class Dpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[void][Dpi]::SetProcessDPIAware()

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W2 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

foreach ($proc in Get-Process WinTool -ErrorAction SilentlyContinue) { $proc.Kill(); Start-Sleep -Milliseconds 400 }

$p = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Milliseconds $WaitMs
$p.Refresh()
if ($p.HasExited) { throw ("process exited early, code " + $p.ExitCode) }

$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { throw "no main window handle" }

$sw = [W2]::GetSystemMetrics(0)
$sh = [W2]::GetSystemMetrics(1)
Write-Host ("screen: {0}x{1}" -f $sw, $sh)

# HWND_TOPMOST = -1 ; SWP_NOSIZE=1 SWP_SHOWWINDOW=0x40
[void][W2]::ShowWindow($h, 9)
[void][W2]::SetWindowPos($h, [IntPtr](-1), 8, 8, 0, 0, 0x41)
Start-Sleep -Milliseconds 1200

$r = New-Object W2+RECT
[void][W2]::GetWindowRect($h, [ref]$r)
$w = $r.R - $r.L
$ht = $r.B - $r.T
Write-Host ("window rect: {0},{1} {2}x{3}" -f $r.L, $r.T, $w, $ht)

$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size($w, $ht)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host ("saved: " + $Out + "  pid=" + $p.Id)
