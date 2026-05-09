#!/usr/bin/env python3
"""
P2.1 AAA-Audit (08.05.2026): Content-Pipeline Skelett — Google Sheets als Source-of-Truth.

Game-Designer und Übersetzer arbeiten in einer Spreadsheet-Tabelle. Dieses Skript
synchronisiert:
- Story-Kapitel: Sheet "Story-Chapters" -> SeasonStorylineCatalog.generated.cs
- Übersetzungen: Sheet-Spalten de/en/es/fr/it/pt -> 6 RESX-Patches
- Battle-Pass-Tiers: Sheet "Battle-Pass" -> battlepass.generated.cs

USAGE:
    pip install gspread google-auth-oauthlib pandas
    python sync_content.py --sheet-id <GOOGLE_SHEET_ID> --output-dir ../../src/Apps/HandwerkerImperium

VERFEINERUNG (Robert TODO):
1. Google-Sheets-API-Credentials in ~/.config/handwerkerimperium-content.json hinterlegen
2. Sheet anlegen mit den unten dokumentierten Spalten
3. CI-Hook in .github/workflows/ci.yml ergänzen:
       - run: python tools/ContentPipeline/sync_content.py --check-only
   damit ungesyncte Änderungen den Build brechen
4. Battle-Pass-Generator dazubauen (analoge Struktur wie Story)
"""

import argparse
import os
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

# Optional dependencies — Skript läuft auch ohne Google-Sheets-Anbindung im "dry-run" Modus.
try:
    import gspread  # type: ignore
    from google.oauth2.service_account import Credentials  # type: ignore
    GSPREAD_AVAILABLE = True
except ImportError:
    GSPREAD_AVAILABLE = False


# ─────────────────────────────────────────────────────────────────────
# SHEET-SCHEMA (Spalten-Konvention)
# ─────────────────────────────────────────────────────────────────────
# Sheet "Story-Chapters":
#   season_id | chapter_idx | chapter_id      | title_key            | text_key             | de    | en    | es | fr | it | pt | required_quickjobs | required_battlepass_tier | required_season_theme | money_reward | screw_reward | xp_reward
#   spring    | 1           | spring_ch1      | Spring1Title         | Spring1Text          | "Der  | "The  | …  | …  | …  | …  | 0                  | 1                        | Spring                | 0            | 0            | 0
#   spring    | 2           | spring_ch2      | Spring2Title         | Spring2Text          | …     | …     | …  | …  | …  | …  | 1                  | 10                       | Spring                | 5000         | 25           | 0
#
# Sheet "Battle-Pass":
#   season_number | tier | type     | reward_amount | reward_currency | de_label | en_label | …
#   1             | 1    | Money    | 1000          |                  | …        | …
#   1             | 5    | Screws   | 50            | golden_screws    | …        | …
#   1             | 10   | TierBoost| 1             |                  | …        | …
# ─────────────────────────────────────────────────────────────────────


# ─────────────────────────────────────────────────────────────────────
# RESX-PATCHING
# ─────────────────────────────────────────────────────────────────────
RESX_HEADER_NAMESPACE = "{http://www.w3.org/XML/1998/namespace}"
RESX_LANGUAGES = ["de", "en", "es", "fr", "it", "pt"]


def load_resx(path: Path) -> ET.ElementTree:
    """Liest eine RESX-Datei + erhält Header-Whitespace."""
    return ET.parse(path)


def upsert_resx_value(tree: ET.ElementTree, key: str, value: str) -> bool:
    """Fügt einen Key ein oder aktualisiert ihn. Returns True bei Änderung."""
    root = tree.getroot()
    for data in root.findall("data"):
        if data.get("name") == key:
            value_el = data.find("value")
            if value_el is not None and value_el.text != value:
                value_el.text = value
                return True
            return False

    # Key fehlt — neuen anlegen
    new_data = ET.SubElement(root, "data")
    new_data.set("name", key)
    new_data.set(f"{RESX_HEADER_NAMESPACE}space", "preserve")
    new_value = ET.SubElement(new_data, "value")
    new_value.text = value
    return True


def save_resx(tree: ET.ElementTree, path: Path) -> None:
    """Schreibt RESX zurück + bewahrt UTF-8-Encoding."""
    tree.write(path, encoding="utf-8", xml_declaration=True)


