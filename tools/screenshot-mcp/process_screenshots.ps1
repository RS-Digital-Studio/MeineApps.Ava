# Screenshot-Bearbeitung für Google Play Store
# Screenshot füllt das gesamte Bild (edge-to-edge), Text als Gradient-Overlay
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
        [int]$CropTop = 37,      # Titelleiste (physisch bei 125% DPI)
        [int]$CropLeft = 8,      # Linker Fensterrand
        [int]$CropRight = 8,     # Rechter Rand
        [int]$CropBottom = 8,    # Unterer Rand
        [System.Drawing.Color]$AccentColor
    )

    $inputPath = "$InputDir/$InputFile"
    if (-not (Test-Path $inputPath)) { Write-Output "SKIP: $InputFile nicht gefunden"; return }

    $src = [System.Drawing.Bitmap]::new($inputPath)
    # DPI normalisieren! Screenshots bei 125% DPI haben 120 DPI,
    # Ziel-Bitmap hat 96 DPI → System.Drawing skaliert automatisch falsch
    $src.SetResolution(96, 96)
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

    # Play Store Dimensionen
    if ($IsLandscape) {
        $targetW = 1920; $targetH = 1080
    } else {
        $targetW = 1080; $targetH = 1920
    }

    $final = New-Object System.Drawing.Bitmap($targetW, $targetH)
    $fg = [System.Drawing.Graphics]::FromImage($final)
    $fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $fg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $fg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    # Dunklen Hintergrund füllen (Fallback für Rundungslücken)
    $fg.Clear([System.Drawing.Color]::FromArgb(255, 20, 20, 30))

    # Screenshot FÜLLT das gesamte Bild (Math.Max = Fill, nicht Fit)
    # Ceiling statt Truncation → garantiert keine Lücken
    $scaleX = $targetW / $cropW
    $scaleY = $targetH / $cropH
    $scale = [Math]::Max($scaleX, $scaleY)
    $drawW = [int][Math]::Ceiling($cropW * $scale)
    $drawH = [int][Math]::Ceiling($cropH * $scale)
    # +2px Sicherheitsmarge gegen Rundungsfehler
    $drawW = [Math]::Max($drawW, $targetW + 2)
    $drawH = [Math]::Max($drawH, $targetH + 2)
    # Zentriert (Überschuss wird abgeschnitten)
    $drawX = [int][Math]::Floor(($targetW - $drawW) / 2.0)
    $drawY = [int][Math]::Floor(($targetH - $drawH) / 2.0)

    # Debug: Werte prüfen
    Write-Output "  Crop: ${cropW}x${cropH} -> Target: ${targetW}x${targetH} -> Draw: ${drawW}x${drawH} at (${drawX},${drawY})"

    # Screenshot zeichnen (skaliert auf gesamten Canvas)
    # Explizit Source+Dest Rectangles für garantierte Skalierung
    $srcRect = New-Object System.Drawing.Rectangle(0, 0, $cropped.Width, $cropped.Height)
    $destRect = New-Object System.Drawing.Rectangle($drawX, $drawY, $drawW, $drawH)
    $fg.DrawImage($cropped, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $cropped.Dispose()

    # Gradient-Overlay oben für Text (dunkel → transparent)
    $gradientH = [int]($targetH * 0.25)
    $gradBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point(0, $gradientH)),
        [System.Drawing.Color]::FromArgb(200, 0, 0, 0),
        [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    )
    $fg.FillRectangle($gradBrush, 0, 0, $targetW, $gradientH)
    $gradBrush.Dispose()

    # Promo-Text: Titel (auf dem Gradient)
    $titleFont = New-Object System.Drawing.Font("Segoe UI", 36, [System.Drawing.FontStyle]::Bold)
    $titleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $titleFormat = New-Object System.Drawing.StringFormat
    $titleFormat.Alignment = [System.Drawing.StringAlignment]::Center

    $titleY = [int]($targetH * 0.02)
    $titleRect = New-Object System.Drawing.RectangleF(0, $titleY, $targetW, 60)
    $fg.DrawString($PromoTitle, $titleFont, $titleBrush, $titleRect, $titleFormat)

    # Untertitel
    $subFont = New-Object System.Drawing.Font("Segoe UI", 20, [System.Drawing.FontStyle]::Regular)
    $subBrush = New-Object System.Drawing.SolidBrush($AccentColor)
    $subY = $titleY + 55
    $subRect = New-Object System.Drawing.RectangleF(0, $subY, $targetW, 40)
    $fg.DrawString($PromoSubtitle, $subFont, $subBrush, $subRect, $titleFormat)

    # Speichern als PNG
    $outputPath = "$OutputDir/${OutputName}.png"
    $final.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $fg.Dispose()
    $final.Dispose()
    $titleFont.Dispose(); $titleBrush.Dispose(); $subFont.Dispose(); $subBrush.Dispose()
    $titleFormat.Dispose()

    Write-Output "Fertig: $outputPath"
}

# Unicode-sichere Umlaute
$ae = [char]0x00E4; $oe = [char]0x00F6; $ue = [char]0x00FC

$orange = [System.Drawing.Color]::FromArgb(255, 255, 165, 0)
$cyan = [System.Drawing.Color]::FromArgb(255, 0, 200, 255)

Write-Output "=== HandwerkerImperium (Portrait) ==="

Process-Screenshot -InputFile "hi_01_werkstaetten.png" -OutputName "hi_promo_01_werkstaetten" `
    -PromoTitle "Baue dein Imperium!" `
    -PromoSubtitle "10 Werkst${ae}tten upgraden & Mitarbeiter einstellen" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_02_imperium.png" -OutputName "hi_promo_02_imperium" `
    -PromoTitle "Prestige & Geb${ae}ude" `
    -PromoSubtitle "7 Geb${ae}ude + Prestige-System f${ue}r permanente Boni" `
    -AccentColor $orange

Process-Screenshot -InputFile "hi_03_missionen.png" -OutputName "hi_promo_03_missionen" `
    -PromoTitle "T${ae}gliche Herausforderungen" `
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
    -PromoSubtitle "Der Bomberman-Klon f${ue}r Android!" `
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
    -PromoSubtitle "9 permanente Upgrades + t${ae}gliches Gl${ue}cksrad" `
    -IsLandscape $true -AccentColor $cyan

Process-Screenshot -InputFile "bb_09_gluecksrad.png" -OutputName "bb_promo_05_gluecksrad" `
    -PromoTitle "T${ae}gliches Gl${ue}cksrad" `
    -PromoSubtitle "Jeden Tag kostenlos drehen & M${ue}nzen gewinnen" `
    -IsLandscape $true -AccentColor $cyan

Write-Output "`n=== Alle Promo-Bilder erstellt! ==="
