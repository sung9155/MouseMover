Add-Type -AssemblyName System.Drawing

# 밝고 꽉 찬 아이콘: 밝은 파랑 풀블리드 원 + 크고 굵은 흰 마우스(중앙).
$BgColor = [System.Drawing.Color]::FromArgb(255, 30, 144, 255)   # 밝은 파랑 (DodgerBlue)

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 원 — 프레임을 꽉 채움(풀블리드)
    $bg = New-Object System.Drawing.SolidBrush($BgColor)
    $g.FillEllipse($bg, 0, 0, $size, $size)
    $bg.Dispose()

    $s = $size / 32.0
    # 마우스 본체 — 크고 중앙 (흰색). 세로로 프레임을 많이 채움.
    $mw = 14.0 * $s
    $mh = 19.0 * $s
    $mx = ($size - $mw) / 2.0
    $my = 6.5 * $s
    $mouse = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($mouse, $mx, $my, $mw, $mh)
    $mouse.Dispose()

    # 마우스 구분선 — 배경색으로 윗부분을 갈라 버튼 느낌
    $pen = New-Object System.Drawing.Pen($BgColor, [single](1.6 * $s))
    $g.DrawLine($pen, [single]($size / 2.0), [single]($my + 1.0 * $s), [single]($size / 2.0), [single]($my + 8.0 * $s))
    $pen.Dispose()

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
