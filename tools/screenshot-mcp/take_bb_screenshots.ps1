# Screenshot-Helper für BomberBlast (Landscape)
param(
    [string]$OutputDir = "F:/Meine_Apps_Ava/tools/screenshot-mcp/captures"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class BBHelper {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int W, int H, bool repaint);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[BBHelper]::SetProcessDPIAware() | Out-Null

function Get-BBWindow {
    $p = Get-Process | Where-Object { $_.MainWindowTitle -like "*BomberBlast*" -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    return $p
}

function Take-Shot {
    param([string]$Name)
    $p = Get-BBWindow
    if (-not $p) { Write-Error "BomberBlast nicht gefunden"; return }
    [BBHelper]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $rect = New-Object BBHelper+RECT
    [BBHelper]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap($w, $h)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $filepath = "$OutputDir/${Name}.png"
    $bitmap.Save($filepath)
    $graphics.Dispose(); $bitmap.Dispose()
    Write-Output "Screenshot: $filepath (${w}x${h})"
}

function Click-Button {
    param([string]$ButtonName)
    $p = Get-BBWindow
    if (-not $p) { return $false }
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
    $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    if (-not $window) { Write-Output "UIAutomation: Fenster nicht gefunden"; return $false }

    $nameCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $ButtonName)
    $btn = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    if ($btn) {
        try {
            $invokePattern = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            $invokePattern.Invoke()
            Write-Output "Button '$ButtonName' geklickt"
            return $true
        } catch {
            Write-Output "InvokePattern fehlgeschlagen für '$ButtonName': $_"
            return $false
        }
    } else {
        Write-Output "Button '$ButtonName' nicht gefunden"
        return $false
    }
}

function List-Buttons {
    $p = Get-BBWindow
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
    $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
    $btnCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCondition)
    foreach ($btn in $buttons) {
        $name = $btn.Current.Name
        if ($name -and $name -notlike "Avalonia.*" -and $name -notlike "Material.*") {
            Write-Output "  [$name]"
        }
    }
}

# Fenster auf Landscape setzen: 1200x675 physisch (960x540 logisch bei 125%)
$p = Get-BBWindow
if (-not $p) { Write-Error "BomberBlast nicht gefunden!"; exit 1 }
[BBHelper]::MoveWindow($p.MainWindowHandle, 100, 50, 1200, 750, $true) | Out-Null
Start-Sleep -Milliseconds 1000
Write-Output "Fenster auf 1200x750 Landscape gesetzt"

# Buttons auflisten
Write-Output "`n=== Verfügbare Buttons ==="
List-Buttons

# 1. Hauptmenü
Write-Output "`n=== Screenshots ==="
Take-Shot -Name "bb_01_menu"

# 2. Level Select navigieren
Start-Sleep -Milliseconds 500
Click-Button -ButtonName "Spielen"
if (-not $?) { Click-Button -ButtonName "Play" }
Start-Sleep -Milliseconds 1000
Take-Shot -Name "bb_02_levelselect"

# Zurück zum Menü
Click-Button -ButtonName "Avalonia.Controls.PathIcon"  # Zurück-Button
Start-Sleep -Milliseconds 500

# 3. Shop
Click-Button -ButtonName "Shop"
Start-Sleep -Milliseconds 1000
Take-Shot -Name "bb_03_shop"

# Zurück
Click-Button -ButtonName "Avalonia.Controls.PathIcon"
Start-Sleep -Milliseconds 500

# 4. Liga
Click-Button -ButtonName "Liga"
if (-not $?) { Click-Button -ButtonName "League" }
Start-Sleep -Milliseconds 1000
Take-Shot -Name "bb_04_liga"

# Zurück
Click-Button -ButtonName "Avalonia.Controls.PathIcon"
Start-Sleep -Milliseconds 500

# 5. Battle Pass
Click-Button -ButtonName "Battle Pass"
Start-Sleep -Milliseconds 1000
Take-Shot -Name "bb_05_battlepass"

Write-Output "`n=== Fertig! ==="
