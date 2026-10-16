# The game's own pictures, drawn here (nothing of the retail games in them):
#   Game\menu.png       the main menu's backdrop, 640x480 -- the installer puts it in its copy of SCREEN.HQR (entry 4, the menu picture)
#                       in LBA2's own palette
#   Installer\pumpkin.ico  the installer's and the game's icon, a jack-o'-lantern
# Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File tools\make_art.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $root 'Game') | Out-Null

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

# A jack-o'-lantern, `s` pixels across, its middle at (cx, cy): ribbed orange body, a stalk, glowing eyes, nose and grin.
function Pumpkin($g, [float]$cx, [float]$cy, [float]$s, [bool]$glow = $true) {
    $w = $s; $h = $s * 0.82
    $x0 = $cx - $w / 2; $y0 = $cy - $h / 2
    if ($glow) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddEllipse($cx - $w * 0.9, $cy - $h * 0.9, $w * 1.8, $h * 1.8)
        $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
        $pg.CenterColor = C 255 140 20 110
        $pg.SurroundColors = @(C 255 90 0 0)
        $g.FillPath($pg, $path)
    }
    # the ribs: overlapping ellipses, darker to the sides
    $ribs = @(@(0.0, 0.62, 0), @(-0.26, 0.56, 1), @(0.26, 0.56, 1), @(-0.12, 0.66, 2), @(0.12, 0.66, 2))
    foreach ($r in $ribs) {
        $rw = $w * $r[1]; $rx = $cx + $w * $r[0] - $rw / 2
        $shade = @(@(214, 96, 14), @(196, 82, 10), @(240, 122, 24))[$r[2]]
        $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF $rx, $y0, $rw, $h), (C $shade[0] $shade[1] $shade[2]), (C ([int]($shade[0] * 0.55)) ([int]($shade[1] * 0.45)) 6), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
        $g.FillEllipse($br, $rx, $y0, $rw, $h)
    }
    $pen = New-Object System.Drawing.Pen (C 120 44 4), ([Math]::Max(1, $s / 60))
    foreach ($r in $ribs) { $rw = $w * $r[1]; $g.DrawEllipse($pen, $cx + $w * $r[0] - $rw / 2, $y0, $rw, $h) }
    # the stalk
    $st = New-Object System.Drawing.SolidBrush (C 70 92 30)
    $g.FillRectangle($st, $cx - $s * 0.05, $y0 - $s * 0.12, $s * 0.1, $s * 0.17)
    # the face: glowing yellow
    $face = New-Object System.Drawing.SolidBrush (C 255 226 90)
    $eyeY = $cy - $h * 0.14
    foreach ($side in @(-1, 1)) {
        $ex = $cx + $side * $w * 0.2
        $pts = @((New-Object System.Drawing.PointF ($ex - $w * 0.1), ($eyeY + $h * 0.08)), (New-Object System.Drawing.PointF ($ex + $w * 0.1), ($eyeY + $h * 0.08)), (New-Object System.Drawing.PointF ($ex + $side * $w * 0.03), ($eyeY - $h * 0.1)))
        $g.FillPolygon($face, $pts)
    }
    $pts = @((New-Object System.Drawing.PointF ($cx - $w * 0.05), ($cy + $h * 0.06)), (New-Object System.Drawing.PointF ($cx + $w * 0.05), ($cy + $h * 0.06)), (New-Object System.Drawing.PointF $cx, ($cy - $h * 0.03)))
    $g.FillPolygon($face, $pts)
    $mouth = New-Object System.Drawing.Drawing2D.GraphicsPath
    $my = $cy + $h * 0.16
    $mouth.AddLines(@(
        (New-Object System.Drawing.PointF ($cx - $w * 0.3), ($my - $h * 0.04)),
        (New-Object System.Drawing.PointF ($cx - $w * 0.18), ($my + $h * 0.02)),
        (New-Object System.Drawing.PointF ($cx - $w * 0.12), ($my - $h * 0.03)),
        (New-Object System.Drawing.PointF ($cx - $w * 0.04), ($my + $h * 0.04)),
        (New-Object System.Drawing.PointF ($cx + $w * 0.04), ($my - $h * 0.03)),
        (New-Object System.Drawing.PointF ($cx + $w * 0.12), ($my + $h * 0.04)),
        (New-Object System.Drawing.PointF ($cx + $w * 0.18), ($my - $h * 0.02)),
        (New-Object System.Drawing.PointF ($cx + $w * 0.3), ($my - $h * 0.04)),
        (New-Object System.Drawing.PointF ($cx + $w * 0.22), ($my + $h * 0.14)),
        (New-Object System.Drawing.PointF ($cx - $w * 0.22), ($my + $h * 0.14))))
    $mouth.CloseFigure()
    $g.FillPath($face, $mouth)
}

