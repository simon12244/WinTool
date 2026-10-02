# Generate src/app.ico (multi-size) and src/version.res for WinTool. ASCII-only.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $root "src"

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [double]$size * 0.035
    $boxSize = [double]$size - (2 * $pad)
    $r = [System.Drawing.RectangleF]::new([float]$pad, [float]$pad, [float]$boxSize, [float]$boxSize)
    $rad = [double]$size * 0.24

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $path.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $path.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $path.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $rect = [System.Drawing.Rectangle]::new(0, 0, [int]$size, [int]$size)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(255, 58, 140, 246),
        [System.Drawing.Color]::FromArgb(255, 20, 74, 176), 55.0)
    $g.FillPath($brush, $path)

    # subtle top highlight
    $hl = [System.Drawing.RectangleF]::new([float]($r.X + $size * 0.06), [float]($r.Y + $size * 0.05), [float]($r.Width - $size * 0.12), [float]($r.Height * 0.40))
    $hlPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hd = $size * 0.16
    $hlPath.AddArc($hl.X, $hl.Y, $hd, $hd, 180, 90)
    $hlPath.AddArc($hl.Right - $hd, $hl.Y, $hd, $hd, 270, 90)
    $hlPath.AddArc($hl.Right - $hd, $hl.Bottom - $hd, $hd, $hd, 0, 90)
    $hlPath.AddArc($hl.X, $hl.Bottom - $hd, $hd, $hd, 90, 90)
    $hlPath.CloseFigure()
    $hlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 255, 255))
    $g.FillPath($hlBrush, $hlPath)

    # white W mark
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, ($size * 0.095))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $x0 = $size * 0.245; $x1 = $size * 0.39; $x2 = $size * 0.50; $x3 = $size * 0.61; $x4 = $size * 0.755
    $yt = $size * 0.42;  $yb = $size * 0.745
    $pts = @(
        (New-Object System.Drawing.PointF($x0, $yt)),
        (New-Object System.Drawing.PointF($x1, $yb)),
        (New-Object System.Drawing.PointF($x2, ($yt + $size * 0.10))),
        (New-Object System.Drawing.PointF($x3, $yb)),
        (New-Object System.Drawing.PointF($x4, $yt))
    )
    $g.DrawLines($pen, $pts)

    $pen.Dispose(); $brush.Dispose(); $hlBrush.Dispose(); $path.Dispose(); $hlPath.Dispose(); $g.Dispose()
    return $bmp
}

# ---- build ICO (PNG-compressed entries, Vista+) ----
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$entries = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += , @{ Size = $s; Data = $ms.ToArray() }
    $ms.Dispose(); $bmp.Dispose()
}

$icoPath = Join-Path $srcDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # type = icon
$bw.Write([UInt16]$entries.Count)    # count
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $s = [int]$e.Size
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([UInt16]1)             # planes
    $bw.Write([UInt16]32)            # bpp
    $bw.Write([UInt32]$e.Data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) { $bw.Write($e.Data) }
$bw.Flush(); $bw.Close(); $fs.Close()
Write-Host ("icon written: " + $icoPath + "  " + (Get-Item $icoPath).Length + " bytes, " + $entries.Count + " sizes")

