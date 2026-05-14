# Content-Pipeline

Ziel: Game-Designer und Übersetzer arbeiten in **Google Sheets**, nicht im Code.

## Setup (einmalig)

1. Google Cloud Console → Service Account anlegen, Sheets API aktivieren
2. JSON-Schlüssel herunterladen → `~/.config/handwerkerimperium-content.json`
3. Sheet anlegen mit zwei Worksheets:
   - **Story-Chapters** (Schema siehe `sync_content.py` Header)
   - **Battle-Pass** (zukünftig)
4. Service-Account-E-Mail als Reader auf das Sheet einladen
5. Sheet-ID aus URL kopieren (`https://docs.google.com/spreadsheets/d/<SHEET_ID>/edit`)

## Lokal syncen

```bash
pip install gspread google-auth-oauthlib pandas
python tools/ContentPipeline/sync_content.py \
    --sheet-id <SHEET_ID> \
    --output-dir src/Apps/HandwerkerImperium
```

## Erzeugt

- `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/SeasonStorylineCatalog.generated.cs` (partial class — wird vom Compiler mit dem manuellen Catalog kombiniert)
- Patches in 6 RESX-Files (`AppStrings.{de,en,es,fr,it,pt}.resx`)

## CI-Hook (Robert TODO)

In `.github/workflows/ci.yml` ergänzen:

```yaml
- name: Content-Pipeline-Drift-Check
  run: python tools/ContentPipeline/sync_content.py --check-only --output-dir src/Apps/HandwerkerImperium
```

Damit bricht der Build ab, falls jemand RESX-Strings manuell ändert ohne sie ins Sheet einzupflegen.

## Status

**Skelett:** Skript läuft im Stub-Modus (ohne `--sheet-id`). Live-Anbindung erfordert Robert's Sheet-Setup + CI-Token.
