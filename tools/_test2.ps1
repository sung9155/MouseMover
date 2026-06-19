Add-Type -AssemblyName System.Drawing
function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($bg, 1, 1, $size - 2, $size - 2)
    $s = $size / 32.0
    $mouse = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($mouse, [int](10*$s), [int](9*$s), [int](9*$s), [int](14*$s))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,30,41,82), [single](1.2*$s))
    Write-Host "size=$size pen null=$($null -eq $pen)"
    $g.DrawLine($pen, [single](14.5*$s), [single](9*$s), [single](14.5*$s), [single](15*$s))
    $moon = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 209, 102))
    $g.FillEllipse($moon, [int](19*$s), [int](4*$s), [int](9*$s), [int](9*$s))
    $cut = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 41, 82))
    $g.FillEllipse($cut, [int](21.5*$s), [int](3*$s), [int](8*$s), [int](8*$s))
    $g.Dispose()
    return $bmp
}
$frames = @(16, 32) | ForEach-Object { New-Frame $_ }
Write-Host "frames count: $($frames.Count)"
