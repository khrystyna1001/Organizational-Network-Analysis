using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Domain.ValueObjects;
using ONA.Graph.Infrastructure.Acl.ExternalContracts;

namespace ONA.Graph.Infrastructure.Acl;

/// <summary>
/// Pure translation HRIS -> Graph model (no I/O). Every rule below is one row of the
/// mapping table in docs/architecture/context-map.md and has a unit test in
/// tests/Graph/ONA.Graph.Infrastructure.Tests/Acl/HrisModelTranslatorTests.cs.
/// </summary>
internal sealed class HrisModelTranslator
{
    internal static readonly IReadOnlyList<string> RequiredColumns =
        ["EmpID", "FirstName", "LastName", "Title", "DepartmentType", "Supervisor"];

    /// <summary>The HRIS writes exactly this literal for an empty cell. Case matters: "Nan" is a real first name.</summary>
    private const string HrisMissingValue = "NaN";

    private const string DefaultTitle = "Employee";
    private const string DefaultDepartment = "General";
    private const string PlaceholderTitle = "Supervisor (not in export)";
    private const string PlaceholderDepartment = "External";
    private const string PlaceholderIdPrefix = "ext:";

    /// <summary>Row 1 of the export is the header, so data row i is file row i + 2.</summary>
    private const int FirstDataRowNumber = 2;

    /// <summary>The HRIS export is just a table with the built-in mapping.</summary>
    public OrganizationSnapshot ToOrganization(HRIS_EXPORT_DTO export)
    {
        ArgumentNullException.ThrowIfNull(export);

        EnsureRequiredColumns(export.COLUMNS);

        var table = new CSV_TABLE_DTO
        {
            COLUMNS = RequiredColumns,
            ROWS = export.ROWS
                .Select(row => new[] { row.EmpID, row.FirstName, row.LastName, row.Title, row.DepartmentType, row.Supervisor })
                .ToArray()
        };
        return ToOrganization(table, ColumnMapping.Hris);
    }

    /// <summary>Any people table: every rule is a row of "Any CSV: column mapping" in docs/architecture/context-map.md.</summary>
    public OrganizationSnapshot ToOrganization(CSV_TABLE_DTO table, ColumnMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(mapping);

        // FullName wins; otherwise first/last name are used.
        var useParts = mapping.FullName is null;
        var first = useParts ? mapping.FirstName : null;
        var last = useParts ? mapping.LastName : null;
        if (mapping.FullName is null && first is null && last is null)
        {
            throw new InvalidColumnMappingException("Map a column that holds the person's name (full name, or first/last name).");
        }

        var missing = new List<string>();
        var id = Locate(table.COLUMNS, mapping.EmployeeId, missing);
        var full = Locate(table.COLUMNS, mapping.FullName, missing);
        var firstIndex = Locate(table.COLUMNS, first, missing);
        var lastIndex = Locate(table.COLUMNS, last, missing);
        var title = Locate(table.COLUMNS, mapping.Title, missing);
        var department = Locate(table.COLUMNS, mapping.Department, missing);
        var supervisor = Locate(table.COLUMNS, mapping.Supervisor, missing);
        if (missing.Count > 0)
        {
            throw new MissingImportColumnsException(missing);
        }

        if (table.ROWS.Count == 0)
        {
            throw new InvalidBoardImportException("CSV must contain at least one employee row.");
        }

        var members = new List<OrganizationMember>(table.ROWS.Count);
        var rawSupervisors = new List<string?>(table.ROWS.Count);
        var employeeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < table.ROWS.Count; index++)
        {
            var row = table.ROWS[index];
            var rowNumber = index + FirstDataRowNumber;

            var employeeId = id >= 0 ? Require(Cell(row, id), rowNumber, mapping.EmployeeId!) : $"row-{rowNumber}";
            var name = full >= 0
                ? Require(Cell(row, full), rowNumber, mapping.FullName!)
                : string.Join(' ', new[] { (firstIndex, first), (lastIndex, last) }
                    .Where(part => part.Item1 >= 0)
                    .Select(part => Require(Cell(row, part.Item1), rowNumber, part.Item2!)));
            var rowTitle = title >= 0 ? Require(Cell(row, title), rowNumber, mapping.Title!) : DefaultTitle;
            var rowDepartment = department >= 0 ? Require(Cell(row, department), rowNumber, mapping.Department!) : DefaultDepartment;

            if (!employeeIds.Add(employeeId))
            {
                throw new InvalidBoardImportException(
                    rowNumber,
                    mapping.EmployeeId ?? "EmpID",
                    $"employee ID '{employeeId}' is duplicated.");
            }

            members.Add(new OrganizationMember(
                employeeId,
                new NodeName(name),
                rowTitle,
                new DepartmentName(rowDepartment),
                null));
            rawSupervisors.Add(supervisor >= 0 ? Clean(Cell(row, supervisor)) : null);
        }

