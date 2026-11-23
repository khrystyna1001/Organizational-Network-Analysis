using System.Text;
using Microsoft.VisualBasic.FileIO;
using ONA.Graph.Application.Ports;
using ONA.Graph.Infrastructure.Acl.ExternalContracts;

namespace ONA.Graph.Infrastructure.Acl;

/// <summary>
/// I/O side of the ACL: reads the HRIS CSV export into the foreign DTOs.
/// Performs no business validation; technical failures become <see cref="HrisIntegrationException"/>.
/// </summary>
internal sealed class CsvHrisAdapter
{
    private const char ByteOrderMark = '﻿';

    public async Task<HRIS_EXPORT_DTO> ReadExportAsync(
        Stream export,
        CancellationToken cancellationToken = default)
    {
        var table = await ReadTableAsync(export, cancellationToken);
        return new HRIS_EXPORT_DTO
        {
            COLUMNS = table.COLUMNS,
            ROWS = table.ROWS.Select(fields => new HRIS_EMPLOYEE_DTO
            {
                EmpID = Cell(table.COLUMNS, fields, "EmpID"),
                FirstName = Cell(table.COLUMNS, fields, "FirstName"),
                LastName = Cell(table.COLUMNS, fields, "LastName"),
                Title = Cell(table.COLUMNS, fields, "Title"),
                DepartmentType = Cell(table.COLUMNS, fields, "DepartmentType"),
                Supervisor = Cell(table.COLUMNS, fields, "Supervisor")
            }).ToArray()
        };
    }

    /// <summary>Reads any CSV into header + raw rows. Technical failures become <see cref="HrisIntegrationException"/>.</summary>
    public async Task<CSV_TABLE_DTO> ReadTableAsync(Stream csv, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csv);

        try
        {
            using var buffer = new MemoryStream();
            await csv.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return Parse(buffer);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or MalformedLineException or DecoderFallbackException)
        {
            throw new HrisIntegrationException($"CSV file could not be read: {exception.Message}", exception);
        }
    }

    private static CSV_TABLE_DTO Parse(Stream stream)
    {
        using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return new CSV_TABLE_DTO();
        }

        var columns = (parser.ReadFields() ?? []).ToArray();
        if (columns.Length > 0)
        {
            columns[0] = columns[0].TrimStart(ByteOrderMark);
        }

        var rows = new List<string?[]>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            if (fields.Length == 0 || (fields.Length == 1 && fields[0].Length == 0))
            {
                continue;
            }

            if (fields.Length > columns.Length)
            {
                throw new InvalidDataException(
                    $"Line {parser.LineNumber - 1} contains more values than the header.");
            }

            rows.Add(fields);
        }

        return new CSV_TABLE_DTO { COLUMNS = columns, ROWS = rows };
    }

    private static string? Cell(IReadOnlyList<string> columns, string?[] fields, string column)
    {
        var index = IndexOf(columns, column);
        return index >= 0 && index < fields.Length ? fields[index] : null;
    }

    /// <summary>Header lookup that ignores case and surrounding spaces; -1 when absent.</summary>
    public static int IndexOf(IReadOnlyList<string> columns, string column)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
