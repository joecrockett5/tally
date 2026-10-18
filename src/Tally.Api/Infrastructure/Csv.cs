using System.Text;

namespace Tally.Api.Infrastructure;

/// <summary>Small RFC 4180 CSV reader/writer (quoted fields, embedded commas, quotes and newlines).</summary>
public static class Csv
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add([.. row]);
                    row.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add([.. row]);
        }

        // Drop blank lines.
        return rows.Where(r => r.Length > 1 || r[0].Trim().Length > 0).ToList();
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // Neutralise spreadsheet formula injection.
        if (value[0] is '=' or '+' or '-' or '@' && !double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public static string Line(IEnumerable<string?> values) => string.Join(',', values.Select(Escape));
}
