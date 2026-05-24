# Scenes — Erste Schritte im Unity-Editor

Diese Doku beschreibt, was beim **ersten Oeffnen** des Unity-Projekts noch
manuell in jeder Szene zu verdrahten ist. Code und Skript-Referenzen sind
vorhanden — die GameObjects in den Szenen muessen aber **per Hand**
zugewiesen werden, weil Scene-YAML-GUIDs zwischen Maschinen abweichen koennen.

## Boot.unity — Pflichtschritte beim ersten Open

1. **Boot.unity** unter `Assets/_Project/Scenes/Boot/` oeffnen.
2. Auf das GameObject `[Bootstrapper]` (in der Hierarchy) klicken.
3. Im Inspector **Add Component → ArcaneKingdom.Bootstrap.RootLifetimeScope** anhaengen.
4. Im RootLifetimeScope-Inspector den Slot **Balancing Config** auf
   `Assets/_Project/ScriptableObjects/Config/BalancingConfig.asset` ziehen.
5. Ein zweites Kind-GameObject `[Audio]` unter Bootstrapper anlegen, dort
   **Add Component → ArcaneKingdom.Game.Services.UnityAudioService** anhaengen.
   Den Slot **Audio Service** im RootLifetimeScope auf dieses Objekt ziehen.
6. Boot.unity speichern und **in Build Settings als 1. Szene** eintragen.

Danach laeuft `Play`:
- Splash-Screen + Auto-Login (Stub) → "ArcaneKingdom gestartet" im Console-Log
- Spaeter: laedt Hub.unity additiv, wenn dort eine Szene angelegt ist.

## Weitere Scenes

| Szene | Status | Verantwortung |
|-------|--------|--------------|
| `Hub/Hub.unity` | nicht angelegt | wird in MVP-Phase erstellt |
| `Battle/Battle.unity` | nicht angelegt | wird in MVP-Phase erstellt |
| `Arena/Arena.unity` | nicht angelegt | wird in MVP-Phase erstellt |
| `Guild/Guild.unity` | nicht angelegt | wird in MVP-Phase erstellt |
| `GuildWorld/GuildWorld.unity` | nicht angelegt | wird in MVP-Phase erstellt |

Scene-Namen sind in `Assets/_Project/Scripts/Game/Bootstrap/SceneNames.cs`
als Konstanten definiert — dort aktualisieren falls Namen geaendert werden.
