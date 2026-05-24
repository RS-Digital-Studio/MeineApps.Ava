# ArcaneKingdom (Arbeitstitel)

Mobile Sammelkartenspiel + RPG fuer Android. Einzige App in dieser Codebase, die **nicht** Avalonia/.NET nutzt — sondern **Unity 6 (6000.4.x) + C#**.

> Status: Pre-MVP (Stand 2026-05-24) — vollstaendige Business-Logik + JSON-Daten + Cloud-Functions-Stubs, UI-Layouts kommen in MVP-Phase
> Tech-Stack: Unity 6 (NICHT Avalonia)
> Plattform: Android (iOS spaeter optional)
> Genre: TCG + RPG, Free-to-Play

---

## Wichtige Dateien

| Datei | Inhalt |
|-------|--------|
| [SETUP.md](SETUP.md) | **Erste Schritte fuer Unity 6** — Pflichtlektuere beim ersten Open |
| [CLAUDE.md](CLAUDE.md) | App-Conventions, Tech-Stack, Build, Verweise |
| [DESIGN.md](DESIGN.md) | Konsolidiertes Game Design Document (GDD v5.4) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Unity-Projektstruktur, DI, Save-System, Networking |
| [Server/SERVEROPS.md](Server/SERVEROPS.md) | Cloud-Functions (Anti-Cheat, Saison-Rewards) |
| [Unity/](Unity/) | Das eigentliche Unity-Projekt (in Unity Hub oeffnen) |

---

## Quickstart (Entwickler)

1. **Unity Hub** installieren ([unity.com/download](https://unity.com/download))
2. **Unity 6000.4.8f1** ueber Unity Hub installieren — Module: **Android Build Support** (inkl. JDK + SDK + NDK)
3. In Unity Hub: `Add project from disk` -> `F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity\`
4. Beim ersten Oeffnen werden Packages aufgeloest (URP 17, Addressables 2.6, Localization 1.5.5, VContainer, UniTask, …) — kann 5-20 Minuten dauern
5. **Setup-Wizard** oeffnet sich automatisch: 3 Klicks → BalancingConfig + DataImport + Build-Scenes registriert
6. Boot.unity oeffnen, [Bootstrapper] mit RootLifetimeScope verdrahten (siehe [SETUP.md](SETUP.md) Schritt 4)
7. `Play` druecken → Console zeigt "ArcaneKingdom gestartet"

> Hinweis: Das Unity-Projekt ist nicht Teil von `MeineApps.Ava.sln` und wird von `dotnet build` ignoriert.

---

## Warum Unity statt Avalonia?

Avalonia + SkiaSharp ist fuer 2D/UI hervorragend (siehe RebornSaga, BomberBlast). Fuer dieses Projekt fiel die Wahl trotzdem auf Unity 6, weil:

- **Photon** (Echtzeit-PvP-Arena, Klan-Matches) hat First-Class Unity-Support
- **Firebase Unity SDK** (Auth, Realtime Database, Cloud Messaging, Remote Config) ist deutlich reifer als die NuGet-Variante fuer Mobile
- **Addressables 2.6** + **AssetBundles** (dynamisches Nachladen von ~90 Karten-Artworks + Welten-Hintergruenden) ist out-of-the-box geloest
- **iOS-Option** spaeter ohne Tech-Switch moeglich
- Riesige Asset-Store-Bibliothek (Particle-FX, Shader, Card-Tween-Libs) verkuerzt Time-to-Market
- Unity 6 bringt URP 17, neue UI Toolkit-Iteration und Multiplayer Center als built-in Module
