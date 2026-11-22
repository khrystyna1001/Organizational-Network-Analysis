using System.Text;
using ONA.Graph.Application.Ports;
using ONA.Graph.Infrastructure.Acl;

namespace ONA.Graph.Infrastructure.Tests.Acl;

public sealed class CsvHrisAdapterTests
{
    private readonly CsvHrisAdapter _sut = new();

    [Fact]
    public async Task ReadExportAsync_WithBomQuotesAndExtraColumns_MapsKnownColumns()
    {
        const string csv =
            "﻿EmpID,FirstName,LastName,StartDate,Title,DepartmentType,Supervisor\n" +
            "100,\"Alex, Jr.\",One,2019-01-01,Director,Management,\n" +
            "\n" +
            "101,Sam,Smith,2020-02-02,Manager,Engineering,100\n";

        var export = await _sut.ReadExportAsync(ToStream(csv));

        Assert.Equal("EmpID", export.COLUMNS[0]);
        Assert.Equal(2, export.ROWS.Count);
        Assert.Equal("Alex, Jr.", export.ROWS[0].FirstName);
        Assert.Equal("Manager", export.ROWS[1].Title);
        Assert.Equal("100", export.ROWS[1].Supervisor);
    }

    [Fact]
    public async Task ReadExportAsync_WhenRowLongerThanHeader_ThrowsHrisIntegrationException()
    {
        const string csv = "EmpID,FirstName\n100,Alex,Unexpected\n";

        var exception = await Assert.ThrowsAsync<HrisIntegrationException>(
            () => _sut.ReadExportAsync(ToStream(csv)));

        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadExportAsync_WhenQuotesAreUnbalanced_ThrowsHrisIntegrationException()
    {
        const string csv = "EmpID,FirstName\n100,\"Alex\n";

        await Assert.ThrowsAsync<HrisIntegrationException>(() => _sut.ReadExportAsync(ToStream(csv)));
    }

    [Fact]
    public async Task ReadExportAsync_WhenEmpty_ReturnsExportWithoutColumns()
    {
        var export = await _sut.ReadExportAsync(ToStream(string.Empty));

        Assert.Empty(export.COLUMNS);
        Assert.Empty(export.ROWS);
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
