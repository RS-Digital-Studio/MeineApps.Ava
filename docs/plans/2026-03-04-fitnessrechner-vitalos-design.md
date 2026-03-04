# FitnessRechner Design-Upgrade: "VitalOS" - Premium Health Monitoring System

**Datum:** 2026-03-04
**Status:** Genehmigt
**Scope:** Voll-Redesign (Full SkiaSharp Immersion)
**Ansatz:** SkiaSharp für alle visuellen Elemente, XAML nur für native Form-Controls

---

## Visuelle Identität

### Konzept
Die App wird zu einem **High-End Medical Dashboard** - ein holografisches Gesundheits-Betriebssystem.
Apple Watch Health trifft Sci-Fi Medical Console. Jede Interaktion fühlt sich an wie
die Bedienung eines Premium-Gesundheitsmonitors.

### Farbpalette

| Element | Farbe | Hex | Verwendung |
|---------|-------|-----|------------|
| Primary | Cyan | `#06B6D4` | EKG-Traces, aktive Elemente, Glow |
| Secondary | Teal | `#14B8A6` | Sekundäre Akzente, Partikel |
| Accent | Electric Blue | `#3B82F6` | BMI-Feature, Highlights |
| Background Deep | Deep Navy | `#0A1A2E → #051020` | Haupt-Hintergrund (radial) |
| Surface | Semi-Trans Blue | `#0F1D32` (85%) | Card-Hintergründe |
| Grid | Cyan-Raster | `#0E7490` (8-10%) | Medical Grid Lines |
| Gewicht | Lila | `#8B5CF6` | Gewichts-Quadrant/Feature |
| BMI | Blau | `#3B82F6` | BMI-Quadrant/Feature |
| Wasser | Grün | `#22C55E` | Wasser-Quadrant/Feature |
| Kalorien | Amber | `#F59E0B` | Kalorien-Quadrant/Feature |
| Warnung/Kritisch | Rot | `#EF4444` | Über-Limit, Kritische Werte |
| Text Primary | Helles Weiß | `#E2E8F0` | Haupttext |
| Text Muted | Cyan-Grau | `#64748B` | Labels, sekundärer Text |

### Typografie "Medical Readout"
- **Zahlen**: Monospace-ähnlich mit subtiler Cyan-Glow (SKMaskFilter.CreateBlur)
- **Labels**: Clean Sans-Serif (Standard Avalonia Font)
- **Einheiten** (kg, kcal, ml): Kleiner, Muted, leicht nach rechts versetzt

### Herzschlag-Synchronisation
- **72 BPM** (1.2 Beats/Sekunde) - wie im vorhandenen Splash-Screen
- Synchronisiert: Background-EKG, Ring-Glow-Pulse, Partikel-Burst, VitalSigns-Center
- Gibt der App einen durchgehend lebendigen Rhythmus

---

## Komponenten (8 Stück)

### 1. MedicalBackgroundRenderer

**Immer aktiv hinter allem Content** (wie GameBackgroundRenderer in HI).

