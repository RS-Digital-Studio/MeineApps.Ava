using Android.Content;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Session-Recovery-Sektion von <see cref="ArCaptureActivity"/>: Save/Restore/Clear via
/// SharedPreferences "smartmeasure_ar". Punkte werden nach jedem PlacePoint persistiert,
/// beim nächsten App-Start zeigt sich ein Bestätigungs-Dialog "X Punkte aus letzter
/// Sitzung wiederherstellen?".
/// Earth-Anchor-Re-Attach (Plan 3.3): GeoLat/GeoLon überleben den Prozesstod, alte
/// AnchorIds nicht — wir queuen sie nach Restore und attachen neue Earth-Anchors sobald
/// Earth.TrackingState=Tracking erreicht ist (in ReattachPendingEarthAnchors).
/// </summary>
public partial class ArCaptureActivity
{
    private void SaveRecoveryState()
    {
        try
        {
            var prefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
            if (prefs == null) return;

            List<ArPoint> pointsCopy;
            List<ArContour> contoursCopy;
            lock (_dataLock)
            {
                // Vorgeladene Punkte (IsPreloaded) gehoeren nicht in den Recovery-State — sie
                // kommen beim naechsten Start ohnehin frisch aus dem Projekt. Sonst doppelter
                // Lade-Pfad (Preload + Recovery-Restore) und inkonsistenter _preloadedPointCount.
                pointsCopy = _points.Where(p => !p.IsPreloaded).ToList();
                contoursCopy = new List<ArContour>(_contours);
                if (_activeContour != null && _activeContour.Points.Count > 0)
                    contoursCopy.Add(_activeContour);
            }

            var pointsJson = System.Text.Json.JsonSerializer.Serialize(pointsCopy);
            var contoursJson = System.Text.Json.JsonSerializer.Serialize(contoursCopy);

            using var editor = prefs.Edit();
            editor?.PutString(RecoveryKeyPoints, pointsJson);
            editor?.PutString(RecoveryKeyContours, contoursJson);
            editor?.PutLong(RecoveryKeyTimestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            editor?.Apply();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"SaveRecoveryState failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Liest Recovery-Daten aus SharedPreferences und zeigt einen Bestätigungs-Dialog
    /// ("X Punkte / Y Konturen aus letzter Sitzung wiederherstellen?").
    /// Vorher: stillschweigender Restore + Toast — User merkte oft nicht dass alte Daten geladen wurden.
    /// </summary>
    private void TryRestoreRecoveryState()
    {
        try
        {
            var prefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
            if (prefs == null) return;

            var timestamp = prefs.GetLong(RecoveryKeyTimestamp, 0);
            if (timestamp == 0) return;

            // Nur wiederherstellen wenn Recovery-Save < 30 Min alt
            var ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
            if (ageMs > 30 * 60 * 1000)
            {
                ClearRecoveryState();
                return;
            }

            var pointsJson = prefs.GetString(RecoveryKeyPoints, null);
            var contoursJson = prefs.GetString(RecoveryKeyContours, null);

            // Daten parsen, aber NICHT ohne Dialog in die Listen schreiben.
            List<ArPoint>? recoveredPoints = null;
            List<ArContour>? recoveredContours = null;

            if (!string.IsNullOrEmpty(pointsJson))
                recoveredPoints = System.Text.Json.JsonSerializer.Deserialize<List<ArPoint>>(pointsJson);
            if (!string.IsNullOrEmpty(contoursJson))
                recoveredContours = System.Text.Json.JsonSerializer.Deserialize<List<ArContour>>(contoursJson);

            var pointCount = recoveredPoints?.Count ?? 0;
            var contourCount = recoveredContours?.Count ?? 0;
            var totalPoints = pointCount + (recoveredContours?.Sum(c => c.Points.Count) ?? 0);

            if (totalPoints == 0)
            {
                ClearRecoveryState();
                return;
            }

            // Dialog nach kurzer Verzögerung (sonst rendert er manchmal vor der Activity sichtbar wird).
            Window?.DecorView?.Post(() =>
            {
                if (IsFinishing || IsDestroyed) return;
                try
                {
                    var ageMin = (int)(ageMs / 60000);
                    var ageText = ageMin >= 1 ? $"vor {ageMin} min" : "vor weniger als 1 min";
                    var msg = contourCount > 0
                        ? $"Letzte AR-Sitzung wurde {ageText} unterbrochen.\n\n" +
                          $"{totalPoints} Punkte und {contourCount} Kontur(en) sind verfügbar."
                        : $"Letzte AR-Sitzung wurde {ageText} unterbrochen.\n\n" +
                          $"{totalPoints} Punkte sind verfügbar.";

                    var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
                    builder.SetTitle("Wiederherstellen?");
                    builder.SetMessage(msg);
                    builder.SetPositiveButton("Wiederherstellen", (_, _) =>
                    {
                        lock (_dataLock)
                        {
                            if (recoveredPoints != null) _points.AddRange(recoveredPoints);
                            if (recoveredContours != null) _contours.AddRange(recoveredContours);
                        }

                        // Plan 3.3: Earth-Anchor-Re-Attach für jeden Punkt mit Geo-Koordinaten.
                        // ARCore-Anchors überleben den Prozesstod nicht → alter AnchorId ist tot.
                        // Wir queuen die Punkte und ReattachPendingEarthAnchors() (im OnDrawFrame)
                        // erstellt neue Earth-Anchors sobald Earth.TrackingState=Tracking erreicht ist.
                        var anchorsToRestore = 0;
                        lock (_pendingRestoreLock)
                        {
                            void EnqueueIfGeo(ArPoint? p)
                            {
                                if (p == null) return;
                                if (p.GeoLatitude.HasValue && p.GeoLongitude.HasValue)
                                {
                                    p.AnchorId = null; // alter ID ist tot, wird beim Re-Attach neu gesetzt
                                    _pendingEarthAnchorRestore.Add(p);
                                    anchorsToRestore++;
                                }
                            }
                            if (recoveredPoints != null)
                                foreach (var p in recoveredPoints) EnqueueIfGeo(p);
                            if (recoveredContours != null)
                                foreach (var c in recoveredContours)
                                    foreach (var p in c.Points) EnqueueIfGeo(p);
                        }

                        // Wiederhergestellte Aktion ist bewusst NICHT undobar — der Undo-Stack
                        // gehört zur alten (toten) Session. Punkte OHNE Geo-Bezug beziehen sich auf
                        // das alte ARCore-Koordinatensystem (pro Session neu) und können in der
                        // neuen Sitzung verschoben erscheinen — das wird dem Nutzer kommuniziert.
                        UpdateCounter();
                        _overlayView?.Invalidate();
                        ClearRecoveryState();
                        var localCount = totalPoints - anchorsToRestore;
                        string hint;
                        if (anchorsToRestore == 0 && totalPoints > 0)
                            hint = $"{totalPoints} Punkte wiederhergestellt — ohne Geo-Bezug, Lage kann in dieser Sitzung abweichen";
                        else if (localCount > 0)
                            hint = $"{totalPoints} Punkte wiederhergestellt — {anchorsToRestore} mit Geo-Anchor, {localCount} ohne Geo-Bezug (Lage kann abweichen)";
                        else
                            hint = $"{totalPoints} Punkte wiederhergestellt — {anchorsToRestore} Geo-Anchors werden re-attached";
                        ShowTransientHint(hint);
                    });
                    builder.SetNegativeButton("Verwerfen", (_, _) => ClearRecoveryState());
                    builder.SetCancelable(false); // Eindeutige Entscheidung erzwingen
                    builder.Show();
                }
                catch (Exception ex)
                {
                    global::Android.Util.Log.Warn("ArCapture",
                        $"Recovery-Dialog fehlgeschlagen: {ex.Message}");
                    // Fallback: bei Dialog-Fehler State behalten — User kann beim nächsten Start wählen.
                }
            });
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArCapture", $"TryRestoreRecoveryState failed: {ex.Message}");
        }
    }

    private void ClearRecoveryState()
    {
        try
        {
            var prefs = GetSharedPreferences("smartmeasure_ar", FileCreationMode.Private);
            using var editor = prefs?.Edit();
            editor?.Remove(RecoveryKeyPoints);
            editor?.Remove(RecoveryKeyContours);
            editor?.Remove(RecoveryKeyTimestamp);
            editor?.Apply();
        }
        catch { /* harmlos */ }
    }
}
