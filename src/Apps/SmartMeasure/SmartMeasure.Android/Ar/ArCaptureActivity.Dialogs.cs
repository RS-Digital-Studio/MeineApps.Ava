using Android.Content;
using Android.Graphics;
using Google.AR.Core;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Dialog-Sektionen von <see cref="ArCaptureActivity"/>: Bestätigungs-Dialoge (Löschen/Fertig),
/// Kontur-Typ-Auswahl, Kompass-Kalibrierung, Readiness-Detail, Help, Coach-Marks.
/// Aus der Haupt-Datei extrahiert um sie unter 3000 Zeilen zu halten.
/// Funktioniert via partial class — Zugriff auf alle privaten Felder bleibt erhalten.
/// </summary>
public partial class ArCaptureActivity
{
    #region Bestätigungs-Dialoge

    /// <summary>Lösch-Bestätigung. Verhindert versehentliche Verluste bei Fehl-Tap.</summary>
    private void ConfirmDeleteSelectedPoint()
    {
        // Nur fragen wenn überhaupt ein Punkt ausgewählt ist
        var hasSelection = _selectedPointIndex >= 0 || _isContourPointSelected;
        if (!hasSelection)
        {
            ShowTransientHint("Kein Punkt ausgewählt");
            VibrateLight();
            return;
        }

        try
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Punkt löschen?");
            builder.SetMessage("Der ausgewählte Punkt wird entfernt. Mit Rückgängig wiederherstellbar.");
            builder.SetPositiveButton("Löschen", (_, _) =>
            {
                VibrateMedium();
                DeleteSelectedPoint();
            });
            builder.SetNegativeButton("Abbrechen", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ConfirmDelete failed: {ex.Message}");
            // Fallback: direkt löschen wenn Dialog nicht möglich
            DeleteSelectedPoint();
        }
    }

