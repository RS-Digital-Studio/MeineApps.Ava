export const meta = {
  name: 'ak-screens-angleichen',
  description: 'Restliche ArcaneKingdom-Screens an die Arcane-Realm-Referenz angleichen (Theme, Gold-Borders, Symbole raus) + adversariale Verifikation der Controller-Bindung',
  phases: [
    { title: 'Angleichen', detail: 'Pro Screen: UXML-Styling an Theme-Tokens/Klassen, Gold-Borders, Symbole raus' },
    { title: 'Verifizieren', detail: 'Adversarial: Controller-Bindung intakt, keine hardcodierten Farben, keine Symbole — fixt gefundene Probleme' },
  ],
}

const ROOT = 'F:/Meine_Apps_Ava/src/Apps/ArcaneKingdom/Unity/Assets/_Project'

const STYLE_GUIDE = `
ZIEL-DESIGN: "Arcane Realm" / Lies-of-Astaroth Dark-Fantasy. Helles, gelbliches Gold
(#f5c842) als Leitfarbe auf sehr dunklem, leicht blau-violettem Grund. Ornamentierte
Gold-Raender. Referenz: F:/AI/ComfyUI_windows_portable/ComfyUI/output/eva/Spiele Ideen Ordner/Ideen/.

VERFUEGBARE THEME-TOKENS (global via var(), in UI/Theme/Tokens.uss — NICHT neu definieren):
  Farben:  --ak-bg-deep rgb(6,6,16) | --ak-bg-base rgb(10,10,24) | --ak-bg-surface rgb(22,18,40)
           --ak-bg-surface-2 rgb(34,28,58) | --ak-bg-overlay rgba(0,0,0,0.78)
           --ak-accent rgb(245,200,66) GOLD | --ak-accent-light rgb(255,217,102) | --ak-accent-dark rgb(184,134,11)
           --ak-primary rgb(107,70,193) | --ak-primary-light rgb(139,92,246)
           --ak-danger rgb(232,64,87) | --ak-success rgb(56,197,137) | --ak-info rgb(77,166,242)
           --ak-text-primary rgb(248,245,255) | --ak-text-secondary rgb(196,188,220) | --ak-text-muted rgb(140,130,168)
  Gold-Borders: --ak-border-gold-soft rgba(245,200,66,0.18) | --ak-border-gold rgba(245,200,66,0.38) | --ak-border-gold-strong rgba(245,200,66,0.65)

VERFUEGBARE THEME-KLASSEN (Common.uss/Components.uss — bevorzugt nutzen statt Inline-Styles):
  Flaechen:    ak-surface (dunkel + gold-soft-Rand) | ak-surface-elevated (gold-Rand) | ak-divider | ak-modal-backdrop | ak-modal | ak-modal__title
  Buttons:     ak-btn (Default, gold-soft-Rand) | ak-btn--primary (Royal-Purple + Gold-Rand) | ak-btn--accent (Gold gefuellt = Haupt-CTA/Kauf) | ak-btn--ghost (transparent, Gold-Rand + Gold-Text) | ak-btn--danger | ak-btn--sm | ak-btn--lg | ak-btn--icon
  Typografie:  ak-h1/ak-h2 (GOLD, gespreizt) | ak-h3/ak-h4 (hell, bold) | ak-body | ak-caption | ak-overline (gold-dark) | ak-text--accent (Gold) | ak-text--danger | ak-text--success
  Layout:      ak-row | ak-col | ak-center | ak-center-v | ak-space-between | ak-grow | ak-hidden
  Eingaben:    ak-input (Gold-Focus)
  Fortschritt: ak-progress (+ ak-progress__fill); Modifier ak-progress--primary/--success/--danger
  Currency:    ak-currency-pill (+ --gold/--diamond/--energy, Kind: ak-currency-pill__icon/__value)

UI-HINTERGRUND-SPRITES (Resources/UI/, via UIAssetService.ApplyUIBackground(element,"<id>")):
  hub_main, login, splash, arena, zauberschmiede, tempel, gilde, star_sprite

HARTE REGELN (nicht verhandelbar):
  1. name="..."-Attribute NIEMALS aendern/entfernen/umbenennen. Der Controller bindet per
     Q<T>("name")/QOptional<T>("name"). Lies den Controller (Grep "class <ScreenName>" unter
     ${ROOT}/Scripts/UI/) und stelle sicher, dass JEDER gebundene Name im UXML weiter existiert.
  2. KEINE Emojis / Unicode-Symbole als UI-Text (Android-Tofu). Close ist bereits "X".
     Pfeile -> "zu"/Wort, Haken -> ASCII/Wort, Warnzeichen -> entfernen.
     STERNE (Bewertung/Rarity): bevorzugt durch star_sprite-Sprites rendern (14-16px VisualElements,
     gefuellt = Gold-Tint, leer = Opacity 0.3). Falls Controller-Umbau zu invasiv: am Ende melden.
  3. KEINE hardcodierten Farben (ausser semantische Element/Rarity-Farben, z.B. Natur gruen):
     - Alter BG rgb(13,13,26)/rgb(13,8,28)/rgb(22,16,41) -> var(--ak-bg-base); Header/Leisten rgb(6,6,16).
     - Orange Buttons rgb(255,122,0)/rgb(255,140,0) -> Klasse ak-btn ak-btn--accent.
     - Graue Panels rgba(255,255,255,0.04..0.08) -> Klasse ak-surface / ak-surface-elevated.
     - Weisse Raender rgba(255,255,255,0.x) -> Gold-Raender var(--ak-border-gold-soft|gold).
     - Hauptaktion-Button -> ak-btn--primary; sekundaer -> ak-btn--ghost.
  4. Layout-STRUKTUR + Hierarchie + Namen bleiben — nur Styling/Klassen angleichen und offensichtliche
     Schwaechen beheben (Touch-Targets < 44px, fehlende Rahmen, Abstaende). Titel -> ak-h2/ak-h3 Gold.
  5. Bearbeite NUR die eigene UXML + (falls noetig) ihren EIGENEN Controller. NIEMALS USS-Dateien in
     UI/Theme/ aendern. KEINE anderen Screens.
  6. Controller-Edits nur fuer: (a) ApplyUIBackground(root,"<bg>") in BindElements falls BG-Sprite passt;
     (b) Symbol-Ersatz in generierten Strings. Logik/DI/Bindings NICHT veraendern.
`

