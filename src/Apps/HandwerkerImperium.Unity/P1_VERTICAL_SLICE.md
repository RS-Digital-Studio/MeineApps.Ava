# P1 — Vertical-Slice-Spec (Hansstadt, spielbar & spaßig)

> Zweite Phase der 3D-Idle-Neuausrichtung ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md), Phase P1).
> Baut direkt auf den P0-Systemskeletten ([P0_GREYBOX_PROTOTYP.md](P0_GREYBOX_PROTOTYP.md)) auf.
> **Zweck:** der **komplette Kern-Loop von Garage bis erstem Prestige** in **einer** echten, hübschen Stadt —
> mit Story-Anker, Stern-Rating, einem Restaurierungs-Wahrzeichen und verdrahteter (noch nicht getunter) Monetarisierung.
> Arbeitsgrundlage: gesetzte Defaults [GDD §16](3D_IDLE_GAME_PLAN.md).

---

## 1. Ziel & Exit-Gate

**Leitfrage:** *Trägt der vollständige Loop — anstellen, ausbauen, sanieren, prestigen — über eine
mehrtägige interne Testphase, und fühlt sich der erste Umzug (Prestige) wie ein echter Aufstieg an?*

**Exit-Gate (alle):**
1. Spieler erreicht **organisch** das erste Prestige (Hansstadt 5★ → Kreisstadt) in **~Woche 1 (~5–7 Tagen)** Spielzeit — bewusst kein Tag-1-Reset.
2. Der **Garage→erste-volle-Stadt**-Bogen hat keine toten Strecken (immer ein nächstes günstiges Ziel).
3. **Restaurierung** eines Wahrzeichens liefert den „Vorher/Nachher"-Wow.
4. **Hans-Intro** macht in <30 s klar, *warum* man hier ist.
5. **60 FPS** Mid-Range / **30+** Low-End-Android; Save übersteht Kill/Restart unbeschädigt.
6. Monetarisierungs-Hooks (Ads-Pad, Offline×2, Premium-Pass) **funktionieren** (Tuning erst P4).

---

## 2. Scope

### Drin
- **1 Stadt „Hansstadt"** mit dem zentralen Hof + **Werkstatt-Plots für alle 10 Handwerks-Arten**, sequенziell freischaltbar.
- **Avatar** mit echten Animationen (Idle/Walk/Carry/Hammer-Geste) + 1–2 Skins; New-Input-Joystick + Tap-to-move.
- **NPC-Arbeiter:** Hire pro Station + **3–5 Tempo-/Trag-Stufen**; „Laune=Tempo" **aus** (§16), nur Ad-Tempo-Boost.
- **Kunden-Queue** am Tresen + gelegentlicher **Eil-Auftrag** (Timer, Ad-verlängerbar).
- **Cash + Auto-Collect-Radius**, **Upgrade-Pads** (Tempo/Kapazität/Wert/Radius), **Plot-/Distrikt-Unlock**.
- **Stern-Rating 1–5★** (Aggregat, Hysterese) als Distrikt-Gate + Prestige-Freigabe bei 5★.
- **1 Restaurierungs-Wahrzeichen** (Marktplatz) mit **5 Bauphasen** + Hans-Cutscene + Stern-Sprung.
- **1 Prestige = Umzug nach Kreisstadt** (Shell-Stadt) mit permanentem Multiplikator + **Prestige-Cinematic** — das **erste von max. 3** Prestiges (→ [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)).
- **Meisterschafts-Track als Save-Slice-Stub** bereits anlegen (kontoweite, nie-reset XP — das Langzeit-Rückgrat, voll ausgebaut in P2).
- **Hans-Intro + ~4 Voice-Beats** (DE+EN); **Offline-Earnings**-Dialog.
- **Schlankes Save-Schema + HMAC** (GDD §12, CLAUDE.md §7).
- **Kern-Monetarisierung verdrahtet:** Free-Cash-Pad, Offline×2, Premium-„Imperium-Pass" (no-ads + Auto-Collect), 1 Gem-IAP-Pack.

### Raus (→ P2+)
Weitere Städte über die Kreisstadt-Shell hinaus, alle übrigen Distrikte/Wahrzeichen, voller Master-Tool-Satz,
Cosmetics-Shop, Daily/BattlePass/Events-Vollausbau, Saison, Multiplayer, 6-Sprachen-Volllokalisierung
(P1 nur DE/EN), Mini-Game-Boosts (optional 1 als Prototyp).

---

## 3. Neue / erweiterte Systeme (Game-Layer)

Aufbauend auf P0; neu in P1 **fett**.

| System | P1-Verantwortung |
|--------|------------------|
| `StationService` | alle 10 Stationen, echte Meshes, Produktions-/Verkaufs-Kurven |
| `WorkerAutomationService` | Hire + 3–5 Stufen, Ad-Tempo-Boost |
| `OrderQueueService` *(neu)* | Kunden-Queue am Tresen + Eil-Auftrag (Timer) |
| **`StarRatingService`** | 1–5★ aus Werkstätten/Sanierung/Volumen, **Hysterese** (3-Punkte-Buffer, persistiert) |
| **`TownRestorationService`** | 1 Wahrzeichen, 5 Bauphasen, Distrikt-Zustand, Stern-Kopplung |
| **`FranchisePrestigeService`** | 5★ → Reset + Umzug Kreisstadt, permanenter Multiplikator, Prestige-Währung |
| **`SaveSystem`** | schlanke Slices (Town/Stations/Workers/Orders/Restoration/Franchise/Economy) + **HMAC** + Migration-Infra |
| **`StoryBeatService`** | Hans-Intro + ~4 Beats (Trigger an Loop-Events), Voice-Playback |
| **`MonetizationGateway`** | Ad-Gateway (Free-Cash/Offline×2) + IAP-Stub (Premium-Pass, 1 Gem-Pack), `IsPremium`→Auto-Collect |
| **`OfflineProgressService`** | aus P0, jetzt mit Premium-Cap/Multiplikator |
| **`CinematicService`** | Prestige-Cinematic (4 Phasen, Cinemachine + Timeline) |

