using ImageMaster.Core.Models;
using Xunit;

namespace ImageMaster.Core.Tests;

public class OperationResultTests
{
    [Fact]
    public void Ok_SetsSuccessAndValue()
    {
        var result = OperationResult<int>.Ok(42);

        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
        Assert.Null(result.ErrorMessage);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Fail_SetsErrorMessageAndNoValue()
    {
        var result = OperationResult<int>.Fail("something went wrong");

        Assert.False(result.Success);
        Assert.Equal(0, result.Value);
        Assert.Equal("something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Ok_WithWarnings_PreservesThem()
    {
        var result = OperationResult<string>.Ok("value", new[] { "warning 1", "warning 2" });

        Assert.Equal(2, result.Warnings.Count);
    }
}
