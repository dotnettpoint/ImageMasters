using ImageMaster.Core.Models;
using Xunit;

namespace ImageMaster.Core.Tests;

public class ResizeRequestValidationTests
{
    [Fact]
    public void Validate_ByDimensions_MissingWidthAndHeight_ReturnsErrors()
    {
        var request = new ResizeRequest { ByPercentage = false, TargetWidth = null, TargetHeight = null };

        var errors = request.Validate();

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validate_ByPercentage_ZeroOrNegative_ReturnsError()
    {
        var request = new ResizeRequest { ByPercentage = true, PercentageValue = 0 };

        var errors = request.Validate();

        Assert.Single(errors);
    }

    [Fact]
    public void Validate_ValidByDimensionsRequest_ReturnsNoErrors()
    {
        var request = new ResizeRequest { ByPercentage = false, TargetWidth = 100, TargetHeight = 200 };

        Assert.Empty(request.Validate());
    }
}

public class DpiChangeRequestValidationTests
{
    [Theory]
    [InlineData(72, 72, true)]
    [InlineData(0, 96, false)]
    [InlineData(96, 2500, false)]
    public void Validate_VariousInputs_MatchesExpectedOutcome(double dpiX, double dpiY, bool expectedValid)
    {
        var request = new DpiChangeRequest { DpiX = dpiX, DpiY = dpiY };

        var isValid = request.Validate().Count == 0;

        Assert.Equal(expectedValid, isValid);
    }
}
