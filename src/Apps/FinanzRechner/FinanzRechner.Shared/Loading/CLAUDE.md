# Loading — Startup-Pipeline

Initialisierungs-Pipeline der App. Erbt von `LoadingPipelineBase` aus `MeineApps.UI.Loading`.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `FinanzRechnerLoadingPipeline.cs` | Zwei-Stufen-Pipeline: DB+Shader parallel, dann MainViewModel |

---

## Pipeline-Schritte

**Schritt 1 — DB+Shader (Weight 40):** Alle Services + Shader parallel via `Task.WhenAll`:
- `IExpenseService.InitializeAsync()` (expenses.json lesen)
- `IAccountService.InitializeAsync()` (accounts.json lesen)
- `ISavingsGoalService.InitializeAsync()` (savings_goals.json lesen)
- `IDebtService.InitializeAsync()` (debts.json lesen)
- `ICustomCategoryService.InitializeAsync()` (custom_categories.json lesen)
- `IPurchaseService.InitializeAsync()` (Premium-Status mit Google Play abgleichen)
- `Task.Run(() => ShaderPreloader.PreloadAll())` (GPU-Shader kompilieren)

Danach: Währungs-Preset aus Preferences laden und `CurrencyHelper.Configure(preset)`.

**Schritt 2 — ViewModel (Weight 15):** `services.GetRequiredService<MainViewModel>()` —
löst alle Child-VMs transitiv auf und bindet an die bereits geladenen Service-Daten.

---

## Warum dieser Aufbau?

Services müssen vor dem MainViewModel vollständig initialisiert sein, weil das VM im
Konstruktor bereits Daten aus den Services liest (Saldo, Budgets, Insights). Das
`Task.WhenAll`-Pattern in Schritt 1 maximiert den Parallelismus — alle JSON-Dateien
und der Shader-Preload laufen gleichzeitig auf Hintergrund-Threads.
