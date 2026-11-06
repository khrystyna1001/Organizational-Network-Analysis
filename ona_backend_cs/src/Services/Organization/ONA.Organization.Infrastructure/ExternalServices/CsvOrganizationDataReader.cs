using System.Text;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;
using ONA.Organization.Application.Abstractions;

namespace ONA.Organization.Infrastructure.ExternalServices;

internal sealed class CsvOrganizationDataReader : IOrganizationDataReader
{
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadCsv(Stream stream)
    {
        using var parser = new TextFieldParser(stream, Encoding.UTF8, true)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return [];
        }

        var headers = MakeUniqueHeaders(parser.ReadFields() ?? []);
        var records = new List<IReadOnlyDictionary<string, object?>>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            if (fields.Length == 1 && fields[0].Length == 0)
            {
                continue;
            }

            if (fields.Length > headers.Length)
            {
                throw new InvalidDataException("A CSV row contains more values than the header.");
            }

            var record = new Dictionary<string, object?>(headers.Length, StringComparer.Ordinal);
            for (var index = 0; index < headers.Length; index++)
            {
                var value = index < fields.Length ? fields[index] : null;
                record.Add(headers[index], ParseValue(value));
            }

            records.Add(record);
        }

        return records;
    }

    private static string[] MakeUniqueHeaders(string[] fields)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return fields.Select((field, index) =>
        {
            var name = index == 0 ? field.TrimStart('\uFEFF') : field;
            var uniqueName = name;
            var suffix = 1;
            while (!names.Add(uniqueName))
            {
                uniqueName = $"{name}.{suffix++}";
            }

            return uniqueName;
        }).ToArray();
    }

    private static object? ParseValue(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return double.IsFinite(number) ? number : null;
        }

        return value;
    }
}
