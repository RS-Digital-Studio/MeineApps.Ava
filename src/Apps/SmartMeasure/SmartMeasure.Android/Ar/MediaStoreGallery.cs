using Android.Content;
using Android.Graphics;
using Android.Provider;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Speichert nutzersichtbare Medien (AR-Screenshots, AR-Session-Aufnahmen) in die geteilten
/// MediaStore-Collections (<c>Pictures/SmartMeasure</c>, <c>Movies/SmartMeasure</c>).
///
/// Anders als app-spezifischer Speicher (<c>GetExternalFilesDir</c>) erscheinen diese Medien in
/// der Foto-/Video-Galerie des Nutzers, ueberleben die Deinstallation der App und brauchen ab
/// Android 10 (API 29) KEINE Storage-Permission, solange die App ihre eigenen Medien schreibt.
///
/// <c>IS_PENDING=1</c> haelt den Eintrag waehrend des Schreibens exklusiv/unsichtbar; bei Fehler
/// wird er via <c>Delete</c> wieder entfernt, damit kein verwaister 0-Byte-Eintrag in der Galerie
/// zurueckbleibt.
/// </summary>
internal static class MediaStoreGallery
{
    /// <summary>Galerie-Album (Unterordner unter Pictures/ bzw. Movies/).</summary>
    private const string Album = "SmartMeasure";

    /// <summary>Schreibt ein Bitmap als Bild nach <c>Pictures/SmartMeasure</c>. Liefert die
    /// <c>content://</c>-Uri als String, oder null bei Fehler.</summary>
    public static string? SaveBitmap(ContentResolver resolver, Bitmap bitmap, string displayName,
        Bitmap.CompressFormat format, string mimeType)
    {
        var collection = MediaStore.Images.Media.GetContentUri(MediaStore.VolumeExternalPrimary)!;
        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
        values.Put(MediaStore.IMediaColumns.MimeType, mimeType);
        values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryPictures + "/" + Album);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        var uri = resolver.Insert(collection, values);
        if (uri == null) return null;
        try
        {
            using (var os = resolver.OpenOutputStream(uri))
            {
                if (os == null) { resolver.Delete(uri, null, null); return null; }
                bitmap.Compress(format, 100, os);
            }
            values.Clear();
            values.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, values, null, null);
            return uri.ToString();
        }
        catch (Exception ex)
        {
            try { resolver.Delete(uri, null, null); } catch { /* Pending-Eintrag aufraeumen */ }
            global::Android.Util.Log.Warn("MediaStoreGallery", $"SaveBitmap fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    /// <summary>Kopiert eine fertige Datei (z.B. MP4) als Video nach <c>Movies/SmartMeasure</c>.
    /// Die Quelldatei bleibt erhalten (der Aufrufer loescht sie nach Erfolg). Liefert die
    /// <c>content://</c>-Uri als String, oder null bei Fehler.</summary>
    public static string? CopyVideo(ContentResolver resolver, string sourcePath, string displayName, string mimeType)
    {
        var collection = MediaStore.Video.Media.GetContentUri(MediaStore.VolumeExternalPrimary)!;
        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
        values.Put(MediaStore.IMediaColumns.MimeType, mimeType);
        values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryMovies + "/" + Album);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        var uri = resolver.Insert(collection, values);
        if (uri == null) return null;
        try
        {
            using (var input = System.IO.File.OpenRead(sourcePath))
            using (var output = resolver.OpenOutputStream(uri))
            {
                if (output == null) { resolver.Delete(uri, null, null); return null; }
                input.CopyTo(output);
            }
            values.Clear();
            values.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, values, null, null);
            return uri.ToString();
        }
        catch (Exception ex)
        {
            try { resolver.Delete(uri, null, null); } catch { /* Pending-Eintrag aufraeumen */ }
            global::Android.Util.Log.Warn("MediaStoreGallery", $"CopyVideo fehlgeschlagen: {ex.Message}");
            return null;
        }
    }
}
