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

    /// <summary>Anzeigbare Beschriftung eines Kontur-Typs. foreach über das Array statt
    /// LINQ-FirstOrDefault mit Closure — alloc-frei, daher auch im Per-Frame-Modus-Chip nutzbar.</summary>
    private static string ContourTypeLabel(ArContourType type)
    {
        foreach (var o in ContourTypeOptions)
            if (o.Type == type) return o.Label;
        return type.ToString();
    }

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
            // Index 0 = gefuehrtes Rechteck, danach die Freihand-Kontur-Typen, jeweils mit Typ-Punkt.
            var items = new List<(string, Color)>(ContourTypeOptions.Length + 1)
            {
                (RectangleEntryLabel, ToolbarAccent),
            };
            foreach (var opt in ContourTypeOptions)
                items.Add((opt.Label, ArPointOverlayView.GetContourTypeColor(opt.Type)));

            var density = Resources?.DisplayMetrics?.Density ?? 1f;
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Fläche zeichnen — Methode wählen");
            builder.SetAdapter(new DotListAdapter(this, items, density), (_, e) =>
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
            var items = ContourTypeOptions
                .Select(o => (o.Label, ArPointOverlayView.GetContourTypeColor(o.Type)))
                .ToList();
            var density = Resources?.DisplayMetrics?.Density ?? 1f;

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Rechteck/Quadrat — Typ wählen");
            builder.SetAdapter(new DotListAdapter(this, items, density),
                (_, e) => StartRectangleMode(ContourTypeOptions[e.Which].Type));
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
        _btnPoint?.SetBackgroundColor(_captureMode == CaptureMode.Point ? ToolbarAccent : ToolbarInactive);
        // Der "Fläche"-Button deckt Freihand-Kontur UND Rechteck ab.
        _btnContour?.SetBackgroundColor(_captureMode is CaptureMode.Contour or CaptureMode.Rectangle
            ? ToolbarAccent : ToolbarInactive);

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
    /// Wertet jede einzelne Bedingung aus und zeigt sie als Zeile mit farbigem Status-Punkt
    /// (grün ok / rot fehlt / bernstein Achtung / grau Info) plus Kurzerklärung + Wert.
    /// </summary>
    private void ShowReadinessDetailDialog()
    {
        VibrateLight();
        try
        {
            // Farbcodierte Status-Dots statt [OK]/[--]-Klartext: grün ok, rot fehlt,
            // bernstein Achtung/lädt, grau reine Info.
            var good = Color.Argb(255, 38, 198, 122);
            var poor = Color.Argb(255, 235, 77, 75);
            var medium = Color.Argb(255, 245, 176, 65);
            var info = Color.Argb(255, 150, 160, 175);

            var rows = new List<(string, Color)>();

            // ARCore-Session aktiv
            rows.Add(($"ARCore-Session: {(_arSession != null ? "läuft" : "fehlt")}",
                _arSession != null ? good : poor));

            // Kamera-Stabilität
            var stability = _stabilityMonitor?.StabilityScore ?? 0f;
            rows.Add(($"Stabilität: {(int)(stability * 100)}% (mind. 60% nötig)",
                stability >= 0.6f ? good : poor));

            // Magnetometer-Accuracy
            var magLabel = _magneticAccuracy switch
            {
                3 => "hoch",
                2 => "mittel",
                1 => "niedrig",
                _ => "keine",
            };
            rows.Add(($"Kompass: {magLabel} (mind. mittel)", _magneticAccuracy >= 2 ? good : poor));

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
            rows.Add(($"Erkannte Flächen: {planeCount}", planeCount > 0 ? good : poor));

            // Anchor-Count (Drift-Kompensation) — reine Info
            rows.Add(($"Aktive Anchors: {_anchorManager.CountTracking()}", info));

            // Geospatial-API
            rows.Add(($"Geospatial-VPS: {(_geospatialActive ? "aktiv" : _geospatialEnabled ? "lokalisiert noch" : "deaktiviert")}",
                _geospatialActive ? good : _geospatialEnabled ? medium : info));

            // GPS
            var gpsAcc = _gpsAccuracy.HasValue ? $"±{_gpsAccuracy.Value:F1}m" : "—";
            rows.Add(($"GPS: {(_gpsLatitude.HasValue ? "Fix" : "kein Fix")} ({gpsAcc})",
                _gpsLatitude.HasValue ? good : poor));

            // Tracking-Continuity — reine Info
            var ratio = _frameCountTotal > 0 ? (float)_frameCountTracking / _frameCountTotal : 1f;
            rows.Add(($"Tracking-Kontinuität: {(int)(ratio * 100)}%", info));

            // Thermal / Battery — Warnungen
            if (!string.IsNullOrEmpty(_thermalWarningText))
                rows.Add((_thermalWarningText!, medium));
            if (!string.IsNullOrEmpty(_batteryWarningText))
                rows.Add((_batteryWarningText!, medium));

            var density = Resources?.DisplayMetrics?.Density ?? 1f;
            var list = new global::Android.Widget.LinearLayout(this)
            {
                Orientation = global::Android.Widget.Orientation.Vertical,
            };
            var padTop = (int)(8 * density);
            list.SetPadding((int)(8 * density), padTop, (int)(8 * density), padTop);
            foreach (var (label, dot) in rows)
                list.AddView(BuildDotRow(label, dot, density));
            var scroll = new global::Android.Widget.ScrollView(this);
            scroll.AddView(list);

            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetTitle("Mess-Bereitschaft");
            builder.SetView(scroll);
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

    /// <summary>Baut eine nicht-klickbare Info-Zeile mit farbigem Status-Punkt links (für den
    /// Readiness-Dialog). Punkt = GradientDrawable-Oval als linkes CompoundDrawable.</summary>
    private global::Android.Views.View BuildDotRow(string label, Color dot, float density)
    {
        var tv = new global::Android.Widget.TextView(this) { Text = label, TextSize = 15 };
        var padH = (int)(16 * density);
        var padV = (int)(9 * density);
        tv.SetPadding(padH, padV, padH, padV);
        tv.Gravity = global::Android.Views.GravityFlags.CenterVertical;

        var size = (int)(12 * density);
        var drawable = new global::Android.Graphics.Drawables.GradientDrawable();
        drawable.SetShape(global::Android.Graphics.Drawables.ShapeType.Oval);
        drawable.SetColor(dot);
        drawable.SetBounds(0, 0, size, size);
        tv.SetCompoundDrawables(drawable, null, null, null);
        tv.CompoundDrawablePadding = (int)(14 * density);
        return tv;
    }

    /// <summary>Listen-Adapter für AlertDialogs mit farbigem Status-/Typ-Punkt links neben jedem
    /// Eintrag — ersetzt die reinen Text-Listen (SetItems) durch eine visuell codierte Auswahl.</summary>
    private sealed class DotListAdapter(
        Context context, IReadOnlyList<(string Label, Color Dot)> items, float density)
        : global::Android.Widget.BaseAdapter
    {
        public override int Count => items.Count;
        public override Java.Lang.Object? GetItem(int position) => null;
        public override long GetItemId(int position) => position;

        public override global::Android.Views.View GetView(
            int position, global::Android.Views.View? convertView, global::Android.Views.ViewGroup? parent)
        {
            var tv = convertView as global::Android.Widget.TextView
                     ?? new global::Android.Widget.TextView(context) { TextSize = 16 };
            tv.Text = items[position].Label;
            var padH = (int)(20 * density);
            var padV = (int)(13 * density);
            tv.SetPadding(padH, padV, padH, padV);
            tv.Gravity = global::Android.Views.GravityFlags.CenterVertical;

            var size = (int)(14 * density);
            var drawable = new global::Android.Graphics.Drawables.GradientDrawable();
            drawable.SetShape(global::Android.Graphics.Drawables.ShapeType.Oval);
            drawable.SetColor(items[position].Dot);
            drawable.SetBounds(0, 0, size, size);
            tv.SetCompoundDrawables(drawable, null, null, null);
            tv.CompoundDrawablePadding = (int)(16 * density);
            return tv;
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
