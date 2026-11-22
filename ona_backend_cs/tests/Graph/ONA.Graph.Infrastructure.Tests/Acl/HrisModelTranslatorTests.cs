using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Infrastructure.Acl;
using ONA.Graph.Infrastructure.Acl.ExternalContracts;

namespace ONA.Graph.Infrastructure.Tests.Acl;

/// <summary>
/// One test per row of the ACL mapping table in docs/architecture/context-map.md.
/// The SUT is the real internal translator (no mocks).
/// </summary>
public sealed class HrisModelTranslatorTests
{
    private static readonly string[] AllColumns =
        ["EmpID", "FirstName", "LastName", "Title", "DepartmentType", "Supervisor"];

    private readonly HrisModelTranslator _sut = new();

    [Fact]
    public void ToOrganization_WithValidRows_MapsNamesDepartmentsAndSupervisor()
    {
        // Rows: EmpID, FirstName+LastName -> NodeName, Title, DepartmentType -> DepartmentName, Supervisor
        var export = Export(
            Row("100", " Alex ", "One", "Director", "Management", ""),
            Row(" 101 ", "Sam", "Smith", " Manager ", " Engineering ", " 100 "));

        var members = _sut.ToOrganization(export).Members;

        Assert.Equal(2, members.Count);
        var sam = members[1];
        Assert.Equal("101", sam.EmployeeId);
        Assert.Equal("Sam Smith", sam.Name.Value);
        Assert.Equal("Manager", sam.Title);
        Assert.Equal("Engineering", sam.Department.Value);
        Assert.Equal("100", sam.SupervisorEmployeeId);
        Assert.Equal("Alex One", members[0].Name.Value);
        Assert.Null(members[0].SupervisorEmployeeId);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ToOrganization_WhenSupervisorIsHrisMissingValue_MapsToTopOfHierarchy(string? supervisor)
    {
        var export = Export(Row("100", "Alex", "One", "Director", "Management", supervisor));

        var member = Assert.Single(_sut.ToOrganization(export).Members);

        Assert.Null(member.SupervisorEmployeeId);
    }

    [Fact]
    public void ToOrganization_WhenRequiredColumnMissing_ThrowsMissingImportColumnsException()
    {
        var export = new HRIS_EXPORT_DTO
        {
            COLUMNS = ["EmpID", "FirstName", "LastName", "Title"],
            ROWS = [Row("100", "Alex", "One", "Director", null, null)]
        };

        var exception = Assert.Throws<MissingImportColumnsException>(() => _sut.ToOrganization(export));

        Assert.Equal("board_import.missing_columns", exception.Code);
        Assert.Equal<string>(new[] { "DepartmentType", "Supervisor" }, exception.MissingColumns);
    }

    [Fact]
    public void ToOrganization_WhenColumnsDifferOnlyByCaseAndSpaces_AcceptsExport()
    {
        var export = new HRIS_EXPORT_DTO
        {
            COLUMNS = ["empid", " FIRSTNAME", "lastname ", "title", "departmenttype", "supervisor"],
            ROWS = [Row("100", "Alex", "One", "Director", "Management", null)]
        };

        Assert.Single(_sut.ToOrganization(export).Members);
    }

    [Fact]
    public void ToOrganization_WhenExportHasNoRows_ThrowsInvalidBoardImportException()
    {
        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(Export()));

        Assert.Equal("board_import.invalid", exception.Code);
    }

    [Theory]
    [InlineData("EmpID")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("Title")]
    [InlineData("DepartmentType")]
    public void ToOrganization_WhenRequiredCellBlankOrNaN_ThrowsInvalidBoardImportWithRowNumber(string column)
    {
        var export = Export(
            Row("100", "Alex", "One", "Director", "Management", null),
            Row(
                column == "EmpID" ? "NaN" : "101",
                column == "FirstName" ? " " : "Sam",
                column == "LastName" ? "" : "Smith",
                column == "Title" ? "NaN" : "Manager",
                column == "DepartmentType" ? null : "Engineering",
                "100"));

        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(export));

        Assert.Equal("board_import.invalid", exception.Code);
        Assert.Contains("row 3", exception.Message);
        Assert.Contains($"'{column}'", exception.Message);
    }

    [Fact]
    public void ToOrganization_WhenEmpIdDuplicated_ThrowsInvalidBoardImportException()
    {
        var export = Export(
            Row("100", "Alex", "One", "Director", "Management", null),
            Row("100", "Sam", "Smith", "Manager", "Engineering", null));

        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(export));

        Assert.Equal("board_import.invalid", exception.Code);
        Assert.Contains("duplicated", exception.Message);
    }

    [Fact]
    public void ToOrganization_WhenSupervisorUnknown_ThrowsInvalidBoardImportException()
    {
        var export = Export(Row("100", "Alex", "One", "Director", "Management", "999"));

        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(export));

        Assert.Equal("board_import.invalid", exception.Code);
        Assert.Contains("'999' does not exist", exception.Message);
    }

    [Fact]
    public void ToOrganization_WhenEmployeeSupervisesThemself_ThrowsInvalidBoardImportException()
    {
        var export = Export(Row("100", "Alex", "One", "Director", "Management", "100"));

        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(export));

        Assert.Equal("board_import.invalid", exception.Code);
        Assert.Contains("cannot supervise themself", exception.Message);
    }

    private static HRIS_EXPORT_DTO Export(params HRIS_EMPLOYEE_DTO[] rows) =>
        new() { COLUMNS = AllColumns, ROWS = rows };

    private static HRIS_EMPLOYEE_DTO Row(
        string? empId,
        string? firstName,
        string? lastName,
        string? title,
        string? departmentType,
        string? supervisor) =>
        new()
        {
            EmpID = empId,
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            DepartmentType = departmentType,
            Supervisor = supervisor
        };
}
