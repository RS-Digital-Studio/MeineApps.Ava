using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Tutorial-Service: 6 interaktive Schritte in 3 geschuetzten Phasen (Sprint 3.2 AAA-Audit #5).
///
/// <para>
/// Die 3 Phasen (T1 Movement / T2 Bombs / T3 PowerUps) verhalten sich wie eigenstaendige
/// "Mini-Levels": jede Phase hat ein eigenes Persistenz-Flag. Bricht der Spieler nach T1 ab,
/// resumed <see cref="Start"/> beim naechsten Mal direkt bei T2 — kein Neustart bei Movement.
/// Das gibt Genre-Neulingen mehr Pacing und schuetzt den Fortschritt.
/// </para>
///
/// <para>
/// Nach Abschluss aller 3 Phasen aktiviert sich die Soft-Onboarding-Curve: die ersten 2
/// Story-Level liefern reduzierte Schwierigkeit (siehe <see cref="ConsumeSoftOnboardingLevel"/>).
/// </para>
/// </summary>
public sealed class TutorialService : ITutorialService
{
    // Legacy-Key (vor Sprint 3.2) — fuer Migration alter Spielstaende.
    private const string LegacyCompletedKey = "TutorialCompleted";

    // Sprint 3.2: Phasen-granulare Persistenz ("3 geschuetzte Tutorial-Levels").
    private const string PhaseT1DoneKey = "tutorial_t1_done";
    private const string PhaseT2DoneKey = "tutorial_t2_done";
    private const string PhaseT3DoneKey = "tutorial_t3_done";

    // Soft-Onboarding-Counter: wird nach Tutorial-Abschluss auf 2 gesetzt, bei jedem
    // Story-Level-Start dekrementiert. Solange > 0 → reduzierte Schwierigkeit.
    private const string SoftOnboardingLevelsKey = "tutorial_soft_onboarding_levels";
    private const int SoftOnboardingLevelCount = 2;

    private readonly IPreferencesService _preferences;

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Schritte sind in 3 Phasen gruppiert.
    /// IsFirstOfPhase=true loest beim Tutorial-Overlay einen Phase-Banner aus
    /// ("Phase 1: Bewegung" / "Phase 2: Bomben" / "Phase 3: Power-Ups").
    /// Soft-Onboarding-Curve: Erst Movement, dann Bomb-Risiken, dann komplexere Mechaniken.
    /// </summary>
    private static readonly TutorialStep[] Steps =
    [
        // Phase 1 — Movement (Genre-Neulinge): nur DPad, kein Bomben-Risiko
        new() { Id = 0, TextKey = "TutorialMove", Type = TutorialStepType.Move,
                Highlight = TutorialHighlight.InputControl,
                Phase = TutorialPhase.Movement, IsFirstOfPhase = true },
        // Phase 2 — Bomb-Mechanik: Bombe legen + sicheres Verstecken
        new() { Id = 1, TextKey = "TutorialBomb", Type = TutorialStepType.PlaceBomb,
                Highlight = TutorialHighlight.BombButton,
                Phase = TutorialPhase.Bombs, IsFirstOfPhase = true },
        new() { Id = 2, TextKey = "TutorialHide", Type = TutorialStepType.Warning,
                Highlight = TutorialHighlight.GameField,
                Phase = TutorialPhase.Bombs },
        // Phase 3 — Power-Ups + Combat (Tiefe der Mechanik)
        new() { Id = 3, TextKey = "TutorialPowerUp", Type = TutorialStepType.CollectPowerUp,
                Highlight = TutorialHighlight.PowerUp,
                Phase = TutorialPhase.PowerUps, IsFirstOfPhase = true },
        new() { Id = 4, TextKey = "TutorialDefeatEnemies", Type = TutorialStepType.DefeatEnemies,
                Highlight = TutorialHighlight.GameField,
                Phase = TutorialPhase.PowerUps },
        new() { Id = 5, TextKey = "TutorialExit", Type = TutorialStepType.FindExit,
                Highlight = TutorialHighlight.Exit,
                Phase = TutorialPhase.PowerUps },
    ];

    private int _currentStepIndex = -1;

    public bool IsActive => _currentStepIndex >= 0 && _currentStepIndex < Steps.Length;
    public TutorialStep? CurrentStep => IsActive ? Steps[_currentStepIndex] : null;

    /// <summary>Alle 3 Phasen abgeschlossen = Tutorial komplett.</summary>
    public bool IsCompleted =>
        IsPhaseCompleted(TutorialPhase.Movement)
        && IsPhaseCompleted(TutorialPhase.Bombs)
        && IsPhaseCompleted(TutorialPhase.PowerUps);

    public TutorialPhase CurrentPhase =>
        IsActive ? Steps[_currentStepIndex].Phase : TutorialPhase.Movement;

    /// <summary>
    /// Sprint 2.2 AAA-Audit #2: Wird vom GameEngine subscribed, feuert Funnel-Event
    /// fuer jeden abgeschlossenen Tutorial-Schritt + ein Final-Complete-Event.
    /// </summary>
    public event Action<int>? StepCompleted;
    /// <summary>Wird beim Abschluss der letzten Phase (Tutorial komplett) gefeuert.</summary>
    public event Action? TutorialCompleted;

    /// <summary>
    /// Sprint 3.2 AAA-Audit #5: Wird gefeuert wenn ein neuer Tutorial-Phase-Schritt aktiv wird
    /// (T1 Movement / T2 Bombs / T3 PowerUps). Tutorial-Overlay zeigt dann einen
    /// Phase-Banner ("Phase 2: Bomb-Mechanik") fuer 1.5s.
    /// </summary>
    public event Action<TutorialPhase>? PhaseChanged;

    public TutorialService(IPreferencesService preferences)
    {
        _preferences = preferences;
        MigrateLegacyState();
    }

    /// <summary>
    /// Migration: Alte Spielstaende haben nur das globale <see cref="LegacyCompletedKey"/>-Flag.
    /// Wenn das gesetzt ist, gelten alle 3 Phasen als abgeschlossen.
    /// </summary>
    private void MigrateLegacyState()
    {
        if (_preferences.Get(LegacyCompletedKey, false)
            && !_preferences.Get(PhaseT3DoneKey, false))
        {
            _preferences.Set(PhaseT1DoneKey, true);
            _preferences.Set(PhaseT2DoneKey, true);
            _preferences.Set(PhaseT3DoneKey, true);
        }
    }

    public bool IsPhaseCompleted(TutorialPhase phase) => phase switch
    {
        TutorialPhase.Movement => _preferences.Get(PhaseT1DoneKey, false),
        TutorialPhase.Bombs => _preferences.Get(PhaseT2DoneKey, false),
        TutorialPhase.PowerUps => _preferences.Get(PhaseT3DoneKey, false),
        _ => false,
    };

    private void MarkPhaseCompleted(TutorialPhase phase)
    {
        var key = phase switch
        {
            TutorialPhase.Movement => PhaseT1DoneKey,
            TutorialPhase.Bombs => PhaseT2DoneKey,
            TutorialPhase.PowerUps => PhaseT3DoneKey,
            _ => null,
        };
        if (key != null)
            _preferences.Set(key, true);
    }

    public void Start()
    {
        if (IsCompleted)
            return;

        // Resume: ersten Step finden, dessen Phase noch nicht abgeschlossen ist.
        // Bricht der Spieler nach T1 ab, startet er beim naechsten Mal direkt bei T2.
        _currentStepIndex = 0;
        for (int i = 0; i < Steps.Length; i++)
        {
            if (!IsPhaseCompleted(Steps[i].Phase))
            {
                _currentStepIndex = i;
                break;
            }
        }

        // Phase-Banner fuer die (resume-)Einstiegsphase ankuendigen.
        PhaseChanged?.Invoke(Steps[_currentStepIndex].Phase);
    }

    public void NextStep()
    {
        if (!IsActive)
            return;

        // Step-Index VOR dem Inkrement merken (das war der gerade abgeschlossene Schritt).
        int completedStepIndex = _currentStepIndex;
        var completedPhase = Steps[completedStepIndex].Phase;
        _currentStepIndex++;

        StepCompleted?.Invoke(completedStepIndex);

        // War das der letzte Schritt seiner Phase? Dann Phasen-Checkpoint persistieren —
        // der Fortschritt ueberlebt App-Beendigung mitten im Tutorial.
        bool phaseFinished = _currentStepIndex >= Steps.Length
            || Steps[_currentStepIndex].Phase != completedPhase;
        if (phaseFinished)
            MarkPhaseCompleted(completedPhase);

        if (_currentStepIndex >= Steps.Length)
        {
            // Tutorial vollstaendig abgeschlossen.
            _currentStepIndex = -1;
            _preferences.Set(LegacyCompletedKey, true);  // Backward-Compat fuer alten Code-Pfad
            // Soft-Onboarding-Curve aktivieren: die naechsten 2 Story-Level leichter.
            _preferences.Set(SoftOnboardingLevelsKey, SoftOnboardingLevelCount);
            TutorialCompleted?.Invoke();
        }
        else if (Steps[_currentStepIndex].IsFirstOfPhase)
        {
            // Phase-Wechsel ankuendigen (T1 → T2 → T3).
            PhaseChanged?.Invoke(Steps[_currentStepIndex].Phase);
        }
    }

    public void Skip()
    {
        _currentStepIndex = -1;
        // Skip schliesst alle Phasen ab — kein erneutes Tutorial-Prompt.
        _preferences.Set(PhaseT1DoneKey, true);
        _preferences.Set(PhaseT2DoneKey, true);
        _preferences.Set(PhaseT3DoneKey, true);
        _preferences.Set(LegacyCompletedKey, true);
        // Skip ueberspringt auch das Soft-Onboarding — wer skippt, will keine Hilfe.
        _preferences.Set(SoftOnboardingLevelsKey, 0);
    }

    public bool CheckStepCompletion(TutorialStepType actionType)
    {
        if (!IsActive || CurrentStep == null)
            return false;

        if (CurrentStep.Type == actionType)
        {
            NextStep();
            return true;
        }

        return false;
    }

    public bool ConsumeSoftOnboardingLevel()
    {
        int remaining = _preferences.Get(SoftOnboardingLevelsKey, 0);
        if (remaining <= 0)
            return false;

        _preferences.Set(SoftOnboardingLevelsKey, remaining - 1);
        return true;
    }

    public void Reset()
    {
        _currentStepIndex = -1;
        _preferences.Set(PhaseT1DoneKey, false);
        _preferences.Set(PhaseT2DoneKey, false);
        _preferences.Set(PhaseT3DoneKey, false);
        _preferences.Set(LegacyCompletedKey, false);
        _preferences.Set(SoftOnboardingLevelsKey, 0);
    }
}