Domain bleibt Unity-frei; wiederverwendete Formeln (Income/Offline/AutoProduction/Order) leben in `Domain/.../*Formulas.cs`.

---

## 4. Daten & Balancing (Quellen)

Wo der GDD Original-Formeln übernimmt, gelten deren Werte ([ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) als Referenz):

| Größe | Quelle / Ansatz |
|-------|-----------------|
| Stations-Produktionsintervall | `AutoProductionFormulas` (Standard 180 s / InnovationLab 120 / MasterSmith 60 — für Idle-Tempo skalierbar) |
| Einkommen / Soft-Cap | `IncomeFormulas` (Log2-Soft-Cap, Multiplikatoren) |
| Upgrade-Kostenkurve | geometrisch (`BalancingConfig`: base × growth^level) |
| Worker Hire/Stufen | `WorkerFormulas`-Teilmenge (ohne Mood/Fatigue) |
| Stern-Rating-Schwellen | neu, aus Werkstatt-Count + Sanierung + Auftragsvolumen, mit Hysterese |
| Prestige-Multiplikator | aus `PP = floor(sqrt(CurrentRunMoney / 100_000))` → Stadt-Multiplikator-Mapping |
| Offline-Verdienst | `OfflineProgressFormulas` (Staffel 0.80/0.35/0.15/0.05) + Premium-Cap |
| Premium-Effekt | +50 % Income, Auto-Collect, +Offline-Cap (wie Original-„Imperium-Pass") |

**Alle Werte über `BalancingConfig` (ScriptableObject)** — kein Hardcoding (CLAUDE.md-Verbot).

---

## 5. Art- & Audio-Bedarf (P1-Teilmenge der GDD §13)

Setzt voraus, dass der **Asset-Pilot** (ASSETS_AI.md) **Go** ist.

| Asset | Menge P1 | Quelle |
|-------|----------|--------|
| Werkstatt-Stationen | 10 (1 Basis + Decal-Sets) | TRELLIS 2 + Decals |
| Avatar | 1 gerigt + Carry-Anim + 1–2 Skins | Mixamo/AccuRIG |
| NPC-Arbeiter | 2–3 Varianten | Recolor/Mixamo |
| Carry-Props / Cash | ~10 Waren + Münzen | Instancing |
| Stadt-Kit Hansstadt | modular (Ruine→teilsaniert) | TripoSG-Batch |
| Wahrzeichen Marktplatz | 1 × 5 Bauphasen | Hero (ggf. Cloud) |
| Prestige-Cinematic | Badge + Kreisstadt-Reveal (light) | Timeline + VFX |
| Audio | Idle-Loop + Stinger + ~4 Hans-Lines (DE/EN) | Stable Audio 3 + ElevenLabs |

---

## 6. Tests & QA

- **EditMode (NUnit):** `IncomeFormulas`, `OfflineProgressFormulas`, Prestige-Multiplikator-Mapping, Stern-Rating-Schwellen (inkl. Hysterese), **Save-Roundtrip** (Slices laden→serialisieren→Feld-Diff leer) + **HMAC-Verifikation** (gültig/ungültig→Sanitize).
- **PlayMode-Smoke:** voller Loop (produzieren→tragen→Kunde→Cash→Pickup), Upgrade wirkt, Worker automatisiert, Plot-Unlock, Wahrzeichen-Sanierung schaltet Stern, Prestige resettet + Multiplikator greift, Offline rechnet korrekt.
- **Monetarisierung:** Ad-Pad gibt Belohnung, Offline×2 verdoppelt, Premium schaltet Auto-Collect + Income-Bonus, IAP-Stub bucht Gems.
- **Perf:** 60 FPS Mid / 30+ Low-End-Android; Memory-Budget grob.
- **Persistenz-Härte:** App-Kill in `OnApplicationPause(true)` → kein Datenverlust (CLAUDE.md-Gotcha).

---

## 7. Aufwand & Abhängigkeiten

- **Größte Phase** (mehrere Wochen, 1 Dev + Asset-Pipeline-Output).
- **Harte Abhängigkeit:** Asset-Pilot-Go (sonst P1 in Greybox starten, Assets nachziehen).
- **Reihenfolge:** Save+HMAC und StarRating früh (alles hängt dran) → Restauration → Prestige → Monetarisierungs-Hooks → Hans-Beats → Polish-Pass.
- **Risiko:** Prestige-Multiplikator-Tuning (zu schwach = kein Aufstiegsgefühl, zu stark = Loop trivial) → früh playtesten.

---

## Verweise
- Spiel-Design: [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) (Loop §3, Stadt §5, Systeme §6, Progression §7, Monetarisierung §9)
- Vorgänger/Skelette: [P0_GREYBOX_PROTOTYP.md](P0_GREYBOX_PROTOTYP.md) · Nachfolger: [P2_CONTENT.md](P2_CONTENT.md)
- Tech: [CLAUDE.md](CLAUDE.md) · [ARCHITECTURE.md](ARCHITECTURE.md) · Werte-Referenz: [ORIGINAL_WERTE.md](ORIGINAL_WERTE.md) · Assets: [ASSETS_AI.md](ASSETS_AI.md)
