param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\Scratchdeck\Assets\Scratchdeck.ico')
)

Add-Type -AssemblyName System.Drawing.Common
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class NativeIconHandle {
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($directory) | Out-Null

$bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$framePoints = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(30, 9),
    [System.Drawing.PointF]::new(226, 9),
    [System.Drawing.PointF]::new(247, 30),
    [System.Drawing.PointF]::new(247, 226),
    [System.Drawing.PointF]::new(226, 247),
    [System.Drawing.PointF]::new(30, 247),
    [System.Drawing.PointF]::new(9, 226),
    [System.Drawing.PointF]::new(9, 30)
)
$frameFill = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#080C14'))
$framePen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#16DCE8'), 9)
$framePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
$graphics.FillPolygon($frameFill, $framePoints)
$graphics.DrawPolygon($framePen, $framePoints)

$boltPoints = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(150, 28),
    [System.Drawing.PointF]::new(67, 142),
    [System.Drawing.PointF]::new(117, 142),
    [System.Drawing.PointF]::new(91, 228),
    [System.Drawing.PointF]::new(190, 112),
    [System.Drawing.PointF]::new(137, 112)
)
$boltFill = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#E54BFF'))
$boltPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#6DF6FF'), 4)
$boltPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Miter
$graphics.FillPolygon($boltFill, $boltPoints)
$graphics.DrawPolygon($boltPen, $boltPoints)

$iconHandle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($iconHandle)
    $stream = [System.IO.File]::Create($OutputPath)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
        $icon.Dispose()
    }
}
finally {
    [NativeIconHandle]::DestroyIcon($iconHandle) | Out-Null
    $boltPen.Dispose()
    $boltFill.Dispose()
    $framePen.Dispose()
    $frameFill.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
