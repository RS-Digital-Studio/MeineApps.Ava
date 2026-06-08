# P3 — Social/Live-Ops & Closed-Beta-Spec

> Vierte Phase der 3D-Idle-Neuausrichtung ([3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md), Phase P3).
> Baut auf dem Content-Stand aus [P2_CONTENT.md](P2_CONTENT.md) auf.
> **Zweck:** das Spiel **messbar & betreibbar** machen und in die **Closed Beta** bringen — Telemetrie,
> Remote-Live-Ops, Retention-Hooks (Push/Referral), leichte soziale Schicht und ein stabiler Beta-Build.

---

## 1. Ziel & Exit-Gate

**Leitfrage:** *Läuft eine Closed Beta stabil, und liefern Telemetrie + Live-Ops verlässliche
Retention-/Monetarisierungs-Signale, auf denen die Cutover-Entscheidung (P4) fußen kann?*

**Exit-Gate (alle):**
1. **Closed Beta** live unter Beta-App-ID `com.meineapps.handwerkerimperium2.beta`.
2. **Telemetrie** feuert vollständig (FTUE-Funnel, Loop-, Prestige-, Ad-, IAP-Events) und ist auswertbar.
3. **Remote-Config-Live-Ops** kann ohne App-Update ein Event schalten.
4. **Push-Notifications** & **Cloud-Save** funktionieren auf mehreren Geräten.
5. **Crash-free-Sessions ≥ 99 %** im Beta-Build.
6. Online-Werte (falls Gilde-lite/Leaderboard aktiv) sind **server-seitig anti-cheat-validiert**.

---

## 2. Scope

### Drin
- **Telemetrie/Analytics:** Event-Taxonomie (FTUE-Funnel, Loop, Upgrade, Hire, Unlock, Restauration, Prestige, Ad-Impression/Reward, IAP), `IAnalyticsService.TrackEvent`.
- **Remote-Config-Live-Ops:** 4 Event-Templates (DoubleReward/Rush-Marathon/Sammel-Sprint/Mini-Game-Mastery), 3-Tier-Reward, A/B-fähig.
- **Push-Notifications (Android):** 8 Trigger sinngemäß (Offline-Cap voll, Sanierung fertig, Rush verfügbar, Daily bereit, Eil-Auftrag, …) mit **Meister-Hans-Persona-Präfix**.
- **Cloud-Save** (Firebase) mit Konfliktauflösung (höhere Version → Alert statt Overwrite).
- **What's-New-Dialog** (versioniert) + **FTUE-light-Politur** (Hans-geführtes learn-by-doing, 3–4 Beats).
- **Referral** (6-stelliger Code, 3-Tier-Reward, server-seitiges Anti-Cheat) + **Cross-Promotion** (House-Ads, Tagesrotation).
- **Endgame-Meistergrade** (Soft-Infinite nach dem 3. Prestige in der Metropole): **Imperium-Renommee**-Ressource + Meistergrad-Loop (`base × 1.5^R`) — der Monate-Langzeit-Tail (→ [PROGRESSION_BALANCING.md](PROGRESSION_BALANCING.md)).
- **Leaderboards** (Cash / **Meistergrad** / Income) — Firebase, HMAC-validiert.
- **Optional hinter Feature-Flag: „Gilde-lite"** (Beitritt + gemeinsames Wochen-Ziel/Co-op-Event) — **kein** voller Kriegs-/Boss-/Auktions-Stack.
- **Crash-Reporting** (Firebase Crashlytics-Äquivalent) + Beta-Build-Pipeline + Store-Closed-Test-Setup.

### Raus (→ P4)
Voller Gilden-Stack (Krieg/Boss/Auktion/Mega), finaler Art/Audio-Polish, Store-Marketing-Assets, KPI-Balancing-Pass.

---

## 3. Neue / erweiterte Systeme & Infra

| System | P3-Verantwortung |
|--------|------------------|
| `AnalyticsService` *(neu)* | Event-Taxonomie (Namen/Props in `AnalyticsEvents`-Katalog), optionale Injektion |
| `RemoteConfigService` *(neu)* | Live-Event-Templates, Feature-Flags, A/B-Buckets |
| `NotificationService` (Android) *(neu)* | 8 Trigger, Scheduling, Hans-Persona-Präfix, Safe-Area/Permission |
| `CloudSaveService` *(neu)* | Upload/Download, Versionskonflikt-Alert, HMAC |
| `MeistergradService` *(neu)* | Endgame-Loop (Metropole): Renommee-Akkumulation, Meistergrad-Kosten `1.5^R`, permanente Soft-Cap-Boni |
| `LeaderboardService` *(neu)* | Firebase-Pfade + `.indexOn`, Server-`validate`-Rules, HMAC |
| `ReferralService` / `CrossPromoService` *(neu)* | Codes, 3-Tier-Reward, Tagesrotation, Anti-Cheat |
| `GuildLiteService` *(optional, Flag)* | Beitritt + Wochen-Co-op-Ziel; atomares Firebase-PATCH |
| `WhatsNewService` *(neu)* | versionierter Feature-Dialog, `lastSeen` vor Render |

