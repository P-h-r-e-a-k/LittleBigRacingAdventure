# The game's own pictures, drawn here (nothing of the retail games in them):
#   Game\menu.png       the main menu's backdrop, 640x480 -- the installer puts it in its copy of SCREEN.HQR (entry 4, the menu picture)
#                       in LBA2's own palette
#   Installer\lbra.ico  the installer's and the game's icon: a chequered flag on a badge
# A year-round racing look since 2026-11-25 (the user: "we need to update the menus it's still halloween themed, it all needs updating
# including the app icon" -- a racing theme with no season, the name Little Big Racing Adventure): an island race road at sunset, a
# start gantry across it, chequered flags either side of the title. (Until then: a Halloween night, pumpkins, bats, a witch.)
# Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File tools\make_art.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $root 'Game') | Out-Null

function C([int]$r, [int]$g, [int]$b, [int]$a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
function P([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }

# A chequered flag waving: `cols` x `rows` squares over (x, y) .. (x + w, y + h), its edges rising and falling along a wave of `amp`
# pixels (`phase` where along it starts), each square a quad on the wave, black and white, a darker band where the cloth folds away.
function Flag($g, [float]$x, [float]$y, [float]$w, [float]$h, [int]$cols, [int]$rows, [float]$amp, [float]$phase) {
    for ($i = 0; $i -lt $cols; $i++) {
        $u0 = $i / $cols; $u1 = ($i + 1) / $cols
        $d0 = $amp * [Math]::Sin($phase + $u0 * 2 * [Math]::PI); $d1 = $amp * [Math]::Sin($phase + $u1 * 2 * [Math]::PI)
        # (the fold's shade: the cloth turned away where the wave goes down)
        $slope = [Math]::Cos($phase + ($u0 + $u1) / 2 * 2 * [Math]::PI)
        for ($j = 0; $j -lt $rows; $j++) {
            $v0 = $j / $rows; $v1 = ($j + 1) / $rows
            $dark = (($i + $j) % 2) -eq 0
            $lite = [int](225 + 30 * $slope); if ($lite -gt 255) { $lite = 255 }
            $shadeDark = [int](22 + 14 * $slope)
            $colour = if ($dark) { C $shadeDark $shadeDark ($shadeDark + 6) } else { C $lite $lite $lite }
            $pts = @((P ($x + $u0 * $w) ($y + $v0 * $h + $d0)), (P ($x + $u1 * $w) ($y + $v0 * $h + $d1)), (P ($x + $u1 * $w) ($y + $v1 * $h + $d1)), (P ($x + $u0 * $w) ($y + $v1 * $h + $d0)))
            $g.FillPolygon((New-Object System.Drawing.SolidBrush $colour), [System.Drawing.PointF[]]$pts)
        }
    }
    $edge = New-Object System.Drawing.Pen (C 10 10 16), ([Math]::Max(1, $h / 40))
    $top = @(); $bottom = @()
    for ($k = 0; $k -le $cols * 2; $k++) {
        $u = $k / ($cols * 2); $d = $amp * [Math]::Sin($phase + $u * 2 * [Math]::PI)
        $top += , (P ($x + $u * $w) ($y + $d)); $bottom += , (P ($x + $u * $w) ($y + $h + $d))
    }
    $g.DrawLines($edge, [System.Drawing.PointF[]]$top); $g.DrawLines($edge, [System.Drawing.PointF[]]$bottom)
}

function Text($g, [string]$text, [string]$font, [float]$size, [System.Drawing.FontStyle]$style, [float]$cx, [float]$cy, $fill, $glow, [float]$slant = 0) {
    $ff = New-Object System.Drawing.FontFamily $font
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $path.AddString($text, $ff, [int]$style, $size, (P $cx $cy), $sf)
    if ($slant -ne 0) {
        # (leaning forward, as racing lettering does: x moved along by how high the point is over the line's middle)
        $m = New-Object System.Drawing.Drawing2D.Matrix 1, 0, (-$slant), 1, ($slant * $cy), 0
        $path.Transform($m)
    }
    foreach ($k in 9, 6, 3) { $pen = New-Object System.Drawing.Pen $glow, $k; $pen.LineJoin = 'Round'; $g.DrawPath($pen, $path) }
    $edge = New-Object System.Drawing.Pen (C 12 10 20), 2.5
    $edge.LineJoin = 'Round'
    $g.DrawPath($edge, $path)
    $g.FillPath($fill, $path)
}

function Poly($g, $brush, [object[]]$pts) { $g.FillPolygon($brush, [System.Drawing.PointF[]]$pts) }

# ---- the menu's backdrop ----------------------------------------------------------------------------------------------------------
$bmp = New-Object System.Drawing.Bitmap 640, 480
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAlias'
$horizon = 300
# the sky at sunset: deep blue overhead, through violet, to gold at the sea
$sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 640, ($horizon + 1)), (C 12 20 48), (C 250 190 96), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$blend = New-Object System.Drawing.Drawing2D.ColorBlend 5
$blend.Colors = @((C 10 18 46), (C 30 36 86), (C 96 52 110), (C 222 112 70), (C 252 196 104))
$blend.Positions = @(0.0, 0.38, 0.62, 0.86, 1.0)
$sky.InterpolationColors = $blend
$g.FillRectangle($sky, 0, 0, 640, ($horizon + 1))
# a few early stars high up
$rnd = New-Object System.Random 25
$star = New-Object System.Drawing.SolidBrush (C 220 226 255 200)
for ($i = 0; $i -lt 40; $i++) { $sx = $rnd.Next(0, 640); $sy = $rnd.Next(0, 120); $g.FillRectangle($star, $sx, $sy, 1, 1) }
# long clouds lit from below
$cloud = New-Object System.Drawing.SolidBrush (C 236 140 110 110)
foreach ($c in @(@(30, 190, 210, 10), @(380, 172, 240, 9), @(120, 226, 180, 7), @(470, 238, 150, 6), @(250, 206, 120, 6))) { $g.FillEllipse($cloud, $c[0], $c[1], $c[2], $c[3]) }
# the sun on the sea, its glow
$sp = New-Object System.Drawing.Drawing2D.GraphicsPath
$sp.AddEllipse(300, 170, 260, 260)
$sg = New-Object System.Drawing.Drawing2D.PathGradientBrush $sp
$sg.CenterColor = C 255 226 140 190
$sg.SurroundColors = @(C 255 150 80 0)
$g.FillPath($sg, $sp)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 232 150)), 390, 262, 80, 80)
# the sea, the sun's path on it
$sea = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, $horizon, 640, 60), (C 60 54 96), (C 16 26 58), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillRectangle($sea, 0, $horizon, 640, 180)
$glint = New-Object System.Drawing.SolidBrush (C 255 214 130 200)
for ($k = 0; $k -lt 14; $k++) { $gw = 70 - $k * 4; $g.FillRectangle($glint, [float](430 - $gw / 2 + $rnd.Next(-6, 7)), [float]($horizon + 3 + $k * 4), [float]$gw, [float]1.5) }
# the islands: far hills either side, a lighthouse on the left one
$far = New-Object System.Drawing.SolidBrush (C 52 36 70)
Poly $g $far @((P 0 $horizon), (P 0 262), (P 60 250), (P 130 262), (P 190 276), (P 240 $horizon))
Poly $g $far @((P 520 $horizon), (P 560 280), (P 610 268), (P 640 272), (P 640 $horizon))
$near = New-Object System.Drawing.SolidBrush (C 22 18 38)
Poly $g $near @((P 0 480), (P 0 300), (P 50 288), (P 120 300), (P 200 318), (P 250 340), (P 236 480))
Poly $g $near @((P 640 480), (P 640 296), (P 590 290), (P 520 306), (P 450 330), (P 404 480))
$g.FillRectangle($near, 72, 232, 12, 60); Poly $g $near @((P 68 236), (P 88 236), (P 78 224))
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 255 230 150 220)), 72, 236, 12, 8)
# palm trees on the right
$trunk = New-Object System.Drawing.Pen (C 22 18 38), 4
foreach ($t in @(@(560, 296, 1), @(600, 292, -1))) {
    $g.DrawBezier($trunk, (P $t[0] $t[1]), (P ($t[0] + 4 * $t[2]) ($t[1] - 30)), (P ($t[0] - 2 * $t[2]) ($t[1] - 50)), (P ($t[0] + 6 * $t[2]) ($t[1] - 72)))
    $tx = $t[0] + 6 * $t[2]; $ty = $t[1] - 72
    foreach ($a in @(-160, -120, -60, -20, 20, 200)) {
        $r = [Math]::PI * $a / 180
        Poly $g $near @((P $tx $ty), (P ($tx + 30 * [Math]::Cos($r)) ($ty + 14 * [Math]::Sin($r) + 6)), (P ($tx + 22 * [Math]::Cos($r)) ($ty + 10 * [Math]::Sin($r) + 10)))
    }
}
# the race road: from the sea's edge in the middle down to the bottom, widening, its kerbs red and white, a dashed line down it
$road = New-Object System.Drawing.SolidBrush (C 46 44 56)
$rows = 40
function RoadAt([double]$t) {
    # (perspective: the far half small; the road bends left as it comes)
    $y = $horizon + 12 + (480 - $horizon - 12) * $t * $t
    $c = 330 - 70 * [Math]::Sin($t * 2.2)
    $w = 8 + 300 * $t * $t
    return @($y, $c, $w)
}
for ($k = 0; $k -lt $rows; $k++) {
    $a = RoadAt ($k / $rows); $b = RoadAt (($k + 1) / $rows)
    $y0 = $a[0]; $c0 = $a[1]; $w0 = $a[2]; $y1 = $b[0]; $c1 = $b[1]; $w1 = $b[2]
    Poly $g $road @((P ($c0 - $w0) $y0), (P ($c0 + $w0) $y0), (P ($c1 + $w1) $y1), (P ($c1 - $w1) $y1))
    $kerb = if (($k % 2) -eq 0) { C 214 36 36 } else { C 236 236 236 }
    $kw0 = $w0 * 0.08 + 1; $kw1 = $w1 * 0.08 + 1
    $kb = New-Object System.Drawing.SolidBrush $kerb
    Poly $g $kb @((P ($c0 - $w0 - $kw0) $y0), (P ($c0 - $w0) $y0), (P ($c1 - $w1) $y1), (P ($c1 - $w1 - $kw1) $y1))
    Poly $g $kb @((P ($c0 + $w0) $y0), (P ($c0 + $w0 + $kw0) $y0), (P ($c1 + $w1 + $kw1) $y1), (P ($c1 + $w1) $y1))
    if (($k % 3) -eq 1) {
        $lw0 = $w0 * 0.03 + 0.5; $lw1 = $w1 * 0.03 + 0.5
        Poly $g (New-Object System.Drawing.SolidBrush (C 236 224 170)) @((P ($c0 - $lw0) $y0), (P ($c0 + $lw0) $y0), (P ($c1 + $lw1) $y1), (P ($c1 - $lw1) $y1))
    }
}
# the start gantry across the road, a little way out: two posts and a chequered banner, lit by the sun from behind
$ga = RoadAt 0.42
$gy = $ga[0]; $gc = $ga[1]; $gw = $ga[2] + 14
$post = New-Object System.Drawing.SolidBrush (C 30 26 40)
$g.FillRectangle($post, [float]($gc - $gw - 4), [float]($gy - 64), [float]6, [float]66); $g.FillRectangle($post, [float]($gc + $gw - 2), [float]($gy - 64), [float]6, [float]66)
Flag $g ($gc - $gw) ($gy - 66) (2 * $gw) 14 14 2 1.5 0.3
# a kart coming down the road at the front: its wheels, its red body with a yellow stripe, its driver's helmet
$kx = 250; $ky = 452
$kart = New-Object System.Drawing.SolidBrush (C 196 30 34)
$tyre = New-Object System.Drawing.SolidBrush (C 14 12 18)
$g.FillEllipse($tyre, $kx - 58, $ky - 6, 26, 22); $g.FillEllipse($tyre, $kx + 32, $ky - 6, 26, 22)
Poly $g $kart @((P ($kx - 50) ($ky + 6)), (P ($kx - 40) ($ky - 14)), (P ($kx + 40) ($ky - 14)), (P ($kx + 50) ($ky + 6)), (P ($kx + 30) ($ky + 14)), (P ($kx - 30) ($ky + 14)))
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C 250 210 60)), $kx - 40, $ky - 6, 80, 4)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C 240 240 244)), $kx - 13, $ky - 40, 26, 26)
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C 30 40 80)), $kx - 10, $ky - 30, 20, 7)
$g.FillEllipse($tyre, $kx - 66, $ky - 22, 22, 16); $g.FillEllipse($tyre, $kx + 44, $ky - 22, 22, 16)
# the title, over the top of the screen (the menu's rows start below 95), a chequered flag either side
Flag $g 20 10 70 46 6 4 4 0.0
Flag $g 550 10 70 46 6 4 4 3.1
$gold = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 4, 640, 50), (C 255 236 120), (C 240 128 20), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
# (all of it over the first row of a seven-row menu, which starts at about 70)
Text $g 'Little Big Racing' 'Impact' 50 ([System.Drawing.FontStyle]::Regular) 320 28 $gold (C 200 30 30 110) 0.18
$white = New-Object System.Drawing.SolidBrush (C 244 246 252)
Text $g 'ADVENTURE' 'Impact' 20 ([System.Drawing.FontStyle]::Regular) 320 58 $white (C 20 40 120 140) 0.18
$g.Dispose()
$menu = Join-Path $root 'Game\menu.png'
$bmp.Save($menu, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"menu: $menu"

# ---- the icon: a chequered flag on a round badge, deep blue with a red rim ------------------------------------------------------------
$sizes = 256, 64, 48, 32, 16
$pngs = @()
foreach ($s in $sizes) {
    $b = New-Object System.Drawing.Bitmap $s, $s
    $gi = [System.Drawing.Graphics]::FromImage($b)
    $gi.SmoothingMode = 'AntiAlias'
    $gi.Clear([System.Drawing.Color]::Transparent)
    $rim = [Math]::Max(1.5, $s * 0.07)
    $gi.FillEllipse((New-Object System.Drawing.SolidBrush (C 206 30 34)), [float]0.5, [float]0.5, [float]($s - 1), [float]($s - 1))
    $inner = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $s, $s), (C 40 64 140), (C 12 20 52), ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $gi.FillEllipse($inner, [float]$rim, [float]$rim, [float]($s - 2 * $rim), [float]($s - 2 * $rim))
    # (fewer squares at the small sizes: they stay squares)
    $cols = if ($s -ge 48) { 5 } else { 4 }; $rows = if ($s -ge 48) { 4 } else { 3 }
    $fw = $s * 0.58; $fh = $s * 0.4
    $fx = $s * 0.27; $fy = $s * 0.24
    # the pole, then the flag off its top
    $pole = New-Object System.Drawing.SolidBrush (C 236 220 160)
    $pw = [Math]::Max(1, $s * 0.05)
    $gi.FillRectangle($pole, [float]($fx - $pw), [float]($fy - $s * 0.02), [float]$pw, [float]($s * 0.62))
    Flag $gi $fx $fy $fw $fh $cols $rows ([Math]::Max(0.6, $s * 0.035)) 0.4
    $gi.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    if ($s -eq 256) { $b.Save('E:\dump\TEMP\nov25\theme\icon_256.png', [System.Drawing.Imaging.ImageFormat]::Png) }
    if ($s -eq 32) { $b.Save('E:\dump\TEMP\nov25\theme\icon_32.png', [System.Drawing.Imaging.ImageFormat]::Png) }
    if ($s -eq 16) { $b.Save('E:\dump\TEMP\nov25\theme\icon_16.png', [System.Drawing.Imaging.ImageFormat]::Png) }
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
$icon = Join-Path $root 'Installer\lbra.ico'
[System.IO.File]::WriteAllBytes($icon, $ico.ToArray())
"icon: $icon"
