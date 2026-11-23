using System.Text;
using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Infrastructure.Acl;
using ONA.Graph.Infrastructure.Acl.ExternalContracts;

namespace ONA.Graph.Infrastructure.Tests.Acl;

/// <summary>"Any CSV: column mapping" in docs/architecture/context-map.md.</summary>
public sealed class CustomColumnMappingTests
{
    private readonly HrisModelTranslator _sut = new();

    [Fact]
    public void FullNameMapping_MapsAnyColumnNamesAndOrder()
    {
        var table = Table(["Boss", "Role", "Team", "Person"],
            ["", "CEO", "Exec", "Alex One"],
            ["Alex One", "Developer", "Eng", "Sam Smith"]);
        var mapping = new ColumnMapping(FullName: "Person", Title: "Role", Department: "Team", Supervisor: "Boss");

        var members = _sut.ToOrganization(table, mapping).Members;

        Assert.Equal(2, members.Count);
        Assert.Equal("Sam Smith", members[1].Name.Value);
        Assert.Equal("Developer", members[1].Title);
        Assert.Equal("Eng", members[1].Department.Value);
        Assert.Equal("row-2", members[1].SupervisorEmployeeId);   // matched by full name -> generated id of Alex
        Assert.Null(members[0].SupervisorEmployeeId);
    }

    [Fact]
    public void WithoutIdColumn_GeneratesIdsFromTheFileRow()
    {
        var table = Table(["Name"], ["Alex One"], ["Sam Smith"]);

        var members = _sut.ToOrganization(table, new ColumnMapping(FullName: "Name")).Members;

        Assert.Equal(["row-2", "row-3"], members.Select(m => m.EmployeeId));
    }

    [Fact]
    public void UnmappedTitleAndDepartment_GetDefaults()
    {
        var member = Assert.Single(_sut.ToOrganization(Table(["Name"], ["Alex One"]), new ColumnMapping(FullName: "Name")).Members);

        Assert.Equal("Employee", member.Title);
        Assert.Equal("General", member.Department.Value);
        Assert.Null(member.SupervisorEmployeeId);
    }

    [Fact]
    public void FirstNameOnly_IsEnoughAsNameSource()
    {
        var member = Assert.Single(_sut.ToOrganization(Table(["Given"], ["Alex"]), new ColumnMapping(FirstName: "Given")).Members);

        Assert.Equal("Alex", member.Name.Value);
    }

    [Fact]
    public void FullName_WinsOverFirstAndLastName()
    {
        var table = Table(["Full", "First", "Last"], ["Alex One", "X", "Y"]);

        var member = Assert.Single(_sut.ToOrganization(table, new ColumnMapping(FullName: "Full", FirstName: "First", LastName: "Last")).Members);

        Assert.Equal("Alex One", member.Name.Value);
    }

    [Fact]
    public void NoNameSource_ThrowsInvalidColumnMapping()
    {
        var exception = Assert.Throws<InvalidColumnMappingException>(
            () => _sut.ToOrganization(Table(["Boss"], ["x"]), new ColumnMapping(Supervisor: "Boss")));

        Assert.Equal("board_import.invalid_mapping", exception.Code);
    }

    [Fact]
    public void MappedColumnNotInHeader_ThrowsMissingImportColumns()
    {
        var exception = Assert.Throws<MissingImportColumnsException>(
            () => _sut.ToOrganization(Table(["Name"], ["Alex"]), new ColumnMapping(FullName: "Name", Title: "Job", Supervisor: " boss ")));

        Assert.Equal<string>(["Job", " boss "], exception.MissingColumns);
    }

    [Fact]
    public void MappedColumnsAreFoundIgnoringCaseAndSpaces()
    {
        var member = Assert.Single(_sut.ToOrganization(Table([" full NAME "], ["Alex One"]), new ColumnMapping(FullName: "Full Name")).Members);

        Assert.Equal("Alex One", member.Name.Value);
    }

    [Fact]
    public void BlankNameCell_RejectsTheFileAndNamesTheMappedColumnAndRow()
    {
        var table = Table(["Full Name"], ["Alex One"], ["  "]);

        var exception = Assert.Throws<InvalidBoardImportException>(
            () => _sut.ToOrganization(table, new ColumnMapping(FullName: "Full Name")));

        Assert.Contains("row 3", exception.Message);
        Assert.Contains("'Full Name'", exception.Message);
    }

    [Fact]
    public void MappedIdColumn_MustBeUnique()
    {
        var table = Table(["Id", "Name"], ["1", "Alex"], ["1", "Sam"]);

        var exception = Assert.Throws<InvalidBoardImportException>(
            () => _sut.ToOrganization(table, new ColumnMapping(EmployeeId: "Id", FullName: "Name")));

        Assert.Contains("duplicated", exception.Message);
    }

