Add-Type -AssemblyName System.Drawing

$repoRoot = "C:\Projetos\SST-APP"
$src = Join-Path $repoRoot "logo_AHBT_natural-removebg-preview.png"
$outDir = Join-Path $repoRoot "src\AAHBRANT.SST.TeamsApp\manifest"

$vinho = [System.Drawing.Color]::FromArgb(255, 0x67, 0x00, 0x00)

# ---- color.png: 192x192, fundo vinho solido, logo centralizada ----
$srcImg = [System.Drawing.Image]::FromFile($src)

$canvasSize = 192
$padding = 24
$maxW = $canvasSize - (2 * $padding)
$maxH = $canvasSize - (2 * $padding)
$scale = [Math]::Min($maxW / $srcImg.Width, $maxH / $srcImg.Height)
$newW = [int]([Math]::Round($srcImg.Width * $scale))
$newH = [int]([Math]::Round($srcImg.Height * $scale))
$offsetX = [int](($canvasSize - $newW) / 2)
$offsetY = [int](($canvasSize - $newH) / 2)

$colorBmp = New-Object System.Drawing.Bitmap($canvasSize, $canvasSize)
$g = [System.Drawing.Graphics]::FromImage($colorBmp)
$g.Clear($vinho)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($srcImg, $offsetX, $offsetY, $newW, $newH)
$g.Dispose()
$colorPath = Join-Path $outDir "color.png"
$colorBmp.Save($colorPath, [System.Drawing.Imaging.ImageFormat]::Png)
$colorBmp.Dispose()
Write-Output "color.png gerado: $colorPath"

$srcImg.Dispose()

# ---- outline.png: 32x32, transparente, monocromatico simples (iniciais "A") ----
# O wordmark completo fica ilegivel reduzido a 32px; o Teams exige um icone monocromatico
# simples para a barra lateral, entao desenhamos as iniciais em vez de reduzir a logo.
$outlineCanvas = 32
$outlineBmp = New-Object System.Drawing.Bitmap($outlineCanvas, $outlineCanvas)
$g3 = [System.Drawing.Graphics]::FromImage($outlineBmp)
$g3.Clear([System.Drawing.Color]::Transparent)
$g3.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g3.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

$font = New-Object System.Drawing.Font("Arial", 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$text = "A"
$textSize = $g3.MeasureString($text, $font)
$tx = ($outlineCanvas - $textSize.Width) / 2
$ty = ($outlineCanvas - $textSize.Height) / 2
$g3.DrawString($text, $font, $brush, $tx, $ty)
$brush.Dispose()
$font.Dispose()
$g3.Dispose()

$outlinePath = Join-Path $outDir "outline.png"
$outlineBmp.Save($outlinePath, [System.Drawing.Imaging.ImageFormat]::Png)
$outlineBmp.Dispose()
Write-Output "outline.png gerado: $outlinePath"
