#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.Game.Catalog;
using ArcaneKingdom.Game.Quest;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Hub
{
    /// <summary>
    /// Hub-Welt (Spielplan v5 Kap. 3, Referenz: Design_Entwurf_Login_HubWelt.html, Screen 3).
    /// Stadt-Szene (hub_main) als Hintergrund mit Top-Bar (Profil + Ressourcen), Energie-Leiste,
    /// Event-Banner, klickbarem Gebaeude-Grid + Right-Nav und Bottom-Navigation.
    ///
    /// Der Hub ist ein reiner Navigations-Knoten: jedes Gebaeude / jeder Nav-Button fuehrt
    /// per ScreenManager zu einem eigenstaendigen Screen. Daily-Income, PendingClaims und
    /// Quest-Restore laufen beim Hub-Eintritt.
    /// </summary>
    public sealed class HubScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly QuestService _questService;
        private readonly ToastService _toast;
        private readonly ArcaneKingdom.Game.World.PrestigeAppService _prestige;
        private readonly ArcaneKingdom.Game.Hub.HubController _hubController;
        private readonly UIAssetService _uiAssets;
        private readonly ILocalizationService _loc;
        private readonly CardCatalogService _cardCatalog;
        private readonly System.Random _rewardRng = new();

        // Top-Bar
        private Label _avatarInitials = null!;
        private Label _displayName = null!;
        private Label _guildTag = null!;
        private Label _levelLabel = null!;
        private Label _levelBadge = null!;
        private Label _arenaBadge = null!;
        private Label _energyValue = null!;
        private Label _goldValue = null!;
        private Label _diamondValue = null!;
        private VisualElement _energyFill = null!;

        private PlayerSave? _saveCached;
        private CancellationTokenSource? _refreshCts;

        public override string Id => ScreenId.Hub;
        protected override string UxmlPath => "UI/HubScreen";

        public HubScreen(ScreenManager screenManager,
                         ISaveService<PlayerSave> save,
                         QuestService questService,
                         ToastService toast,
                         ArcaneKingdom.Game.World.PrestigeAppService prestige,
                         ArcaneKingdom.Game.Hub.HubController hubController,
                         UIAssetService uiAssets,
                         ILocalizationService loc,
                         CardCatalogService cardCatalog)
        {
            _screenManager = screenManager;
            _save = save;
            _questService = questService;
            _toast = toast;
            _prestige = prestige;
            _hubController = hubController;
            _uiAssets = uiAssets;
            _loc = loc;
            _cardCatalog = cardCatalog;
        }

        protected override void BindElements(VisualElement root)
        {
            // Stadt-Szene als formatfuellender Hintergrund (16:9, scale-and-crop).
            _uiAssets.ApplyUIBackground(root, "hub_main");

            // === Top-Bar ===
            _avatarInitials = Q<Label>("header-avatar-initials");
            _displayName    = Q<Label>("header-display-name");
            _guildTag       = Q<Label>("header-guild-tag");
            _levelLabel     = Q<Label>("header-level");
            _levelBadge     = Q<Label>("header-level-badge");
            _arenaBadge     = Q<Label>("header-arena-badge");
            _energyValue    = Q<Label>("header-energy-value");
            _goldValue      = Q<Label>("header-gold-value");
            _diamondValue   = Q<Label>("header-diamond-value");
            _energyFill     = Q<VisualElement>("hub-energy-fill");

            // Avatar-Sprite + Gold-Frame-Ring + ausgeblendete Initialen (Sprite ist sichtbar).
            var headerAvatar = Q<VisualElement>("header-avatar");
            _uiAssets.ApplyAvatar(headerAvatar, "avatar01");
            _avatarInitials.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            var frameOverlay = new VisualElement { name = "header-avatar-frame" };
            frameOverlay.style.position = Position.Absolute;
            frameOverlay.style.left = -4;
            frameOverlay.style.top = -4;
            frameOverlay.style.right = -4;
            frameOverlay.style.bottom = -4;
            frameOverlay.pickingMode = PickingMode.Ignore;
            _uiAssets.ApplyBackground(frameOverlay, "Icons/Frames/frame_gold", ScaleMode.ScaleToFit);
            headerAvatar.Add(frameOverlay);

            // Ressourcen-Pill-Icons + Energie-Icon durch generierte Sprites ersetzen.
            ApplyCurrencyPillIcon("header-gold-pill",   "currency_gold");
            ApplyCurrencyPillIcon("header-diamond-pill","currency_diamant");
            _uiAssets.ApplyCurrencyIcon(Q<VisualElement>("hub-energy-icon"), "currency_energie");

            // === Gebaeude-Grid (Navigation zu eigenstaendigen Screens) ===
            BindNav("building-cards",      ScreenId.Codex,          "Karten-Sammlung nicht verfuegbar.");
            BindNav("building-schmiede",   ScreenId.Schmiede,       "Zauberschmiede nicht verfuegbar.");
            BindNav("building-bibliothek", ScreenId.QuestCenter,    "Bibliothek nicht verfuegbar.");
            BindNav("building-tempel",     ScreenId.Tempel,         "Tempel nicht verfuegbar.");
            BindNav("building-gilde",      ScreenId.Guild,          "Gilde nicht verfuegbar.");
            BindNav("building-markt",      ScreenId.Shop,           "Marktplatz nicht verfuegbar.");
            BindNav("building-ehre",       ScreenId.MeritRanking,   "Wand der Ehre nicht verfuegbar.");
            BindNav("building-post",       ScreenId.ChatOverlay,    "Postamt nicht verfuegbar.");

            // === Right-Nav ===
            BindNav("nav-worldmap", ScreenId.WorldMap,      "Welt-Karte nicht verfuegbar.");
            BindNav("nav-arena",    ScreenId.Arena,         "Arena nicht verfuegbar.");
            BindNav("nav-runes",    ScreenId.Runes,         "Runen-Verwaltung nicht verfuegbar.");
            BindNav("nav-profile",  ScreenId.PlayerProfile, "Spieler-Profil nicht verfuegbar.");

            // === Bottom-Navigation ===
            BindNav("bottom-menu",    ScreenId.Settings,    "Einstellungen nicht verfuegbar.");
            BindNav("bottom-shop",    ScreenId.Shop,        "Laden nicht verfuegbar.");
            BindNav("bottom-deck",    ScreenId.DeckBuilder, "Deck-Builder nicht verfuegbar.");
            BindNav("bottom-friends", ScreenId.Friends,     "Freunde nicht verfuegbar.");
            // "bottom-hub" ist der aktive Screen — kein Handler noetig.
        }

        /// <summary>Verkabelt einen Button mit der Navigation zu einem Screen (Fallback-Toast wenn nicht registriert).</summary>
        private void BindNav(string buttonName, string screenId, string fallbackMessage)
        {
            var btn = QOptional<Button>(buttonName);
            if (btn != null)
                btn.clicked += () => GoToScreen(screenId, fallbackMessage);
        }

        private void GoToScreen(string id, string fallbackMessage)
        {
            if (_screenManager.IsRegistered(id))
                _screenManager.PushAsync(id).Forget();
            else
                _toast.Show(fallbackMessage, ToastKind.Info);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            _refreshCts?.Cancel();
            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _refreshCts.Token;

            // Quest-Fortschritt aus dem Save wiederherstellen (idempotent).
            var restoreR = await _save.LoadAsync(token);
            if (restoreR.IsSuccess && restoreR.Value != null)
                _questService.RestoreFromSave(restoreR.Value);

            // Offene PendingClaims (Season-Rewards etc.) atomar + idempotent einloesen.
            await RedeemPendingClaimsAsync(token);

            // Daily-Income Tick (Designplan v4 Oeko Kap. 6.3) — passives Gold pro Welt.
            var incomeTick = await _prestige.TickDailyIncomeAsync(System.DateTime.UtcNow, token);
            if (incomeTick.IsSuccess && incomeTick.Value > 0)
            {
                _toast.Show(string.Format(
                    _loc.Get("hub.passive_income", "Passives Einkommen: +{0} Gold"),
                    incomeTick.Value.ToString("N0")), ToastKind.Success);
            }

            // Energie-Regeneration (Spielplan v5 Kap. 7.6: 1 Energie / 6 Min bis Cap 60).
            // Wird beim Hub-Eintritt aus der vergangenen Zeit nachberechnet.
            await _hubController.RegenerateEnergyAsync(token);

            var result = await _save.LoadAsync(token);
            if (!result.IsSuccess)
            {
                _toast.Show(string.Format(
                    _loc.Get("hub.save_load_failed", "Save laden fehlgeschlagen: {0}"),
                    result.ErrorMessage), ToastKind.Danger);
                return;
            }
            _saveCached = result.Value;
            RefreshHeader();

            await _questService.FlushAsync(token);
        }

        public override async UniTask OnLeaveAsync(CancellationToken ct)
        {
            await _questService.FlushAsync(ct);
            _refreshCts?.Cancel();
            _refreshCts = null;
        }

        /// <summary>
        /// Loest alle offenen PendingClaims des Spielers ein (Currency/Scrap/Pack/Card/
        /// FeatureUnlock/RuneSlotUnlock/Title/AvatarFrame). Atomar im MutateAsync-Lambda,
        /// jeder eingeloeste Eintrag wird aus PendingClaims entfernt -> idempotent.
        /// </summary>
        private async UniTask RedeemPendingClaimsAsync(CancellationToken ct)
        {
            var redeemed = 0;
            await _save.MutateAsync(save =>
            {
                if (save.PendingClaims == null || save.PendingClaims.Count == 0) return save;

                var pending = new List<ArcaneKingdom.Domain.Save.PendingClaim>(save.PendingClaims);
                save.PendingClaims.Clear();

                foreach (var claim in pending)
                {
                    switch (claim.Kind)
                    {
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Currency:
                            ApplyCurrencyClaim(save, claim.SubType, claim.Amount);
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Scrap:
                            if (System.Enum.TryParse<ArcaneKingdom.Domain.Economy.ScrapType>(claim.SubType, out var scrap))
                                save.Currencies.AddScraps(scrap, claim.Amount);
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Card:
                            for (var i = 0; i < claim.Amount; i++)
                            {
                                // Platzhalter-Token (z.B. "card_random_4star") zu einer echten Karte
                                // aufloesen — vorher wurde der rohe Token als CardDefinitionId angelegt,
                                // was eine unbenutzbare "Geister-Karte" im Inventar erzeugte.
                                var cardId = ResolveRewardCardId(claim.SubType);
                                if (cardId == null) continue;   // nicht aufloesbar -> keinen Muell anlegen
                                var instId = System.Guid.NewGuid().ToString("N");
                                save.CardInventory[instId] = new CardInstance(
                                    instId, cardId, 0, 0, System.DateTime.UtcNow);
                            }
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Rune:
                            for (var i = 0; i < claim.Amount; i++)
                            {
                                var runeId = System.Guid.NewGuid().ToString("N");
                                save.RuneInventory[runeId] = new ArcaneKingdom.Domain.Runes.RuneInstance(
                                    runeId, claim.SubType, 1, System.DateTime.UtcNow);
                            }
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.FeatureUnlock:
                            save.UnlockedFeatureKeys ??= new HashSet<string>();
                            if (!string.IsNullOrEmpty(claim.SubType))
                                save.UnlockedFeatureKeys.Add(claim.SubType);
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.RuneSlotUnlock:
                            save.UnlockedFeatureKeys ??= new HashSet<string>();
                            save.UnlockedFeatureKeys.Add($"rune_slot_{claim.SubType}");
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Title:
                            save.UnlockedFeatureKeys ??= new HashSet<string>();
                            save.UnlockedFeatureKeys.Add($"title_{claim.SubType}");
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.AvatarFrame:
                            save.UnlockedFeatureKeys ??= new HashSet<string>();
                            save.UnlockedFeatureKeys.Add($"frame_{claim.SubType}");
                            break;
                        case ArcaneKingdom.Domain.Save.PendingClaimKind.Pack:
                            // Pack behaelt seinen Pending-Eintrag (Oeffnen passiert separat im Shop).
                            save.PendingClaims.Add(claim);
                            continue;
                    }
                    redeemed++;
                }
                return save;
            }, ct);

            if (redeemed > 0)
                _toast.Show(string.Format(
                    _loc.Get("hub.claims_redeemed", "{0} Belohnung(en) eingeloest"), redeemed),
                    ToastKind.Success, 4f);
        }

        /// <summary>
        /// Loest eine Belohnungs-Karten-Referenz zu einer echten Karten-ID auf. Konkrete IDs
        /// (existieren im Catalog) werden direkt zurueckgegeben; Platzhalter-Token aus den
        /// Belohnungs-JSONs ("card_random_4star", "card_chosen_3star", "any_rare", ...) zu einer
        /// zufaelligen, NICHT-exklusiven Karte der jeweiligen Seltenheit. Nicht aufloesbare Token
        /// (z.B. "card_specific" ohne erkennbare Seltenheit) liefern null.
        /// </summary>
        private string? ResolveRewardCardId(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            if (_cardCatalog.Find(token) != null) return token;   // schon eine echte Karte

            var rarity = RarityFromToken(token);
            if (rarity == null) return null;

            var pool = _cardCatalog.AllCards
                .Where(c => c.Rarity == rarity.Value && !c.IsExclusive)
                .ToList();
            if (pool.Count == 0) return null;
            return pool[_rewardRng.Next(pool.Count)].Id;
        }

        /// <summary>Leitet die Seltenheit aus einem Belohnungs-Token ab. "uncommon" wird vor "common"
        /// geprueft (Substring-Falle).</summary>
        private static Rarity? RarityFromToken(string token)
        {
            var t = token.ToLowerInvariant();
            if (t.Contains("2star") || t.Contains("uncommon") || t.Contains("ungewoehnlich")) return Rarity.Ungewoehnlich;
            if (t.Contains("1star") || t.Contains("common") || t.Contains("gewoehnlich")) return Rarity.Gewoehnlich;
            if (t.Contains("3star") || t.Contains("rare") || t.Contains("selten")) return Rarity.Selten;
            if (t.Contains("4star") || t.Contains("epic")) return Rarity.Epic;
            if (t.Contains("5star") || t.Contains("legend")) return Rarity.Legendaer;
            if (t.Contains("6star") || t.Contains("myth")) return Rarity.Mythisch;
            return null;
        }

        /// <summary>Schreibt eine Currency-PendingClaim auf den passenden Saldo.</summary>
        private static void ApplyCurrencyClaim(PlayerSave save, string subType, long amount)
        {
            switch (subType)
            {
                case nameof(ArcaneKingdom.Domain.Economy.Currency.Gold):            save.Currencies.AddGold(amount); break;
                case nameof(ArcaneKingdom.Domain.Economy.Currency.Diamond):         save.Currencies.AddDiamond(amount); break;
                case nameof(ArcaneKingdom.Domain.Economy.Currency.UniversalScraps): save.Currencies.AddUniversalScraps(amount); break;
                case nameof(ArcaneKingdom.Domain.Economy.Currency.MeritPoints):     save.Currencies.AddMeritPoints(amount); break;
                case nameof(ArcaneKingdom.Domain.Economy.Currency.GuildPoints):     save.Currencies.AddGuildPoints(amount); break;
                case nameof(ArcaneKingdom.Domain.Economy.Currency.Energy):          save.Currencies.AddEnergyAdaptive((int)amount); break;
            }
        }

        // ============================================================
        // Top-Bar
        // ============================================================

        private void RefreshHeader()
        {
            if (_saveCached == null) return;
            var p = _saveCached.Profile;
            var c = _saveCached.Currencies;

            var name = string.IsNullOrEmpty(p.DisplayName) ? "Spieler" : p.DisplayName;
            _displayName.text    = name;
            _avatarInitials.text = ComputeInitials(name);
            _guildTag.text       = string.IsNullOrEmpty(p.GuildId)
                ? string.Empty
                : $"[{p.GuildId.Substring(0, System.Math.Min(5, p.GuildId.Length)).ToUpperInvariant()}]";

            _levelLabel.text = p.Level.ToString();
            _levelBadge.text = $"LV {p.Level}";

            // Arena-Rang nur zeigen, wenn der Spieler bereits gewertet ist (sonst ausblenden).
            _arenaBadge.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            var energy = c.EnergyBonus > 0 ? c.TotalEnergy : c.Energy;
            var energyCap = PlayerCurrencies.EnergyDefaultCap;
            _energyValue.text = $"{energy}/{energyCap}";
            var ratio = energyCap > 0 ? Mathf.Clamp01((float)energy / energyCap) : 0f;
            _energyFill.style.width = new Length(ratio * 100f, LengthUnit.Percent);

            _goldValue.text    = FormatNumber(c.Gold);
            _diamondValue.text = FormatNumber(c.Diamond);
        }

        /// <summary>1-2 Initialen aus dem DisplayName (z.B. "Robert Schneider" -> "RS").</summary>
        private static string ComputeInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ', '_', '-' },
                System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return char.ToUpper(parts[0][0]).ToString();
            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        }

        /// <summary>
        /// Currency-Format nach DESIGN.md 3.2: Tausender-Trennung mit "." bis 100 Mio,
        /// danach Mio/Mrd-Kurzform (sonst wird die Pill zu breit).
        /// </summary>
        private static string FormatNumber(long n)
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            if (n >= 1_000_000_000) return string.Format(culture, "{0:0.##} Mrd", n / 1_000_000_000.0);
            if (n >= 100_000_000)   return string.Format(culture, "{0:0.#} Mio", n / 1_000_000.0);
            return n.ToString("N0", culture);
        }

        /// <summary>
        /// Ersetzt das textbasierte Icon-Label einer Currency-Pill durch ein echtes Sprite.
        /// Das erste Kind der Pill MUSS das Icon-Label sein.
        /// </summary>
        private void ApplyCurrencyPillIcon(string pillName, string currencyId)
        {
            var pill = QOptional<VisualElement>(pillName);
            if (pill == null || pill.childCount == 0) return;
            if (pill[0] is Label iconLabel)
            {
                _uiAssets.ApplyCurrencyIcon(iconLabel, currencyId);
                iconLabel.text = string.Empty;
                iconLabel.style.minWidth = 18;
                iconLabel.style.minHeight = 18;
                iconLabel.style.width = 18;
                iconLabel.style.height = 18;
            }
        }
    }
}
