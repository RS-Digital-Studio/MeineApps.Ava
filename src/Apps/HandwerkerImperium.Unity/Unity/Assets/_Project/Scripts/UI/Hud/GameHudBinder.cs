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

        private Label _money, _income, _gems, _city, _star, _toast, _offlineAmount;
        private VisualElement _offlineOverlay;
        private readonly Label[] _tasks = new Label[3];
        private float _slowTimer;
        private float _toastTimer;
        private string _lastBeat = "";
        private bool _offlineShown;

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
                continueButton.clicked += () => _offlineOverlay?.AddToClassList("modal-overlay--hidden");
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
            }
            else if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f)
                    _toast.AddToClassList("story-toast--hidden");
            }
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
