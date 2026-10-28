using Erp.SharedKernel.Results;

namespace Erp.SharedKernel.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Factories_set_error_type()
    {
        Assert.Equal(ErrorType.Validation, Error.Validation("C", "M").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("C", "M").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("C", "M").Type);
        Assert.Equal(ErrorType.Failure, new Error("C", "M").Type);
    }

    [Fact]
    public void Validation_error_carries_errors_per_property()
    {
        var error = new ValidationError(new Dictionary<string, string[]> { ["Name"] = ["NotEmptyValidator"] });

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Validation.Failed", error.Code);
        Assert.Equal(["NotEmptyValidator"], error.Errors["Name"]);
    }

    [Fact]
    public void Validation_error_converts_to_failed_result()
    {
        Result<int> result = new ValidationError(new Dictionary<string, string[]>());

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }
}
