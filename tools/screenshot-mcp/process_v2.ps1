# Screenshot-Bearbeitung fÃ¼r Google Play Store
# Schneidet Fensterrahmen, fÃ¼gt Promo-Text hinzu, speichert als Play-Store-Format
param(
    [string]$InputDir = "F:/Meine_Apps_Ava/tools/screenshot-mcp/captures",
    [string]$OutputDir = "F:/Meine_Apps_Ava/tools/screenshot-mcp/captures/processed"
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) { New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null }

function Process-Screenshot {
    param(
        [string]$InputFile,
        [string]$OutputName,
        [string]$PromoTitle,
        [string]$PromoSubtitle,
        [bool]$IsLandscape = $false,
        [int]$CropTop = 37,      # Titelleiste hÃ¶he (physisch bei 125% DPI)
        [int]$CropLeft = 8,      # Linker Fensterrand
        [int]$CropRight = 8,     # Rechter Rand
        [int]$CropBottom = 8,    # Unterer Rand
        [System.Drawing.Color]$AccentColor
    )

    $inputPath = "$InputDir/$InputFile"
    if (-not (Test-Path $inputPath)) { Write-Output "SKIP: $InputFile nicht gefunden"; return }

    $src = [System.Drawing.Image]::FromFile($inputPath)
    $srcW = $src.Width; $srcH = $src.Height

    # Croppen: Fensterrahmen entfernen
    $cropX = $CropLeft
    $cropY = $CropTop
    $cropW = $srcW - $CropLeft - $CropRight
    $cropH = $srcH - $CropTop - $CropBottom

    $cropped = New-Object System.Drawing.Bitmap($cropW, $cropH)
    $g = [System.Drawing.Graphics]::FromImage($cropped)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, (New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $src.Dispose()

    # Play Store Dimensionen: 1080x1920 (Portrait) oder 1920x1080 (Landscape)
    if ($IsLandscape) {
        $targetW = 1920; $targetH = 1080
    } else {
        $targetW = 1080; $targetH = 1920
    }

    # Finale Ausgabe mit Promo-Text-Banner oben
    $bannerHeight = [int]($targetH * 0.15)  # 15% fÃ¼r Text-Banner
    $screenshotHeight = $targetH - $bannerHeight
    $final = New-Object System.Drawing.Bitmap($targetW, $targetH)
    $fg = [System.Drawing.Graphics]::FromImage($final)
    $fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $fg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $fg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    # Hintergrund: Dunkler Gradient
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point(0, $targetH)),
        [System.Drawing.Color]::FromArgb(255, 30, 30, 40),
        [System.Drawing.Color]::FromArgb(255, 15, 15, 25)
    )
    $fg.FillRectangle($bgBrush, 0, 0, $targetW, $targetH)
    $bgBrush.Dispose()

    # Screenshot skaliert einfÃ¼gen (unten, mit Rand)
    $margin = [int]($targetW * 0.04)
    $availW = $targetW - 2 * $margin
    $availH = $screenshotHeight - $margin

    # Skalierungsfaktor berechnen
    $scaleX = $availW / $cropW
    $scaleY = $availH / $cropH
    $scale = [Math]::Min($scaleX, $scaleY)
    $drawW = [int]($cropW * $scale)
    $drawH = [int]($cropH * $scale)
    $drawX = [int](($targetW - $drawW) / 2)
    $drawY = $bannerHeight + [int](($screenshotHeight - $drawH) / 2)

    # Schatten hinter Screenshot
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 0, 0, 0))
    $fg.FillRectangle($shadowBrush, $drawX + 6, $drawY + 6, $drawW, $drawH)
    $shadowBrush.Dispose()

    # Abgerundeter Rahmen um Screenshot
    $borderPen = New-Object System.Drawing.Pen($AccentColor, 3)
    $fg.DrawRectangle($borderPen, $drawX - 2, $drawY - 2, $drawW + 3, $drawH + 3)
    $borderPen.Dispose()

    # Screenshot zeichnen
    $fg.DrawImage($cropped, $drawX, $drawY, $drawW, $drawH)
    $cropped.Dispose()

    # Promo-Text: Titel
    $titleFont = New-Object System.Drawing.Font("Segoe UI", 36, [System.Drawing.FontStyle]::Bold)
    $titleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $titleFormat = New-Object System.Drawing.StringFormat
    $titleFormat.Alignment = [System.Drawing.StringAlignment]::Center

    $titleRect = New-Object System.Drawing.RectangleF(0, [int]($bannerHeight * 0.1), $targetW, [int]($bannerHeight * 0.55))
    $fg.DrawString($PromoTitle, $titleFont, $titleBrush, $titleRect, $titleFormat)

    # Untertitel
    $subFont = New-Object System.Drawing.Font("Segoe UI", 20, [System.Drawing.FontStyle]::Regular)
    $subBrush = New-Object System.Drawing.SolidBrush($AccentColor)
    $subRect = New-Object System.Drawing.RectangleF(0, [int]($bannerHeight * 0.55), $targetW, [int]($bannerHeight * 0.4))
    $fg.DrawString($PromoSubtitle, $subFont, $subBrush, $subRect, $titleFormat)

    # Akzent-Linie unter Text
    $linePen = New-Object System.Drawing.Pen($AccentColor, 3)
    $lineY = $bannerHeight - 5
    $fg.DrawLine($linePen, [int]($targetW * 0.2), $lineY, [int]($targetW * 0.8), $lineY)
    $linePen.Dispose()

    # Speichern als PNG
    $outputPath = "$OutputDir/${OutputName}.png"
    $final.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $fg.Dispose()
    $final.Dispose()
    $titleFont.Dispose(); $titleBrush.Dispose(); $subFont.Dispose(); $subBrush.Dispose()
    $titleFormat.Dispose()

    Write-Output "Fertig: $outputPath"
}