    /// <summary>Fertig-Bestätigung. Zeigt Anzahl Punkte/Konturen + die Übertragung.</summary>
    private void ConfirmFinishCapture()
    {
        int totalPoints, contourCount;
        lock (_dataLock)
        {
            totalPoints = _points.Count + _contours.Sum(c => c.Points.Count)
                + (_activeContour?.Points.Count ?? 0);
            contourCount = _contours.Count + (_activeContour?.Points.Count >= 3 ? 1 : 0);
        }

        if (totalPoints == 0)
        {
            try
            {
                var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
                builder.SetTitle("Keine Daten");
                builder.SetMessage("Du hast noch keinen Punkt gesetzt. Aufnahme trotzdem beenden?");
                builder.SetPositiveButton("Beenden", (_, _) =>
                {
                    SetResult(Result.Canceled);
                    Finish();
                });
                builder.SetNegativeButton("Weiter messen", (_, _) => { });
                builder.Show();
            }
            catch
            {
                SetResult(Result.Canceled);
                Finish();
            }
            return;
        }

        try
        {
            var msg = contourCount > 0
                ? $"{totalPoints} Punkte und {contourCount} Kontur(en) werden übertragen."
                : $"{totalPoints} Punkte werden übertragen.";

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Aufnahme beenden?");
            builder.SetMessage(msg);
            builder.SetPositiveButton("Übertragen", (_, _) =>
            {
                VibrateMedium();
                FinishCapture();
            });
            builder.SetNegativeButton("Weiter messen", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ConfirmFinish failed: {ex.Message}");
            FinishCapture();
        }
    }

    /// <summary>Abbruch/Zurück mit Bestätigung: bei bereits erfassten Daten erst nachfragen
    /// (Symmetrie zu <see cref="ConfirmFinishCapture"/>). Ein Fehl-Tap auf X oder eine Back-Geste
    /// am Display-Rand verwirft sonst eine ganze Vermessungs-Session ohne jede Rückfrage.</summary>
    private void ConfirmDiscardAndExit()
    {
        int totalPoints;
        lock (_dataLock)
        {
            totalPoints = _points.Count + _contours.Sum(c => c.Points.Count)
                + (_activeContour?.Points.Count ?? 0);
        }

        if (totalPoints == 0)
        {
            CancelAndFinish();
            return;
        }

        try
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Aufnahme verwerfen?");
            builder.SetMessage($"{totalPoints} erfasste Punkt(e) gehen verloren. " +
                "Mit \"Übernehmen\" stattdessen ins Projekt übertragen.");
            builder.SetPositiveButton("Verwerfen", (_, _) => CancelAndFinish());
            builder.SetNeutralButton("Übernehmen", (_, _) => { VibrateMedium(); FinishCapture(); });
            builder.SetNegativeButton("Weiter messen", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ConfirmDiscard failed: {ex.Message}");
            CancelAndFinish();
        }
    }

    /// <summary>Bricht die Session ab (Result.Canceled) und schließt die Activity — mit dem
    /// Doppel-Submit-Guard, der auch FinishCapture/OnBackPressed schützt.</summary>
    private void CancelAndFinish()
    {
        if (System.Threading.Interlocked.Exchange(ref _finished, 1) == 0)
        {
            lock (_lastResultLock) _lastResult = null;
            SetResult(Result.Canceled);
        }
        Finish();
    }

    #endregion

    #region Kontur-Typ-Auswahl (Gartenplanung)

    private static readonly (ArContourType Type, string Label)[]
        ContourTypeOptions =
        [
            (ArContourType.Weg,       "Weg"),
            (ArContourType.Beet,      "Beet"),
            (ArContourType.Mauer,     "Mauer"),
            (ArContourType.Zaun,      "Zaun"),
            (ArContourType.Terrasse,  "Terrasse"),
            (ArContourType.Gebaeude,  "Gebäude"),
            (ArContourType.Wasser,    "Wasser/Teich"),
            (ArContourType.Grenze,    "Grenze"),
            (ArContourType.Kante,     "Kante"),
        ];

    /// <summary>Beschriftung des gefuehrten Rechteck-/Quadrat-Eintrags — steht als erste Option
    /// oben im Flaechen-Dialog (vor den Freihand-Kontur-Typen).</summary>
    private const string RectangleEntryLabel = "Rechteck / Quadrat (rechtwinklig)";

    /// <summary>
    /// Zeigt den Flaechen-Dialog. Erste Option ist die gefuehrte Rechteck-/Quadrat-Methode,
    /// danach die Freihand-Kontur-Typen. Bei Typ-Auswahl wird die aktive Kontur abgeschlossen
    /// (falls vorhanden) und eine neue vom gewählten Typ gestartet — ermöglicht Multi-Kontur-
    /// Zeichnung für Gartenplanung (z.B. 3 Wege + 2 Beete + 1 Rechteck-Terrasse pro Session).
    /// </summary>
    private void ShowContourTypeDialog()
    {
        try
        {
            // Index 0 = gefuehrtes Rechteck, danach die Freihand-Kontur-Typen.
            var labels = new string[ContourTypeOptions.Length + 1];
            labels[0] = RectangleEntryLabel;
            for (var i = 0; i < ContourTypeOptions.Length; i++)
                labels[i + 1] = ContourTypeOptions[i].Label;

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Fläche zeichnen — Methode wählen");
            builder.SetItems(labels, (_, e) =>
            {
                if (e.Which == 0)
                    ShowRectangleTypeDialog();
                else
                    StartNewContour(ContourTypeOptions[e.Which - 1].Type);
            });
            builder.SetNegativeButton("Abbrechen", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ContourTypeDialog failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Zweite Stufe der Rechteck-Methode: Garten-Typ des Rechtecks wählen (Terrasse, Beet, ...).
    /// Default-Vorauswahl ist Terrasse — die häufigste rechtwinklige Garten-Fläche.
    /// </summary>
    private void ShowRectangleTypeDialog()
    {
        try
        {
            var labels = ContourTypeOptions.Select(o => o.Label).ToArray();

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Rechteck/Quadrat — Typ wählen");
            builder.SetItems(labels, (_, e) => StartRectangleMode(ContourTypeOptions[e.Which].Type));
            builder.SetNegativeButton("Abbrechen", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"RectangleTypeDialog failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Startet den gefuehrten Rechteck-Modus vom gewählten Typ. Schließt eine aktive Freihand-
    /// Kontur ab (wie <see cref="StartNewContour"/>) und leert einen evtl. halb gesetzten
    /// Rechteck-Buffer.
    /// </summary>
    private void StartRectangleMode(ArContourType type)
    {
        lock (_dataLock)
        {
            // Aktive Freihand-Kontur einheitlich abschliessen (>=3 committen, sonst verwerfen).
            FinalizeOrDiscardActiveContour();
            _rectangleCorners.Clear();
        }

        _currentContourType = type;
        _captureMode = CaptureMode.Rectangle;

        UpdateModeButtonHighlight();
        UpdateCounter();
        _overlayView?.Invalidate();

        var typeLabel = ContourTypeOptions.FirstOrDefault(o => o.Type == type).Label ?? type.ToString();
        ShowTransientHint($"Rechteck {typeLabel}: erste Ecke der Basiskante tippen");
        VibrateMedium();
    }

    /// <summary>
    /// Schließt aktive Kontur (wenn >=3 Punkte mit Bowditch), startet neue vom gewählten Typ.
    /// Modus wechselt automatisch zu Contour.
    /// </summary>
    private void StartNewContour(ArContourType type)
    {
        lock (_dataLock)
        {
            // Aktive Kontur einheitlich abschliessen: ab 3 Punkten committen (mit Bowditch +
            // Anchor-Detach + Undo-Eintrag), unter 3 Punkten verwerfen — dieselbe Logik wie
            // SetMode/FinishCapture/StartRectangleMode (vorher inline dupliziert ohne Undo).
            FinalizeOrDiscardActiveContour();
            // Eine evtl. begonnene Rechteck-Basiskante beim Wechsel zu Freihand verwerfen.
            _rectangleCorners.Clear();
        }

        _currentContourType = type;
        _captureMode = CaptureMode.Contour;

        UpdateModeButtonHighlight();
        UpdateCounter();
        _overlayView?.Invalidate();

        var typeLabel = ContourTypeOptions.FirstOrDefault(o => o.Type == type).Label ?? type.ToString();
        ShowTransientHint($"Neue {typeLabel}-Kontur — Punkte tippen");
        VibrateMedium();
    }

    private void UpdateModeButtonHighlight()
    {
        _btnPoint?.SetBackgroundColor(_captureMode == CaptureMode.Point
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));
        // Der "Fläche"-Button deckt Freihand-Kontur UND Rechteck ab.
        _btnContour?.SetBackgroundColor(_captureMode is CaptureMode.Contour or CaptureMode.Rectangle
            ? Color.Argb(220, 255, 107, 0)
            : Color.Argb(80, 255, 255, 255));

        // Der aktive Modus inkl. Kontur-Typ läuft jetzt über den Canvas-Modus-Chip
        // (BuildModeChipLabel pro Frame) — kein nativer Modus-Text mehr.
    }

    #endregion

    #region Kompass-Kalibrierung

    private void ShowCompassCalibrationHint()
    {
        try
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Kompass-Kalibrierung nötig");
            builder.SetMessage(
                "Der Kompass ist ungenau.\n\n" +
                "Bewege das Gerät langsam in einer liegenden Acht (∞) für ca. 5 Sekunden.\n\n" +
                "Das stabilisiert die Magnetfeld-Messung und macht alle Richtungs-Angaben präziser.");
            builder.SetPositiveButton("OK", (_, _) => { });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"CompassCalib-Dialog failed: {ex.Message}");
        }
    }

    #endregion

    #region Readiness-Detail-Dialog

    /// <summary>
    /// Detail-Checkliste der Mess-Bereitschafts-Conditions. Wird beim Tap auf das Badge geöffnet.
    /// Wertet jede einzelne Bedingung aus und zeigt ✓/✗ mit Kurzerklärung + Wert.
    /// </summary>
    private void ShowReadinessDetailDialog()
    {
        VibrateLight();
        try
        {
            var sb = new System.Text.StringBuilder();

            // Klartext-Marker statt Symbole: [OK] / [--] / [..] / [i] / [!]
            // ARCore-Session aktiv
            sb.Append(_arSession != null ? "[OK] " : "[--] ");
            sb.AppendLine($"ARCore-Session: {(_arSession != null ? "läuft" : "fehlt")}");

            // Kamera-Stabilität
            var stability = _stabilityMonitor?.StabilityScore ?? 0f;
            sb.Append(stability >= 0.6f ? "[OK] " : "[--] ");
            sb.AppendLine($"Stabilität: {(int)(stability * 100)}% (mind. 60% nötig)");

            // Magnetometer-Accuracy
            var magOk = _magneticAccuracy >= 2;
            var magLabel = _magneticAccuracy switch
            {
                3 => "hoch",
                2 => "mittel",
                1 => "niedrig",
                _ => "keine",
            };
            sb.Append(magOk ? "[OK] " : "[--] ");
            sb.AppendLine($"Kompass: {magLabel} (mind. mittel)");

            // Erkannte Planes
            var planeCount = 0;
            try
            {
                var trackables = _arSession?.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane)));
                if (trackables != null)
                    foreach (var t in trackables)
                        if (t is Plane p && p.TrackingState == TrackingState.Tracking && p.SubsumedBy == null)
                            planeCount++;
            }
            catch { /* harmlos */ }
            sb.Append(planeCount > 0 ? "[OK] " : "[--] ");
            sb.AppendLine($"Erkannte Flächen: {planeCount}");

            // Anchor-Count (Drift-Kompensation)
            var anchors = _anchorManager.CountTracking();
            sb.AppendLine($"[i]  Aktive Anchors: {anchors}");

            // Geospatial-API
            sb.Append(_geospatialActive ? "[OK] " : "[..] ");
            sb.AppendLine($"Geospatial-VPS: {(_geospatialActive ? "aktiv" : _geospatialEnabled ? "lokalisiert noch" : "deaktiviert")}");

            // GPS
            sb.Append(_gpsLatitude.HasValue ? "[OK] " : "[--] ");
            var gpsAcc = _gpsAccuracy.HasValue ? $"±{_gpsAccuracy.Value:F1}m" : "—";
            sb.AppendLine($"GPS: {(_gpsLatitude.HasValue ? "Fix" : "kein Fix")} ({gpsAcc})");

            // Tracking-Continuity
            var ratio = _frameCountTotal > 0
                ? (float)_frameCountTracking / _frameCountTotal
                : 1f;
            sb.AppendLine($"[i]  Tracking-Kontinuität: {(int)(ratio * 100)}%");

            // Thermal
            if (!string.IsNullOrEmpty(_thermalWarningText))
                sb.AppendLine($"[!]  {_thermalWarningText}");
            if (!string.IsNullOrEmpty(_batteryWarningText))
                sb.AppendLine($"[!]  {_batteryWarningText}");

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Mess-Bereitschaft");
            builder.SetMessage(sb.ToString());
            builder.SetPositiveButton("Schließen", (_, _) => { });
            if (_magneticAccuracy < 2)
            {
                builder.SetNeutralButton("Kompass kalibrieren", (_, _) =>
                {
                    ShowCompassCalibrationHint();
                });
            }
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"ReadinessDetail-Dialog failed: {ex.Message}");
        }
    }

