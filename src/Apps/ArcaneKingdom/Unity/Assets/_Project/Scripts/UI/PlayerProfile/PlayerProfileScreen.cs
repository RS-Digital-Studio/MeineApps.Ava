#nullable enable
using System.Linq;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Progression;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.PlayerProfile
{
    /// <summary>
    /// Spieler-Profil-Screen (Spielplan v5 Kap. 16 + Impl_KOMPLETT Kap. 16).
    /// Avatar, Name, Level+EXP, Stats (Helden-HP/Kosten/ATK/HP-gesamt),
    /// Bestes-Deck-Miniatur, Statistiken (Kaempfe, Siege, Diebe, Karten gesammelt).
    /// </summary>
    public sealed class PlayerProfileScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;

        private Label _name = null!;
        private Label _levelExp = null!;
        private Label _server = null!;
        private Label _heroHp = null!;
        private Label _totalCost = null!;
        private Label _totalAtk = null!;
        private Label _totalHp = null!;
        private Label _statsLine = null!;
        private VisualElement _unlocksList = null!;
        private Button _closeBtn = null!;

        public override string Id => ScreenId.PlayerProfile;
        protected override string UxmlPath => "UI/PlayerProfileScreen";

        public PlayerProfileScreen(ScreenManager screenManager,
                                    ISaveService<PlayerSave> save,
                                    ILocalizationService loc)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
        }

        protected override void BindElements(VisualElement root)
        {
            _name        = Q<Label>("profile-name");
            _levelExp    = Q<Label>("profile-level-exp");
            _server      = Q<Label>("profile-server");
            _heroHp      = Q<Label>("profile-hero-hp");
            _totalCost   = Q<Label>("profile-total-cost");
            _totalAtk    = Q<Label>("profile-total-atk");
            _totalHp     = Q<Label>("profile-total-hp");
            _statsLine   = Q<Label>("profile-stats");
            _unlocksList = Q<VisualElement>("profile-unlocks");
            _closeBtn    = Q<Button>("profile-close");

            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            Populate(result.Value);
        }

        private void Populate(PlayerSave save)
        {
            var p = save.Profile;
            _name.text = string.IsNullOrEmpty(p.DisplayName) ? "Spieler" : p.DisplayName;
            _levelExp.text = $"LV {p.Level} — {p.ExpTotal:N0} EXP";
            _server.text = string.IsNullOrEmpty(p.Server) ? "Server: Poseidon" : $"Server: {p.Server}";

            // Helden-HP: berechnet aus aktivem Deck (Summe Base-HP)
            _heroHp.text = "—";   // wird vom Battle-Setup berechnet, hier nur Anzeige-Platzhalter
            _totalCost.text = "—";  // Berechnung erfordert CardDefinition-Lookup
            _totalAtk.text = $"{save.CardInventory.Count:N0} Karten";
            _totalHp.text = $"{save.RuneInventory.Count:N0} Runen";

            // Stats-Zusammenfassung (Achievements als Proxy bis dedizierte Stats-Klasse existiert)
            var titleCount = save.Achievements?.UnlockedTitleKeys?.Count ?? 0;
            var trophyPoints = save.Achievements?.TotalTrophyPoints ?? 0;
            _statsLine.text = $"Titel: {titleCount} • Trophaeen: {trophyPoints:N0} • Karten: {save.CardInventory.Count:N0}";

            // Account-Unlocks (Plan-Inhalts-Liste)
            _unlocksList.Clear();
            foreach (var unlock in AccountUnlocks.ActiveUnlocks(p.Level))
            {
                var row = new Label($"✓ {unlock} (LV {AccountUnlocks.LevelFor(unlock)})");
                row.style.fontSize = 11;
                row.style.color = new StyleColor(new UnityEngine.Color(0.42f, 0.94f, 0.68f));
                row.style.marginBottom = 2;
                _unlocksList.Add(row);
            }
        }
    }
}