$orange = [System.Drawing.Color]::FromArgb(255, 255, 165, 0)
$cyan = [System.Drawing.Color]::FromArgb(255, 0, 200, 255)

Write-Output "=== HandwerkerImperium (Portrait) ==="

Process-Screenshot -InputFile "hi_01_werkstaetten.png" -OutputName "hi_promo_01_werkstaetten" `
    -PromoTitle "Baue dein Imperium!" `
    -PromoSubtitle "10 WerkstÃ¤tten upgraden & Mitarbeiter einstellen" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_02_imperium.png" -OutputName "hi_promo_02_imperium" `
    -PromoTitle "Prestige & GebÃ¤ude" `
    -PromoSubtitle "7 GebÃ¤ude + Prestige-System fÃ¼r permanente Boni" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_03_missionen.png" -OutputName "hi_promo_03_missionen" `
    -PromoTitle "TÃ¤gliche Herausforderungen" `
    -PromoSubtitle "Missionen, Turniere, Battle Pass & mehr" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_04_gilde.png" -OutputName "hi_promo_04_gilde" `
    -PromoTitle "Spiele mit Freunden!" `
    -PromoSubtitle "Gilden, Wochenziele & gemeinsame Forschung" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_05_shop.png" -OutputName "hi_promo_05_shop" `
    -PromoTitle "Premium & Booster" `
    -PromoSubtitle "Beschleunige deinen Fortschritt" `
    -AccentColor $orange

Write-Output "`n=== BomberBlast (Landscape) ==="

Process-Screenshot -InputFile "bb_01_menu_clean.png" -OutputName "bb_promo_01_menu" `
    -PromoTitle "BomberBlast" `
    -PromoSubtitle "Der Bomberman-Klon fÃ¼r Android!" `
    -IsLandscape $true -AccentColor $cyan

Process-Screenshot -InputFile "bb_02_levelselect.png" -OutputName "bb_promo_02_levelselect" `
    -PromoTitle "100 Level in 10 Welten" `
    -PromoSubtitle "Story-Modus, Survival & Dungeon" `
    -IsLandscape $true -AccentColor $cyan

Process-Screenshot -InputFile "bb_05_ingame.png" -OutputName "bb_promo_03_gameplay" `
    -PromoTitle "Klassisches Bomberman!" `
    -PromoSubtitle "12 Gegnertypen, 5 Bosse, PowerUps & mehr" `
    -IsLandscape $true -AccentColor $cyan

Process-Screenshot -InputFile "bb_08_shop.png" -OutputName "bb_promo_04_shop" `
    -PromoTitle "Upgrades & Items" `
    -PromoSubtitle "9 permanente Upgrades + tÃ¤gliches GlÃ¼cksrad" `
    -IsLandscape $true -AccentColor $cyan

Process-Screenshot -InputFile "bb_09_gluecksrad.png" -OutputName "bb_promo_05_gluecksrad" `
    -PromoTitle "TÃ¤gliches GlÃ¼cksrad" `
    -PromoSubtitle "Jeden Tag kostenlos drehen & MÃ¼nzen gewinnen" `
    -IsLandscape $true -AccentColor $cyan

Write-Output "`n=== Alle Promo-Bilder erstellt! ==="
