using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Domain.ValueObjects;

namespace ONA.Graph.Domain.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NodeName_WhenBlank_ShouldThrowInvalidNodeException(string? value)
    {
        var exception = Assert.Throws<InvalidNodeException>(() => new NodeName(value));

        Assert.Equal("node.invalid_name", exception.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void DepartmentName_WhenBlank_ShouldThrowInvalidNodeException(string? value)
    {
        var exception = Assert.Throws<InvalidNodeException>(() => new DepartmentName(value));

        Assert.Equal("node.invalid_department", exception.Code);
    }

    [Fact]
    public void BoardName_WhenBlank_ShouldThrowInvalidBoardException()
    {
        var exception = Assert.Throws<InvalidBoardException>(() => new BoardName(" "));

        Assert.Equal("board.invalid_name", exception.Code);
    }

    [Fact]
    public void NodeName_ShouldKeepValueUnchangedAndCompareByValue()
    {
        var name = new NodeName(" Alex One ");

        Assert.Equal(" Alex One ", name.Value);
        Assert.Equal(new NodeName(" Alex One "), name);
        Assert.NotEqual(new NodeName("Alex One"), name);
    }

    [Fact]
    public void RenameTo_ShouldReturnNewInstanceAndLeaveOriginalUnchanged()
    {
        var original = new DepartmentName("Engineering");

        var renamed = original.RenameTo("People");

        Assert.Equal("Engineering", original.Value);
        Assert.Equal("People", renamed.Value);
    }
}
