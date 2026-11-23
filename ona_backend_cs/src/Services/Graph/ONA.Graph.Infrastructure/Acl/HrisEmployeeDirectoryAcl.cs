using ONA.Graph.Application.Ports;

namespace ONA.Graph.Infrastructure.Acl;

/// <summary>ACL facade: implements the domain port by combining the adapter (I/O) and the translator.</summary>
internal sealed class HrisEmployeeDirectoryAcl(CsvHrisAdapter adapter, HrisModelTranslator translator)
    : IEmployeeDirectoryPort
{
    private const int PreviewRows = 5;

    public async Task<OrganizationSnapshot> ReadOrganizationAsync(
        Stream employeeExport,
        ColumnMapping? mapping = null,
        CancellationToken cancellationToken = default)
    {
        var table = await adapter.ReadTableAsync(employeeExport, cancellationToken);
        return translator.ToOrganization(table, mapping ?? ColumnMapping.Hris);
    }

    public async Task<CsvPreview> PreviewAsync(Stream employeeExport, CancellationToken cancellationToken = default)
    {
        var table = await adapter.ReadTableAsync(employeeExport, cancellationToken);
        var sample = table.ROWS
            .Take(PreviewRows)
            .Select(row => (IReadOnlyList<string>)table.COLUMNS.Select((_, i) => (i < row.Length ? row[i] : null) ?? "").ToArray())
            .ToArray();
        return new CsvPreview(table.COLUMNS, sample, table.ROWS.Count, translator.SuggestMapping(table.COLUMNS));
    }
}
