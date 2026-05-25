#nullable enable
using System.Collections.Generic;
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.RaceSelection
{
    /// <summary>
    /// Onboarding-Screen: Rassen-Wahl beim ersten Spielstart (Designplan v4 Kap. 2.1 + Story-Kap. 3).
    /// Spieler waehlt zwischen Ritter / Elfen / Tiergeister / Daemonen.
    /// Goetter sind NICHT waehlbar (nur durch Crafting/Endgame erreichbar).
    ///
    /// Die Wahl bestimmt:
    ///   1. Helden-Passiv-Skill (KoeniglicheAura/Waldlaeufer/Rudelbund/LebensraubAura)
    ///   2. Tutorial-Mentor-NPC (Aldor/Lira/Grimmfang/Lilith)
    ///   3. Starter-Karten-Schwerpunkt
    /// </summary>
    public sealed class RaceSelectionScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ILocalizationService _loc;
        private readonly ToastService _toast;

        private readonly Dictionary<Race, VisualElement> _raceCards = new();
        private Race _selectedRace = Race.Ritter;

        private Label _selectedNameLabel = null!;
        private Label _selectedPassivLabel = null!;
        private Label _selectedMentorLabel = null!;
        private Button _confirmButton = null!;

        public override string Id => ScreenId.RaceSelection;
        protected override string UxmlPath => "UI/RaceSelectionScreen";

        public RaceSelectionScreen(
            ScreenManager screenManager,
            ISaveService<PlayerSave> save,
            ILocalizationService loc,
            ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _loc = loc;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _selectedNameLabel = Q<Label>("selected-race-name");
            _selectedPassivLabel = Q<Label>("selected-passiv");
            _selectedMentorLabel = Q<Label>("selected-mentor");
            _confirmButton = Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;

            // 4 Karten-Buttons fuer Ritter / Elfen / Tiergeister / Daemonen
            var ritterCard = Q<VisualElement>("race-card-ritter");
            var elfenCard = Q<VisualElement>("race-card-elfen");
            var tierCard = Q<VisualElement>("race-card-tiergeister");
            var daemonCard = Q<VisualElement>("race-card-daemonen");

            _raceCards[Race.Ritter] = ritterCard;
            _raceCards[Race.Elfen] = elfenCard;
            _raceCards[Race.Tiergeister] = tierCard;
            _raceCards[Race.Daemonen] = daemonCard;

            ritterCard.AddManipulator(new Clickable(() => OnRaceClicked(Race.Ritter)));
            elfenCard.AddManipulator(new Clickable(() => OnRaceClicked(Race.Elfen)));
            tierCard.AddManipulator(new Clickable(() => OnRaceClicked(Race.Tiergeister)));
            daemonCard.AddManipulator(new Clickable(() => OnRaceClicked(Race.Daemonen)));

            RefreshSelection();
        }

        private void OnRaceClicked(Race race)
        {
            _selectedRace = race;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (var kv in _raceCards)
            {
                if (kv.Key == _selectedRace) kv.Value.AddToClassList("ak-race-card--selected");
                else                          kv.Value.RemoveFromClassList("ak-race-card--selected");
            }

            _selectedNameLabel.text = _loc.Get($"race.{_selectedRace.ToString().ToLower()}.name")
                                     ?? _selectedRace.ToString();
            _selectedPassivLabel.text = DescribePassiv(_selectedRace);
            _selectedMentorLabel.text = DescribeMentor(_selectedRace);
        }

        private string DescribePassiv(Race race)
        {
            var passivKey = race switch
            {
                Race.Ritter      => "hero.ritter.skill.desc",
                Race.Elfen       => "hero.elfen.skill.desc",
                Race.Tiergeister => "hero.tiergeister.skill.desc",
                Race.Daemonen    => "hero.daemonen.skill.desc",
                _                => "hero.unknown.desc"
            };
            return _loc.Get(passivKey) ?? passivKey;
        }

        private string DescribeMentor(Race race) => race switch
        {
            Race.Ritter      => _loc.Get("npc.marschall_aldor.name") ?? "Marschall Aldor",
            Race.Elfen       => _loc.Get("npc.mondpriesterin_lira.name") ?? "Mondpriesterin Lira",
            Race.Tiergeister => _loc.Get("npc.grimmfang.name") ?? "Grimmfang",
            Race.Daemonen    => _loc.Get("npc.lilith.name") ?? "Lilith",
            _                => "?"
        };

        private void OnConfirmClicked() => ConfirmAsync().Forget();

        private async UniTaskVoid ConfirmAsync()
        {
            _confirmButton.SetEnabled(false);
            var result = await _save.MutateAsync(s =>
            {
                s.Story.ChosenRace = _selectedRace;
                return s;
            }, CancellationToken.None);

            if (!result.IsSuccess)
            {
                _toast.Show(result.ErrorMessage ?? "Speichern fehlgeschlagen", ToastKind.Danger);
                _confirmButton.SetEnabled(true);
                return;
            }

            _toast.Show($"✓ {_loc.Get("race_selection.confirmed") ?? "Rasse gewaehlt"}: {_selectedRace}", ToastKind.Success);
            await _screenManager.ReplaceAsync(ScreenId.Hub);
        }
    }
}
