// Foreign model of the external HR information system (HRIS) employee export.
// Field names intentionally mirror the HRIS columns. These types must never leave Infrastructure/Acl.
namespace ONA.Graph.Infrastructure.Acl.ExternalContracts;

internal sealed class HRIS_EXPORT_DTO
{
    /// <summary>Header row exactly as delivered by the HRIS (BOM removed).</summary>
    public IReadOnlyList<string> COLUMNS { get; init; } = [];

    public IReadOnlyList<HRIS_EMPLOYEE_DTO> ROWS { get; init; } = [];
}

internal sealed class HRIS_EMPLOYEE_DTO
{
    public string? EmpID { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Title { get; init; }
    public string? DepartmentType { get; init; }
    public string? Supervisor { get; init; }
}

/// <summary>Any CSV file as delivered: the header and the raw cells of every row. No business meaning yet.</summary>
internal sealed class CSV_TABLE_DTO
{
    public IReadOnlyList<string> COLUMNS { get; init; } = [];

    /// <summary>Cells in header order; a row may be shorter than the header.</summary>
    public IReadOnlyList<string?[]> ROWS { get; init; } = [];
}
