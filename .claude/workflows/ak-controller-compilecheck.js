export const meta = {
  name: 'ak-controller-compilecheck',
  description: 'Adversariale Compile-Korrektheit-Pruefung der in dieser Session geaenderten ArcaneKingdom-Controller (MCP-unabhaengig)',
  phases: [
    { title: 'Compile-Check', detail: 'Pro Controller ein Agent: C#-Syntax/Typen/usings/API/Bindung pruefen + fixen' },
  ],
}

const ROOT = 'F:/Meine_Apps_Ava/src/Apps/ArcaneKingdom/Unity/Assets/_Project'

const CONTROLLERS = [
  `${ROOT}/Scripts/UI/Hub/HubScreen.cs`,
  `${ROOT}/Scripts/UI/Friends/FriendsScreen.cs`,
  `${ROOT}/Scripts/UI/PlayerProfile/PlayerProfileScreen.cs`,
  `${ROOT}/Scripts/UI/BattleReport/BattleReportScreen.cs`,
  `${ROOT}/Scripts/UI/Modals/DifficultyPickerModal.cs`,
  `${ROOT}/Scripts/UI/Schmiede/SchmiedeScreen.cs`,
  `${ROOT}/Scripts/UI/Tempel/TempelScreen.cs`,
  `${ROOT}/Scripts/UI/WorldMap/WorldMapScreen.cs`,
  `${ROOT}/Scripts/UI/DeckBuilder/DeckBuilderScreen.cs`,
  `${ROOT}/Scripts/UI/RaceSelection/RaceSelectionScreen.cs`,
]

log(`Adversariale Compile-Pruefung von ${CONTROLLERS.length} geaenderten Controllern.`)

const reports = await parallel(CONTROLLERS.map((path) => () => agent(
  `Du bist ein strenger C#-Compiler-Simulator fuer Unity 6 (.NET Standard 2.1, C#, UnityEngine + UnityEngine.UIElements,
UniTask, VContainer). Pruefe die folgende, kuerzlich geaenderte Datei adversarial auf JEDEN moeglichen
Compile-Fehler und behebe gefundene Fehler SELBST mit dem Edit-Tool.

Datei: ${path}

Pruefe systematisch:
1. SYNTAX: Klammern/Semikolons/Generics balanciert, keine kaputten Statements, gueltige String-Interpolation.
2. USINGS: Jeder genutzte Typ ist via using importiert ODER voll-qualifiziert. Pruefe besonders neu genutzte
   Typen (z.B. UnityEngine.UIElements: VisualElement, Button, Label, Length, LengthUnit, StyleEnum, DisplayStyle,
   ScaleMode, Background, StyleBackground; UnityEngine: Mathf, Color, Sprite, Texture2D).
3. API-EXISTENZ: Verifiziere per Grep in der Codebase (unter ${ROOT}), dass aufgerufene Methoden/Properties/Felder
   wirklich existieren und die richtige Signatur haben — insbesondere bei UIAssetService (z.B. GetStarSprite(),
   ApplyBackground(...), ApplyUIBackground(...)), bei Domain-Typen (PlayerCurrencies, Profile, CardInstance,
   PrestigeStufe, Rarity), und bei ScreenBase-Helpern (Q<T>, QOptional<T>). Falsch benannte/nicht existierende
   Member sind Compile-Fehler -> korrigieren.
4. NULLABILITY: #nullable enable beachtet? Keine offensichtlichen CS86xx-Brueche durch neue Zeilen.
5. STERN-SPRITE-UMBAU (falls vorhanden): Wenn Stern-Glyphen durch star_sprite-Sprites ersetzt wurden, pruefe dass
   die Sprite-Lade-API real existiert (UIAssetService) und die zurueckgegebenen Typen korrekt verwendet werden
   (Sprite -> StyleBackground/Background fuer VisualElement.style.backgroundImage). KEIN new string('*', n) mehr,
   das Glyphen erzeugt.
6. BINDUNG: Q<...>("name")/QOptional<...>("name") — die required Q<> duerfen nur fuer Elemente genutzt werden, die
   im zugehoerigen UXML existieren ODER zur Laufzeit erzeugt werden. (Reine Info — UXML nicht aendern.)

Wenn du einen echten Compile-Fehler findest: behebe ihn minimal-invasiv (nur Korrektheit, keine Logik-/Design-
Aenderung). Wenn alles kompiliert: nichts aendern.

Beende mit knapper Text-Zusammenfassung: compiles (ja/nein), gefundene+behobene Fehler (Liste), unsichere Stellen.`,
  { label: `cc:${path.split('/').pop()}`, phase: 'Compile-Check' }
)))

const done = reports.filter(Boolean)
log(`Compile-Pruefung fertig: ${done.length}/${CONTROLLERS.length}.`)

return {
  total: CONTROLLERS.length,
  completed: done.length,
  reports: CONTROLLERS.map((c, i) => ({ controller: c.split('/').pop(), report: reports[i] || 'FEHLGESCHLAGEN' })),
}
