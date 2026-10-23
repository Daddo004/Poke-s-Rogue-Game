using System.Text;

namespace PokeRogue.DataTool.Csv;

/// <summary>Minimal RFC 4180 reader (quoted fields, escaped quotes, embedded newlines) for the PokeAPI CSV dump.</summary>
public sealed class CsvTable
{
    private readonly Dictionary<string, int> _columns;

    private CsvTable(string name, string[] header, List<string[]> rows)
    {
        Name = name;
        Rows = rows;
        _columns = header.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i, StringComparer.Ordinal);
    }

    public string Name { get; }

    public IReadOnlyList<string[]> Rows { get; }

    public static CsvTable Load(string path)
    {
        var records = Parse(File.ReadAllText(path, Encoding.UTF8)).ToList();
        if (records.Count == 0)
            throw new InvalidDataException($"{path} has no header.");

        return new CsvTable(Path.GetFileNameWithoutExtension(path), records[0], records.Skip(1).Where(r => r.Length > 1 || r[0].Length > 0).ToList());
    }

    public int Column(string name) =>
        _columns.TryGetValue(name, out var index) ? index : throw new KeyNotFoundException($"Column '{name}' not found in {Name}.");

    public string Str(string[] row, string column) => row[Column(column)];

    public int Int(string[] row, string column) => int.Parse(Str(row, column));

    public int? IntOrNull(string[] row, string column)
    {
        var value = Str(row, column);
        return value.Length == 0 ? null : int.Parse(value);
    }

    public bool Bool(string[] row, string column) => Str(row, column) == "1";

    public IEnumerable<T> Select<T>(Func<CsvTable, string[], T> selector) => Rows.Select(r => selector(this, r));

    private static IEnumerable<string[]> Parse(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return fields.ToArray();
                    fields.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return fields.ToArray();
        }
    }
}