**5 Render-Layer:**
1. **Deep Navy Gradient** - Radialer Gradient, Mitte (#0A1A2E) → Ecken (#030810)
2. **Medical Grid** - EKG-Papier-Raster, Cyan 8% Opacity, feine Linien alle 40px, dicke alle 200px
3. **EKG-Trace** - Animierte Herzschlag-Welle (horizontal, 72 BPM), Cyan #06B6D4 + Glow, Trail-Verblassung nach links
4. **Vital-Partikel** - 40-60 Struct-Pool (0 GC): Kleine Kreuze (+), Herzen, Wassertropfen, Waagen. 15-25% Opacity, langsam aufsteigend
5. **Corner-Vignette** - Dunkler Rand für Tiefenwirkung

**Performance:** ~20fps Render-Loop, Struct-basierter Partikel-Pool

**Datei:** `Graphics/MedicalBackgroundRenderer.cs`

---

### 2. MedicalTabBarRenderer (64dp)

**Vollständig SkiaSharp** (wie GameTabBarRenderer in HI).

**Design:**
- Hintergrund: Dunkles Glas-Panel (#0D1B2A, 90% Opacity), 1px Cyan-Glow obere Kante
- 4 Tabs: Home (HeartPulse), Progress (ChartLine), Food (FoodApple/Magnify), Settings (Cog)
- Aktiver Tab: Cyan-Underline (3px) + Glow, Icon volle Helligkeit, Label sichtbar
- Inaktive Tabs: Icons gedimmt (40% Opacity), kein Label
- Touch-Feedback: Cyan-Pulse radial von Touch-Point
- Separator-Lines: Vertikale Trennlinien (5% Opacity)

**Datei:** `Graphics/MedicalTabBarRenderer.cs`

---

### 3. VitalSignsHeroRenderer (280x280dp)

**Das Herzstück** - ersetzt das 2x2 Dashboard-Grid durch einen kreisförmigen Vital Signs Monitor.

**Layout:**
- **Äußerer Ring**: EKG-Trace der um den Kreis läuft (72 BPM sync)
- **4 Quadranten** (NW/NE/SW/SE):
  - NW: Gewicht (#8B5CF6) - Wert + Trend-Pfeil (↑/↓/→)
  - NE: BMI (#3B82F6) - Wert + Kategorie-Farbe
  - SW: Wasser (#22C55E) - Fortschritts-Arc + ml
  - SE: Kalorien (#F59E0B) - Fortschritts-Arc + kcal
- **Zentrum**: Tages-Score, große Zahl, pulsierender Cyan-Ring (72 BPM)
- **Quadranten-Trenner**: Kreuz-Linien (Cyan, 10% Opacity)
- **Data-Stream**: Punkte fließen zwischen Quadranten und Zentrum

**Interaktion:** Tap auf Quadrant → öffnet den entsprechenden Rechner
**Hit-Testing:** Per Winkelberechnung (atan2) welcher Quadrant getappt wurde

**Datei:** `Graphics/VitalSignsHeroRenderer.cs`

---

### 4. MedicalCardRenderer

**Universeller Card-Hintergrund** für alle Dashboard-Cards.

**Design:**
- Hintergrund: #0F1D32 bei 85% Opacity
- Obere Kante: 1px Gradient (Cyan → Transparent → Cyan)
- Corner Radius: 12px
- Ecken-Akzente: Kleine L-förmige Linien in Ecken (HUD-Bracketing, Cyan 30%)
- Optional: Feature-Farbe als subtiler Accent am linken Rand

**Verwendung:** Background für Streak-Card, Challenge-Card, XP-Bar, Badges, etc.
**Integration:** Als Static Render-Methode die direkt auf jeden Card-Canvas gerufen wird

**Datei:** `Graphics/MedicalCardRenderer.cs`

---

### 5. QuickActionButtonRenderer

**Ersetzt die 3 Gradient-Buttons** (+kg, +250ml, +kcal).

**Design:**
- Hintergrund: Feature-Farbe bei 30% Opacity
- Hologramm-Rand: Feature-Farbe, 1px, subtiles Pulsieren (alle 3s)
- Icon: Medical-Symbol-Variante + Glow
- Text: Monospace-Zahlen, Feature-Farbe
- Press-Effekt: Scale 0.95 + heller Flash + Haptic
- Idle: Dezentes Rand-Pulsieren

**Datei:** `Graphics/QuickActionButtonRenderer.cs`

---

### 6. Calculator Header + View Upgrades

**Neuer CalculatorHeaderRenderer** (ersetzt farbige Border-Header):
- Feature-Farbe Gradient + Medical Grid Overlay
- Mini-EKG in Feature-Farbe
- Holographic Title-Text mit Glow-Kante
- Hologramm-Kreis Back-Button mit Chevron

**Bestehende Renderer-Upgrades:**
- **BmiGaugeRenderer**: + Holographic Glow auf Nadel, + Medical Grid, + Scan-Line-Sweep
- **CalorieRingRenderer**: + Pulsierender Glow (72 BPM), + Data-Stream Partikel
- **BodyFatRenderer**: + Holographische Cyan-Kontur, + Scan-Linie über Körper

**Dateien:** `Graphics/CalculatorHeaderRenderer.cs` + bestehende Renderer modifizieren

---

### 7. Dashboard-Cards als SkiaSharp-Renderer

**StreakCardRenderer:**
- Medical Monitor Style: "VITAL STREAK: 7 DAYS"
- Pulsierender Herzschlag-Icon (oder Flamme + EKG-Line dahinter)
- Mini-EKG Trace rechts

**ChallengeCardRenderer:**
- "DAILY MISSION" Briefing-Style
- Holographischer Progress-Bar
- XP-Badge mit Glow

**LevelProgressRenderer:**
- Medical Progress-Bar mit Scan-Line-Gleitung
- Level-Badge als holographisches Schild

**Dateien:** `Graphics/StreakCardRenderer.cs`, `Graphics/ChallengeCardRenderer.cs`, `Graphics/LevelProgressRenderer.cs`

---

### 8. FoodSearch + ProgressView Upgrades

**FoodSearchView:**
- Search-Bar mit Scan-Line Animation
- Food-Items mit MedicalCardRenderer-Hintergrund
- Barcode-Button mit Hologramm-Puls
- Quick-Add Panel mit holographischem Rand

**ProgressView:**
- Chart-Hintergrund: Medical Grid (statt Standard)
- HealthTrendVisualization: + Medical Grid Lines, + Glow auf Datenpunkten
- Sub-Tab Navigation: Medical-Style mit Glow-Underline

**Dateien:** Bestehende Views modifizieren + SkiaSharp-Integration

---

## Performance-Anforderungen

| Aspekt | Ziel | Technik |
|--------|------|---------|
| Background FPS | 20fps | DispatcherTimer, Struct-Pools |
| GC im Render-Loop | 0 Allokationen | Struct-Pools, Paint-Caching, Path.Rewind() |
| Partikel-Max | 60 (Background) | Fixed-Size Array, kein List<T> |
| Paint-Caching | Alle SKPaint als Instanzfelder | Keine new SKPaint() im Render |
| Shader-Caching | Gradient-Shader beim Init | SKShader.CreateLinearGradient gecacht |

---

## Implementierungs-Reihenfolge (vorgeschlagen)

1. **MedicalBackgroundRenderer** - Visuelles Fundament, sofort sichtbarer Unterschied
2. **MedicalTabBarRenderer** - Zweites Kern-Element, framt die ganze App
3. **VitalSignsHeroRenderer** - Dashboard Hero-Element
4. **MedicalCardRenderer** - Universeller Card-Background
5. **QuickActionButtonRenderer** - Quick-Action Buttons
6. **Dashboard-Cards** (Streak, Challenge, Level) - Cards als Renderer
7. **Calculator Header + Renderer-Upgrades** - Rechner-Views aufwerten
8. **FoodSearch + ProgressView** - Restliche Views

---

## Theme-Kompatibilität

Das "VitalOS" Design wird als **fünftes Theme-Preset speziell für FitnessRechner** behandelt:
- Die Medical-Farben werden über das bestehende Theme-System eingebunden
- Der Background-Renderer nutzt Theme-Farben wo möglich
- Andere Themes (Midnight, Aurora, Daylight, Forest) bleiben verfügbar
- Der MedicalBackgroundRenderer passt sich an das gewählte Theme an (z.B. Forest → Grüne Grid-Lines statt Cyan)

---

## Dateien-Übersicht (neue/modifizierte)

### Neue Dateien
| Datei | Zweck |
|-------|-------|
| `Graphics/MedicalBackgroundRenderer.cs` | Animierter Hintergrund (EKG, Grid, Partikel) |
| `Graphics/MedicalTabBarRenderer.cs` | Holographische Tab-Bar |
| `Graphics/VitalSignsHeroRenderer.cs` | Kreisförmiger Vital Signs Monitor |
| `Graphics/MedicalCardRenderer.cs` | Universeller Card-Hintergrund |
| `Graphics/QuickActionButtonRenderer.cs` | Premium Quick-Action Buttons |
| `Graphics/CalculatorHeaderRenderer.cs` | Medical-Style Calculator Header |
| `Graphics/StreakCardRenderer.cs` | Medical Streak-Anzeige |
| `Graphics/ChallengeCardRenderer.cs` | Medical Challenge-Card |
| `Graphics/LevelProgressRenderer.cs` | Medical XP/Level-Bar |

### Modifizierte Dateien
| Datei | Änderung |
|-------|----------|
| `Graphics/BmiGaugeRenderer.cs` | + Glow, Grid, Scan-Line |
| `Graphics/CalorieRingRenderer.cs` | + Pulse, Data-Stream |
| `Graphics/BodyFatRenderer.cs` | + Holographische Kontur, Scan-Line |
| `Views/MainView.axaml` | SkiaSharp Background + Tab-Bar Integration |
| `Views/HomeView.axaml` | VitalSignsHero + neue Card-Renderer |
| `Views/FoodSearchView.axaml` | Medical-Style Cards + Scan-Line |
| `Views/ProgressView.axaml` | Medical Grid + Glow-Charts |
| `Views/Calculators/*.axaml` | Neue Header + Renderer-Upgrades |
