# Scenes

> **Hinweis (8.6.2026):** Tech-Scaffold unverändert gültig. Das **Spiel-Design** folgt dem neuen GDD
> ([3D_IDLE_GAME_PLAN.md](../../../../3D_IDLE_GAME_PLAN.md), 3D-Walk-around-Idle): die Szenen-Hierarchie ist der
> **begehbare Werkstatt-Stadt-Hub** + additive Szenen (gemäß ARCHITECTURE.md § 4). „MiniGame" ist hier nur noch
> optionaler Boost (GDD §6.7), keine Pflicht-Szene je Auftrag.

Dieser Ordner ist beim Scaffold absichtlich leer (nur `.gitkeep`). Die `Boot.unity`-Scene wird
**nicht** von Hand als YAML angelegt, weil per Hand geschriebene Scene-/`.meta`-GUIDs Referenzen
brechen.

## Beim ersten Editor-Open anlegen

1. Projekt in Unity 6000.4.8f1 öffnen (Unity Hub -> Add project ->
   `F:\Meine_Apps_Ava\src\Apps\HandwerkerImperium.Unity\Unity\`). Unity generiert beim ersten Open
   alle `.meta`-Dateien.
2. Neue Scene anlegen und als `Boot.unity` in diesem Ordner speichern.
3. Boot-Scene als Einstiegs-Scene gemäß [ARCHITECTURE.md](../../../../ARCHITECTURE.md) § 4
   einrichten (RootLifetimeScope, PersistentCanvas, AudioListener, PersistentEventSystem).
4. Das `RootLifetimeScope`-Skript (`Assets/_Project/Scripts/Bootstrap/RootLifetimeScope.cs`) auf das
   Bootstrap-GameObject ziehen.

Die weitere Scene-Hierarchie (Hub, Workshop, MiniGame) folgt beim Game-Port,
ebenfalls gemäß ARCHITECTURE.md § 4.
