using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.UI.Hud
{
    /// <summary>
    /// Premium-HUD-Binder (UI Toolkit, MVVM-Light: View = GameHud.uxml, dieser Binder verdrahtet):
    /// Statuskarten (Geld/Einkommen/Gems/Stadt/Stern), Tagesaufgaben-Panel und Meister-Hans-Toast.
    /// Pollt den <see cref="RuntimeGameController"/> (Geld pro Frame, Rest gedrosselt) — keine
    /// Logik hier, nur Anzeige. Safe-Area-Ränder (Notch) werden zur Laufzeit gesetzt.
    /// Beat-/Aufgaben-Texte sind UI-Content (Mapping unten; später Unity-Localization-Keys).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameHudBinder : MonoBehaviour
    {
        [SerializeField] private RuntimeGameController controller;
        [SerializeField] private GameAudio audioHub;
        [SerializeField] private float slowPollSeconds = 0.25f;
        [SerializeField] private float toastSeconds = 4.5f;

        private static readonly Dictionary<string, string> BeatTexts = new Dictionary<string, string>
        {
            { "hans_intro", "Hans: \"Willkommen! Pack mit an — die Stadt braucht uns.\"" },
            { "hans_first_production", "Hans: \"Die erste Ware liegt bereit. Trag sie zum Tresen!\"" },
            { "hans_first_worker", "Hans: \"Ein Arbeiter! Jetzt läuft es auch ohne dich weiter.\"" },
            { "hans_first_plot", "Hans: \"Ein neues Gewerk! So wächst unser Imperium.\"" },
            { "hans_first_landmark", "Hans: \"Sieh dir das an — die Stadt wird wieder schön!\"" },
            { "hans_first_prestige", "Hans: \"Eine neue Stadt wartet. Auf geht's, Meister!\"" },
        };

        private static readonly Dictionary<string, string> TaskTexts = new Dictionary<string, string>
        {
            { "dt_serve_10", "10 Kunden bedienen" },
            { "dt_serve_50", "50 Kunden bedienen" },
            { "dt_upgrades_3", "3 Upgrades kaufen" },
            { "dt_worker_1", "1 Arbeiter anstellen" },
            { "dt_restore_1", "1 Bauphase sanieren" },
            { "dt_cash_10000", "10.000 Geld ansammeln" },
        };

        // Gewerk-Namen für das Verwaltungs-Panel (Reihenfolge = Stations-Index, GDD §6.1)
        private static readonly string[] StationNames =
        {
            "Schreinerei", "Klempnerei", "Elektriker", "Malerei", "Dachdeckerei",
            "Bauunternehmen", "Architekturbüro", "Generalunternehmer", "Meisterschmiede", "Innovationslabor"
        };

        private Label _money, _income, _gems, _city, _star, _toast, _offlineAmount, _prestigeBonus;
        private VisualElement _offlineOverlay, _prestigeOverlay, _workerOverlay;
        private ScrollView _workerList;
        private Button _prestigeButton;
        private readonly Label[] _tasks = new Label[3];
        private float _slowTimer;
        private float _toastTimer;
        private string _lastBeat = "";
        private bool _offlineShown;
        private bool _workerPanelOpen;
        private Button _freeCashButton;
        private float _freeCashReadyTime;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _money = root.Q<Label>("money-value");
            _income = root.Q<Label>("income-value");
            _gems = root.Q<Label>("gems-value");
            _city = root.Q<Label>("city-value");
            _star = root.Q<Label>("star-value");
            _toast = root.Q<Label>("story-toast");
            for (int i = 0; i < 3; i++) _tasks[i] = root.Q<Label>("task-" + i);
            _offlineOverlay = root.Q<VisualElement>("offline-overlay");
            _offlineAmount = root.Q<Label>("offline-amount");
            var continueButton = root.Q<Button>("offline-continue");
            if (continueButton != null)
                continueButton.clicked += () =>
                {
                    audioHub?.Play(GameSfx.ButtonTap);
                    _offlineOverlay?.AddToClassList("modal-overlay--hidden");
                };
            var doubleButton = root.Q<Button>("offline-double");
            if (doubleButton != null)
                doubleButton.clicked += () =>
                {
                    decimal extra = controller != null ? controller.DoubleOfflineOnce() : 0m;
                    if (extra > 0m && _offlineAmount != null)
                    {
                        _offlineAmount.text = "+" + MoneyFormat.Short(extra * 2m);
                        audioHub?.Play(GameSfx.MoneyEarned);
                    }
                    doubleButton.SetEnabled(false); // einmalig je Start
                };

            // Arbeiter-Verwaltungs-Panel (GDD §6.2)
            _workerOverlay = root.Q<VisualElement>("worker-overlay");
            _workerList = root.Q<ScrollView>("worker-list");
            var workerButton = root.Q<Button>("worker-button");
            if (workerButton != null)
                workerButton.clicked += () =>
                {
                    audioHub?.Play(GameSfx.ButtonTap);
                    OpenWorkerPanel();
                };
            var workerClose = root.Q<Button>("worker-close");
            if (workerClose != null)
                workerClose.clicked += () =>
                {
                    audioHub?.Play(GameSfx.ButtonTap);
                    _workerPanelOpen = false;
                    _workerOverlay?.AddToClassList("modal-overlay--hidden");
                };

            // Gratis-Geld (GDD §9.1): HUD-Button mit Countdown statt Boden-Platte
            _freeCashButton = root.Q<Button>("freecash-button");
            if (_freeCashButton != null)
                _freeCashButton.clicked += () =>
                {
                    if (controller == null || Time.time < _freeCashReadyTime) return;
                    decimal reward = controller.ClaimFreeCash();
                    if (reward <= 0m) return;
                    _freeCashReadyTime = Time.time + (float)controller.Balancing.Monetization.FreeCashBlockSeconds;
                    audioHub?.Play(GameSfx.OfflineEarnings);
                };

            _prestigeButton = root.Q<Button>("prestige-button");
            _prestigeOverlay = root.Q<VisualElement>("prestige-overlay");
            _prestigeBonus = root.Q<Label>("prestige-bonus");
            if (_prestigeButton != null)
                _prestigeButton.clicked += OpenPrestigeModal;
            var prestigeConfirm = root.Q<Button>("prestige-confirm");
            if (prestigeConfirm != null)
                prestigeConfirm.clicked += () =>
                {
                    if (controller != null && controller.TryPrestige())
                    {
                        audioHub?.Play(GameSfx.Prestige);
                        _prestigeOverlay?.AddToClassList("modal-overlay--hidden");
                    }
                };
            var prestigeCancel = root.Q<Button>("prestige-cancel");
            if (prestigeCancel != null)
                prestigeCancel.clicked += () =>
                {
                    audioHub?.Play(GameSfx.ButtonTap);
                    _prestigeOverlay?.AddToClassList("modal-overlay--hidden");
                };

            ApplySafeArea(root.Q<VisualElement>("top-bar"));
        }

        private void Update()
        {
            if (controller == null || controller.Model == null || _money == null) return;
            var m = controller.Model;

            // Offline-Verdienst-Modal einmalig zeigen (Runtime rechnet den Betrag in Start an)
            if (!_offlineShown)
            {
                _offlineShown = true;
                if (controller.LastOfflineEarned > 0m && _offlineOverlay != null && _offlineAmount != null)
                {
                    _offlineAmount.text = "+" + MoneyFormat.Short(controller.LastOfflineEarned);
                    _offlineOverlay.RemoveFromClassList("modal-overlay--hidden");
                    audioHub?.Play(GameSfx.OfflineEarnings);
                }
            }

            _money.text = MoneyFormat.Short(m.Idle.Money);

            _slowTimer += Time.deltaTime;
            if (_slowTimer >= slowPollSeconds)
            {
                _slowTimer = 0f;
                _income.text = "+" + MoneyFormat.Short(controller.EffectiveIncomePerSecond()) + "/s";
                _gems.text = MoneyFormat.Short(m.Gems);
                _city.text = "Stadt " + (m.Meta.CityIndex + 1);
                _star.text = "Stern " + m.Meta.CurrentStar + "/5";
                UpdateTasks(m);
                if (_workerPanelOpen) RebuildWorkerList(); // Leistbarkeit folgt dem Geldstand
                UpdateFreeCashButton();

                // Prestige-Button erscheint, sobald der Umzug möglich ist (5★, Limit nicht erreicht)
                if (_prestigeButton != null)
                {
                    bool can = controller.CanPrestige();
                    if (can == _prestigeButton.ClassListContains("prestige-button--hidden"))
                    {
                        if (can) _prestigeButton.RemoveFromClassList("prestige-button--hidden");
                        else _prestigeButton.AddToClassList("prestige-button--hidden");
                    }
                }
            }

            UpdateToast();
        }

        private void UpdateTasks(HandwerkerImperium.Domain.Runtime.GameModel m)
        {
            for (int i = 0; i < 3; i++)
            {
                var label = _tasks[i];
                if (label == null) continue;
                if (m.DailyTasks == null || i >= m.DailyTasks.Count)
                {
                    label.text = "";
                    continue;
                }
                var t = m.DailyTasks[i];
                string name = TaskTexts.TryGetValue(t.Id, out var txt) ? txt : t.Id;
                if (t.Claimed)
                {
                    label.text = name + " — fertig (+" + t.GemReward + " Gems)";
                    label.AddToClassList("task-row--done");
                }
                else
                {
                    int pct = (int)(controller.DailyTaskProgress01(t) * 100.0);
                    label.text = name + " — " + pct + "%";
                    label.RemoveFromClassList("task-row--done");
                }
            }
        }

        private void UpdateToast()
        {
            string beat = controller.LatestStoryBeat;
            if (!string.IsNullOrEmpty(beat) && beat != _lastBeat)
            {
                _lastBeat = beat;
                _toast.text = BeatTexts.TryGetValue(beat, out var txt) ? txt : beat;
                _toast.RemoveFromClassList("story-toast--hidden");
                _toastTimer = toastSeconds;
                audioHub?.Play(GameSfx.StoryPing);
            }
            else if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f)
                    _toast.AddToClassList("story-toast--hidden");
            }
        }

        /// <summary>Öffnet das Worker-Verwaltungs-Panel und baut die Zeilen aus dem Modell-Status.</summary>
        private void OpenWorkerPanel()
        {
            if (_workerOverlay == null) return;
            _workerPanelOpen = true;
            _workerOverlay.RemoveFromClassList("modal-overlay--hidden");
            RebuildWorkerList();
        }

        /// <summary>
        /// Baut/aktualisiert die Werkstatt-Zeilen (Name · Status · Kauf-Button). Anstellen + Tempo-
        /// Stufen laufen direkt über den Controller; nach jedem Kauf wird die Liste neu aufgebaut.
        /// Wird auch periodisch refresht, solange das Panel offen ist (Leistbarkeit folgt dem Geld).
        /// </summary>
        private void RebuildWorkerList()
        {
            if (_workerList == null || controller == null) return;
            _workerList.Clear();
            int n = controller.StationCount;
            for (int i = 0; i < n; i++)
            {
                int idx = i; // Closure-Capture
                var info = controller.GetWorkerRow(i);

                var row = new VisualElement();
                row.AddToClassList("worker-row");
                if (!info.Unlocked) row.AddToClassList("worker-row--locked");

                var name = new Label(idx < StationNames.Length ? StationNames[idx] : "Gewerk " + (idx + 1));
                name.AddToClassList("worker-row__name");
                row.Add(name);

                var status = new Label();
                status.AddToClassList("worker-row__status");
                row.Add(status);

                // Ausbau-Aktion (Werkstatt-Bau, GDD §6.1) — nur für freigeschaltete Gewerke
                if (info.Unlocked)
                {
                    var build = new Button();
                    build.AddToClassList("worker-row__action");
                    if (info.BuildAtMax)
                    {
                        build.text = "Ausbau MAX";
                        build.AddToClassList("worker-row__action--disabled");
                        build.SetEnabled(false);
                    }
                    else
                    {
                        build.text = "Ausbau " + (info.BuildLevel + 1) + "  " + MoneyFormat.Short(info.BuildCost);
                        SetActionState(build, controller.Money >= info.BuildCost);
                        build.clicked += () =>
                        {
                            if (controller.UpgradeStationBuild(idx)) { audioHub?.Play(GameSfx.UpgradePaid); RebuildWorkerList(); }
                        };
                    }
                    row.Add(build);
                }

                var action = new Button();
                action.AddToClassList("worker-row__action");
                row.Add(action);

                if (!info.Unlocked)
                {
                    status.text = "gesperrt";
                    action.text = "—";
                    action.AddToClassList("worker-row__action--disabled");
                    action.SetEnabled(false);
                }
                else if (!info.HasWorker)
                {
                    status.text = "kein Arbeiter";
                    action.text = "Anstellen " + MoneyFormat.Short(info.HireCost);
                    bool afford = controller.Money >= info.HireCost;
                    SetActionState(action, afford);
                    action.clicked += () =>
                    {
                        if (controller.HireWorker(idx)) { audioHub?.Play(GameSfx.WorkerHired); RebuildWorkerList(); }
                    };
                }
                else if (info.AtMax)
                {
                    status.text = "Tempo MAX (" + info.MaxLevel + ")";
                    action.text = "Maximal";
                    action.AddToClassList("worker-row__action--disabled");
                    action.SetEnabled(false);
                }
                else
                {
                    status.text = "Tempo " + info.Level + "/" + info.MaxLevel;
                    action.text = "Tempo +1  " + MoneyFormat.Short(info.UpgradeCost);
                    bool afford = controller.Money >= info.UpgradeCost;
                    SetActionState(action, afford);
                    action.clicked += () =>
                    {
                        if (controller.UpgradeWorker(idx)) { audioHub?.Play(GameSfx.UpgradePaid); RebuildWorkerList(); }
                    };
                }

                _workerList.Add(row);
            }
        }

        /// <summary>Gratis-Geld-Button: „bereit" oder Countdown (mm:ss), abgekühlt gedämpft.</summary>
        private void UpdateFreeCashButton()
        {
            if (_freeCashButton == null) return;
            bool ready = Time.time >= _freeCashReadyTime;
            if (ready)
            {
                _freeCashButton.text = "Gratis-Geld";
                _freeCashButton.RemoveFromClassList("freecash-button--cooldown");
            }
            else
            {
                int rem = Mathf.CeilToInt(_freeCashReadyTime - Time.time);
                _freeCashButton.text = $"Gratis-Geld {rem / 60}:{rem % 60:00}";
                _freeCashButton.AddToClassList("freecash-button--cooldown");
            }
        }

        private static void SetActionState(Button action, bool affordable)
        {
            action.SetEnabled(affordable);
            if (affordable) action.RemoveFromClassList("worker-row__action--disabled");
            else action.AddToClassList("worker-row__action--disabled");
        }

        /// <summary>Öffnet das Prestige-Modal mit dem konkreten Multiplikator-Sprung (×alt → ×neu).</summary>
        private void OpenPrestigeModal()
        {
            if (controller == null || controller.Model == null || _prestigeOverlay == null) return;
            var meta = controller.Model.Meta;
            var stages = controller.Balancing.Prestige.StageMultipliers;
            if (_prestigeBonus != null && meta.PrestigeCount >= 0 && meta.PrestigeCount < stages.Count)
            {
                decimal next = meta.PrestigeMultiplier * stages[meta.PrestigeCount];
                _prestigeBonus.text = "×" + meta.PrestigeMultiplier.ToString("0.#") + "  →  ×" + next.ToString("0.#");
            }
            _prestigeOverlay.RemoveFromClassList("modal-overlay--hidden");
        }

        /// <summary>Notch/Safe-Area (CLAUDE.md §8): obere Leiste unter die Aussparung schieben.</summary>
        private void ApplySafeArea(VisualElement topBar)
        {
            if (topBar == null) return;
            var safe = Screen.safeArea;
            float topInset = Screen.height - (safe.y + safe.height);
            if (topInset > 0f) topBar.style.top = 10f + topInset;
            if (safe.x > 0f) topBar.style.left = 12f + safe.x;
        }
    }
}
