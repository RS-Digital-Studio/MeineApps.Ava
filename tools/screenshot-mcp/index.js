#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { execSync, spawn } from "child_process";
import { readFileSync, existsSync, mkdirSync } from "fs";
import { join } from "path";

const SCREENSHOT_DIR = "F:/Meine_Apps_Ava/tools/screenshot-mcp/captures";

// Sicherstellen dass der Capture-Ordner existiert
if (!existsSync(SCREENSHOT_DIR)) {
  mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

const server = new McpServer({
  name: "screenshot-mcp",
  version: "1.0.0",
});

// Tool: Screenshot aufnehmen (ganzer Bildschirm oder bestimmtes Fenster)
server.tool(
  "take_screenshot",
  "Nimmt einen Screenshot auf und speichert ihn als PNG. Kann den ganzen Bildschirm oder ein bestimmtes Fenster erfassen.",
  {
    filename: z.string().optional().describe("Dateiname (ohne Pfad/Endung). Default: screenshot_[timestamp]"),
    windowTitle: z.string().optional().describe("Fenstertitel (Teilmatch). Wenn leer: ganzer Bildschirm"),
  },
  async ({ filename, windowTitle }) => {
    const name = filename || `screenshot_${Date.now()}`;
    const filepath = join(SCREENSHOT_DIR, `${name}.png`);

    let psScript;
    if (windowTitle) {
      // Fenster-spezifischer Screenshot
      psScript = `
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WinAPI {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$procs = Get-Process | Where-Object { $_.MainWindowTitle -like '*${windowTitle}*' -and $_.MainWindowHandle -ne 0 }
if ($procs.Count -eq 0) { Write-Error "Fenster '${windowTitle}' nicht gefunden"; exit 1 }
$hwnd = $procs[0].MainWindowHandle
[WinAPI]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 300
[WinAPI]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500
$rect = New-Object WinAPI+RECT
[WinAPI]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap($w, $h)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bitmap.Save('${filepath.replace(/\\/g, "/")}')
$graphics.Dispose()
$bitmap.Dispose()
Write-Output "OK: ${filepath.replace(/\\/g, "/")}"
`;
    } else {
      // Ganzer Bildschirm
      psScript = `
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$screen = [System.Windows.Forms.Screen]::PrimaryScreen
$bitmap = New-Object System.Drawing.Bitmap($screen.Bounds.Width, $screen.Bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($screen.Bounds.Location, [System.Drawing.Point]::Empty, $screen.Bounds.Size)
$bitmap.Save('${filepath.replace(/\\/g, "/")}')
$graphics.Dispose()
$bitmap.Dispose()
Write-Output "OK: ${filepath.replace(/\\/g, "/")}"
`;
    }

    try {
      const result = execSync(`powershell -NoProfile -Command "${psScript.replace(/"/g, '\\"').replace(/\n/g, ' ')}"`, {
        encoding: "utf-8",
        timeout: 15000,
      });

      // Screenshot als Base64 zurückgeben damit Claude es sehen kann
      if (existsSync(filepath)) {
        const imageBuffer = readFileSync(filepath);
        const base64 = imageBuffer.toString("base64");
        return {
          content: [
            { type: "text", text: `Screenshot gespeichert: ${filepath}` },
            { type: "image", data: base64, mimeType: "image/png" },
          ],
        };
      }
      return { content: [{ type: "text", text: `Fehler: Datei nicht erstellt. Output: ${result}` }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Screenshot-Fehler: ${err.message}` }] };
    }
  }
);

// Tool: Fenster auflisten
server.tool(
  "list_windows",
  "Listet alle sichtbaren Fenster mit Titel und Prozessname auf.",
  {},
  async () => {
    try {
      const result = execSync(
        `powershell -NoProfile -Command "Get-Process | Where-Object { $_.MainWindowTitle -ne '' } | Select-Object ProcessName, MainWindowTitle, Id | Format-Table -AutoSize | Out-String -Width 200"`,
        { encoding: "utf-8", timeout: 10000 }
      );
      return { content: [{ type: "text", text: result.trim() }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Fehler: ${err.message}` }] };
    }
  }
);

// Tool: App starten
server.tool(
  "start_app",
  "Startet eine Desktop-App (dotnet run) und wartet bis das Fenster erscheint.",
  {
    appName: z.string().describe("App-Name (z.B. BomberBlast, HandwerkerImperium)"),
    waitSeconds: z.number().optional().describe("Wartezeit in Sekunden bis Screenshot. Default: 5"),
  },
  async ({ appName, waitSeconds }) => {
    const wait = waitSeconds || 5;
    const projectPath = `F:/Meine_Apps_Ava/src/Apps/${appName}/${appName}.Desktop`;

    if (!existsSync(projectPath)) {
      return { content: [{ type: "text", text: `Projekt nicht gefunden: ${projectPath}` }] };
    }

    try {
      // App im Hintergrund starten
      const child = spawn("dotnet", ["run", "--project", projectPath], {
        detached: true,
        stdio: "ignore",
        cwd: "F:/Meine_Apps_Ava",
      });
      child.unref();

      // Warten bis Fenster erscheint
      await new Promise(resolve => setTimeout(resolve, wait * 1000));

      return {
        content: [{ type: "text", text: `${appName} gestartet (PID: ${child.pid}). Warte ${wait}s für Fenster-Initialisierung abgeschlossen. Nutze take_screenshot um einen Screenshot zu machen.` }],
      };
    } catch (err) {
      return { content: [{ type: "text", text: `Start-Fehler: ${err.message}` }] };
    }
  }
);

