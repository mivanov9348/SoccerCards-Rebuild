<#
  Draws the Dugout Deck app icon (a card with a ball, like client/public/favicon.svg) and writes
  server\DugoutDeck.Desktop\app.ico with 16-256 px images.
  Usage: powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
#>
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'server\DugoutDeck.Desktop\app.ico'
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)
  $k = $s / 64.0
  # card
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $x = 10 * $k; $y = 4 * $k; $w = 44 * $k; $h = 56 * $k; $r = 7 * $k * 2
  $path.AddArc($x, $y, $r, $r, 180, 90); $path.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
  $path.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90); $path.AddArc($x, $y + $h - $r, $r, $r, 90, 90); $path.CloseFigure()
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(246, 239, 220))), $path)
  $g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(29, 138, 74)), ([Math]::Max(1.5, 5 * $k))), $path)
  # ball
  $dark = [System.Drawing.Color]::FromArgb(29, 85, 48)
  $g.DrawEllipse((New-Object System.Drawing.Pen $dark, ([Math]::Max(1, 3.5 * $k))), 19 * $k, 19 * $k, 26 * $k, 26 * $k)
  $pts = @(@(32, 24.5), @(38.3, 29.1), @(35.9, 36.5), @(28.1, 36.5), @(25.7, 29.1)) | ForEach-Object { New-Object System.Drawing.PointF ($_[0] * $k), ($_[1] * $k) }
  $g.FillPolygon((New-Object System.Drawing.SolidBrush $dark), [System.Drawing.PointF[]]$pts)
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += , $ms.ToArray()
  $g.Dispose(); $bmp.Dispose()
}
# ICO container with PNG images
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $len = $pngs[$i].Length
  $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset); $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
Write-Host "Icon written: $out"
