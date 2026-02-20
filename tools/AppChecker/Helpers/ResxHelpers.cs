using System.Xml.Linq;

namespace AppChecker.Helpers;

static class ResxHelpers
{
    /// <summary>Extrahiert alle data-Keys aus einer .resx Datei</summary>
    public static HashSet<string> ExtractResxKeys(string resxPath)
    {
        var keys = new HashSet<string>();
        try
        {
            var doc = XDocument.Load(resxPath);
            foreach (var data in doc.Descendants("data"))
            {
                var name = data.Attribute("name")?.Value;
                if (name != null)
                    keys.Add(name);
            }
        }
        catch
        {
            // Fehler beim Parsen - leeres Set zurueck
        }
        return keys;
    }
}