function Bat($g, [float]$x, [float]$y, [float]$s) {
    $b = New-Object System.Drawing.SolidBrush (C 8 4 12)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddLines(@(
        (New-Object System.Drawing.PointF $x, ($y - $s * 0.1)),
        (New-Object System.Drawing.PointF ($x + $s * 0.25), ($y - $s * 0.35)),
        (New-Object System.Drawing.PointF ($x + $s * 0.55), ($y - $s * 0.3)),
        (New-Object System.Drawing.PointF ($x + $s), ($y - $s * 0.45)),
        (New-Object System.Drawing.PointF ($x + $s * 0.8), ($y - $s * 0.05)),
        (New-Object System.Drawing.PointF ($x + $s * 0.62), ($y + $s * 0.02)),
        (New-Object System.Drawing.PointF ($x + $s * 0.45), ($y + $s * 0.15)),
        (New-Object System.Drawing.PointF ($x + $s * 0.3), ($y + $s * 0.02)),
        (New-Object System.Drawing.PointF $x, ($y + $s * 0.12)),
        (New-Object System.Drawing.PointF ($x - $s * 0.3), ($y + $s * 0.02)),
        (New-Object System.Drawing.PointF ($x - $s * 0.45), ($y + $s * 0.15)),
        (New-Object System.Drawing.PointF ($x - $s * 0.62), ($y + $s * 0.02)),
        (New-Object System.Drawing.PointF ($x - $s * 0.8), ($y - $s * 0.05)),
        (New-Object System.Drawing.PointF ($x - $s), ($y - $s * 0.45)),
        (New-Object System.Drawing.PointF ($x - $s * 0.55), ($y - $s * 0.3)),
        (New-Object System.Drawing.PointF ($x - $s * 0.25), ($y - $s * 0.35))))
    $p.CloseFigure()
    $g.FillPath($b, $p)
}

function Text($g, [string]$text, [string]$font, [float]$size, [System.Drawing.FontStyle]$style, [float]$cy, $fill, $glow) {
    $ff = New-Object System.Drawing.FontFamily $font
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $path.AddString($text, $ff, [int]$style, $size, (New-Object System.Drawing.PointF 320, $cy), $sf)
    foreach ($k in 7, 5, 3) { $pen = New-Object System.Drawing.Pen $glow, $k; $pen.LineJoin = 'Round'; $g.DrawPath($pen, $path) }
    $edge = New-Object System.Drawing.Pen (C 20 6 2), 2.5
    $g.DrawPath($edge, $path)
    $g.FillPath($fill, $path)
}

