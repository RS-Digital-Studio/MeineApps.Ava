# P4 — Polish & Cutover-Entscheid-Spec

> Letzte Phase der 3D-Idle-Neuausrichtung ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md), Phase P4).
> Baut auf den Beta-Daten aus [P3_SOCIAL_BETA.md](P3_SOCIAL_BETA.md) auf.
> **Zweck:** das Spiel **release-reif polieren**, anhand echter Beta-KPIs **balancen** und die **Cutover-Entscheidung**
> gegenüber der produktiven Avalonia-Version **datenbasiert** treffen.

---

## 1. Ziel & Exit-Gate

**Leitfrage:** *Erreicht die Beta die KPI-Schwellen (Retention, Monetarisierung, Stabilität, Performance),
sodass ein Cutover gegenüber der Avalonia-Version gerechtfertigt ist — oder wird iteriert?*

**Exit-Gate = Cutover-Go/No-Go-Entscheidung** (Kriterien in §4), dokumentiert mit Datenbeleg.

---

## 2. Scope

### Drin
- **Balancing-Pass gegen Beta-KPIs:** Loop-Kurven, Upgrade-/Prestige-Kosten, Offline-Cap, Ad-Frequenz, Preis-Punkte — getunt anhand realer Funnel-/Monetarisierungs-Daten (A/B wo sinnvoll).
- **Performance-Pass Low-End:** Quality-Tiers (Low/Mid/High), LOD-Stufen, Texture-Atlas/-Compression (ASTC), Particle-Caps, Draw-Call-Budget → **60 FPS Mobile-Ziel**, **AAB < 500 MB**.
- **Art-/Audio-Polish:** Game-Juice (Münz-Fly/Shake/Floating-Text/Burst), finale VFX/Shader, Prestige-/Sanierungs-Cinematics final, Audio-Mix/Mastering.
- **Lokalisierungs-Review** (6 Sprachen, 1–2 Pässe inkl. Voice-Check).
- **Store-Assets:** Icon, Feature-Graphic, Screenshots, Kurz-Trailer (analog `StoreAssetGenerator`/`SocialPostGenerator`).
- **Migration-Politur:** Avalonia-Premium→Pass, Migration-Bonus, „Migration-Storytelling" (Hans begrüßt Rückkehrer).
- **Release-Härtung:** Full-AOT/IL2CPP-Build-Verifikation, Crash-Symbolikation, Datenschutz/Consent final, Rollback-Plan.

### Raus
Neue Features/Systeme (Feature-Freeze ab P4-Start). Voller Gilden-Stack bleibt Post-Cutover-Backlog (GDD §15.8).

---

## 3. Balancing- & Performance-Arbeit

| Bereich | Vorgehen |
|---------|----------|
| Retention-Tuning | FTUE-Funnel-Drop-offs schließen, frühe „nur-noch-eins"-Dichte erhöhen, erste-Prestige-Zeitfenster justieren |
| Monetarisierungs-Tuning | Ad-Pad-Werte, Offline×2-Reiz, Premium-Pass-Sichtbarkeit (Einkommens-Vergleich ohne/mit), Gem-Pack-Preise & Whale-Bundles |
| Ökonomie-Kurven | Income-Soft-Cap, Upgrade-Wachstum, die **3** Prestige-Multiplikatoren (4 Städte), **Meisterschafts-Steigung** (`1.15^N`) + **Meistergrad-Verlangsamung** (`1.5^R`) — keine toten Zonen, kein Eskalations-Bruch (→ [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)) |
| Performance | LOD/Atlas/Instancing/Particle-Cap je Quality-Tier; Frame-Time-Budget je Szene; Memory < Ziel; Cold-Start-Zeit |
| Stabilität | Crash-free ≥ 99,5 %, ANR-Rate niedrig, Save-Robustheit (Kill/Resume), Edge-Cases (Timezone, Notch, Low-RAM) |

Alle Tuning-Werte über `BalancingConfig`/Remote-Config — Hot-Tuning ohne Build wo möglich.

---

## 4. Cutover-Entscheidungsrahmen (KPI-Schwellen)

Cutover-**Go** nur, wenn die Beta (stabilisiert, ausreichende N) die Genre-Benchmarks aus [GDD §14](3D_IDLE_GAME_PLAN.md) trifft:

| KPI | Ziel-Schwelle (Go) | No-Go → Maßnahme |
|-----|--------------------|------------------|
| **D1-Retention** | ≥ 35–40 % | FTUE/early-loop iterieren |
| **D7-Retention** | ≥ 12–15 % | Mid-Game-Content/Live-Ops verstärken |
| **Session-Länge / Frequenz** | ~4–6 min, mehrere/Tag | Loop-Dichte/Push justieren |
| **ARPDAU (Ad + IAP)** | ≥ Avalonia-Baseline | Ad-Placements/Preise tunen |
| **Premium-Conversion** | markttypisch (Idle-Arcade) | Pass-Wert/Sichtbarkeit erhöhen |
| **Crash-free Sessions** | ≥ 99,5 % | Stabilisieren, Beta verlängern |
| **Performance** | 60 FPS Mid / 30+ Low-End, AAB < 500 MB | Perf-Pass vertiefen |

**Go:** Soft-Launch erweitern → schrittweiser Cutover (Avalonia bleibt parallel, bis Unity ≥ Baseline trägt).
**No-Go:** gezielt die reißende KPI-Achse iterieren (kein Blind-Relaunch), Beta verlängern.

---

## 5. Tests & QA (Release-Gate)

- **Full-Regression** (EditMode + PlayMode) grün; **Save-Migration** Avalonia-Premium→Pass verifiziert.
- **Performance-Budget-Gates** je Quality-Tier automatisiert (Frame-Time/Memory/Build-Größe als CI-Schwellen).
- **Store-Policy-Compliance:** Datenschutz/Datensicherheits-Formular, Consent (UMP), Altersfreigabe, Ad-Policy, Ziel-API-Level.
- **Device-Matrix:** Low/Mid/High + großer Notch + Tablet; Cold-Start, Resume, Offline→Online, App-Update-Pfad.
- **Lokalisierung:** finaler Text-/Voice-Review je Sprache.

---

## 6. Aufwand & Abhängigkeiten

- **Tuning-/Polish-/Launch-lastig**, Dauer datengetrieben (Beta-Lernzyklen).
- **Abhängigkeit:** P3-Telemetrie + stabile Beta liefern die Entscheidungsdaten.
- **Feature-Freeze** ab P4-Start — nur Bugfixes/Tuning/Polish.
- **Ergebnis:** dokumentierte Cutover-Entscheidung; bei Go gestufter Rollout, Avalonia bleibt Fallback bis zur Ablösung.

---

## Verweise
- Spiel-Design & KPIs: [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) (Roadmap/KPIs §14, Monetarisierung §9)
- Vorgänger: [P3_SOCIAL_BETA.md](P3_SOCIAL_BETA.md)
- Tech/Build/Store: [CLAUDE.md](CLAUDE.md) · [ARCHITECTURE.md](ARCHITECTURE.md) · [SETUP.md](SETUP.md) · Store-Assets-Werkzeuge: `tools/StoreAssetGenerator`, `tools/SocialPostGenerator`