        return new OrganizationSnapshot(ResolveSupervisors(members, rawSupervisors, employeeIds));
    }

    /// <summary>Guesses a mapping from common header names. Only a suggestion: the caller confirms it.</summary>
    public ColumnMapping SuggestMapping(IReadOnlyList<string> columns)
    {
        string? Find(params string[] synonyms) =>
            columns.FirstOrDefault(column => synonyms.Contains(Normalize(column)));

        var first = Find("firstname", "givenname", "fname", "forename");
        var last = Find("lastname", "surname", "familyname", "lname");
        var full = first is null && last is null
            ? Find("fullname", "name", "employeename", "personname", "displayname", "person", "employee")
            : null;

        return new ColumnMapping(
            EmployeeId: Find("empid", "employeeid", "employeenumber", "employeeno", "id", "staffid", "personid", "userid", "workerid"),
            FullName: full,
            FirstName: first,
            LastName: last,
            Title: Find("title", "jobtitle", "position", "role", "jobrole", "designation"),
            Department: Find("departmenttype", "department", "dept", "team", "businessunit", "division"),
            Supervisor: Find("supervisor", "supervisorname", "manager", "managername", "boss", "reportsto", "teamlead", "lead", "managerid", "supervisorid"));
    }

    private static string Normalize(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Index of a mapped column, or -1 when it is not mapped. A mapped name that is absent is added to <paramref name="missing"/>.</summary>
    private static int Locate(IReadOnlyList<string> columns, string? mapped, List<string> missing)
    {
        if (mapped is null)
        {
            return -1;
        }

        var index = CsvHrisAdapter.IndexOf(columns, mapped);
        if (index < 0)
        {
            missing.Add(mapped);
        }

        return index;
    }

    private static string? Cell(string?[] row, int index) => index < row.Length ? row[index] : null;

    /// <summary>
    /// Mapping rows 8-11 and 14: a supervisor cell is an EmpID, else the full name of exactly one employee,
    /// else a placeholder person (one per distinct value) so the reporting line is kept.
    /// </summary>
    private static List<OrganizationMember> ResolveSupervisors(
        List<OrganizationMember> members,
        List<string?> rawSupervisors,
        HashSet<string> employeeIds)
    {
        var idsByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            if (!idsByName.TryGetValue(member.Name.Value, out var ids))
            {
                idsByName[member.Name.Value] = ids = [];
            }

            ids.Add(member.EmployeeId);
        }

        var resolved = new List<OrganizationMember>(members.Count);
        var placeholders = new Dictionary<string, OrganizationMember>(StringComparer.OrdinalIgnoreCase);
        var placeholderOrder = new List<OrganizationMember>();

        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            var raw = rawSupervisors[index];
            if (raw is null)
            {
                resolved.Add(member);
                continue;
            }

            if (string.Equals(raw, member.EmployeeId, StringComparison.Ordinal) ||
                string.Equals(raw, member.Name.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidBoardImportException(
                    index + FirstDataRowNumber,
                    "Supervisor",
                    "an employee cannot supervise themself.");
            }

            string supervisorId;
            if (employeeIds.Contains(raw))
            {
                supervisorId = raw;
            }
            else if (idsByName.TryGetValue(raw, out var matches) && matches.Count == 1)
            {
                supervisorId = matches[0];
            }
            else
            {
                if (!placeholders.TryGetValue(raw, out var placeholder))
                {
                    placeholder = new OrganizationMember(
                        PlaceholderIdPrefix + raw,
                        new NodeName(raw),
                        PlaceholderTitle,
                        new DepartmentName(PlaceholderDepartment),
                        null);
                    placeholders.Add(raw, placeholder);
                    placeholderOrder.Add(placeholder);
                }

                supervisorId = placeholder.EmployeeId;
            }

            resolved.Add(member with { SupervisorEmployeeId = supervisorId });
        }

        resolved.AddRange(placeholderOrder);
        return resolved;
    }

    private static void EnsureRequiredColumns(IReadOnlyList<string> columns)
    {
        var present = columns
            .Select(column => column.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredColumns.Where(column => !present.Contains(column)).ToArray();

        if (missing.Length > 0)
        {
            throw new MissingImportColumnsException(missing);
        }
    }

    private static string Require(string? value, int rowNumber, string column) =>
        Clean(value) ?? throw new InvalidBoardImportException(rowNumber, column, "value is required.");

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ||
               string.Equals(trimmed, HrisMissingValue, StringComparison.Ordinal)
            ? null
            : trimmed;
    }
}
