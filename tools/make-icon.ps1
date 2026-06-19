Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Background circle (navy)
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($bg, 1, 1, $size - 2, $size - 2)
    $bg.Dispose()

    $s = $size / 32.0
    # Mouse body (white ellipse approximating rounded rect)
    $mouse = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($mouse, [int](10*$s), [int](9*$s), [int](9*$s), [int](14*$s))
    $mouse.Dispose()
    # Mouse divider line
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,30,41,82), [single](1.2*$s))
    $g.DrawLine($pen, [single](14.5*$s), [single](9*$s), [single](14.5*$s), [single](15*$s))
    $pen.Dispose()

    # Crescent moon (yellow) -- upper right
    $moon = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 209, 102))
    $g.FillEllipse($moon, [int](19*$s), [int](4*$s), [int](9*$s), [int](9*$s))
    $moon.Dispose()
    $cut = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($cut, [int](21.5*$s), [int](3*$s), [int](8*$s), [int](8*$s))
    $cut.Dispose()

    $g.Dispose()
    return $bmp
}

$frames = @(16, 32) | ForEach-Object { New-Frame $_ }

# Write .ico directly (ICONDIR + ICONDIRENTRY + PNG data)
$pngs = $frames | ForEach-Object {
    $ms = New-Object System.IO.MemoryStream
    $_.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $data = $ms.ToArray()
    $ms.Dispose()
    $_.Dispose()
    ,$data
}

# Resolve output path robustly (no Resolve-Path on non-existent file)
$outDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\MouseMover'))
$out = Join-Path $outDir 'app.ico'

$fs = [System.IO.File]::Open($out, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR header
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $sz = $frames[$i].Width
    $bw.Write([byte]($(if ($sz -ge 256) {0} else {$sz})))   # width
    $bw.Write([byte]($(if ($sz -ge 256) {0} else {$sz})))   # height
    $bw.Write([byte]0); $bw.Write([byte]0)                   # colors, reserved
    $bw.Write([uint16]1); $bw.Write([uint16]32)              # planes, bpp
    $bw.Write([uint32]$pngs[$i].Length)                      # bytes
    $bw.Write([uint32]$offset)                               # offset
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close()
Write-Host "Wrote $out"
