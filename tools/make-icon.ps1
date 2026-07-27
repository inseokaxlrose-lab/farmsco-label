# Generates FarmscoLabel/Assets/app.ico — a shipping-label themed icon.
# Pure System.Drawing, no external assets. ASCII-only (safe without BOM).
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function Get-RoundedRect([single]$x,[single]$y,[single]$w,[single]$h,[single]$r){
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $p.AddArc($x,          $y,          $d, $d, 180, 90)
  $p.AddArc($x+$w-$d,    $y,          $d, $d, 270, 90)
  $p.AddArc($x+$w-$d,    $y+$h-$d,    $d, $d,   0, 90)
  $p.AddArc($x,          $y+$h-$d,    $d, $d,  90, 90)
  $p.CloseFigure()
  return $p
}

function New-LabelPng([int]$size){
  $bmp = New-Object System.Drawing.Bitmap($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)
  $s = $size / 256.0

  $green = [System.Drawing.Color]::FromArgb(255, 27,158, 75)
  $white = [System.Drawing.Color]::White
  $gray  = [System.Drawing.Color]::FromArgb(255,200,205,210)
  $bar   = [System.Drawing.Color]::FromArgb(255, 34, 34, 34)

  $bWhite = New-Object System.Drawing.SolidBrush($white)
  $bGreen = New-Object System.Drawing.SolidBrush($green)
  $bGray  = New-Object System.Drawing.SolidBrush($gray)
  $bBar   = New-Object System.Drawing.SolidBrush($bar)
  $pGreen = New-Object System.Drawing.Pen($green, [single](6*$s))
  $pGreen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

  # card
  $card = Get-RoundedRect ([single](48*$s)) ([single](36*$s)) ([single](160*$s)) ([single](184*$s)) ([single](18*$s))
  $g.FillPath($bWhite, $card)

  # green header, clipped to the rounded card so top corners stay round
  $g.SetClip($card)
  $g.FillRectangle($bGreen, [single](48*$s),[single](36*$s),[single](160*$s),[single](46*$s))
  $g.ResetClip()

  # border
  $g.DrawPath($pGreen, $card)

  # title bar (white) on the green header
  $t = Get-RoundedRect ([single](70*$s)) ([single](52*$s)) ([single](92*$s)) ([single](14*$s)) ([single](7*$s))
  $g.FillPath($bWhite, $t)

  # body text lines (gray)
  foreach($ln in @(@(104,120),@(126,120),@(148,86))){
    $lp = Get-RoundedRect ([single](70*$s)) ([single]($ln[0]*$s)) ([single]($ln[1]*$s)) ([single](11*$s)) ([single](5*$s))
    $g.FillPath($bGray, $lp)
  }

  # barcode bars
  $widths = @(4,2,5,3,2,4,2,6,3,2,4,2,3,5,2,4)
  $x = 70.0
  foreach($wd in $widths){
    if($x -gt 186){ break }
    $g.FillRectangle($bBar, [single]($x*$s),[single](172*$s),[single]($wd*$s),[single](40*$s))
    $x += $wd + 4
  }

  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  # unary comma keeps PowerShell from unrolling the byte[] on return
  return ,$ms.ToArray()
}

$sizes = @(256,64,48,32,16)
$pngs  = @{}
foreach($sz in $sizes){ $pngs[$sz] = New-LabelPng $sz }

$out = Join-Path $PSScriptRoot '..\FarmscoLabel\Assets\app.ico'
$dir = Split-Path $out
if(-not (Test-Path $dir)){ New-Item -ItemType Directory -Force $dir | Out-Null }

$fs = New-Object System.IO.FileStream($out,[System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
foreach($sz in $sizes){
  $data = $pngs[$sz]
  $wb = if($sz -ge 256){0}else{$sz}
  $bw.Write([Byte]$wb); $bw.Write([Byte]$wb); $bw.Write([Byte]0); $bw.Write([Byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
  $offset += $data.Length
}
foreach($sz in $sizes){ $bw.Write([byte[]]$pngs[$sz]) }
$bw.Flush(); $fs.Close()
Write-Output "ICO written: $out ($((Get-Item $out).Length) bytes)"
