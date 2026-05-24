# ArcaneKingdom (Arbeitstitel)

Mobile Sammelkartenspiel + RPG fuer Android. Einzige App in dieser Codebase, die **nicht** Avalonia/.NET nutzt — sondern **Unity 2022 LTS + C#**.

> Status: Konzept-Phase (Stand 2026-05-24)
> Tech-Stack: Unity (NICHT Avalonia)
> Plattform: Android (iOS spaeter optional)
> Genre: TCG + RPG, Free-to-Play

---

## Wichtige Dateien

| Datei | Inhalt |
|-------|--------|
| [CLAUDE.md](CLAUDE.md) | App-Conventions, Tech-Stack, Build, Verweise — Pflichtlektuere |
| [DESIGN.md](DESIGN.md) | Konsolidiertes Game Design Document (GDD v5.1) |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Unity-Projektstruktur, DI, Save-System, Networking |
| [Unity/](Unity/) | Das eigentliche Unity-Projekt (in Unity Hub oeffnen) |

---

## Quickstart (Entwickler)

1. **Unity Hub** installieren ([unity.com/download](https://unity.com/download))
2. **Unity 2022 LTS** (empfohlen: `2022.3.x`) ueber Unity Hub installieren
3. In Unity Hub: `Add project` -> `F:\Meine_Apps_Ava\src\Apps\ArcaneKingdom\Unity\`
4. Beim ersten Oeffnen werden Packages aufgeloest (Firebase, Photon, Addressables, UI Toolkit, TextMeshPro) — kann mehrere Minuten dauern
5. Boot-Szene unter `Assets/_Project/Scenes/Boot/Boot.unity` oeffnen und `Play` druecken

> Hinweis: Das Unity-Projekt ist nicht Teil von `MeineApps.Ava.sln` und wird von `dotnet build` ignoriert.

---

## Warum Unity statt Avalonia?

Avalonia + SkiaSharp ist fuer 2D/UI hervorragend (siehe RebornSaga, BomberBlast). Fuer dieses Projekt fiel die Wahl trotzdem auf Unity, weil:

- **Photon** (Echtzeit-PvP-Arena, Klan-Matches) hat First-Class Unity-Support
- **Firebase Unity SDK** (Auth, Realtime Database, Cloud Messaging, Remote Config) ist deutlich reifer als die NuGet-Variante fuer mobile
- **Addressables** + **AssetBundles** (dynamisches Nachladen von ~90 Karten-Artworks + Welten-Hintergruenden) ist out-of-the-box geloest
- **iOS-Option** spaeter ohne Tech-Switch moeglich
- Riesige Asset-Store-Bibliothek (Particle-FX, Shader, Card-Tween-Libs) verkuerzt Time-to-Market
