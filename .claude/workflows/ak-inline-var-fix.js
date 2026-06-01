export const meta = {
  name: 'ak-inline-var-fix',
  description: 'Inline var(--token) in ArcaneKingdom-UXML durch Theme-Utility-KLASSEN ersetzen (Root-Cause: Unity crasht bei inline var() beim Instantiate)',
  phases: [
    { title: 'Klassen-Migration', detail: 'Pro Screen: inline var() raus, Theme-Utility-Klassen ans class-Attribut' },
  ],
}

const ROOT = 'F:/Meine_Apps_Ava/src/Apps/ArcaneKingdom/Unity/Assets/_Project'

const RULES = `
HINTERGRUND: Unity UI Toolkit loest var(--token) NUR in USS-Regeln auf, NICHT in Inline-Styles
(style="..." im UXML). Inline var() crasht beim VisualTreeAsset.Instantiate (StyleVariableResolver
-> NullReferenceException). Loesung: Theme-Werte ueber USS-KLASSEN statt inline var(). Diese Utility-
Klassen existieren bereits in Common.uss (NICHT neu definieren).

DEINE AUFGABE: Ersetze in der UXML JEDES inline "<property>: var(--token);" so:
  1. Entferne die Property "<property>: var(--token);" aus dem style="..."-Attribut.
  2. Fuege die passende KLASSE (Tabelle unten) zum class="..."-Attribut des SELBEN Elements hinzu
     (anhaengen mit Leerzeichen, falls schon ein class-Attribut da ist; sonst class="..." neu anlegen).
  3. ALLE anderen Inline-Styles (border-width, padding, margin, width, height, flex-*, font-size,
     -unity-*, position, top/left/..., translate, scale-mode etc.) BLEIBEN unveraendert inline.
  4. border-WIDTH bleibt inline — nur die border-*-COLOR wird zur Klasse.

MAPPING-TABELLE (property : var(--token)  ->  Klasse):
  background-color: var(--ak-bg-deep)        -> ak-bg-deep
  background-color: var(--ak-bg-base)        -> ak-bg-base
  background-color: var(--ak-bg-surface)     -> ak-bg-surface
  background-color: var(--ak-bg-surface-2)   -> ak-bg-surface-2
  background-color: var(--ak-bg-overlay)     -> ak-bg-overlay
  background-color: var(--ak-accent)         -> ak-bg-accent
  background-color: var(--ak-primary)        -> ak-bg-primary
  background-color: var(--ak-danger)         -> ak-bg-danger

  border-color: var(--ak-border-gold-soft)   -> ak-bd-gold-soft
  border-color: var(--ak-border-gold)        -> ak-bd-gold
  border-color: var(--ak-border-gold-strong) -> ak-bd-gold-strong
  border-color: var(--ak-danger)             -> ak-bd-danger

  border-bottom-color: var(--ak-border-gold-soft) -> ak-bd-b-gold-soft
  border-bottom-color: var(--ak-border-gold)       -> ak-bd-b-gold
  border-bottom-color: var(--ak-accent)            -> ak-bd-b-accent
  border-bottom-color: var(--ak-danger)            -> ak-bd-b-danger
  border-bottom-color: var(--ak-success)           -> ak-bd-b-success

  color: var(--ak-text-muted)        -> ak-text--muted
  color: var(--ak-text-secondary)    -> ak-text--secondary
  color: var(--ak-accent)            -> ak-text--accent
  color: var(--ak-accent-light)      -> ak-text--accent-light
  color: var(--ak-primary-light)     -> ak-text--primary-light
  color: var(--ak-success)           -> ak-text--success

  border-radius: var(--ak-radius-lg) -> ak-rad-lg
  border-radius: var(--ak-radius-sm) -> ak-rad-sm
  border-radius: var(--ak-radius-md) -> ak-rad-md
  border-radius: var(--ak-radius-xl) -> ak-rad-xl

SONDERFALL Mehr-Seiten-Rahmen: Wenn ein Element ALLE VIER Seiten einzeln mit DEMSELBEN Token faerbt
  (border-left-color + border-right-color + border-top-color + border-bottom-color: var(--TOK)),
  ersetze ALLE VIER durch EINE all-sides-Klasse:
    var(--ak-accent)          -> ak-bd-accent
    var(--ak-border-gold)     -> ak-bd-gold
    var(--ak-border-gold-soft)-> ak-bd-gold-soft
    var(--ak-border-gold-strong)-> ak-bd-gold-strong
    var(--ak-danger)          -> ak-bd-danger
    var(--ak-success)         -> ak-bd-success
  (Die border-*-width bleiben inline.)

HARTE REGELN:
  - name="..."-Attribute NIE aendern.
  - Nach deiner Aenderung darf die Datei KEIN "var(--" mehr enthalten (grep-sauber).
  - KEINE hardcodierten rgb/rgba-Farben einfuegen (das waere Verschleierung). NUR Klassen.
  - XML muss wohlgeformt bleiben (gueltige Tags/Attribute, ein Root).
  - Nur DIESE eine UXML editieren, sonst nichts.
`

const SCREENS = [
  'ArenaScreen','BattleReportScreen','BattleScreen','ChatOverlay','DifficultyPickerModal',
  'FriendsScreen','PvpMatchmakingScreen','QuestCenterScreen','SaisonPassScreen','SchmiedeScreen',
  'WorldMapScreen',
].map(n => ({ name: n, uxml: `${ROOT}/Resources/UI/${n}.uxml` }))
SCREENS.push({ name: 'MemoryFragmentModal', uxml: `${ROOT}/Resources/UI/Modals/MemoryFragmentModal.uxml` })

log(`Migriere inline var() -> Utility-Klassen in ${SCREENS.length} Screens.`)

const reports = await parallel(SCREENS.map((s) => () => agent(
  `Migriere inline var() im ArcaneKingdom-UXML "${s.name}" auf Theme-Utility-Klassen.

Datei (NUR diese editieren): ${s.uxml}

${RULES}

VORGEHEN: (1) Datei lesen. (2) Jedes inline "<property>: var(--token)" gemaess Mapping-Tabelle umstellen
(Property aus style entfernen, Klasse ans class-Attribut). (3) Gegenpruefen: kein "var(--" mehr in der Datei,
XML wohlgeformt, alle name="..." unveraendert, keine hardcodierten Farben eingefuegt.

Mache die Edits TATSAECHLICH mit dem Edit-Tool. Es gibt KEIN Output-Tool — beende mit knapper
Text-Zusammenfassung: wie viele var() ersetzt, welche Klassen hinzugefuegt, "var(-- jetzt 0?" ja/nein.`,
  { label: `varfix:${s.name}`, phase: 'Klassen-Migration' }
)))

const done = reports.filter(Boolean)
log(`Fertig: ${done.length}/${SCREENS.length} Screens migriert.`)
return { total: SCREENS.length, completed: done.length, reports: SCREENS.map((s, i) => ({ screen: s.name, report: reports[i] || 'FEHLGESCHLAGEN' })) }
