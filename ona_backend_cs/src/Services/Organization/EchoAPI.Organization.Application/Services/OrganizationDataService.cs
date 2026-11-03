using EchoAPI.Organization.Application.Abstractions;
using EchoAPI.Organization.Application.DTOs;

namespace EchoAPI.Organization.Application.Services;

public sealed class OrganizationDataService(IOrganizationDataReader reader)
{
    public OrganizationDataDto ReadCsv(Stream stream)
    {
        var records = reader.ReadCsv(stream);
        return new OrganizationDataDto(records.Count, records);
    }
}
