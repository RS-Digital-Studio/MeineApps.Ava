#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Quest;
using ArcaneKingdom.Domain.Shop;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.Game.Quest;
using ArcaneKingdom.Game.Shop;
using ArcaneKingdom.UI.Common;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Hub
{
    /// <summary>
    /// Zentrale Hub-Ansicht — Header (Profil + Currencies), TabBar (Cards/Quests/Shop/Arena/Mehr),
    /// Content-Container der das aktive Tab-Panel zeigt.
    ///
    /// In Stufe 3 ist nur der Cards-Tab voll implementiert. Quests/Shop/Arena/Mehr
    /// kommen in Stufe 4.
    /// </summary>
    public sealed class HubScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly CardCatalogService _cardCatalog;
        private readonly QuestService _questService;
        private readonly ShopController _shopController;
        private readonly ToastService _toast;

        // Header
        private Label _displayName = null!;
        private Label _levelLabel = null!;
        private Label _energyValue = null!;
        private Label _goldValue = null!;
        private Label _diamondValue = null!;

        // Tab-Panels
        private VisualElement _tabCards = null!;
        private VisualElement _tabQuests = null!;
        private VisualElement _tabShop = null!;
        private VisualElement _tabArena = null!;
        private VisualElement _tabMore = null!;

        // Tab-Buttons
        private VisualElement _btnCards = null!;
        private VisualElement _btnQuests = null!;
        private VisualElement _btnShop = null!;
        private VisualElement _btnArena = null!;
        private VisualElement _btnMore = null!;

        // Cards-Tab
        private TextField _cardsSearch = null!;
        private DropdownField _cardsRarityFilter = null!;
        private DropdownField _cardsElementFilter = null!;
        private VisualElement _cardsGrid = null!;
        private VisualElement _cardsEmpty = null!;

        // Quests-Tab
        private Button _questsFilterDaily = null!;
        private Button _questsFilterWeekly = null!;
        private Button _questsFilterAchievements = null!;
        private VisualElement _questsList = null!;
        private VisualElement _questsEmpty = null!;
        private QuestPeriod _questsPeriodFilter = QuestPeriod.Daily;

        // Shop-Tab
        private VisualElement _shopPacksGrid = null!;
        private VisualElement _shopDirectList = null!;
        private Button _shopBuyDiamondsButton = null!;

        // Arena-Tab
        private Label _arenaRankName = null!;
        private Label _arenaRankPoints = null!;
        private VisualElement _arenaRankFill = null!;
        private Label _arenaTicketsValue = null!;
        private Button _arenaSearchMatch = null!;
        private Button _arenaViewLeaderboard = null!;

        // More-Tab
        private Button _moreDeckBuilder = null!;
        private Button _moreWorldMap = null!;
        private Button _moreGuild = null!;
        private Button _moreFriends = null!;
        private Button _moreSaisonPass = null!;
        private Button _moreCodex = null!;
        private Button _moreSettings = null!;

        private PlayerSave? _saveCached;
        private CancellationTokenSource? _refreshCts;

        public override string Id => ScreenId.Hub;
        protected override string UxmlPath => "UI/HubScreen";

        public HubScreen(ScreenManager screenManager,
                         ISaveService<PlayerSave> save,
                         CardCatalogService cardCatalog,
                         QuestService questService,
                         ShopController shopController,
                         ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _cardCatalog = cardCatalog;
            _questService = questService;
            _shopController = shopController;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            // Header
            _displayName  = Q<Label>("header-display-name");
            _levelLabel   = Q<Label>("header-level");
            _energyValue  = Q<Label>("header-energy-value");
            _goldValue    = Q<Label>("header-gold-value");
            _diamondValue = Q<Label>("header-diamond-value");

            // Tab-Panels
            _tabCards  = Q<VisualElement>("tab-cards");
            _tabQuests = Q<VisualElement>("tab-quests");
            _tabShop   = Q<VisualElement>("tab-shop");
            _tabArena  = Q<VisualElement>("tab-arena");
            _tabMore   = Q<VisualElement>("tab-more");

            // Tab-Buttons
            _btnCards  = Q<VisualElement>("tab-button-cards");
            _btnQuests = Q<VisualElement>("tab-button-quests");
            _btnShop   = Q<VisualElement>("tab-button-shop");
            _btnArena  = Q<VisualElement>("tab-button-arena");
            _btnMore   = Q<VisualElement>("tab-button-more");

            _btnCards.AddManipulator(new Clickable(() => SwitchTab("cards")));
            _btnQuests.AddManipulator(new Clickable(() => SwitchTab("quests")));
            _btnShop.AddManipulator(new Clickable(() => SwitchTab("shop")));
            _btnArena.AddManipulator(new Clickable(() => SwitchTab("arena")));
            _btnMore.AddManipulator(new Clickable(() => SwitchTab("more")));

            // Cards-Tab
            _cardsSearch        = Q<TextField>("cards-search");
            _cardsRarityFilter  = Q<DropdownField>("cards-rarity-filter");
            _cardsElementFilter = Q<DropdownField>("cards-element-filter");
            _cardsGrid          = Q<VisualElement>("cards-grid");
            _cardsEmpty         = Q<VisualElement>("cards-empty");

            // Filter-Dropdowns befuellen
            _cardsRarityFilter.choices = new List<string> { "Alle Raritaeten",
                "Gewoehnlich", "Ungewoehnlich", "Selten", "Epic", "Legendaer" };
            _cardsRarityFilter.index = 0;

            _cardsElementFilter.choices = new List<string> { "Alle Elemente",
                "Natur", "Feuer", "Wasser", "Licht", "Dunkel" };
            _cardsElementFilter.index = 0;

            _cardsSearch.RegisterValueChangedCallback(_ => RefreshCardsGrid());
            _cardsRarityFilter.RegisterValueChangedCallback(_ => RefreshCardsGrid());
            _cardsElementFilter.RegisterValueChangedCallback(_ => RefreshCardsGrid());

            BindQuestsTab();
            BindShopTab();
            BindArenaTab();
            BindMoreTab();

            SwitchTab("cards");
        }

        // ============================================================
        // Quests-Tab Bindings
        // ============================================================

        private void BindQuestsTab()
        {
            _questsFilterDaily = Q<Button>("quests-filter-daily");
            _questsFilterWeekly = Q<Button>("quests-filter-weekly");
            _questsFilterAchievements = Q<Button>("quests-filter-achievements");
            _questsList = Q<VisualElement>("quests-list");
            _questsEmpty = Q<VisualElement>("quests-empty");

            _questsFilterDaily.clicked += () => SetQuestsFilter(QuestPeriod.Daily);
            _questsFilterWeekly.clicked += () => SetQuestsFilter(QuestPeriod.Weekly);
            _questsFilterAchievements.clicked += () => SetQuestsFilter(QuestPeriod.Achievement);
        }

        private void SetQuestsFilter(QuestPeriod period)
        {
            _questsPeriodFilter = period;
            UpdateQuestsFilterButtonStyles();
            RefreshQuestsTab();
        }

        private void UpdateQuestsFilterButtonStyles()
        {
            UpdateButtonStyle(_questsFilterDaily, _questsPeriodFilter == QuestPeriod.Daily);
            UpdateButtonStyle(_questsFilterWeekly, _questsPeriodFilter == QuestPeriod.Weekly);
            UpdateButtonStyle(_questsFilterAchievements, _questsPeriodFilter == QuestPeriod.Achievement);
        }

        private static void UpdateButtonStyle(Button btn, bool active)
        {
            btn.RemoveFromClassList("ak-btn--primary");
            btn.RemoveFromClassList("ak-btn--ghost");
            btn.AddToClassList(active ? "ak-btn--primary" : "ak-btn--ghost");
        }

        private void RefreshQuestsTab()
        {
            _questsList.Clear();
            var defs = _questService.AllDefinitions
                .Where(q => q.Period == _questsPeriodFilter)
                .ToList();

            if (defs.Count == 0)
            {
                SetActive(_questsList, false);
                SetActive(_questsEmpty, true);
                return;
            }
            SetActive(_questsEmpty, false);
            SetActive(_questsList, true);

            foreach (var def in defs)
                _questsList.Add(BuildQuestRow(def));
        }

        private VisualElement BuildQuestRow(QuestDefinition def)
        {
            var progress = _questService.GetProgress(def.Id);

            var row = new VisualElement { name = $"quest-{def.Id}" };
            row.AddToClassList("ak-surface");
            row.style.marginBottom = 8;

            // Titel + Belohnungen
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var nameLabel = new Label(LocalizeFallback(def.DisplayNameKey, def.Id));
            nameLabel.AddToClassList("ak-h4");
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);

            // Rewards-Summary (z.B. "200 Gold + 2 Common-Scraps")
            var rewardSummary = string.Join(" • ", def.Rewards.Select(r => $"{r.Amount} {r.SubType}"));
            var rewardLabel = new Label(rewardSummary);
            rewardLabel.AddToClassList("ak-caption");
            rewardLabel.AddToClassList("ak-text--accent");
            header.Add(rewardLabel);
            row.Add(header);

            // Beschreibung
            var desc = new Label(LocalizeFallback(def.DescriptionKey, $"{def.Objective} x {def.TargetCount}"));
            desc.AddToClassList("ak-body");
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginTop = 4;
            row.Add(desc);

            // Progress-Bar + Anzeige
            var progressContainer = new VisualElement();
            progressContainer.style.flexDirection = FlexDirection.Row;
            progressContainer.style.alignItems = Align.Center;
            progressContainer.style.marginTop = 8;

            var progressBar = new VisualElement();
            progressBar.AddToClassList("ak-progress");
            progressBar.style.flexGrow = 1;
            var fill = new VisualElement();
            fill.AddToClassList("ak-progress__fill");
            var pct = def.TargetCount > 0 ? (float)progress.CurrentCount * 100f / def.TargetCount : 0f;
            fill.style.width = new Length(System.Math.Min(pct, 100f), LengthUnit.Percent);
            progressBar.Add(fill);
            progressContainer.Add(progressBar);

            var counter = new Label($"{progress.CurrentCount}/{def.TargetCount}");
            counter.AddToClassList("ak-caption");
            counter.style.marginLeft = 8;
            counter.style.minWidth = 48;
            progressContainer.Add(counter);

            row.Add(progressContainer);

            // Claim-Button (nur wenn completed und nicht claimed)
            if (progress.Completed && !progress.RewardClaimed)
            {
                var claimBtn = new Button(() => OnClaimQuest(def.Id)) { text = "Einloesen" };
                claimBtn.AddToClassList("ak-btn");
                claimBtn.AddToClassList("ak-btn--sm");
                claimBtn.AddToClassList("ak-btn--accent");
                claimBtn.style.marginTop = 8;
                row.Add(claimBtn);
            }
            else if (progress.RewardClaimed)
            {
                var done = new Label("✓ Eingeloest");
                done.AddToClassList("ak-caption");
                done.AddToClassList("ak-text--success");
                done.style.marginTop = 8;
                done.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(done);
            }

            return row;
        }

        private void OnClaimQuest(string questId)
        {
            ClaimQuestAsync(questId).Forget();
        }

        private async UniTask ClaimQuestAsync(string questId)
        {
            var result = await _questService.ClaimAsync(questId);
            if (result.IsSuccess)
            {
                _toast.Show("Quest eingeloest!", ToastKind.Success);
                await OnEnterAsync(CancellationToken.None); // Re-Load Save + Header refresh
            }
            else
            {
                _toast.Show(result.ErrorMessage ?? "Fehler beim Einloesen", ToastKind.Danger);
            }
        }

        // ============================================================
        // Shop-Tab Bindings
        // ============================================================

        private void BindShopTab()
        {
            _shopPacksGrid = Q<VisualElement>("shop-packs-grid");
            _shopDirectList = Q<VisualElement>("shop-direct-list");
            _shopBuyDiamondsButton = Q<Button>("shop-buy-diamonds");

            _shopBuyDiamondsButton.clicked += () =>
                _toast.Show("IAP-Integration kommt in einer spaeteren Stufe.", ToastKind.Info);
        }

        private void RefreshShopTab()
        {
            _shopPacksGrid.Clear();
            _shopDirectList.Clear();

            foreach (var pack in _shopController.AvailablePacks)
                _shopPacksGrid.Add(BuildPackTile(pack));

            // Direkt-Angebote (Energie-Nachkauf, Scrap-Pakete, etc.)
            _shopDirectList.Add(BuildDirectOffer("60 Energie sofort",
                "100 Diamanten",
                () => BuyEnergyAsync(60, 100).Forget()));
            _shopDirectList.Add(BuildDirectOffer("Bonus-Energie +30 (8h)",
                "50 Diamanten",
                () => BuyEnergyAsync(30, 50).Forget()));
        }

        private VisualElement BuildPackTile(CardPackDefinition pack)
        {
            var tile = new VisualElement { name = $"pack-{pack.Id}" };
            tile.AddToClassList("ak-surface-elevated");
            tile.style.width = 220;
            tile.style.marginRight = 12;
            tile.style.marginBottom = 12;
            tile.style.alignItems = Align.Center;

            var name = new Label(LocalizeFallback(pack.DisplayNameKey, pack.Id));
            name.AddToClassList("ak-h4");
            tile.Add(name);

            var meta = new Label($"{pack.CardCount} Karten • Min: {pack.GuaranteedMinRarity}");
            meta.AddToClassList("ak-caption");
            meta.style.marginBottom = 8;
            tile.Add(meta);

            var price = new Label($"{pack.DiamondCost} Diamanten");
            price.AddToClassList("ak-h4");
            price.AddToClassList("ak-text--accent");
            tile.Add(price);

            var buyBtn = new Button(() => BuyPackAsync(pack).Forget()) { text = "Kaufen" };
            buyBtn.AddToClassList("ak-btn");
            buyBtn.AddToClassList("ak-btn--primary");
            buyBtn.style.marginTop = 8;
            buyBtn.style.minWidth = 140;
            tile.Add(buyBtn);

            return tile;
        }

        private VisualElement BuildDirectOffer(string title, string price, System.Action onBuy)
        {
            var row = new VisualElement();
            row.AddToClassList("ak-surface");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var name = new Label(title);
            name.AddToClassList("ak-body");
            name.style.flexGrow = 1;
            row.Add(name);

            var priceLabel = new Label(price);
            priceLabel.AddToClassList("ak-caption");
            priceLabel.AddToClassList("ak-text--accent");
            priceLabel.style.marginRight = 12;
            row.Add(priceLabel);

            var btn = new Button(onBuy) { text = "Kaufen" };
            btn.AddToClassList("ak-btn");
            btn.AddToClassList("ak-btn--sm");
            btn.AddToClassList("ak-btn--accent");
            row.Add(btn);

            return row;
        }

        private async UniTask BuyPackAsync(CardPackDefinition pack)
        {
            var result = await _shopController.BuyPackAsync(pack);
            if (result.Success)
            {
                var rarities = string.Join(", ", result.AwardedRarities);
                _toast.Show($"Pack geoeffnet: {rarities}" +
                            (result.PityTriggered ? " (Pity!)" : ""),
                    ToastKind.Success, 6f);
                await OnEnterAsync(CancellationToken.None);
            }
            else
            {
                _toast.Show(result.Error ?? "Pack-Kauf fehlgeschlagen", ToastKind.Danger);
            }
        }

        private async UniTask BuyEnergyAsync(int amount, long diamondCost)
        {
            var result = await _shopController.BuyEnergyAsync(amount, diamondCost);
            if (result.IsSuccess)
            {
                _toast.Show($"+{amount} Energie", ToastKind.Success);
                await OnEnterAsync(CancellationToken.None);
            }
            else
            {
                _toast.Show(result.ErrorMessage ?? "Energie-Kauf fehlgeschlagen", ToastKind.Danger);
            }
        }

        // ============================================================
        // Arena-Tab Bindings
        // ============================================================

        private void BindArenaTab()
        {
            _arenaRankName = Q<Label>("arena-rank-name");
            _arenaRankPoints = Q<Label>("arena-rank-points");
            _arenaRankFill = Q<VisualElement>("arena-rank-fill");
            _arenaTicketsValue = Q<Label>("arena-tickets-value");
            _arenaSearchMatch = Q<Button>("arena-search-match");
            _arenaViewLeaderboard = Q<Button>("arena-view-leaderboard");

            _arenaSearchMatch.clicked += () =>
                _toast.Show("Matchmaking folgt in Stufe 9.", ToastKind.Info);
            _arenaViewLeaderboard.clicked += () =>
                _toast.Show("Leaderboard folgt in Stufe 9.", ToastKind.Info);
        }

        private void RefreshArenaTab()
        {
            if (_saveCached == null) return;
            var tickets = _saveCached.Currencies.ArenaTickets;
            var meritPoints = _saveCached.Currencies.MeritPoints;

            _arenaTicketsValue.text = tickets.ToString();
            // Bronze III/II/I, Silber III/II/I, Gold III/II/I, Platin III/II/I, Diamant III/II/I, Meister
            var (rankName, pointsInTier, tierMax) = ComputeRank(meritPoints);
            _arenaRankName.text = rankName;
            _arenaRankPoints.text = $"{pointsInTier} / {tierMax} Rang-Punkte";

            var pct = tierMax > 0 ? (float)pointsInTier * 100f / tierMax : 0f;
            _arenaRankFill.style.width = new Length(System.Math.Min(pct, 100f), LengthUnit.Percent);
        }

        /// <summary>
        /// Vereinfachte Rang-Berechnung — 100 Punkte pro Tier, 3 Tiers pro Liga,
        /// 5 Ligen + Meister (insgesamt 16 Stufen, max 1.500 Pkt).
        /// </summary>
        private static (string rank, int pointsInTier, int tierMax) ComputeRank(long merit)
        {
            string[] leagues = { "Bronze", "Silber", "Gold", "Platin", "Diamant", "Meister" };
            string[] tiers = { "III", "II", "I" };
            const int pointsPerTier = 100;

            var capped = System.Math.Min(merit, 5 * 3 * pointsPerTier); // 5 Ligen × 3 Tiers × 100
            var totalTiers = (int)(capped / pointsPerTier);
            if (totalTiers >= 15)
                return ("Meister", (int)(merit - 1500), 1000);

            var league = totalTiers / 3;
            var tier = totalTiers % 3;
            return ($"{leagues[league]} {tiers[tier]}",
                    (int)(capped % pointsPerTier),
                    pointsPerTier);
        }

        // ============================================================
        // More-Tab Bindings + Sub-Navigation
        // ============================================================

        private void BindMoreTab()
        {
            _moreDeckBuilder = Q<Button>("more-deck-builder");
            _moreWorldMap = Q<Button>("more-world-map");
            _moreGuild = Q<Button>("more-guild");
            _moreFriends = Q<Button>("more-friends");
            _moreSaisonPass = Q<Button>("more-saison-pass");
            _moreCodex = Q<Button>("more-codex");
            _moreSettings = Q<Button>("more-settings");

            _moreDeckBuilder.clicked += () => GoToScreen(ScreenId.DeckBuilder, "Deck-Builder kommt in Stufe 6.");
            _moreWorldMap.clicked   += () => GoToScreen(ScreenId.WorldMap, "Welt-Karte kommt in Stufe 7.");
            _moreGuild.clicked      += () => GoToScreen(ScreenId.Guild, "Gilde kommt in Stufe 9.");
            _moreFriends.clicked    += () => GoToScreen(ScreenId.Friends, "Freunde kommen in Stufe 9.");
            _moreSaisonPass.clicked += () => GoToScreen(ScreenId.SaisonPass, "Saison-Pass kommt in Stufe 9.");
            _moreCodex.clicked      += () => GoToScreen(ScreenId.Codex, "Codex kommt in Stufe 10.");
            _moreSettings.clicked   += () => GoToScreen(ScreenId.Settings, "Einstellungen kommen in Stufe 10.");
        }

        private void GoToScreen(string id, string fallbackMessage)
        {
            if (_screenManager.IsRegistered(id))
                _screenManager.PushAsync(id).Forget();
            else
                _toast.Show(fallbackMessage, ToastKind.Info);
        }

        // ============================================================
        // Lokalisierungs-Fallback
        // ============================================================

        /// <summary>Bis Localization verdrahtet ist: Key-Suffix als lesbarer Text.</summary>
        private static string LocalizeFallback(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            var dot = key.LastIndexOf('.');
            if (dot < 0 || dot >= key.Length - 1) return fallback;
            var raw = key.Substring(dot + 1).Replace('_', ' ');
            return raw.Length == 0 ? fallback : char.ToUpper(raw[0]) + raw.Substring(1);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _refreshCts?.Cancel();
            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var result = await _save.LoadAsync(_refreshCts.Token);
            if (!result.IsSuccess)
            {
                _toast.Show($"Save laden fehlgeschlagen: {result.ErrorMessage}", ToastKind.Danger);
                return;
            }
            _saveCached = result.Value;
            RefreshHeader();
            RefreshCardsGrid();
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _refreshCts?.Cancel();
            _refreshCts = null;
            return UniTask.CompletedTask;
        }

        // ============================================================
        // Header
        // ============================================================

        private void RefreshHeader()
        {
            if (_saveCached == null) return;
            var p = _saveCached.Profile;
            var c = _saveCached.Currencies;

            _displayName.text  = string.IsNullOrEmpty(p.DisplayName) ? "Spieler" : p.DisplayName;
            _levelLabel.text   = $"Stufe {p.Level}";
            _energyValue.text  = c.EnergyBonus > 0
                ? $"{c.TotalEnergy}/{PlayerCurrencies.EnergyDefaultCap} (+{c.EnergyBonus})"
                : $"{c.Energy}/{PlayerCurrencies.EnergyDefaultCap}";
            _goldValue.text    = FormatNumber(c.Gold);
            _diamondValue.text = FormatNumber(c.Diamond);
        }

        // ============================================================
        // Tabs
        // ============================================================

        private void SwitchTab(string id)
        {
            SetActive(_tabCards,  id == "cards");
            SetActive(_tabQuests, id == "quests");
            SetActive(_tabShop,   id == "shop");
            SetActive(_tabArena,  id == "arena");
            SetActive(_tabMore,   id == "more");

            SetTabButtonActive(_btnCards,  id == "cards");
            SetTabButtonActive(_btnQuests, id == "quests");
            SetTabButtonActive(_btnShop,   id == "shop");
            SetTabButtonActive(_btnArena,  id == "arena");
            SetTabButtonActive(_btnMore,   id == "more");

            // Lazy-Refresh: nur den gerade aktivierten Tab neu zeichnen.
            switch (id)
            {
                case "cards":  RefreshCardsGrid(); break;
                case "quests": UpdateQuestsFilterButtonStyles(); RefreshQuestsTab(); break;
                case "shop":   RefreshShopTab(); break;
                case "arena":  RefreshArenaTab(); break;
                // "more" hat keine dynamischen Daten — Buttons sind statisch verdrahtet
            }

            GameLogger.Verbose("Hub", $"Tab -> {id}");
        }

        private static void SetActive(VisualElement panel, bool active)
        {
            if (active) panel.RemoveFromClassList("ak-hidden");
            else panel.AddToClassList("ak-hidden");
        }

        private static void SetTabButtonActive(VisualElement button, bool active)
        {
            if (active) button.AddToClassList("ak-tab--active");
            else button.RemoveFromClassList("ak-tab--active");
        }

        // ============================================================
        // Cards-Tab
        // ============================================================

        private void RefreshCardsGrid()
        {
            _cardsGrid.Clear();
            if (_saveCached == null) return;

            // Welche Karten besitzt der Spieler? (Card-Inventory enthaelt CardInstance,
            // wir zaehlen pro CardDefinitionId)
            var ownedCounts = new Dictionary<string, int>();
            foreach (var instance in _saveCached.CardInventory.Values)
            {
                var defId = instance.CardDefinitionId;
                ownedCounts[defId] = ownedCounts.TryGetValue(defId, out var c) ? c + 1 : 1;
            }

            var rarityFilter = ParseRarityFilter(_cardsRarityFilter.value);
            var elementFilter = ParseElementFilter(_cardsElementFilter.value);
            var search = (_cardsSearch.value ?? string.Empty).Trim().ToLowerInvariant();

            // Wir zeigen ALLE Cards aus dem Catalog. Nicht besessene werden "locked" dargestellt.
            // So sieht der Spieler die Vollstaendigkeit der Collection.
            var cards = _cardCatalog.AllCards
                .Where(c => rarityFilter == null || c.Rarity == rarityFilter)
                .Where(c => elementFilter == null || c.Element == elementFilter)
                .Where(c => string.IsNullOrEmpty(search)
                            || c.Id.ToLowerInvariant().Contains(search))
                .ToList();

            foreach (var card in cards)
            {
                var owned = ownedCounts.ContainsKey(card.Id);
                var tile = CardTileFactory.Build(card,
                    onClick: OnCardClicked,
                    locked: !owned);
                _cardsGrid.Add(tile);
            }

            var empty = cards.Count == 0;
            SetActive(_cardsEmpty, empty);
            SetActive(_cardsGrid, !empty);
        }

        private void OnCardClicked(CardDefinition card)
        {
            // Stufe 5 bringt das Card-Detail-Modal. Bis dahin nur Toast.
            _toast.Show($"{card.Id} ({card.Rarity}, {card.Element})", ToastKind.Info);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static Rarity? ParseRarityFilter(string value) => value switch
        {
            "Gewoehnlich"   => Rarity.Gewoehnlich,
            "Ungewoehnlich" => Rarity.Ungewoehnlich,
            "Selten"        => Rarity.Selten,
            "Epic"          => Rarity.Epic,
            "Legendaer"     => Rarity.Legendaer,
            _               => null
        };

        private static Element? ParseElementFilter(string value) => value switch
        {
            "Natur"  => Element.Natur,
            "Feuer"  => Element.Feuer,
            "Wasser" => Element.Wasser,
            "Licht"  => Element.Licht,
            "Dunkel" => Element.Dunkel,
            _        => null
        };

        private static string FormatNumber(long n)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
            if (n >= 10_000)    return $"{n / 1_000.0:0.#}K";
            return n.ToString("N0");
        }
    }
}