    #endregion

    #region Help-Dialog + Coach-Marks

    private void ShowHelpDialog()
    {
        VibrateLight();
        try
        {
            const string helpText =
                "SmartMeasure AR-Modus\n\n" +
                "Werkzeugleiste unten:\n" +
                "Punkt — einzelne Messpunkte setzen\n" +
                "Fläche — Kontur (Weg/Beet/Mauer/...) beginnen\n" +
                "Schließen — aktive Kontur abschließen\n" +
                "Zurück / Vor — Aktion rückgängig / wiederholen\n" +
                "Mehr — Maßband, Tachymeter, Abstecken, Punkt löschen,\n" +
                "        Screenshot, Aufnahme, Hilfe\n" +
                "Fertig — Aufnahme beenden und ins Projekt übertragen\n\n" +
                "Genauigkeit:\n" +
                "AR misst auf ca. 5–50 cm genau — ideal für Gartenplanung\n" +
                "und Flächen. Für zentimetergenaue Grenzpunkte einen\n" +
                "RTK-Stab verbinden (in den App-Optionen).\n\n" +
                "Tipps:\n" +
                "Fadenkreuz-Farbe = Mess-Qualität:\n" +
                "   Grün = Fläche erkannt (beste Qualität)\n" +
                "   Gelb = geschätzt (Instant Placement)\n" +
                "   Rot/Weiß = kein Treffer, Kamera langsam bewegen\n" +
                "Langsame Bewegung und gute Beleuchtung verbessern die Qualität.\n" +
                "Lange auf einen Button drücken zeigt seinen Namen.\n" +
                "Tippen auf das Bereitschafts-Feld oben zeigt eine Checkliste.\n" +
                "Deine Punkte werden laufend gesichert (Wiederherstellung).";

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("AR-Bedienung");
            builder.SetMessage(helpText);
            builder.SetPositiveButton("Verstanden", (_, _) => { });
            builder.SetNeutralButton(_soundEnabled ? "Sound aus" : "Sound an", (_, _) =>
            {
                SetSoundEnabled(!_soundEnabled);
                ShowTransientHint(_soundEnabled ? "Sound aktiv" : "Sound aus");
            });
            builder.Show();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"Help-Dialog failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Zeigt einen einmaligen Coach-Marks-Dialog beim ersten AR-Start. Erklärt die wichtigsten
    /// Konzepte (Crosshair, Toolbar, Fertig-Button). Persistiert in SharedPreferences,
    /// nur bei explizitem "Verstanden" verschwindet er endgültig — "Später nochmal" zeigt
    /// ihn beim nächsten Start wieder.
    /// </summary>
    private void ShowCoachMarksIfNeeded()
    {
        if (_coachMarksShown) return;
        // Nicht direkt im OnCreate aufrufen — wir warten bis View fertig gerendert ist,
        // sonst sieht der Dialog "leer" aus, weil die Kamera-Preview noch nicht da ist.
        Window?.DecorView?.PostDelayed(() =>
        {
            if (IsFinishing || IsDestroyed) return;
            try
            {
                const string coachText =
                    "Willkommen im AR-Modus!\n\n" +
                    "Du vermisst mit der Kamera — ganz ohne Zusatz-Hardware.\n" +
                    "Genauigkeit ca. 5–50 cm, ideal für Garten und Flächen.\n\n" +
                    "Fadenkreuz (Bildmitte):\n" +
                    "   Hier wird gemessen.\n" +
                    "   Grün = Fläche erkannt, Gelb = Schätzung,\n" +
                    "   Rot = kein Treffer (Kamera langsam bewegen).\n\n" +
                    "Erste Schritte:\n" +
                    "   1. Telefon langsam über den Boden bewegen,\n" +
                    "      bis grüne Flächen erscheinen.\n" +
                    "   2. Mit dem Fadenkreuz zielen und aufs Bild tippen.\n" +
                    "   3. Kurz still halten — der Punkt wird gesetzt.\n\n" +
                    "Werkzeugleiste unten:\n" +
                    "   Punkt, Fläche, Schließen, Zurück/Vor, Mehr, Fertig.\n" +
                    "   Lange auf einen Button drücken zeigt seinen Namen.\n\n" +
                    "'Fertig' überträgt deine Punkte ins Projekt.\n" +
                    "'Löschen' und 'Fertig' fragen vorher nach.\n\n" +
                    "Das Bereitschafts-Feld oben links zeigt, was für eine\n" +
                    "präzise Messung noch fehlt.";

                var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
                builder.SetTitle("So funktioniert AR-Messen");
                builder.SetMessage(coachText);
                builder.SetPositiveButton("Verstanden", (_, _) =>
                {
                    _coachMarksShown = true;
                    PersistCoachMarksShown();
                });
                builder.SetNeutralButton("Später nochmal", (_, _) =>
                {
                    // Pref nicht setzen — beim nächsten Start zeigen wir es wieder.
                });
                builder.SetCancelable(false);
                builder.Show();
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn("ArCapture",
                    $"CoachMarks-Dialog fehlgeschlagen: {ex.Message}");
            }
        }, 600); // 600ms Delay — Kamera-Preview ist sicher schon sichtbar
    }

    private void PersistCoachMarksShown()
    {
        try
        {
            var prefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
            using var editor = prefs?.Edit();
            editor?.PutBoolean("ar.coachmarks.shown", true);
            editor?.Apply();
        }
        catch { /* harmlos */ }
    }

    #endregion
}
