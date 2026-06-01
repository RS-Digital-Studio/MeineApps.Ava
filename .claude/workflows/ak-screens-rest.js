export const meta = {
  name: 'ak-screens-rest',
  description: 'Verbleibende 19 ArcaneKingdom-Screens an die Arcane-Realm-Referenz angleichen (1 Agent/Screen, weniger Rate-Limit-Druck)',
  phases: [
    { title: 'Angleichen', detail: 'Pro Screen ein Agent: UXML an Theme angleichen, Symbole raus, Bindung selbst gegenpruefen' },
  ],
}

const ROOT = 'F:/Meine_Apps_Ava/src/Apps/ArcaneKingdom/Unity/Assets/_Project'

const STYLE_GUIDE = `
ZIEL-DESIGN: "Arcane Realm" / Lies-of-Astaroth Dark-Fantasy. Helles, gelbliches Gold
(#f5c842) als Leitfarbe auf sehr dunklem, leicht blau-violettem Grund. Ornamentierte Gold-Raender.

VERFUEGBARE THEME-TOKENS (global via var(), in UI/Theme/Tokens.uss — NICHT neu definieren):
  Farben:  --ak-bg-deep rgb(6,6,16) | --ak-bg-base rgb(10,10,24) | --ak-bg-surface rgb(22,18,40)
           --ak-bg-surface-2 rgb(34,28,58) | --ak-bg-overlay rgba(0,0,0,0.78)
           --ak-accent rgb(245,200,66) GOLD | --ak-accent-light rgb(255,217,102) | --ak-accent-dark rgb(184,134,11)
           --ak-primary rgb(107,70,193) | --ak-primary-light rgb(139,92,246)
           --ak-danger rgb(232,64,87) | --ak-success rgb(56,197,137) | --ak-info rgb(77,166,242)
           --ak-text-primary rgb(248,245,255) | --ak-text-secondary rgb(196,188,220) | --ak-text-muted rgb(140,130,168)
  Gold-Borders: --ak-border-gold-soft rgba(245,200,66,0.18) | --ak-border-gold rgba(245,200,66,0.38) | --ak-border-gold-strong rgba(245,200,66,0.65)

VERFUEGBARE THEME-KLASSEN (Common.uss/Components.uss — bevorzugt nutzen):
  Flaechen:    ak-surface | ak-surface-elevated | ak-divider | ak-modal-backdrop | ak-modal | ak-modal__title
  Buttons:     ak-btn | ak-btn--primary | ak-btn--accent (Gold-CTA/Kauf) | ak-btn--ghost (Gold-Rand+Gold-Text) | ak-btn--danger | ak-btn--sm | ak-btn--lg | ak-btn--icon
  Typografie:  ak-h1/ak-h2 (GOLD) | ak-h3/ak-h4 (hell) | ak-body | ak-caption | ak-overline | ak-text--accent/--danger/--success
  Layout:      ak-row | ak-col | ak-center | ak-center-v | ak-space-between | ak-grow | ak-hidden
  Eingaben:    ak-input | Fortschritt: ak-progress (+ __fill; --primary/--success/--danger)
  Currency:    ak-currency-pill (+ --gold/--diamond/--energy; Kind ak-currency-pill__icon/__value)

UI-HINTERGRUND-SPRITES (via _uiAssets.ApplyUIBackground(root,"<id>")): hub_main, login, splash, arena, zauberschmiede, tempel, gilde, star_sprite

HARTE REGELN:
  1. name="..."-Attribute NIE aendern/entfernen. Controller bindet per Q<T>("name")/QOptional<T>("name").
     Lies den Controller (Grep "class <ScreenName>" unter ${ROOT}/Scripts/UI/) und stelle sicher, dass JEDER
     gebundene Name im UXML bleibt.
  2. KEINE Emojis/Unicode-Symbole als UI-Text (Android-Tofu). Close="X" schon erledigt. Pfeile->"zu"/Wort,
     Haken->Wort/ASCII, Warnzeichen->weg. STERNE (Bewertung/Rarity): bevorzugt star_sprite-Sprites (14-16px
     VisualElements; gefuellt=Gold-Tint, leer=Opacity 0.3); falls Controller-Umbau zu invasiv: am Ende melden.
  3. KEINE hardcodierten Farben (ausser semantische Element/Rarity-Farben): Alt-BG rgb(13,13,26)/rgb(13,8,28)/
     rgb(22,16,41) -> var(--ak-bg-base) (Leisten/Header rgb(6,6,16)); orange Buttons -> ak-btn--accent;
     graue Panels rgba(255,255,255,0.0x) -> ak-surface; weisse Raender -> var(--ak-border-gold-soft|gold).
  4. Layout-Struktur + Hierarchie + Namen bleiben. Nur Styling/Klassen + offensichtliche Schwaechen (Touch <44px,
     fehlende Rahmen, Abstaende). Titel -> ak-h2/ak-h3 Gold.
  5. NUR die eigene UXML + (falls noetig) ihren EIGENEN Controller editieren. NIE USS in UI/Theme/. KEINE anderen Screens.
  6. Controller-Edits nur: (a) ApplyUIBackground(root,"<bg>") falls BG-Sprite passt UND _uiAssets/UIAssetService
     bereits injiziert ist (sonst lassen — KEINEN DI-Eingriff); (b) Symbol-Ersatz in generierten Strings.
`

