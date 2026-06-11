# Scenes

Dieser Ordner hält die Unity-Szenen von BomberBlast.Unity.

## Boot-Scene beim ersten Editor-Open anlegen

Die `Boot.unity`-Szene wird **nicht** von Hand als YAML gebaut (handgeschriebene Scene-YAML
mit falschen GUIDs bricht Referenzen). Stattdessen beim ersten Öffnen des Projekts im Editor:

1. Projekt im Unity Hub hinzufügen und mit Unity 6000.4.8f1 öffnen.
2. Neue Szene anlegen (`File -> New Scene`) und als `Assets/_Project/Scenes/Boot.unity` speichern.
3. Ein leeres GameObject `[Bootstrapper]` anlegen und die `RootLifetimeScope`-Komponente
   (`BomberBlast.Bootstrap`) daran hängen.
4. `Boot.unity` in `File -> Build Settings` als erste Szene (Index 0) eintragen.

Weitere Szenen (MainMenu, Game, Cinematic, Tutorial) folgen gemäß
[ARCHITECTURE.md](../../../ARCHITECTURE.md) Kapitel 5 (Scene-Architektur).
