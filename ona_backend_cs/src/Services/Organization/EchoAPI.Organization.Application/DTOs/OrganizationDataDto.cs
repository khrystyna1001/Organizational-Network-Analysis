namespace EchoAPI.Organization.Application.DTOs;

public sealed record OrganizationDataDto(
    int TotalRows,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data);