const SCREENS = [
  { name: 'CodexScreen',           uxml: `${ROOT}/Resources/UI/CodexScreen.uxml`,            bg: null },
  { name: 'PackOpeningModal',      uxml: `${ROOT}/Resources/UI/PackOpeningModal.uxml`,       bg: null },
  { name: 'SettingsScreen',        uxml: `${ROOT}/Resources/UI/SettingsScreen.uxml`,         bg: null },
  { name: 'TutorialOverlay',       uxml: `${ROOT}/Resources/UI/TutorialOverlay.uxml`,        bg: null },
  { name: 'WorldMapScreen',        uxml: `${ROOT}/Resources/UI/WorldMapScreen.uxml`,         bg: null, stars: true },
  { name: 'SchmiedeScreen',        uxml: `${ROOT}/Resources/UI/SchmiedeScreen.uxml`,         bg: 'zauberschmiede' },
  { name: 'TempelScreen',          uxml: `${ROOT}/Resources/UI/TempelScreen.uxml`,           bg: 'tempel' },
  { name: 'PrestigeUpgradeModal',  uxml: `${ROOT}/Resources/UI/Modals/PrestigeUpgradeModal.uxml`, bg: null },
  { name: 'DifficultyPickerModal', uxml: `${ROOT}/Resources/UI/DifficultyPickerModal.uxml`,  bg: null, stars: true },
  { name: 'ShopScreen',            uxml: `${ROOT}/Resources/UI/ShopScreen.uxml`,             bg: null },
  { name: 'QuestCenterScreen',     uxml: `${ROOT}/Resources/UI/QuestCenterScreen.uxml`,      bg: null },
  { name: 'MeritRankingScreen',    uxml: `${ROOT}/Resources/UI/MeritRankingScreen.uxml`,     bg: null },
  { name: 'BattleReportScreen',    uxml: `${ROOT}/Resources/UI/BattleReportScreen.uxml`,     bg: null, stars: true },
  { name: 'ThiefScreen',           uxml: `${ROOT}/Resources/UI/ThiefScreen.uxml`,            bg: null },
  { name: 'GuildWorldMapScreen',   uxml: `${ROOT}/Resources/UI/GuildWorldMapScreen.uxml`,    bg: null },
  { name: 'ChatOverlay',           uxml: `${ROOT}/Resources/UI/ChatOverlay.uxml`,            bg: null },
  { name: 'PvpMatchmakingScreen',  uxml: `${ROOT}/Resources/UI/PvpMatchmakingScreen.uxml`,   bg: null },
  { name: 'CollectionTradeScreen', uxml: `${ROOT}/Resources/UI/CollectionTradeScreen.uxml`,  bg: null },
  { name: 'MemoryFragmentModal',   uxml: `${ROOT}/Resources/UI/Modals/MemoryFragmentModal.uxml`, bg: null },
]

log(`Gleiche ${SCREENS.length} verbleibende Screens an die Arcane-Realm-Referenz an (1 Agent/Screen).`)

const reports = await parallel(SCREENS.map((s) => () => agent(
  `Du gleichst den ArcaneKingdom-Screen "${s.name}" vollstaendig an das Arcane-Realm-Referenz-Design an und
verifizierst deine Arbeit selbst.

UXML-Datei (DIESE editierst du): ${s.uxml}
${s.bg ? `Passendes UI-Background-Sprite: "${s.bg}" — setze es via _uiAssets.ApplyUIBackground(root, "${s.bg}") in BindElements NUR falls _uiAssets/UIAssetService dort bereits injiziert ist (sonst dunkler Token-BG).` : 'Kein BG-Sprite — nutze dunklen Token-BG var(--ak-bg-base) (Leisten/Header rgb(6,6,16)).'}
${s.stars ? 'ACHTUNG: zeigt Bewertungs-/Rarity-STERNE als Text — ersetze via star_sprite-Sprites (Regel 2) oder melde am Ende, falls Controller-Umbau zu invasiv.' : ''}

${STYLE_GUIDE}

VORGEHEN (strikt): (1) UXML lesen. (2) Controller per Grep finden + lesen, ALLE gebundenen Namen
(Q/QOptional) notieren. (3) ALLE noetigen Edits am UXML mit dem Edit-Tool DURCHFUEHREN (alte BG-Farben,
graue Panels, weisse Raender, hardcodierte Buttons/Inline-Farben, Symbole). (4) SELBST gegenpruefen:
jeder gebundene Name noch im UXML? XML wohlgeformt? keine Symbole/Alt-BG mehr?

Mache die Edits TATSAECHLICH (nicht nur beschreiben). Es gibt KEIN Output-Tool — beende mit knapper
Text-Zusammenfassung: Edits, gepruefte gebundene Namen (bindingIntact ja/nein), Controller geaendert?,
verbleibende Symbole/Sterne.`,
  { label: `style:${s.name}`, phase: 'Angleichen' }
)))

const done = reports.filter(Boolean)
log(`Fertig: ${done.length}/${SCREENS.length} Screens durchlaufen.`)

return {
  total: SCREENS.length,
  completed: done.length,
  reports: SCREENS.map((s, i) => ({ screen: s.name, report: reports[i] || 'FEHLGESCHLAGEN' })),
}
