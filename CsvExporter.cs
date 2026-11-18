using System.Diagnostics;
using System.Text;

namespace panialera;

// Simple CSV exporter: saves the file to Documents\Cariñitos and opens it.
// Uses ',' as separator and '.' as decimal mark (standard CSV), with quoted text.
public static class CsvExporter
{
    public static string Export(string fileNameBase, string content)
    {
        var path = NewFilePath(fileNameBase, "csv");

        // UTF-8 with BOM so Excel keeps the accents.
        File.WriteAllText(path, content, new UTF8Encoding(true));
        Open(path);
        return path;
    }

    // Path for a new exported file: Documents/Cariñitos folder, name base-date.ext.
    // The stock count PDF uses it too.
    public static string NewFilePath(string fileNameBase, string extension)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Cariñitos");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{fileNameBase}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
    }

    // Opens the file with the default program. If that fails, the file is still saved.
    public static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorHub.Report(ex, "No se pudo abrir el archivo exportado");
        }
    }

    // Quotes a CSV field when it contains a comma, quotes or line breaks.
    public static string Field(string? s)
    {
        s ??= string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