# ---- the menu's backdrop ----------------------------------------------------------------------------------------------------------
$bmp = New-Object System.Drawing.Bitmap 640, 480
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAlias'
# the night: deep purple overhead to a dusky orange-brown over the hills
$sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 640, 480), (C 22 8 40), (C 90 36 26), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
$blend.Colors = @((C 18 6 34), (C 44 14 66), (C 86 30 74), (C 120 52 30))
$blend.Positions = @(0.0, 0.45, 0.75, 1.0)
$sky.InterpolationColors = $blend
$g.FillRectangle($sky, 0, 0, 640, 480)
# stars
$rnd = New-Object System.Random 31
$star = New-Object System.Drawing.SolidBrush (C 230 220 255)
for ($i = 0; $i -lt 70; $i++) { $sx = $rnd.Next(0, 640); $sy = $rnd.Next(0, 300); $ss = 1 + $rnd.Next(0, 2); $g.FillRectangle($star, $sx, $sy, $ss, $ss) }
# the moon, with its glow, and a witch on her broom across it
$mp = New-Object System.Drawing.Drawing2D.GraphicsPath
$mp.AddEllipse(420, 40, 220, 220)
$mg = New-Object System.Drawing.Drawing2D.PathGradientBrush $mp
$mg.CenterColor = C 250 236 190 150
$mg.SurroundColors = @(C 200 160 220 0)
$g.FillPath($mg, $mp)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 246 234 196)), 470, 90, 120, 120)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 226 210 168)), 500, 120, 22, 18)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 226 210 168)), 548, 160, 16, 14)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 230 214 172)), 540, 108, 12, 10)
$black = New-Object System.Drawing.SolidBrush (C 8 4 12)
# (the witch: a broom, a cloak, a pointed hat)
$g.FillPolygon($black, @((New-Object System.Drawing.PointF 486, 168), (New-Object System.Drawing.PointF 580, 148), (New-Object System.Drawing.PointF 581, 151), (New-Object System.Drawing.PointF 487, 171)))
$g.FillPolygon($black, @((New-Object System.Drawing.PointF 572, 142), (New-Object System.Drawing.PointF 600, 132), (New-Object System.Drawing.PointF 604, 150), (New-Object System.Drawing.PointF 580, 156)))
$g.FillPolygon($black, @((New-Object System.Drawing.PointF 520, 160), (New-Object System.Drawing.PointF 548, 128), (New-Object System.Drawing.PointF 556, 156)))
$g.FillPolygon($black, @((New-Object System.Drawing.PointF 512, 166), (New-Object System.Drawing.PointF 528, 146), (New-Object System.Drawing.PointF 536, 162)))
$g.FillEllipse($black, 540, 116, 14, 14)
$g.FillPolygon($black, @((New-Object System.Drawing.PointF 534, 120), (New-Object System.Drawing.PointF 562, 120), (New-Object System.Drawing.PointF 552, 92)))
# bats
foreach ($bat in @(@(90, 150, 22), @(140, 120, 14), @(60, 200, 12), @(400, 70, 12), @(610, 260, 16), @(30, 110, 10), @(250, 190, 9))) { Bat $g $bat[0] $bat[1] $bat[2] }
# the hills, a temple's stepped silhouette, dead trees and graves
$hill = New-Object System.Drawing.SolidBrush (C 14 8 18)
$g.FillPolygon($hill, @((New-Object System.Drawing.PointF 0, 360), (New-Object System.Drawing.PointF 120, 330), (New-Object System.Drawing.PointF 260, 352), (New-Object System.Drawing.PointF 400, 322), (New-Object System.Drawing.PointF 520, 345), (New-Object System.Drawing.PointF 640, 318), (New-Object System.Drawing.PointF 640, 480), (New-Object System.Drawing.PointF 0, 480)))
for ($k = 0; $k -lt 5; $k++) { $w = 150 - $k * 28; $g.FillRectangle($hill, 250 + (150 - $w) / 2, 330 - $k * 22, $w, 24) }
$g.FillRectangle($hill, 314, 210, 22, 22)
$tree = New-Object System.Drawing.Pen (C 14 8 18), 5
$tree.StartCap = 'Round'; $tree.EndCap = 'Round'
foreach ($t in @(@(40, 350, 1), @(600, 330, -1))) {
    $tx = $t[0]; $ty = $t[1]; $d = $t[2]
    $g.DrawLine($tree, $tx, $ty, $tx + 4 * $d, $ty - 90)
    $tree.Width = 3
    $g.DrawLine($tree, $tx + 2 * $d, $ty - 50, $tx + 30 * $d, $ty - 80); $g.DrawLine($tree, $tx + 30 * $d, $ty - 80, $tx + 40 * $d, $ty - 76)
    $g.DrawLine($tree, $tx + 3 * $d, $ty - 70, $tx - 22 * $d, $ty - 100); $g.DrawLine($tree, $tx + 4 * $d, $ty - 88, $tx + 18 * $d, $ty - 112)
    $tree.Width = 5
}
$grave = New-Object System.Drawing.SolidBrush (C 30 24 40)
foreach ($gx in 96, 150, 470, 540) { $g.FillRectangle($grave, $gx, 372, 20, 30); $g.FillEllipse($grave, $gx, 362, 20, 22) }
$g.FillRectangle($grave, 205, 360, 6, 40); $g.FillRectangle($grave, 197, 370, 22, 6)
$g.FillRectangle($grave, 590, 362, 6, 40); $g.FillRectangle($grave, 582, 372, 22, 6)
# a ghost, low on the left
$gp = New-Object System.Drawing.Drawing2D.GraphicsPath
$gp.AddArc(16, 236, 44, 44, 180, 180)
$gp.AddLines(@((New-Object System.Drawing.PointF 60, 258), (New-Object System.Drawing.PointF 60, 300), (New-Object System.Drawing.PointF 52, 292), (New-Object System.Drawing.PointF 45, 302), (New-Object System.Drawing.PointF 38, 292), (New-Object System.Drawing.PointF 31, 302), (New-Object System.Drawing.PointF 23, 292), (New-Object System.Drawing.PointF 16, 300), (New-Object System.Drawing.PointF 16, 258)))
$gp.CloseFigure()
$g.FillPath((New-Object System.Drawing.SolidBrush (C 226 230 240 220)), $gp)
$g.FillEllipse($black, 28, 254, 7, 10); $g.FillEllipse($black, 42, 254, 7, 10); $g.FillEllipse($black, 34, 270, 9, 10)
# the pumpkins along the bottom
Pumpkin $g 70 440 70
Pumpkin $g 150 452 46
Pumpkin $g 570 438 76
Pumpkin $g 494 454 44
Pumpkin $g 320 460 40
# the title, over the top of the screen (the menu's rows start below 95)
$orange = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 6, 640, 62), (C 255 214 80), (C 236 92 10), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
Text $g 'Little Big Halloween' 'Chiller' 66 ([System.Drawing.FontStyle]::Bold) 36 $orange (C 120 30 160 90)
$lilac = New-Object System.Drawing.SolidBrush (C 222 206 255)
Text $g 'Racing Adventure' 'Chiller' 38 ([System.Drawing.FontStyle]::Regular) 78 $lilac (C 40 10 60 120)
$g.Dispose()
$menu = Join-Path $root 'Game\menu.png'
$bmp.Save($menu, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"menu: $menu"

# ---- the icon ---------------------------------------------------------------------------------------------------------------------
$sizes = 256, 64, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
    $b = New-Object System.Drawing.Bitmap $s, $s
    $gi = [System.Drawing.Graphics]::FromImage($b)
    $gi.SmoothingMode = 'AntiAlias'
    $gi.Clear([System.Drawing.Color]::Transparent)
    Pumpkin $gi ($s / 2) ($s * 0.56) ($s * 0.92) $false
    $gi.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $b.Dispose()
}
$ico = New-Object System.IO.MemoryStream
$wr = New-Object System.IO.BinaryWriter $ico
$wr.Write([UInt16]0); $wr.Write([UInt16]1); $wr.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $wr.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $wr.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $wr.Write([byte]0); $wr.Write([byte]0); $wr.Write([UInt16]1); $wr.Write([UInt16]32)
    $wr.Write([UInt32]$pngs[$i].Length); $wr.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $wr.Write($p) }
$wr.Flush()
$icon = Join-Path $root 'Installer\pumpkin.ico'
[System.IO.File]::WriteAllBytes($icon, $ico.ToArray())
"icon: $icon"
