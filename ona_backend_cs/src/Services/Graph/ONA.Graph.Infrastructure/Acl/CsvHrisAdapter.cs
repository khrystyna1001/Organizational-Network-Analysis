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
        ArgumentNullException.ThrowIfNull(export);

        try
        {
            using var buffer = new MemoryStream();
            await export.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return Parse(buffer);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or MalformedLineException or DecoderFallbackException)
        {
            throw new HrisIntegrationException($"HRIS export could not be read: {exception.Message}", exception);
        }
    }

    private static HRIS_EXPORT_DTO Parse(Stream stream)
    {
        using var parser = new TextFieldParser(stream, new UTF8Encoding(false, true), true)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return new HRIS_EXPORT_DTO();
        }

        var columns = (parser.ReadFields() ?? []).ToArray();
        if (columns.Length > 0)
        {
            columns[0] = columns[0].TrimStart(ByteOrderMark);
        }

        var rows = new List<HRIS_EMPLOYEE_DTO>();
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

            rows.Add(new HRIS_EMPLOYEE_DTO
            {
                EmpID = Cell(columns, fields, "EmpID"),
                FirstName = Cell(columns, fields, "FirstName"),
                LastName = Cell(columns, fields, "LastName"),
                Title = Cell(columns, fields, "Title"),
                DepartmentType = Cell(columns, fields, "DepartmentType"),
                Supervisor = Cell(columns, fields, "Supervisor")
            });
        }

        return new HRIS_EXPORT_DTO { COLUMNS = columns, ROWS = rows };
    }

    private static string? Cell(string[] columns, string[] fields, string column)
    {
        var index = Array.FindIndex(
            columns,
            candidate => string.Equals(candidate.Trim(), column, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < fields.Length ? fields[index] : null;
    }
}