**Firebase (Kompatibilität gewahrt — CLAUDE.md §7):** Pfade nutzen **PlayerId** (stabile UUID, nicht Firebase-UID);
Online-Werte (Leaderboard/Co-op) **server-seitig** per atomarem PATCH + `validate`-Rules + Rate-Limit abgesichert;
Security-Rules in `Server/DatabaseRules/database.rules.json`, `.indexOn` für `orderBy`. Lokaler Save: HMAC-Reparatur statt Wipe.

---

## 4. Daten & Telemetrie

| Bereich | Inhalt |
|---------|--------|
| Analytics-Events | `AnalyticsEvents`-Katalog (Original als Namens-Referenz, an neue Mechanik gemappt): ftue_step, loop_collect, upgrade_bought, worker_hired, plot_unlocked, landmark_restored, prestige_done, ad_impression/ad_reward, iap_purchase |
| Remote-Config-Keys | Event-Template-Aktivierung, Reward-Tiers, Feature-Flags, A/B-Buckets |
| Push-Trigger | 8 Bedingungen (lokal geplant + remote-getriggert) |
| Leaderboard-Schema | Score-Felder + Server-Timestamp `{".sv":"timestamp"}` + HMAC |
| Referral | Code, Tier-Fortschritt (50/200/500 GS sinngemäß), Anti-Cheat |

---

## 5. Beta-Operations

- **Build:** über Unity-Editor / Cloud Build (IL2CPP Release, AAB), Beta-App-ID, **Symbol-Upload** für Crash-Symbolikation.
- **Store:** Google Play **Closed Test**-Track, Tester-Liste, Datenschutz/Consent (UMP/Consent-Dialog für Ads).
- **Migration-Hook:** bestehende Avalonia-`IsPremium`-Spieler → „Imperium-Pass" + Migration-Bonus (100 GS) beim ersten Login.
- **Monitoring:** Crash-Free-Rate, FTUE-Funnel-Drop-off, D1/D7-Retention, Ad-Fill/eCPM, IAP-Conversion — Dashboards für P4.

---

## 6. Tests & QA

- **EditMode:** Analytics-Event-Schemas, Referral-Tier-Logik, CloudSave-Versionskonflikt, Notification-Scheduling-Zeiten (UTC).
- **Server/Anti-Cheat:** Leaderboard-/Co-op-Write gegen `validate`-Rules (gültig akzeptiert, manipuliert abgelehnt), Rate-Limit greift.
- **PlayMode/Integration:** Remote-Config schaltet Event live; Push kommt an; Cloud-Save Roundtrip über 2 Geräte; What's-New zeigt nur Neues.
- **Beta-Smoke-Matrix:** mehrere Android-Geräte inkl. **Low-End** + großer Notch (Safe-Area), Cold-Start, Hintergrund/Resume, Offline→Online.
- **Datenschutz:** Consent-Flow vor Ads/Analytics, Opt-out respektiert.

---

## 7. Aufwand & Abhängigkeiten

- **Backend-/Live-Ops-lastig** — Firebase-Projekt, Security-Rules, Remote-Config, Crash-Reporting, Beta-Pipeline.
- **Abhängigkeit:** P2-Content steht (sonst nichts zu messen); Firebase-Pfade/HMAC aus CLAUDE.md §7.
- **Entscheidung:** „Gilde-lite" nur, wenn Beta-Kapazität es zulässt — sonst hinter Flag aus (Genre funktioniert single-player).
- **Risiko:** Anti-Cheat für Online-Werte (clientseitig niemals vertrauen) → strikt server-validate.

---

## Verweise
- Spiel-Design: [3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md) (Monetarisierung §9, Live-Ops §10, Multiplayer-Verschiebung §15.8)
- Vorgänger: [P2_CONTENT.md](P2_CONTENT.md) · Nachfolger: [P4_POLISH_CUTOVER.md](P4_POLISH_CUTOVER.md)
- Tech/Firebase/Anti-Cheat: [CLAUDE.md](CLAUDE.md) §7 · [ARCHITECTURE.md](ARCHITECTURE.md)