# ---- build version resource (.res) : Win32 VERSIONINFO + icon ----
# Build the raw Win32 resource section by hand (no rc.exe / no Win32ResourceWriter in .NET).
function New-Tree([object[]]$children, [string]$value) {
    $pairs = New-Object System.Collections.ArrayList
    foreach ($c in $children) { [void]$pairs.Add($c) }
    $hasValue = $null -ne $value
    if ($hasValue) {
        $vb = [System.Text.Encoding]::Unicode.GetBytes($value + [char]0)
        [void]$pairs.Add((New-Object System.Collections.Hashtable))
        $pairs[$pairs.Count - 1].Key = [System.Text.Encoding]::Unicode.GetBytes("Value")
        $pairs[$pairs.Count - 1].Val = $vb
    }
    $items = @($pairs | Sort-Object { [BitConverter]::ToString($_.Key) })

    $inner = New-Object System.Collections.Generic.List[byte]
    foreach ($it in $items) {
        $kb = $it.Key
        $block = New-Object System.Collections.Generic.List[byte]
        $block.AddRange([byte[]](0,0))                       # wLength placeholder
        $vl = 0
        if ($it.ContainsKey("Val")) { $vl = $it.Val.Length }
        $block.AddRange([BitConverter]::GetBytes([uint16]$vl))
        $block.AddRange([BitConverter]::GetBytes([uint16]1)) # wType = text
        $block.AddRange($kb)
        $block.AddRange([byte[]](0,0))                       # null terminator
        $pad = (4 - ($block.Count % 4)) % 4
        for ($i = 0; $i -lt $pad; $i++) { $block.Add(0) }
        if ($vl -gt 0) { $block.AddRange($it.Val) }
        while ($block.Count % 4 -ne 0) { $block.Add(0) }
        $len = [BitConverter]::GetBytes([uint16]$block.Count)
        $block[0] = $len[0]; $block[1] = $len[1]
        $inner.AddRange($block)
    }

    $head = New-Object System.Collections.Generic.List[byte]
    $head.AddRange([byte[]](0,0))
    $hl = 0
    if ($hasValue) { $hl = ([System.Text.Encoding]::Unicode.GetBytes($value + [char]0)).Length }
    $head.AddRange([BitConverter]::GetBytes([uint16]$hl))
    $head.AddRange([BitConverter]::GetBytes([uint16]0))      # wType = binary
    $head.AddRange([System.Text.Encoding]::Unicode.GetBytes("VS_VERSION_INFO"))
    $head.AddRange([byte[]](0,0))
    $pad = (4 - ($head.Count % 4)) % 4
    for ($i = 0; $i -lt $pad; $i++) { $head.Add(0) }
    if ($hasValue) { $head.AddRange([System.Text.Encoding]::Unicode.GetBytes($value + [char]0)) }
    while ($head.Count % 4 -ne 0) { $head.Add(0) }
    $head.AddRange($inner)
    $len = [BitConverter]::GetBytes([uint16]$head.Count)
    $head[0] = $len[0]; $head[1] = $len[1]
    return , $head.ToArray()
}

function New-VsVar([string]$name, [string]$value) {
    $h = @{ Key = [System.Text.Encoding]::Unicode.GetBytes($name); Val = [System.Text.Encoding]::Unicode.GetBytes($value + [char]0) }
    return $h
}

