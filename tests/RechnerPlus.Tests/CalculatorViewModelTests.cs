using FluentAssertions;
using MeineApps.CalcLib;
using MeineApps.Core.Ava.Services;
using NSubstitute;
using RechnerPlus.ViewModels;
using Xunit;

namespace RechnerPlus.Tests;

/// <summary>
/// Tests für CalculatorViewModel: Display-Logik, Eingabe, Berechnungen,
/// Fehlerbehandlung, Undo/Redo, Memory, Operator-Highlight.
/// </summary>
public class CalculatorViewModelTests
{
    #region Initialer Zustand

    [Fact]
    public void Konstruktor_InitialerZustand_DisplayZero()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.Display.Should().Be("0");
    }

    [Fact]
    public void Konstruktor_InitialerZustand_ExpressionLeer()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.Expression.Should().BeEmpty();
    }

    [Fact]
    public void Konstruktor_InitialerZustand_KeinFehler()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.HasError.Should().BeFalse();
    }

    [Fact]
    public void Konstruktor_InitialerZustand_BasicMode()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.CurrentMode.Should().Be(CalculatorMode.Basic);
        sut.IsBasicMode.Should().BeTrue();
        sut.IsScientificMode.Should().BeFalse();
    }

    #endregion

    #region Ziffern-Eingabe

    [Fact]
    public void InputDigit_AusgangsDisplay0_ErsetzDurchZiffer()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.Display.Should().Be("5");
    }

    [Fact]
    public void InputDigit_MehrereZiffern_WerdenAngehaengt()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("1");
        sut.InputDigitCommand.Execute("2");
        sut.InputDigitCommand.Execute("3");
        sut.Display.Should().Be("123");
    }

    [Fact]
    public void InputDigit_NachBerechnung_StartetNeuEingabe()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null);

        // Nach "=" zeigt Display das Ergebnis, neue Eingabe überschreibt
        sut.InputDigitCommand.Execute("9");
        sut.Display.Should().Be("9");
    }

    [Fact]
    public void InputDigit_NachOperator_StartetNeuEingabe()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        // Zahl neu eingeben → Display muss bei 0 starten
        sut.InputDigitCommand.Execute("3");
        sut.Display.Should().Be("3");
    }

    #endregion

    #region Dezimalpunkt

    [Fact]
    public void InputDecimal_NochKeinPunkt_FuugtPunktHinzu()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("3");
        sut.InputDecimalCommand.Execute(null);
        sut.Display.Should().Be("3.");
    }

    [Fact]
    public void InputDecimal_PunktBereitsVorhanden_WirdIgnoriert()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("3");
        sut.InputDecimalCommand.Execute(null);
        sut.InputDecimalCommand.Execute(null); // zweiter Klick ignoriert
        sut.Display.Should().Be("3.");
    }

    #endregion

    #region Berechnung

    [Fact]
    public void Calculate_EinfacheAddition_GibtKorektesErgebnis()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null);
        sut.Display.Should().Be("8");
    }

    [Fact]
    public void Calculate_EinfacheSubtraktion_GibtKorektesErgebnis()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("9");
        sut.InputOperatorCommand.Execute("−");
        sut.InputDigitCommand.Execute("4");
        sut.CalculateCommand.Execute(null);
        sut.Display.Should().Be("5");
    }

    [Fact]
    public void Calculate_NachBerechnung_ExpressionLeer()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null);
        sut.Expression.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_NachBerechnung_HistoryEintragGespeichert()
    {
        var historyService = new HistoryService();
        var sut = TestHelper.ErstelleCalculatorViewModel(historyService: historyService);

        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null);

        historyService.History.Should().HaveCount(1);
        historyService.History[0].ResultValue.Should().Be(8);
    }

    [Fact]
    public void Calculate_WiederholungMitEquals_WiederholtLetzteOperation()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null); // = 8

        sut.CalculateCommand.Execute(null); // = 11 (8+3)
        sut.Display.Should().Be("11");
    }

    [Fact]
    public void Calculate_MitFehler_GibtKeinenFehlerWennFehlerAktiv()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        // Bewusst Fehler provozieren
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.InputDigitCommand.Execute("0");
        sut.CalculateCommand.Execute(null);

        sut.HasError.Should().BeTrue();
        // Erneuter Calculate bei HasError wird ignoriert
        sut.CalculateCommand.Execute(null);
        sut.HasError.Should().BeTrue();
    }

    #endregion

    #region Fehlerbehandlung

    [Fact]
    public void DivisionDurchNull_SetztHasErrorTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.InputDigitCommand.Execute("0");
        sut.CalculateCommand.Execute(null);
        sut.HasError.Should().BeTrue();
    }

    [Fact]
    public void Clear_NachFehler_SetztFehlerZurueck()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.InputDigitCommand.Execute("0");
        sut.CalculateCommand.Execute(null);
        sut.HasError.Should().BeTrue();

        sut.ClearCommand.Execute(null);
        sut.HasError.Should().BeFalse();
        sut.Display.Should().Be("0");
    }

    [Fact]
    public void InputDigit_NachFehler_SetztFehlerZurueck()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        // Fehler provozieren
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.InputDigitCommand.Execute("0");
        sut.CalculateCommand.Execute(null);
        sut.HasError.Should().BeTrue();

        // Neue Ziffer löscht Fehler
        sut.InputDigitCommand.Execute("7");
        sut.HasError.Should().BeFalse();
        sut.Display.Should().Be("7");
    }

    [Fact]
    public void SquareRoot_NegativeZahl_SetztHasErrorTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("9");
        sut.NegateCommand.Execute(null);
        sut.SquareRootCommand.Execute(null);
        sut.HasError.Should().BeTrue();
    }

    [Fact]
    public void Factorial_NegativeZahl_SetztHasErrorTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.NegateCommand.Execute(null);
        sut.FactorialCommand.Execute(null);
        sut.HasError.Should().BeTrue();
    }

    #endregion

    #region Clear und Backspace

    [Fact]
    public void Clear_SetztallesZurueck()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.ClearCommand.Execute(null);

        sut.Display.Should().Be("0");
        sut.Expression.Should().BeEmpty();
        sut.HasError.Should().BeFalse();
    }

    [Fact]
    public void Backspace_EineZiffer_SetzDisplayAufNull()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.BackspaceCommand.Execute(null);
        sut.Display.Should().Be("0");
    }

    [Fact]
    public void Backspace_MehrereZiffern_EntferntLetzte()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("1");
        sut.InputDigitCommand.Execute("2");
        sut.InputDigitCommand.Execute("3");
        sut.BackspaceCommand.Execute(null);
        sut.Display.Should().Be("12");
    }

    [Fact]
    public void Backspace_BeiHasError_WirdIgnoriert()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.InputDigitCommand.Execute("0");
        sut.CalculateCommand.Execute(null);
        sut.HasError.Should().BeTrue();

        sut.BackspaceCommand.Execute(null); // bei Fehler ignoriert
        sut.HasError.Should().BeTrue(); // Fehler bleibt
    }

    #endregion

    #region Negate

    [Fact]
    public void Negate_PositiveZahl_MachtNegativ()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.NegateCommand.Execute(null);
        sut.Display.Should().Be("-5");
    }

    [Fact]
    public void Negate_NegativeZahl_MachtPositiv()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.NegateCommand.Execute(null);
        sut.NegateCommand.Execute(null); // zweimal → wieder positiv
        sut.Display.Should().Be("5");
    }

    [Fact]
    public void Negate_Null_AendertNichts()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.NegateCommand.Execute(null);
        sut.Display.Should().Be("0");
    }

    #endregion

    #region Operator-Highlight

    [Fact]
    public void InputOperator_SetztActiveOperator()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.ActiveOperator.Should().Be("+");
        sut.IsAddActive.Should().BeTrue();
    }

    [Fact]
    public void InputOperator_Division_SetztDivisionAktiv()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("÷");
        sut.IsDivideActive.Should().BeTrue();
    }

    [Fact]
    public void InputOperator_Ersetzt_AenderungsOperatorWird()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputOperatorCommand.Execute("÷"); // Operator ersetzen
        sut.ActiveOperator.Should().Be("÷");
        sut.IsDivideActive.Should().BeTrue();
        sut.IsAddActive.Should().BeFalse();
    }

    [Fact]
    public void Calculate_NachBerechnung_ActiveOperatorNull()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+");
        sut.InputDigitCommand.Execute("3");
        sut.CalculateCommand.Execute(null);
        sut.ActiveOperator.Should().BeNull();
    }

    #endregion

    #region Wissenschaftliche Funktionen

    [Fact]
    public void SquareRoot_Vier_GibtZwei()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("4");
        sut.SquareRootCommand.Execute(null);
        sut.Display.Should().Be("2");
    }

    [Fact]
    public void Square_Drei_GibtNeun()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("3");
        sut.SquareCommand.Execute(null);
        sut.Display.Should().Be("9");
    }

    [Fact]
    public void Reciprocal_Fuenf_GibtEinFuenftel()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.ReciprocalCommand.Execute(null);
        // 1/5 = 0.2
        sut.Display.Should().Be("0.2");
    }

    [Fact]
    public void Reciprocal_Null_SetztHasErrorTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        // Display ist "0" → 1/0 = Fehler
        sut.ReciprocalCommand.Execute(null);
        sut.HasError.Should().BeTrue();
    }

    [Fact]
    public void Factorial_Fuenf_GibtHundertzwanzig()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.FactorialCommand.Execute(null);
        sut.Display.Should().Be("120");
    }

    [Fact]
    public void ToggleInverse_SetzInverseModeUm()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.IsInverseMode.Should().BeFalse();
        sut.ToggleInverseCommand.Execute(null);
        sut.IsInverseMode.Should().BeTrue();
    }

    [Fact]
    public void INVModus_SinButtonText_ZeigtInvers()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.SinButtonText.Should().Be("sin");
        sut.ToggleInverseCommand.Execute(null);
        sut.SinButtonText.Should().Contain("sin");
        sut.SinButtonText.Should().NotBe("sin"); // enthält ⁻¹
    }

    [Fact]
    public void Pi_SetztDisplayAufPi()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.PiCommand.Execute(null);
        sut.Display.Should().StartWith("3.14159");
    }

    [Fact]
    public void Euler_SetztDisplayAufE()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.EulerCommand.Execute(null);
        sut.Display.Should().StartWith("2.71828");
    }

    [Fact]
    public void ToggleAngleMode_WechseltZwischenRadUndDeg()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.IsRadians.Should().BeTrue();
        sut.AngleModeText.Should().Be("RAD");

        sut.ToggleAngleModeCommand.Execute(null);
        sut.IsRadians.Should().BeFalse();
        sut.AngleModeText.Should().Be("DEG");
    }

    #endregion

    #region Undo/Redo

    [Fact]
    public void Undo_NachEinerEingabe_StelltDisplayWiederHer()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+"); // SaveState wird aufgerufen
        sut.UndoCommand.Execute(null);

        // Nach Undo ist Display wieder "5" (Zustand vor dem Operator)
        sut.Display.Should().Be("5");
    }

    [Fact]
    public void Redo_NachUndo_StelltZustandWiederHer()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+"); // SaveState
        var displayNachOperator = sut.Display;

        sut.UndoCommand.Execute(null);
        sut.RedoCommand.Execute(null);

        // Nach Redo: Zustand nach dem Operator wiederhergestellt
        sut.CanRedo.Should().BeFalse(); // Redo-Stack leer
    }

    [Fact]
    public void CanUndo_OhneEingabe_IsFalse()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void CanUndo_NachEingabe_IsTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.InputOperatorCommand.Execute("+"); // SaveState
        sut.CanUndo.Should().BeTrue();
    }

    #endregion

    #region Memory-Funktionen

    [Fact]
    public void MemoryStore_UndMemoryRecall_GibtWertZurueck()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("7");
        sut.MemoryStoreCommand.Execute(null);

        sut.InputDigitCommand.Execute("3"); // andere Zahl eingeben
        sut.MemoryRecallCommand.Execute(null);

        sut.Display.Should().Be("7");
        sut.HasMemory.Should().BeTrue();
    }

    [Fact]
    public void MemoryAdd_AddiertZumGespeichertenWert()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.MemoryStoreCommand.Execute(null); // Memory = 5

        // Neue Zahl eingeben: erst Clear Entry damit neue Zahl startet
        sut.ClearEntryCommand.Execute(null);
        sut.InputDigitCommand.Execute("3");
        sut.MemoryAddCommand.Execute(null); // Memory = 5+3 = 8

        sut.Memory.Should().Be(8);
    }

    [Fact]
    public void MemorySubtract_SubtrahiertVomGespeichertenWert()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.MemoryStoreCommand.Execute(null); // Memory = 5

        // Neue Zahl eingeben: erst Clear Entry damit neue Zahl startet
        sut.ClearEntryCommand.Execute(null);
        sut.InputDigitCommand.Execute("2");
        sut.MemorySubtractCommand.Execute(null); // Memory = 5-2 = 3

        sut.Memory.Should().Be(3);
    }

    [Fact]
    public void MemoryClear_SetzMemoryZurueck()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.MemoryStoreCommand.Execute(null);
        sut.HasMemory.Should().BeTrue();

        sut.MemoryClearCommand.Execute(null);
        sut.HasMemory.Should().BeFalse();
        sut.Memory.Should().Be(0);
    }

    #endregion

    #region Modus-Wechsel

    [Fact]
    public void SetMode_Scientific_SetzScientificMode()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.SetModeCommand.Execute(CalculatorMode.Scientific);
        sut.CurrentMode.Should().Be(CalculatorMode.Scientific);
        sut.IsScientificMode.Should().BeTrue();
        sut.IsBasicMode.Should().BeFalse();
    }

    [Fact]
    public void SetMode_SpeichertModus()
    {
        var preferences = TestHelper.ErstellePreferencesMock();
        var sut = TestHelper.ErstelleCalculatorViewModel(preferences: preferences);

        sut.SetModeCommand.Execute(CalculatorMode.Scientific);

        // Modus muss in Preferences gespeichert worden sein
        preferences.Received().Set(Arg.Is("calculator_mode"), Arg.Is(1));
    }

    #endregion

    #region History-Sichtbarkeit

    [Fact]
    public void ShowHistory_SetztIsHistoryVisibleTrue()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.ShowHistoryCommand.Execute(null);
        sut.IsHistoryVisible.Should().BeTrue();
    }

    [Fact]
    public void HideHistory_SetztIsHistoryVisibleFalse()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.ShowHistoryCommand.Execute(null);
        sut.HideHistoryCommand.Execute(null);
        sut.IsHistoryVisible.Should().BeFalse();
    }

    #endregion

    #region Display-Schriftgröße

    [Fact]
    public void Display_KurzeZahl_GrosseSchrift()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.InputDigitCommand.Execute("5");
        sut.DisplayFontSize.Should().Be(52); // ≤ 8 Zeichen
    }

    [Fact]
    public void Display_LangeZahl_KleineSchrift()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        // Mehr als 20 Zeichen im Display → kleinste Schriftgröße
        for (int i = 0; i < 22; i++)
            sut.InputDigitCommand.Execute("1");
        sut.DisplayFontSize.Should().Be(20);
    }

    #endregion

    #region PropertyChanged-Events

    [Fact]
    public void Display_WertAendert_LoestPropertyChangedAus()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        var geaenderteProperties = new List<string?>();
        sut.PropertyChanged += (s, e) => geaenderteProperties.Add(e.PropertyName);

        sut.InputDigitCommand.Execute("5");

        geaenderteProperties.Should().Contain(nameof(sut.Display));
    }

    [Fact]
    public void CurrentMode_Aendert_LoestPropertyChangedFuerIsBasicModeAus()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        var geaenderteProperties = new List<string?>();
        sut.PropertyChanged += (s, e) => geaenderteProperties.Add(e.PropertyName);

        sut.SetModeCommand.Execute(CalculatorMode.Scientific);

        geaenderteProperties.Should().Contain(nameof(sut.IsBasicMode));
        geaenderteProperties.Should().Contain(nameof(sut.IsScientificMode));
    }

    #endregion

    #region PasteValue

    [Fact]
    public void PasteValue_GueltigeZahl_SetztDisplay()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.PasteValue("3.14");
        sut.Display.Should().Be("3.14");
        sut.HasError.Should().BeFalse();
    }

    [Fact]
    public void PasteValue_NullEingabe_ZeigtWarnung()
    {
        bool warningAusgeloest = false;
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.FloatingTextRequested += (text, kategorie) =>
        {
            if (kategorie == "warning") warningAusgeloest = true;
        };

        sut.PasteValue(null);
        warningAusgeloest.Should().BeTrue();
    }

    [Fact]
    public void PasteValue_UngueltigeZahl_ZeigtWarnung()
    {
        bool warningAusgeloest = false;
        var sut = TestHelper.ErstelleCalculatorViewModel();
        sut.FloatingTextRequested += (text, kategorie) =>
        {
            if (kategorie == "warning") warningAusgeloest = true;
        };

        sut.PasteValue("kein_Wert");
        warningAusgeloest.Should().BeTrue();
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_MehrfachAufgerufen_KeineException()
    {
        var sut = TestHelper.ErstelleCalculatorViewModel();
        var action = () =>
        {
            sut.Dispose();
            sut.Dispose(); // zweites Dispose ignoriert
        };
        action.Should().NotThrow();
    }

    #endregion
}
