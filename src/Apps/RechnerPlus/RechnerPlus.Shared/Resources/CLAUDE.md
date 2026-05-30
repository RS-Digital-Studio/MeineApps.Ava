# Resources — Lokalisierung

| Ordner/Datei | Zweck |
|--------------|-------|
| `Strings/AppStrings.resx` (+ `.{de,en,es,fr,it,pt}.resx`) | UI-Strings, **6 Sprachen** (DE, EN, ES, FR, IT, PT). |
| `Strings/AppStrings.Designer.cs` | **Manuell** pflegen — wird bei CLI-Build nicht auto-generiert. |

Zugriff ausschließlich über `ILocalizationService.GetString("Key")`. Englisch ist Basis-Sprache
für Fallbacks. Keine hardcodierten User-Strings in Views/VMs.
