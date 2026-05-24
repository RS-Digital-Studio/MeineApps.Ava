using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.14: Generiert einen PDF-Bericht im Vermessungs-Standard-Layout —
/// Titelblatt, Uebersichtskarte, Punkt-Tabelle mit Lat/Lon/Hoehe/Genauigkeit/Foto-
/// Thumbnail, Konturen-Liste, Material-Schaetzung, Differential-Karte,
/// QR-Code zum Original-Projekt. PdfSharpCore mit
/// <c>MeineApps.Core.Premium.Ava.Android.AndroidFontResolver</c> auf Android.</summary>
public interface ISurveyReportService
{
    /// <summary>Generiert den PDF-Bericht fuer das Projekt und speichert ihn in
    /// <c>IAppPaths.ExportFolder</c>. Liefert den absoluten Pfad zur erzeugten PDF.</summary>
    Task<string> GeneratePdfReportAsync(SurveyProject project, SurveyReportOptions options, CancellationToken ct = default);
}

/// <summary>Konfiguration fuer den PDF-Bericht — was alles rein soll und in welcher Sprache.</summary>
public sealed record SurveyReportOptions(
    string ClientName,
    string SurveyorName,
    string AccuracyClass,
    bool IncludePhotos = true,
    bool IncludeMaterials = true,
    bool IncludeMap = true,
    bool IncludeDifferential = false,
    SurveyProject? PreviousSession = null);
