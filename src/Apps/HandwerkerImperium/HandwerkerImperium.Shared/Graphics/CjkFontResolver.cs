using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
///  (08.05.2026): CJK-Font-Resolver.
///
/// Problem: Wenn die App-Sprache zh-CN/zh-TW/ja/ko ist, rendern unsere Skia-Renderer
/// CJK-Glyphen als ☐☐☐ Tofu-Boxes weil <see cref="SKFont"/> ohne expliziten Typeface
/// nur die Default-Roboto-Glyphen kennt.
///
/// Lösung: Diese Klasse liefert das passende <see cref="SKTypeface"/> aus dem System
/// (oder Bundled-Font wenn beigelegt). Renderer holen sich beim ersten Render-Call
/// das passende Typeface und cachen es.
///
/// (diese Klasse): Resolver + System-Fallback. Renderer-Updates folgen iterativ.
///  (zukünftig): Bundled-Font (NotoSansCJK ~10MB) für Glyph-Vollständigkeit.
/// </summary>
public static class CjkFontResolver
{
    private static SKTypeface? s_cachedSc;
    private static SKTypeface? s_cachedTc;
    private static SKTypeface? s_cachedJp;
    private static SKTypeface? s_cachedKr;

    // Code-Review-Fix [MITTEL]: Sentinel-Flags verhindern dass Resolve() bei null-Result
    // den teuren LoadFamily-Pfad bei jedem Render-Frame nochmal triggert.
    private static bool s_resolvedSc;
    private static bool s_resolvedTc;
    private static bool s_resolvedJp;
    private static bool s_resolvedKr;

    /// <summary>True wenn die aktuelle Sprache CJK-Glyphen benötigt.</summary>
    public static bool IsCjkLanguage(string? cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode)) return false;
        return cultureCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Liefert ein <see cref="SKTypeface"/> das die meisten CJK-Glyphen abdeckt.
    /// Reihenfolge: System-CJK-Default → SkiaSharp-Default-Fallback.
    /// </summary>
    public static SKTypeface? Resolve(string? cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode)) return null;

        if (cultureCode.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
        {
            if (!s_resolvedSc) { s_cachedSc = LoadFamily("Noto Sans CJK SC", "PingFang SC", "Microsoft YaHei", "SimSun"); s_resolvedSc = true; }
            return s_cachedSc;
        }

        if (cultureCode.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
            || cultureCode.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            if (!s_resolvedTc) { s_cachedTc = LoadFamily("Noto Sans CJK TC", "PingFang TC", "Microsoft JhengHei", "MingLiU"); s_resolvedTc = true; }
            return s_cachedTc;
        }

        if (cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            if (!s_resolvedJp) { s_cachedJp = LoadFamily("Noto Sans CJK JP", "Hiragino Sans", "Yu Gothic", "MS Gothic"); s_resolvedJp = true; }
            return s_cachedJp;
        }

        if (cultureCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            if (!s_resolvedKr) { s_cachedKr = LoadFamily("Noto Sans CJK KR", "Apple SD Gothic Neo", "Malgun Gothic", "Gulim"); s_resolvedKr = true; }
            return s_cachedKr;
        }

        return null;
    }

    /// <summary>
    /// Hilfs-Methode: Versucht eine Liste von Font-Families bis eine gefunden wird.
    /// </summary>
    private static SKTypeface? LoadFamily(params string[] families)
    {
        foreach (var family in families)
        {
            try
            {
                var face = SKTypeface.FromFamilyName(family);
                if (face is null) continue;
                // SkiaSharp gibt manchmal ein Default-Face zurück wenn die Family fehlt.
                // Prüfe via FamilyName ob es wirklich die gewünschte ist.
                if (string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                    return face;
                face.Dispose();
            }
            catch
            {
                // Nächste Family probieren
            }
        }
        // Letzter Fallback: Default-Typeface (rendert evtl. mit Tofu-Boxes — besser als crash)
        return SKTypeface.Default;
    }

    /// <summary>
    /// Diagnose-Helper: Listet die geladenen CJK-Faces fuer eine Tabelle.
    /// Wird vom CJK-Test in der Test-Suite genutzt.
    /// </summary>
    public static (string Lang, string? FamilyName)[] AuditAvailableFaces()
    {
        return new[]
        {
            ("zh-CN", Resolve("zh-CN")?.FamilyName),
            ("zh-TW", Resolve("zh-TW")?.FamilyName),
            ("ja",    Resolve("ja")?.FamilyName),
            ("ko",    Resolve("ko")?.FamilyName),
        };
    }
}
