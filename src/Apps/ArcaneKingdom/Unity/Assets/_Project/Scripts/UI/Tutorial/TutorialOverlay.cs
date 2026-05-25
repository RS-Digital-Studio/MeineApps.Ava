#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Tutorial;
using ArcaneKingdom.Game.Tutorial;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Tutorial
{
    /// <summary>
    /// Tutorial-Overlay als Onboarding-Sequenz. Wird vom TutorialService getriggert
    /// und zeigt einen Schritt nach dem anderen mit "Weiter"-Button.
    ///
    /// Aufruf: ModalContext mit Key "tutorial_step" + aktuellem TutorialStep setzen
    /// bevor PushAsync(TutorialOverlay) aufgerufen wird.
    /// </summary>
    public sealed class TutorialOverlay : ScreenBase
    {
        public const string StepContextKey = "tutorial_step";

        private readonly ScreenManager _screenManager;
        private readonly ModalContext _context;
        private readonly TutorialService _tutorialService;
        private readonly ISaveService<PlayerSave> _save;

        private VisualElement _backdrop = null!;
        private Label _stepCounter = null!;
        private Label _title = null!;
        private Label _text = null!;
        private Button _skipBtn = null!;
        private Button _nextBtn = null!;

        public override string Id => ScreenId.TutorialOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/TutorialOverlay";

        public TutorialOverlay(ScreenManager screenManager,
                               ModalContext context,
                               TutorialService tutorialService,
                               ISaveService<PlayerSave> save)
        {
            _screenManager = screenManager;
            _context = context;
            _tutorialService = tutorialService;
            _save = save;
        }

        protected override void BindElements(VisualElement root)
        {
            _backdrop    = Q<VisualElement>("tutorial-backdrop");
            _stepCounter = Q<Label>("tutorial-step-counter");
            _title       = Q<Label>("tutorial-title");
            _text        = Q<Label>("tutorial-text");
            _skipBtn     = Q<Button>("tutorial-skip");
            _nextBtn     = Q<Button>("tutorial-next");

            _nextBtn.clicked += OnNext;
            _skipBtn.clicked += OnSkip;
        }

        public override UniTask OnEnterAsync(CancellationToken ct)
        {
            var step = _context.Get<TutorialStep>(StepContextKey);
            if (step == null)
            {
                GameLogger.Warning("Tutorial", "Kein Step im ModalContext — Overlay schliesst.");
                _screenManager.PopAsync().Forget();
                return UniTask.CompletedTask;
            }

            PopulateStep(step);
            _skipBtn.SetEnabled(step.Skippable);
            return UniTask.CompletedTask;
        }

        public override UniTask OnLeaveAsync(CancellationToken ct)
        {
            _context.Remove(StepContextKey);
            return UniTask.CompletedTask;
        }

        private void PopulateStep(TutorialStep step)
        {
            var totalSteps = _tutorialService.AllSteps.Count;
            _stepCounter.text = totalSteps > 0
                ? $"Schritt {step.Order + 1} / {totalSteps}"
                : $"Schritt {step.Order + 1}";

            _title.text = LocalizeFallback(step.TitleKey, step.Id);
            _text.text  = LocalizeFallback(step.BodyKey, "");
        }

        private void OnNext()
        {
            OnNextAsync().Forget();
        }

        private async UniTask OnNextAsync()
        {
            var currentStep = _context.Get<TutorialStep>(StepContextKey);
            if (currentStep != null)
            {
                await _save.MutateAsync(save =>
                {
                    save.Tutorial.CurrentStepId = currentStep.Id;
                    if (currentStep.Order + 1 >= _tutorialService.AllSteps.Count)
                        save.Tutorial.TutorialCompleted = true;
                    return save;
                });
            }
            await _screenManager.PopAsync();
        }

        private void OnSkip()
        {
            SkipAsync().Forget();
        }

        private async UniTask SkipAsync()
        {
            await _save.MutateAsync(save =>
            {
                save.Tutorial.TutorialSkipped = true;
                return save;
            });
            await _screenManager.PopAsync();
        }

        private static string LocalizeFallback(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            var dot = key.LastIndexOf('.');
            if (dot < 0 || dot >= key.Length - 1) return fallback;
            var raw = key.Substring(dot + 1).Replace('_', ' ');
            return raw.Length == 0 ? fallback : char.ToUpper(raw[0]) + raw.Substring(1);
        }
    }
}
