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
    [InlineData("   ")]
    [InlineData(null)]
    public void ToOrganization_WhenSupervisorIsHrisMissingValue_MapsToTopOfHierarchy(string? supervisor)
    {
        var export = Export(Row("100", "Alex", "One", "Director", "Management", supervisor));

        var member = Assert.Single(_sut.ToOrganization(export).Members);

        Assert.Null(member.SupervisorEmployeeId);
    }

    [Theory]
    [InlineData("Nan")]
    [InlineData("nan")]
    public void ToOrganization_WhenNameOnlyLooksLikeNaN_KeepsItAsARealName(string firstName)
    {
        // Regression: the HRIS marks an empty cell with the exact literal "NaN"; "Nan" is a real first name
        // (employee_data.csv row 893 "Nan Singh" was rejected as "FirstName: value is required").
        var export = Export(Row("100", firstName, "Singh", "Administrative Assistant", "Admin Offices", ""));

        var member = Assert.Single(_sut.ToOrganization(export).Members);

        Assert.Equal($"{firstName} Singh", member.Name.Value);
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
    public void ToOrganization_WhenSupervisorIsNotInExport_CreatesOnePlaceholderSharedByAllItsReports()
    {
        var export = Export(
            Row("100", "Alex", "One", "Director", "Management", " Donald Decker "),
            Row("101", "Sam", "Smith", "Manager", "Engineering", "donald decker"));

        var members = _sut.ToOrganization(export).Members;

        Assert.Equal(3, members.Count);
        var placeholder = members[2];
        Assert.Equal("ext:Donald Decker", placeholder.EmployeeId);
        Assert.Equal("Donald Decker", placeholder.Name.Value);
        Assert.Equal("Supervisor (not in export)", placeholder.Title);
        Assert.Equal("External", placeholder.Department.Value);
        Assert.Null(placeholder.SupervisorEmployeeId);
        Assert.Equal(placeholder.EmployeeId, members[0].SupervisorEmployeeId);
        Assert.Equal(placeholder.EmployeeId, members[1].SupervisorEmployeeId);
    }

    [Fact]
    public void ToOrganization_WhenSupervisorIsTheFullNameOfOneEmployee_LinksToThatEmployee()
    {
        var export = Export(
            Row("100", "Alex", "One", "Director", "Management", null),
            Row("101", "Sam", "Smith", "Manager", "Engineering", " alex ONE "));

        var members = _sut.ToOrganization(export).Members;

        Assert.Equal(2, members.Count);
        Assert.Equal("100", members[1].SupervisorEmployeeId);
    }

    [Fact]
    public void ToOrganization_WhenSupervisorNameIsSharedByTwoEmployees_UsesAPlaceholderInsteadOfGuessing()
    {
        var export = Export(
            Row("100", "Pat", "Lee", "Developer", "Engineering", null),
            Row("101", "Pat", "Lee", "Tester", "Engineering", null),
            Row("102", "Sam", "Smith", "Manager", "Engineering", "Pat Lee"));

        var members = _sut.ToOrganization(export).Members;

        Assert.Equal(4, members.Count);
        Assert.Equal("ext:Pat Lee", members[2].SupervisorEmployeeId);
        Assert.Equal("External", members[3].Department.Value);
    }

    [Fact]
    public void ToOrganization_WhenEmpIdAndNameBothMatch_TheEmpIdWins()
    {
        var export = Export(
            Row("200", "Bob", "Ray", "Director", "Management", null),
            Row("100", "200", "Smith", "Manager", "Engineering", null),
            Row("101", "Sam", "Jones", "Developer", "Engineering", "200 Smith"),
            Row("102", "Kim", "Park", "Developer", "Engineering", "200"));

        var members = _sut.ToOrganization(export).Members;

        Assert.Equal("100", members[2].SupervisorEmployeeId);   // matched by full name "200 Smith"
        Assert.Equal("200", members[3].SupervisorEmployeeId);   // "200" is an EmpID, not the first name of employee 100
        Assert.Equal(4, members.Count);
    }

    [Fact]
    public void ToOrganization_WhenEmployeeNamesThemselfAsSupervisor_ThrowsInvalidBoardImportException()
    {
        var export = Export(Row("100", "Alex", "One", "Director", "Management", "Alex One"));

        var exception = Assert.Throws<InvalidBoardImportException>(() => _sut.ToOrganization(export));

        Assert.Contains("cannot supervise themself", exception.Message);
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
