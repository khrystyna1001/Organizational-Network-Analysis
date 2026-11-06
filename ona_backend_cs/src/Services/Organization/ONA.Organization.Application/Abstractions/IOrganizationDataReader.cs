namespace ONA.Organization.Application.Abstractions;

public interface IOrganizationDataReader
{
    IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadCsv(Stream stream);
}