const SCREENS = [
  { name: 'ArenaScreen',           uxml: `${ROOT}/Resources/UI/ArenaScreen.uxml`,            bg: 'arena' },
  { name: 'BattleScreen',          uxml: `${ROOT}/Resources/UI/BattleScreen.uxml`,           bg: null },
  { name: 'CodexScreen',           uxml: `${ROOT}/Resources/UI/CodexScreen.uxml`,            bg: null },
  { name: 'FriendsScreen',         uxml: `${ROOT}/Resources/UI/FriendsScreen.uxml`,          bg: null },
  { name: 'PackOpeningModal',      uxml: `${ROOT}/Resources/UI/PackOpeningModal.uxml`,       bg: null },
  { name: 'SaisonPassScreen',      uxml: `${ROOT}/Resources/UI/SaisonPassScreen.uxml`,       bg: null },
  { name: 'SettingsScreen',        uxml: `${ROOT}/Resources/UI/SettingsScreen.uxml`,         bg: null },
  { name: 'TutorialOverlay',       uxml: `${ROOT}/Resources/UI/TutorialOverlay.uxml`,        bg: null },
  { name: 'WorldMapScreen',        uxml: `${ROOT}/Resources/UI/WorldMapScreen.uxml`,         bg: null, stars: true },
  { name: 'GuildScreen',           uxml: `${ROOT}/Resources/UI/GuildScreen.uxml`,            bg: 'gilde' },
  { name: 'SchmiedeScreen',        uxml: `${ROOT}/Resources/UI/SchmiedeScreen.uxml`,         bg: 'zauberschmiede' },
  { name: 'TempelScreen',          uxml: `${ROOT}/Resources/UI/TempelScreen.uxml`,           bg: 'tempel' },
  { name: 'PrestigeUpgradeModal',  uxml: `${ROOT}/Resources/UI/Modals/PrestigeUpgradeModal.uxml`, bg: null },
  { name: 'DifficultyPickerModal', uxml: `${ROOT}/Resources/UI/DifficultyPickerModal.uxml`,  bg: null, stars: true },
  { name: 'RuneScreen',            uxml: `${ROOT}/Resources/UI/RuneScreen.uxml`,             bg: null },
  { name: 'PlayerProfileScreen',   uxml: `${ROOT}/Resources/UI/PlayerProfileScreen.uxml`,    bg: null },
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

log(`Gleiche ${SCREENS.length} Screens an die Arcane-Realm-Referenz an (parallel, mit adversarialer Verifikation).`)

const results = await pipeline(
  SCREENS,
  // Stage 1: Angleichen (Text-Output, KEIN Schema)
  (s) => agent(
    `Du gleichst den ArcaneKingdom-Screen "${s.name}" vollstaendig an das Arcane-Realm-Referenz-Design an.

UXML-Datei (DIESE Datei editierst du): ${s.uxml}
${s.bg ? `Passendes UI-Background-Sprite: "${s.bg}" — setze es im Controller via _uiAssets.ApplyUIBackground(root, "${s.bg}") in BindElements, falls UIAssetService verfuegbar und sauber einfuegbar.` : 'Kein spezifisches Background-Sprite — nutze dunklen Token-BG (var(--ak-bg-base); Header/Leisten rgb(6,6,16)).'}
${s.stars ? 'ACHTUNG: Dieser Screen zeigt Bewertungs-/Rarity-STERNE als Text. Ersetze sie nach Moeglichkeit durch star_sprite-Sprites (Regel 2).' : ''}

${STYLE_GUIDE}

WICHTIG ZUM VORGEHEN:
- Diese Datei hat mit hoher Wahrscheinlichkeit Angleichungsbedarf (alte BG-Farben rgb(13,13,26),
  graue rgba(255,255,255,..)-Panels, weisse Raender, hardcodierte Buttons, Inline-Farben).
- Reihenfolge strikt: (1) UXML lesen. (2) Controller per Grep finden + lesen, gebundene Namen notieren.
  (3) ALLE noetigen Edits am UXML (und ggf. eigenem Controller) DURCHFUEHREN mit dem Edit-Tool.
  (4) Final gegenpruefen, dass jeder gebundene Name noch im UXML existiert.
- Mache die Edits TATSAECHLICH (nicht nur beschreiben). Es gibt KEIN Output-Tool — beende einfach mit
  einer knappen Text-Zusammenfassung: welche Edits gemacht, welche gebundenen Namen verifiziert,
  ob der Controller geaendert wurde, welche Symbole/Sterne (falls) noch offen sind.`,
    { label: `style:${s.name}`, phase: 'Angleichen' }
  ),
  // Stage 2: Adversarial verifizieren + selbst fixen (Text-Output)
  (r, s) => agent(
    `Adversariale Verifikation + Korrektur des bereits angeglichenen ArcaneKingdom-Screens "${s.name}".
UXML: ${s.uxml}
Bericht der Angleichungs-Stufe:
${r}

Pruefe HART und behebe gefundene Probleme SELBST mit dem Edit-Tool (nicht nur melden):
1. CONTROLLER-BINDUNG (kritisch): Finde den Controller (Grep "class ${s.name}" unter ${ROOT}/Scripts/UI/).
   Sammle ALLE Q<...>("name")/QOptional<...>("name")-Aufrufe. Verifiziere, dass JEDER Name als name="..."
   im UXML existiert. Fehlt einer -> Screen crasht/bricht -> im UXML wiederherstellen.
2. Keine hardcodierten Farben mehr (ausser semantische Element/Rarity-Farben) -> durch Tokens/Klassen ersetzen.
3. Keine Emoji/Unicode-Symbole als UI-Text (UXML + Controller-generierte Strings). Sterne via Sprite oder melden.
4. Theme-Konsistenz: Panels = ak-surface, Buttons = ak-btn-Varianten, Titel = ak-h2/h3 Gold, dunkler Token-BG.
5. UXML wohlgeformt (gueltiges XML, keine kaputten Tags/Attribute).

${STYLE_GUIDE}

Beende mit knapper Text-Zusammenfassung: bindingIntact (ja/nein + welche Namen geprueft), welche Fixes du
angewandt hast, verbleibende hardcodierte Farben (Anzahl), verbleibende Symbole, offene Probleme.`,
    { label: `verify:${s.name}`, phase: 'Verifizieren' }
  )
)

const clean = results.filter(Boolean)
log(`Fertig: ${clean.length}/${SCREENS.length} Screens durchlaufen.`)

return {
  total: SCREENS.length,
  completed: clean.length,
  reports: SCREENS.map((s, i) => ({ screen: s.name, verify: results[i] || 'FEHLGESCHLAGEN' })),
}
