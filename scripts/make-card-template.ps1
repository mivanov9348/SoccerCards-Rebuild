<#
  Draws docs\card-template-600x840.png: a transparent 600x840 (5:7) guide to put on top of a card picture in Photoshop.
  Red = covered by the card name bar, blue circle = covered by the icon badge. Keep faces and the ball in the clear area.
  Usage: powershell -ExecutionPolicy Bypass -File scripts\make-card-template.ps1
#>
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'docs\card-template-600x840.png'
$w = 600; $h = 840
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

$bar = [int]($h * 0.18)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 220, 40, 40))), 0, $h - $bar, $w, $bar)
$badge = [int]($w * 0.16)
$g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 40, 120, 220))), 12, 12, $badge, $badge)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(160, 255, 255, 255)), 2
$pen.DashStyle = 'Dash'
$g.DrawRectangle($pen, 1, 1, $w - 3, $h - 3)
$g.DrawLine($pen, $w / 2, 0, $w / 2, $h - $bar)
$g.DrawLine($pen, 0, ($h - $bar) / 2, $w, ($h - $bar) / 2)

$font = New-Object System.Drawing.Font 'Segoe UI', 22, ([System.Drawing.FontStyle]::Bold)
$small = New-Object System.Drawing.Font 'Segoe UI', 14
$white = [System.Drawing.Brushes]::White
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
$g.DrawString('CARD NAME (written by the game)', $font, $white, (New-Object System.Drawing.RectangleF 0, ($h - $bar), $w, $bar), $fmt)
$g.DrawString('icon', $small, $white, (New-Object System.Drawing.RectangleF 12, 12, $badge, $badge), $fmt)
$g.DrawString('keep faces and the ball in this area', $small, $white, (New-Object System.Drawing.RectangleF 0, 40, $w, 60), $fmt)

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Template written: $out"
