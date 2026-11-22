using ONA.Graph.Application.Ports;

namespace ONA.Graph.Infrastructure.Acl;

/// <summary>ACL facade: implements the domain port by combining the adapter (I/O) and the translator.</summary>
internal sealed class HrisEmployeeDirectoryAcl(CsvHrisAdapter adapter, HrisModelTranslator translator)
    : IEmployeeDirectoryPort
{
    public async Task<OrganizationSnapshot> ReadOrganizationAsync(
        Stream employeeExport,
        CancellationToken cancellationToken = default)
    {
        var export = await adapter.ReadExportAsync(employeeExport, cancellationToken);
        return translator.ToOrganization(export);
    }
}