try {
    $resPath = Join-Path $srcDir "version.res"
    if (Test-Path $resPath) { Remove-Item $resPath -Force }

    $fixed = New-Object System.Collections.Generic.List[byte]
    $fixed.AddRange([BitConverter]::GetBytes([uint32]4277077181))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]65536))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]65536))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]65536))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]65536))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]0))        # dwFileFlags
    $fixed.AddRange([BitConverter]::GetBytes([uint32]0))
    $fixed.AddRange([BitConverter]::GetBytes([uint32]262148)) # VOS_NT_WINDOWS32
    $fixed.AddRange([BitConverter]::GetBytes([uint32]1))          # VFT_APP

    $strs = @(
        (New-VsVar "FileDescription"  "WinTool system toolbox - native Windows desktop utility"),
        (New-VsVar "FileVersion"      "1.0.0.0"),
        (New-VsVar "InternalName"     "WinTool"),
        (New-VsVar "OriginalFilename" "WinTool.exe"),
        (New-VsVar "ProductName"      "WinTool"),
        (New-VsVar "ProductVersion"   "1.0.0.0"),
        (New-VsVar "LegalCopyright"   "Free to use and redistribute"),
        (New-VsVar "Comments"         "Scheduled shutdown / IP info / DNS / system tools / batch library")
    )
    $strBlock = @{ Key = [System.Text.Encoding]::Unicode.GetBytes("080404B0"); Children = $strs }
    $stringFileInfo = @{ Key = [System.Text.Encoding]::Unicode.GetBytes("StringFileInfo"); Children = @($strBlock) }

    $tr = New-Object System.Collections.Hashtable
    $tr.Key = [System.Text.Encoding]::Unicode.GetBytes("Translation")
    $tr.Val = [byte[]](0x04,0x08,0xB0,0x04)
    $varFileInfo = @{ Key = [System.Text.Encoding]::Unicode.GetBytes("VarFileInfo"); Children = @($tr) }

    $viChildren = @($stringFileInfo, $varFileInfo)
    # New-Tree expects children as hashtables with Key/Val or Key/Children
    function Convert-Node($node) {
        if ($node.ContainsKey("Children")) {
            $kids = @()
            foreach ($k in $node.Children) { $kids += (Convert-Node $k) }
            return @{ Key = $node.Key; Children = $kids }
        }
        return @{ Key = $node.Key; Val = $node.Val }
    }
    $nodes = @()
    foreach ($c in $viChildren) { $nodes += (Convert-Node $c) }
    $verBytes = New-Tree $nodes ([string][char]0)

    $payload = New-Object System.Collections.Generic.List[byte]
    $payload.AddRange([byte[]]$fixed.ToArray())
    $payload.AddRange($verBytes)

    $res = New-Object System.Collections.Generic.List[byte]
    $res.AddRange([byte[]](0,0,0,0))                    # DataSize
    $res.AddRange([BitConverter]::GetBytes([uint32]32)) # HeaderSize
    $res.AddRange([BitConverter]::GetBytes([uint32]0))
    $res.AddRange([BitConverter]::GetBytes([uint16]16)) # type = RT_VERSION
    $res.AddRange([BitConverter]::GetBytes([uint16]1))
    $res.AddRange([BitConverter]::GetBytes([uint32]0))
    $res.AddRange([BitConverter]::GetBytes([uint32]1))
    $res.AddRange([BitConverter]::GetBytes([uint32]0))
    $res.AddRange([BitConverter]::GetBytes([uint32]0))
    $ds = [BitConverter]::GetBytes([uint32]$payload.Count)
    $res[0] = $ds[0]; $res[1] = $ds[1]; $res[2] = $ds[2]; $res[3] = $ds[3]
    $res.AddRange($payload.ToArray())
    while ($res.Count % 4 -ne 0) { $res.Add(0) }

    # ---- append icon resources (RT_ICON + RT_GROUP_ICON) ----
    # Parse the .ico written above so icon and version live in a single .res
    # (csc rejects /win32res together with /win32icon).
    $icoBytes = [System.IO.File]::ReadAllBytes($icoPath)
    $cnt = [BitConverter]::ToUInt16($icoBytes, 4)
    $images = @()
    for ($k = 0; $k -lt $cnt; $k++) {
        $off = 6 + 16 * $k
        $w = $icoBytes[$off]
        $h = $icoBytes[$off + 1]
        $bpp = [BitConverter]::ToUInt16($icoBytes, $off + 6)
        $len = [BitConverter]::ToUInt32($icoBytes, $off + 8)
        $pos = [BitConverter]::ToUInt32($icoBytes, $off + 12)
        $data = New-Object byte[] $len
        [Array]::Copy($icoBytes, $pos, $data, 0, $len)
        $images += , @{ W = $w; H = $h; Bpp = $bpp; Data = $data; Id = (100 + $k) }
    }

    function Add-Res($list, [int]$type, [int]$name, [byte[]]$data) {
        $list.AddRange([BitConverter]::GetBytes([uint32]$data.Length))
        $list.AddRange([BitConverter]::GetBytes([uint32]32))
        $list.AddRange([BitConverter]::GetBytes([uint32]0))
        $list.AddRange([BitConverter]::GetBytes([uint16]$type))
        $list.AddRange([BitConverter]::GetBytes([uint16]$name))
        $list.AddRange([BitConverter]::GetBytes([uint32]0))
        $list.AddRange([BitConverter]::GetBytes([uint32]1))
        $list.AddRange([BitConverter]::GetBytes([uint32]0))
        $list.AddRange([BitConverter]::GetBytes([uint32]0))
        $list.AddRange($data)
        while ($list.Count % 4 -ne 0) { $list.Add(0) }
    }

    foreach ($im in $images) { Add-Res $res 3 $im.Id $im.Data }

    $grp = New-Object System.Collections.Generic.List[byte]
    $grp.AddRange([byte[]](0,0))
    $grp.AddRange([byte[]](1,0))
    $grp.AddRange([BitConverter]::GetBytes([uint16]$images.Count))
    foreach ($im in $images) {
        $grp.Add($im.W); $grp.Add($im.H); $grp.Add(0); $grp.Add(0)
        $grp.AddRange([BitConverter]::GetBytes([uint16]1))
        $grp.AddRange([BitConverter]::GetBytes([uint16]$im.Bpp))
        $grp.AddRange([BitConverter]::GetBytes([uint32]$im.Data.Length))
        $grp.AddRange([BitConverter]::GetBytes([uint16]$im.Id))
    }
    Add-Res $res 14 1 $grp.ToArray()

    # ---- append the application manifest (RT_MANIFEST, id 1) ----
    # Required because csc rejects /win32res combined with /win32manifest.
    $manifestPath = Join-Path $srcDir "app.manifest"
    if (Test-Path $manifestPath) {
        $manifestBytes = [System.IO.File]::ReadAllBytes($manifestPath)
        Add-Res $res 24 1 $manifestBytes
        Write-Host ("manifest embedded: " + $manifestBytes.Length + " bytes")
    }

    [System.IO.File]::WriteAllBytes($resPath, $res.ToArray())
    Write-Host ("version.res written: " + (Get-Item $resPath).Length + " bytes (version + " + $images.Count + " icon images)")
} catch {
    Write-Host ("version res failed: " + $_.Exception.Message)
}
