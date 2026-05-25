#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Login;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Tempel
{
    /// <summary>
    /// Sternkarten-Tempel + Login-Belohnungen (Designplan v4 Oeko Kap. 5).
    /// Zwei Bereiche:
    ///   - Oben: Login-Tageskalender (30 Tage), heutige Belohnung claimbar
    ///   - Unten: Sternkarten-Tempel mit 6 Eintausch-Optionen + Mythic-Fragment-Pfad
    /// </summary>
    public sealed class TempelScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly LoginRewardController _loginRewards;
        private readonly SternkartenService _sternService;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        // UI
        private Label _bronzeCount = null!;
        private Label _silberCount = null!;
        private Label _goldCount = null!;
        private Label _platinCount = null!;
        private Label _sternpunkteAvailable = null!;
        private Label _mythicFragments = null!;
        private VisualElement _loginCalendar = null!;
        private Button _claimTodayButton = null!;
        private Label _claimTodayLabel = null!;
        private VisualElement _exchangeList = null!;
        private Button _backButton = null!;

        private PlayerSave? _cachedSave;

        public override string Id => ScreenId.Tempel;
        protected override string UxmlPath => "UI/TempelScreen";

        private readonly UIAssetService _uiAssets;

        public TempelScreen(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            LoginRewardController loginRewards,
            SternkartenService sternService,
            ILocalizationService loc,
            ToastService toast,
            UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _loginRewards = loginRewards;
            _sternService = sternService;
            _loc = loc;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _uiAssets.ApplyUIBackground(root, "tempel");
            _bronzeCount = Q<Label>("count-bronze");
            _silberCount = Q<Label>("count-silber");
            _goldCount = Q<Label>("count-gold");
            _platinCount = Q<Label>("count-platin");
            _sternpunkteAvailable = Q<Label>("sternpunkte-available");
            _mythicFragments = Q<Label>("mythic-fragments");
            _loginCalendar = Q<VisualElement>("login-calendar");
            _claimTodayButton = Q<Button>("claim-today-button");
            _claimTodayLabel = Q<Label>("claim-today-label");
            _exchangeList = Q<VisualElement>("exchange-list");
            _backButton = Q<Button>("back-button");

            _claimTodayButton.clicked += OnClaimTodayClicked;
            _backButton.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var saveR = await _save.LoadAsync(ct);
            if (!saveR.IsSuccess || saveR.Value == null)
            {
                _toast.Show(_loc.Get("tempel.no_save") ?? "Save nicht geladen", ToastKind.Danger);
                return;
            }
            _cachedSave = saveR.Value;
            RefreshAll();
        }

        // ==========================================================================
        // Anzeige aktualisieren
        // ==========================================================================

        private void RefreshAll()
        {
            if (_cachedSave == null) return;
            var inv = _cachedSave.Sternkarten.Inventory;

            _bronzeCount.text = inv.Bronze.ToString("N0");
            _silberCount.text = inv.Silber.ToString("N0");
            _goldCount.text = inv.Gold.ToString("N0");
            _platinCount.text = inv.Platin.ToString("N0");
            _sternpunkteAvailable.text = $"{inv.AvailableSternpunkte:N0} {_loc.Get("tempel.sternpunkte") ?? "Sternpunkte"}";
            _mythicFragments.text = $"{inv.MythicCoreFragments}/{SternkartenWerte.MythicFragmentsPerCore} Mythic-Fragmente";

            RefreshLoginCalendar();
            RefreshExchangeList();
        }

        private void RefreshLoginCalendar()
        {
            if (_cachedSave == null) return;
            _loginCalendar.Clear();

            var tracker = _cachedSave.Sternkarten.Tracker;
            var nextDay = tracker.NextDayInCycle;
            var canClaim = tracker.CanClaimToday(DateTime.UtcNow);

            // 30-Tage-Strip
            for (var day = 1; day <= 30; day++)
            {
                var cell = new VisualElement { name = $"day-{day}" };
                cell.AddToClassList("ak-tempel__day-cell");

                if (day < nextDay) cell.AddToClassList("ak-tempel__day-cell--claimed");
                if (day == nextDay && canClaim) cell.AddToClassList("ak-tempel__day-cell--today");
                if (day == 7 || day == 14 || day == 21 || day == 30)
                    cell.AddToClassList("ak-tempel__day-cell--milestone");

                cell.Add(new Label($"{day}"));
                _loginCalendar.Add(cell);
            }

            // Heute-Claim-Button
            var preview = _loginRewards.PreviewToday(_cachedSave, DateTime.UtcNow);
            if (preview != null)
            {
                _claimTodayLabel.text = $"{_loc.Get("tempel.day") ?? "Tag"} {preview.Day}: {FormatRewardItems(preview.Items)}";
                _claimTodayButton.SetEnabled(true);
            }
            else
            {
                _claimTodayLabel.text = _loc.Get("tempel.already_claimed") ?? "Heute schon abgeholt — komm morgen wieder";
                _claimTodayButton.SetEnabled(false);
            }
        }

        private static string FormatRewardItems(IReadOnlyList<LoginRewardController.RewardItemDto> items)
        {
            var parts = new List<string>(items.Count);
            foreach (var item in items)
            {
                parts.Add(item.type switch
                {
                    "gold" => $"{item.magnitude:N0} Gold",
                    "diamonds" => $"{item.magnitude} 💎",
                    "common_scrap" => $"{item.magnitude}x Common Scrap",
                    "rare_scrap" => $"{item.magnitude}x Rare Scrap",
                    "epic_scrap" => $"{item.magnitude}x Epic Scrap",
                    "legendary_scrap" => $"{item.magnitude}x Legendary Scrap",
                    "sternkarte" => $"{item.magnitude}x {item.sternkartenStufe}-Sternkarte",
                    _ => $"{item.magnitude}x {item.type}"
                });
            }
            return string.Join(", ", parts);
        }

        private void RefreshExchangeList()
        {
            if (_cachedSave == null) return;
            _exchangeList.Clear();

            var available = _cachedSave.Sternkarten.Inventory.AvailableSternpunkte;
            var options = new (string Key, int Cost, string LabelKey)[]
            {
                ("random_2star",     SternkartenWerte.CostRandom2Star,    "tempel.exchange.random_2star"),
                ("chosen_3star",     SternkartenWerte.CostChosen3Star,    "tempel.exchange.chosen_3star"),
                ("exclusive_3star",  SternkartenWerte.CostExclusive3Star, "tempel.exchange.exclusive_3star"),
                ("exclusive_4star",  SternkartenWerte.CostExclusive4Star, "tempel.exchange.exclusive_4star"),
                ("legendary_scrap",  SternkartenWerte.CostLegendaryScrap, "tempel.exchange.legendary_scrap"),
                ("mythic_fragment",  SternkartenWerte.CostMythicFragment, "tempel.exchange.mythic_fragment"),
            };

            foreach (var opt in options)
            {
                var row = new VisualElement { name = $"exchange-{opt.Key}" };
                row.AddToClassList("ak-tempel__exchange-row");

                var label = new Label(_loc.Get(opt.LabelKey) ?? opt.Key);
                label.AddToClassList("ak-tempel__exchange-label");
                row.Add(label);

                var costLabel = new Label($"{opt.Cost} SP");
                costLabel.AddToClassList("ak-tempel__exchange-cost");
                row.Add(costLabel);

                var btn = new Button(() => OnExchangeClicked(opt.Key, opt.Cost)) { text = _loc.Get("tempel.exchange.confirm") ?? "Eintauschen" };
                btn.AddToClassList("ak-tempel__exchange-button");
                btn.SetEnabled(available >= opt.Cost);
                row.Add(btn);

                _exchangeList.Add(row);
            }
        }

        // ==========================================================================
        // Aktionen
        // ==========================================================================

        private void OnClaimTodayClicked() => ClaimTodayAsync().Forget();

        private async UniTaskVoid ClaimTodayAsync()
        {
            _claimTodayButton.SetEnabled(false);
            var result = await _loginRewards.ClaimTodayAsync();
            if (!result.IsSuccess)
            {
                _toast.Show(result.ErrorMessage ?? "Claim fehlgeschlagen", ToastKind.Danger);
                _claimTodayButton.SetEnabled(true);
                return;
            }

            var outcome = result.Value!;
            _toast.Show($"✨ Tag {outcome.Day}: {FormatRewardItems(outcome.GrantedItems)}", ToastKind.Success);

            var saveR = await _save.LoadAsync();
            if (saveR.IsSuccess && saveR.Value != null) _cachedSave = saveR.Value;
            RefreshAll();
        }

        private void OnExchangeClicked(string optionKey, int cost) => RunExchangeAsync(optionKey, cost).Forget();

        private async UniTaskVoid RunExchangeAsync(string optionKey, int cost)
        {
            if (_cachedSave == null) return;

            // Mythic-Fragment Sonderpfad
            if (optionKey == "mythic_fragment")
            {
                var r = _sternService.ExchangeForMythicFragment(_cachedSave.Sternkarten.Inventory);
                if (r.IsSuccess)
                {
                    await _save.MutateAsync(s => s, default);   // Trigger persistierung
                    _toast.Show($"✨ {_loc.Get("tempel.fragment_gained") ?? "Mythic-Fragment erhalten"} ({_cachedSave.Sternkarten.Inventory.MythicCoreFragments}/3)", ToastKind.Success);
                }
                else
                {
                    _toast.Show(r.ErrorMessage ?? "Eintausch fehlgeschlagen", ToastKind.Danger);
                }
            }
            else
            {
                var canDo = _sternService.CanExchange(_cachedSave.Sternkarten.Inventory, cost);
                if (!canDo.IsSuccess)
                {
                    _toast.Show(canDo.ErrorMessage ?? "Nicht genug Sternpunkte", ToastKind.Danger);
                    return;
                }
                _sternService.Exchange(_cachedSave.Sternkarten.Inventory, cost);
                await _save.MutateAsync(s => s, default);
                _toast.Show($"✓ {_loc.Get("tempel.exchange.success") ?? "Eingetauscht"}: {optionKey} (-{cost} SP)", ToastKind.Success);

                // TODO Phase 2: tatsaechliche Belohnung (Karte/Scrap) ins Inventar legen
            }

            var saveR = await _save.LoadAsync();
            if (saveR.IsSuccess && saveR.Value != null) _cachedSave = saveR.Value;
            RefreshAll();
        }
    }
}