    [Fact]
    public void ShortRows_AreReadAsEmptyCells()
    {
        var table = Table(["Name", "Boss"], ["Alex One"]);   // the Boss cell is missing in the file

        var member = Assert.Single(_sut.ToOrganization(table, new ColumnMapping(FullName: "Name", Supervisor: "Boss")).Members);

        Assert.Null(member.SupervisorEmployeeId);
    }

    [Fact]
    public void SuggestMapping_RecognisesCommonHeaderNames()
    {
        var mapping = _sut.SuggestMapping(["Employee ID", "Full Name", "Job Title", "Department", "Reports To", "Salary"]);

        Assert.Equal(new ColumnMapping(EmployeeId: "Employee ID", FullName: "Full Name", Title: "Job Title", Department: "Department", Supervisor: "Reports To"), mapping);
    }

    [Fact]
    public void SuggestMapping_PrefersFirstAndLastNameOverAGenericName()
    {
        var mapping = _sut.SuggestMapping(["id", "name", "first_name", "surname", "manager"]);

        Assert.Equal(new ColumnMapping(EmployeeId: "id", FirstName: "first_name", LastName: "surname", Supervisor: "manager"), mapping);
    }

    [Fact]
    public void SuggestMapping_ForTheHrisHeader_IsTheHrisMapping()
    {
        Assert.Equal(ColumnMapping.Hris,
            _sut.SuggestMapping(["EmpID", "FirstName", "LastName", "StartDate", "Title", "Supervisor", "DepartmentType"]));
    }

    [Fact]
    public void SuggestMapping_WhenNothingMatches_MapsNothing()
    {
        Assert.Equal(new ColumnMapping(), _sut.SuggestMapping(["foo", "bar"]));
    }

    // ---- facade (real adapter + translator, no mocks) ----

    private readonly HrisEmployeeDirectoryAcl _acl = new(new CsvHrisAdapter(), new HrisModelTranslator());

    [Fact]
    public async Task Facade_ReadsAnyCsvWithAMapping()
    {
        var csv = "Person,Manager,Role\nAlex One,,CEO\n\"Smith, Sam\",Alex One,Developer\n";

        var members = (await _acl.ReadOrganizationAsync(Stream(csv), new ColumnMapping(FullName: "Person", Title: "Role", Supervisor: "Manager"))).Members;

        Assert.Equal(2, members.Count);
        Assert.Equal("Smith, Sam", members[1].Name.Value);
        Assert.Equal("row-2", members[1].SupervisorEmployeeId);
    }

    [Fact]
    public async Task Facade_WithoutMapping_StillReadsTheHrisFormat()
    {
        var csv = "EmpID,FirstName,LastName,Title,DepartmentType,Supervisor\n100,Alex,One,CEO,Mgmt,\n101,Sam,Smith,Dev,Eng,100\n";

        var members = (await _acl.ReadOrganizationAsync(Stream(csv))).Members;

        Assert.Equal("100", members[1].SupervisorEmployeeId);
    }

    [Fact]
    public async Task Facade_WithoutMappingAndWithoutHrisColumns_ReportsTheMissingColumns()
    {
        var exception = await Assert.ThrowsAsync<MissingImportColumnsException>(
            () => _acl.ReadOrganizationAsync(Stream("Person,Manager\nAlex,\n")));

        Assert.Contains("EmpID", exception.MissingColumns);
    }

    [Fact]
    public async Task Preview_ReturnsHeaderFirstFiveRowsCountAndSuggestion()
    {
        var csv = "Employee ID,Full Name,Reports To\n" + string.Concat(Enumerable.Range(1, 8).Select(i => $"{i},Person {i},\n"));

        var preview = await _acl.PreviewAsync(Stream(csv));

        Assert.Equal(["Employee ID", "Full Name", "Reports To"], preview.Columns);
        Assert.Equal(5, preview.SampleRows.Count);
        Assert.Equal(["1", "Person 1", ""], preview.SampleRows[0]);
        Assert.Equal(8, preview.RowCount);
        Assert.Equal(new ColumnMapping(EmployeeId: "Employee ID", FullName: "Full Name", Supervisor: "Reports To"), preview.SuggestedMapping);
    }

    [Fact]
    public async Task Preview_OfAnEmptyFile_HasNoColumns()
    {
        var preview = await _acl.PreviewAsync(Stream(""));

        Assert.Empty(preview.Columns);
        Assert.Equal(0, preview.RowCount);
    }

    private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static CSV_TABLE_DTO Table(string[] columns, params string[][] rows) => new() { COLUMNS = columns, ROWS = rows };
}
