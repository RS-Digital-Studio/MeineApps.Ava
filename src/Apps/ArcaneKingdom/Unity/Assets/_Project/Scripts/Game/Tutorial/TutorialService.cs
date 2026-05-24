#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Core.Utility;
using ArcaneKingdom.Domain.Tutorial;
using Newtonsoft.Json;
using UnityEngine;

namespace ArcaneKingdom.Game.Tutorial
{
    /// <summary>
    /// Tutorial-Steuerung: laedt Schritte aus JSON, hoert auf Event-Trigger und
    /// feuert ShowStep wenn ein Schritt noch nicht abgeschlossen ist.
    /// Progress wird im PlayerSave-Schema v2 persistiert.
    /// </summary>
    public sealed class TutorialService
    {
        private readonly IAnalyticsService _analytics;
        private readonly List<TutorialStep> _steps = new();
        private readonly HashSet<string> _completedStepIds = new();
        private bool _skipped;

        public event Action<TutorialStep>? StepRequested;
        public event Action? TutorialFinished;

        public TutorialService(IAnalyticsService analytics)
        {
            _analytics = analytics;
            LoadStepsFromResources();
        }

        public IReadOnlyList<TutorialStep> AllSteps => _steps;
        public bool IsSkipped => _skipped;
        public bool IsFinished => _completedStepIds.Count >= _steps.Count;

        public void RestoreProgress(TutorialProgress progress)
        {
            _skipped = progress.TutorialSkipped;
            if (progress.TutorialCompleted)
            {
                foreach (var s in _steps) _completedStepIds.Add(s.Id);
            }
            else if (!string.IsNullOrEmpty(progress.CurrentStepId))
            {
                var idx = _steps.FindIndex(s => s.Id == progress.CurrentStepId);
                for (var i = 0; i < idx; i++) _completedStepIds.Add(_steps[i].Id);
            }
        }

        /// <summary>
        /// Wird vom Game-Layer bei jedem Trigger-Event aufgerufen. Liefert true wenn
        /// ein Tutorial-Schritt gefeuert wurde.
        /// </summary>
        public bool OnEvent(string eventName)
        {
            if (_skipped || IsFinished) return false;
            var step = _steps.FirstOrDefault(s => s.TriggerEvent == eventName && !_completedStepIds.Contains(s.Id));
            if (step == null) return false;
            StepRequested?.Invoke(step);
            _analytics.Track("tutorial_step_shown", new Dictionary<string, object> { ["step_id"] = step.Id });
            return true;
        }

        public void MarkStepCompleted(string stepId)
        {
            if (!_completedStepIds.Add(stepId)) return;
            _analytics.Track("tutorial_step_completed", new Dictionary<string, object> { ["step_id"] = stepId });
            if (IsFinished) { TutorialFinished?.Invoke(); _analytics.Track("tutorial_finished"); }
        }

        public void Skip()
        {
            if (_skipped) return;
            _skipped = true;
            _analytics.Track("tutorial_skipped", new Dictionary<string, object> { ["completed_steps"] = _completedStepIds.Count });
            TutorialFinished?.Invoke();
        }

        private void LoadStepsFromResources()
        {
            var asset = Resources.Load<TextAsset>("Data/tutorial");
            if (asset == null)
            {
                GameLogger.Warning("Tutorial", "Resources/Data/tutorial.json fehlt.");
                return;
            }
            try
            {
                var steps = JsonConvert.DeserializeObject<List<TutorialStep>>(asset.text);
                if (steps != null) _steps.AddRange(steps.OrderBy(s => s.Order));
                GameLogger.Info("Tutorial", $"{_steps.Count} Tutorial-Schritte geladen.");
            }
            catch (Exception ex)
            {
                GameLogger.Error("Tutorial", "Tutorial-Deserialisierung fehlgeschlagen", ex);
            }
        }
    }
}