# ─────────────────────────────────────────────────────────────────────
# C#-CODE-GEN
# ─────────────────────────────────────────────────────────────────────
def generate_seasonstoryline_cs(rows, output_path: Path) -> None:
    """Erzeugt SeasonStorylineCatalog.generated.cs aus den Story-Sheet-Rows.

    Erwartet rows = [{"season_id": "spring", "chapter_idx": 1, "chapter_id": "spring_ch1",
                       "title_key": "...", "text_key": "...", "required_battlepass_tier": 1, ...}, ...]
    """
    by_season: dict[str, list[dict]] = {}
    for row in rows:
        by_season.setdefault(row["season_id"].lower(), []).append(row)

    lines = [
        "// AUTO-GENERATED — DO NOT EDIT MANUALLY. Sync via tools/ContentPipeline/sync_content.py",
        "// P2.1 AAA-Audit (08.05.2026): Source-of-Truth ist Google Sheets.",
        "namespace HandwerkerImperium.Models;",
        "",
        "public partial class SeasonStorylineCatalog",
        "{",
        "    public static readonly SeasonStoryline[] AllGenerated =",
        "    [",
    ]
    for season_id, season_rows in sorted(by_season.items()):
        season_rows.sort(key=lambda r: int(r["chapter_idx"]))
        chapter_ids = ", ".join(f'"{r["chapter_id"]}"' for r in season_rows)
        tier_triggers = ", ".join(str(r["required_battlepass_tier"]) for r in season_rows)
        lines.extend([
            "        new SeasonStoryline {",
            f"            Theme = Season.{season_id.capitalize()},",
            f'            ThemeKey = "SeasonStory{season_id.capitalize()}Theme",',
            f"            ChapterIds = [{chapter_ids}],",
            f"            TierTriggers = [{tier_triggers}],",
            "        },",
        ])
    lines.extend([
        "    ];",
        "}",
        "",
    ])
    output_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"[CS] Wrote {output_path} ({len(rows)} chapters in {len(by_season)} seasons)")


# ─────────────────────────────────────────────────────────────────────
# MAIN
# ─────────────────────────────────────────────────────────────────────
def fetch_rows_from_sheet(sheet_id: str, worksheet_name: str) -> list[dict]:
    if not GSPREAD_AVAILABLE:
        raise RuntimeError(
            "gspread nicht installiert — bitte `pip install gspread google-auth-oauthlib pandas`"
        )
    creds_path = os.environ.get("GOOGLE_APPLICATION_CREDENTIALS",
                                os.path.expanduser("~/.config/handwerkerimperium-content.json"))
    if not os.path.exists(creds_path):
        raise RuntimeError(f"Credentials-Datei fehlt: {creds_path}")

    scopes = ["https://www.googleapis.com/auth/spreadsheets.readonly"]
    creds = Credentials.from_service_account_file(creds_path, scopes=scopes)
    client = gspread.authorize(creds)
    sheet = client.open_by_key(sheet_id)
    ws = sheet.worksheet(worksheet_name)
    return ws.get_all_records()


def main() -> int:
    parser = argparse.ArgumentParser(description="Content-Pipeline-Sync (Google Sheets → C# + RESX)")
    parser.add_argument("--sheet-id", help="Google Sheets ID (aus URL)")
    parser.add_argument("--output-dir", required=True, help="Pfad zum HandwerkerImperium-App-Ordner")
    parser.add_argument("--check-only", action="store_true", help="Build-fail wenn Diff vorhanden (CI-Modus)")
    parser.add_argument("--dry-run", action="store_true", help="Nichts schreiben, nur Plan ausgeben")
    args = parser.parse_args()

    output_dir = Path(args.output_dir).resolve()
    if not output_dir.exists():
        print(f"FEHLER: Output-Dir existiert nicht: {output_dir}")
        return 2

    if not args.sheet_id:
        print("Kein --sheet-id angegeben. Skript läuft im Stub-Modus (zeigt nur Workflow).")
        print(f"  Würde C# generieren nach: {output_dir / 'HandwerkerImperium.Shared/Models/SeasonStorylineCatalog.generated.cs'}")
        print(f"  Würde RESX patchen in:    {output_dir / 'HandwerkerImperium.Shared/Resources/Strings/AppStrings.{lang}.resx'}")
        return 0

    print(f"[FETCH] Story-Chapters aus Sheet {args.sheet_id}...")
    try:
        story_rows = fetch_rows_from_sheet(args.sheet_id, "Story-Chapters")
    except Exception as e:
        print(f"FEHLER beim Sheet-Fetch: {e}")
        return 3

    # 1) C#-Code generieren
    cs_target = output_dir / "HandwerkerImperium.Shared" / "Models" / "SeasonStorylineCatalog.generated.cs"
    if not args.dry_run:
        generate_seasonstoryline_cs(story_rows, cs_target)

    # 2) RESX patchen pro Sprache
    resx_dir = output_dir / "HandwerkerImperium.Shared" / "Resources" / "Strings"
    changes_per_lang: dict[str, int] = {}
    for lang in RESX_LANGUAGES:
        resx_path = resx_dir / f"AppStrings.{lang}.resx"
        if not resx_path.exists():
            print(f"  [WARN] RESX fehlt: {resx_path}")
            continue
        tree = load_resx(resx_path)
        changed = 0
        for row in story_rows:
            if row.get(lang):
                # Title- und Text-Keys ergänzen
                if upsert_resx_value(tree, row["title_key"], row[lang]):
                    changed += 1
        if not args.dry_run and changed > 0:
            save_resx(tree, resx_path)
        changes_per_lang[lang] = changed
        print(f"  [{lang.upper()}] {changed} keys updated/added")

    if args.check_only and (any(changes_per_lang.values())):
        print("FEHLER: Änderungen pending — bitte sync_content.py ohne --check-only laufen lassen.")
        return 1

    print("[OK] Sync abgeschlossen.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
