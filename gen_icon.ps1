Add-Type -AssemblyName System.Drawing

function New-FanBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $s = $size / 32.0
  $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 102, 255))
  $g.FillRectangle($bgBrush, 0, 0, $size, $size)
  $white = [System.Drawing.Brushes]::White
  $g.FillEllipse($white, [int](13*$s), [int](13*$s), [int](6*$s), [int](6*$s))
  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, (3*$s))
  $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
  $g.DrawArc($pen, [int](6*$s), [int](2*$s), [int](10*$s), [int](10*$s), 90, 200)
  $g.DrawArc($pen, [int](16*$s), [int](2*$s), [int](10*$s), [int](10*$s), 0, 200)
  $g.DrawArc($pen, [int](6*$s), [int](20*$s), [int](10*$s), [int](10*$s), 180, 200)
  $g.DrawArc($pen, [int](16*$s), [int](20*$s), [int](10*$s), [int](10*$s), 270, 200)
  $g.Dispose()
  return $bmp
}

$sizes = @(16, 32, 48, 256)
$mem = New-Object System.IO.MemoryStream
# ICO header: reserved=0, type=1, count
$bw = New-Object System.IO.BinaryWriter($mem)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
$imgData = @()
foreach ($sz in $sizes) {
  $bmp = New-FanBitmap $sz
  $ms2 = New-Object System.IO.MemoryStream
  $bmp.Save($ms2, [System.Drawing.Imaging.ImageFormat]::Png)
  $png = $ms2.ToArray()
  $ms2.Close(); $bmp.Dispose()
  $bw.Write([byte]($sz -band 0xFF))   # width (256 -> 0)
  $bw.Write([byte]($sz -band 0xFF))   # height
  $bw.Write([byte]0)                  # palette
  $bw.Write([byte]0)                  # reserved
  $bw.Write([uint16]1)                # color planes
  $bw.Write([uint16]32)               # bpp
  $bw.Write([uint32]$png.Length)      # size
  $bw.Write([uint32]$offset)          # offset
  $offset += $png.Length
  $imgData += ,$png
}
foreach ($png in $imgData) { $bw.Write($png) }
$bw.Flush()
[System.IO.File]::WriteAllBytes('D:\software\fanctl\app\fan.ico', $mem.ToArray())
$bw.Close(); $mem.Close()
Write-Host 'ICON_OK'