// Tool: Fenster schließen
server.tool(
  "close_window",
  "Schließt ein Fenster anhand des Titels.",
  {
    windowTitle: z.string().describe("Fenstertitel (Teilmatch)"),
  },
  async ({ windowTitle }) => {
    try {
      const result = execSync(
        `powershell -NoProfile -Command "Get-Process | Where-Object { $_.MainWindowTitle -like '*${windowTitle}*' } | Stop-Process -Force"`,
        { encoding: "utf-8", timeout: 10000 }
      );
      return { content: [{ type: "text", text: `Fenster '${windowTitle}' geschlossen.` }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Fehler: ${err.message}` }] };
    }
  }
);

// Tool: Fenstergröße ändern (wichtig für konsistente Screenshots)
server.tool(
  "resize_window",
  "Ändert Position und Größe eines Fensters. Nützlich für konsistente Screenshot-Auflösungen.",
  {
    windowTitle: z.string().describe("Fenstertitel (Teilmatch)"),
    width: z.number().describe("Breite in Pixeln"),
    height: z.number().describe("Höhe in Pixeln"),
    x: z.number().optional().describe("X-Position. Default: 0"),
    y: z.number().optional().describe("Y-Position. Default: 0"),
  },
  async ({ windowTitle, width, height, x, y }) => {
    const posX = x || 0;
    const posY = y || 0;
    try {
      execSync(
        `powershell -NoProfile -Command "
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class WinAPI2 {
    [DllImport(\\"user32.dll\\")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int W, int H, bool repaint);
    [DllImport(\\"user32.dll\\")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@
$p = Get-Process | Where-Object { $_.MainWindowTitle -like '*${windowTitle}*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p) { [WinAPI2]::SetForegroundWindow($p.MainWindowHandle); [WinAPI2]::MoveWindow($p.MainWindowHandle, ${posX}, ${posY}, ${width}, ${height}, $true); Write-Output 'OK' } else { Write-Error 'Fenster nicht gefunden' }
"`,
        { encoding: "utf-8", timeout: 10000 }
      );
      return { content: [{ type: "text", text: `Fenster '${windowTitle}' auf ${width}x${height} bei (${posX},${posY}) gesetzt.` }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Fehler: ${err.message}` }] };
    }
  }
);

// Tool: Mausklick senden
server.tool(
  "click",
  "Sendet einen Mausklick an eine bestimmte Bildschirmposition.",
  {
    x: z.number().describe("X-Koordinate"),
    y: z.number().describe("Y-Koordinate"),
    button: z.enum(["left", "right"]).optional().describe("Maustaste. Default: left"),
  },
  async ({ x, y, button }) => {
    const btn = button || "left";
    try {
      execSync(
        `powershell -NoProfile -Command "
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class MouseSim {
    [DllImport(\\"user32.dll\\")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport(\\"user32.dll\\")] public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);
}
'@
[MouseSim]::SetCursorPos(${x}, ${y})
Start-Sleep -Milliseconds 100
${btn === "left"
    ? "[MouseSim]::mouse_event(0x0002, 0, 0, 0, 0); [MouseSim]::mouse_event(0x0004, 0, 0, 0, 0)"
    : "[MouseSim]::mouse_event(0x0008, 0, 0, 0, 0); [MouseSim]::mouse_event(0x0010, 0, 0, 0, 0)"
}
Write-Output 'Klick bei (${x}, ${y})'
"`,
        { encoding: "utf-8", timeout: 5000 }
      );
      return { content: [{ type: "text", text: `${btn}-Klick bei (${x}, ${y}) ausgeführt.` }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Klick-Fehler: ${err.message}` }] };
    }
  }
);

// Tool: Tastatureingabe senden
server.tool(
  "send_keys",
  "Sendet Tastatureingaben an das aktive Fenster.",
  {
    keys: z.string().describe("Tasten die gesendet werden (z.B. '{ENTER}', 'Hello', '{TAB}')"),
  },
  async ({ keys }) => {
    try {
      execSync(
        `powershell -NoProfile -Command "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait('${keys}')"`,
        { encoding: "utf-8", timeout: 5000 }
      );
      return { content: [{ type: "text", text: `Tasten '${keys}' gesendet.` }] };
    } catch (err) {
      return { content: [{ type: "text", text: `Tasten-Fehler: ${err.message}` }] };
    }
  }
);

// Server starten
const transport = new StdioServerTransport();
await server.connect(transport);
