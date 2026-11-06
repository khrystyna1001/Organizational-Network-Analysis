using ONA.Organization.Application.Abstractions;
using ONA.Organization.Application.DTOs;

namespace ONA.Organization.Application.Services;

public sealed class OrganizationDataService(IOrganizationDataReader reader)
{
    public OrganizationDataDto ReadCsv(Stream stream)
    {
        var records = reader.ReadCsv(stream);
        return new OrganizationDataDto(records.Count, records);
    }
}
