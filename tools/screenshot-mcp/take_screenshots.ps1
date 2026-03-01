# Screenshot-Helper für HandwerkerImperium
# Verwendet DPI-aware Koordinaten für Klicks, non-DPI-aware für Screenshots
param(
    [string]$AppTitle = "HandwerkerImperium",
    [string]$OutputDir = "F:/Meine_Apps_Ava/tools/screenshot-mcp/captures"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ScreenHelper {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int W, int H, bool repaint);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# DPI-aware für korrekte Fensterposition und Klicks
[ScreenHelper]::SetProcessDPIAware() | Out-Null

function Get-AppWindow {
    $p = Get-Process | Where-Object { $_.MainWindowTitle -like "*$AppTitle*" -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    return $p
}

function Click-At {
    param([int]$X, [int]$Y)
    [ScreenHelper]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [ScreenHelper]::mouse_event(0x0002, 0, 0, 0, 0)
    [ScreenHelper]::mouse_event(0x0004, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 100
}

function Take-Screenshot {
    param([string]$Name)
    $p = Get-AppWindow
    if (-not $p) { Write-Error "App nicht gefunden"; return }

    [ScreenHelper]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500

    $rect = New-Object ScreenHelper+RECT
    [ScreenHelper]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top

    $bitmap = New-Object System.Drawing.Bitmap($w, $h)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))

    $filepath = "$OutputDir/${Name}.png"
    $bitmap.Save($filepath)
    $graphics.Dispose()
    $bitmap.Dispose()

    Write-Output "Screenshot: $filepath (${w}x${h})"
}

function Click-Tab {
    param([int]$TabIndex)
    $p = Get-AppWindow
    if (-not $p) { return }

    $rect = New-Object ScreenHelper+RECT
    [ScreenHelper]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $winW = $rect.Right - $rect.Left
    $winH = $rect.Bottom - $rect.Top

    # Tab-Bar: 5 Tabs, 80px hoch (64dp * 1.25), am unteren Rand
    $tabWidth = [int]($winW / 5)
    $tabCenterY = $rect.Top + $winH - 40  # Mitte der Tab-Bar
    $tabCenterX = $rect.Left + ($TabIndex * $tabWidth) + [int]($tabWidth / 2)

    [ScreenHelper]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 200
    Click-At -X $tabCenterX -Y $tabCenterY
    Write-Output "Tab $TabIndex geklickt bei ($tabCenterX, $tabCenterY)"
}

# Fenster positionieren (physische Koordinaten bei DPI-aware)
$p = Get-AppWindow
if (-not $p) { Write-Error "HandwerkerImperium nicht gefunden!"; exit 1 }

# 675x1200 physisch = 540x960 logisch bei 125% DPI
[ScreenHelper]::MoveWindow($p.MainWindowHandle, 300, 0, 675, 1200, $true) | Out-Null
Start-Sleep -Milliseconds 500
Write-Output "Fenster positioniert: 675x1200 physisch"

# Screenshot-Serie
Write-Output "`n=== Screenshot-Serie ==="

# 1. Werkstätten (Tab 0 - schon aktiv)
Take-Screenshot -Name "hi_01_werkstaetten"

# 2. Imperium (Tab 1)
Click-Tab -TabIndex 1
Start-Sleep -Milliseconds 1000
Take-Screenshot -Name "hi_02_imperium"

# 3. Missionen (Tab 2)
Click-Tab -TabIndex 2
Start-Sleep -Milliseconds 1000
Take-Screenshot -Name "hi_03_missionen"

# 4. Gilde (Tab 3)
Click-Tab -TabIndex 3
Start-Sleep -Milliseconds 1000
Take-Screenshot -Name "hi_04_gilde"

# 5. Shop (Tab 4)
Click-Tab -TabIndex 4
Start-Sleep -Milliseconds 1000
Take-Screenshot -Name "hi_05_shop"

# Zurück zu Werkstätten für weitere Detail-Screenshots
Click-Tab -TabIndex 0
Start-Sleep -Milliseconds 500

Write-Output "`n=== Fertig! ==="
