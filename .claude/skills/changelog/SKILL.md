---
name: changelog
description: Aktualisiert den Changelog und die Social-Media Promo-Posts fuer eine App.
user-invocable: true
allowed-tools: Read, Edit, Write, Glob, Grep
argument-hint: "<AppName>"
---

# Changelog + Social Posts aktualisieren

Fuehre folgende Schritte fuer die App `$ARGUMENTS` aus:

1. **Aktuelle Version ermitteln:**
   - Lese `ApplicationDisplayVersion` aus der Android .csproj
   - Naechste Version = aktuelle Version + Patch +1

2. **Changelog finden oder erstellen:**
   - Datei: `Releases/{App}/CHANGELOG_v{naechsteVersion}.md`
   - Falls nicht vorhanden: Neue Datei erstellen mit Header

3. **Aenderungen zusammenfassen:**
   - Pruefe `git diff` und `git log` fuer die App
   - Schreibe benutzerfreundliche Changelog-Eintraege (kein Entwickler-Jargon)
   - Sprache: Deutsch

4. **Social-Media Posts aktualisieren:**
   - **X-Post**: Ausfuehrlich (Robert hat X Premium, kein 280-Zeichen-Limit), alle wichtigen Aenderungen, Hashtags ans Ende, Tester-Link: `https://groups.google.com/g/testersrsdigital`
   - **Reddit-Post**: Titel (Neugier weckend, emotional, Hook) + Body (authentisch, technische Details, konkreter Mehrwert)
   - Posts muessen ANSPRECHEND und SPANNEND klingen - nicht wie generiertes Marketing!
   - Posts ERGAENZEN, nicht ersetzen - alle bisherigen Verbesserungen der Version gehoeren rein
   - Lockerer Follow-CTA am Ende

5. **WICHTIG:**
   - Jede App/jeder Post hat eigenen Stil und Tonfall
   - Nicht templatehaft ("I'm a solo indie developer...")
   - Emotionen wecken, Vorher/Nachher-Vergleiche, persoenliche Anekdoten
