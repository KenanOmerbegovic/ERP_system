using Erp.SharedKernel.Results;

namespace Erp.SharedKernel.Tests.Results;

public class ResultTests
{
    private static readonly Error SomeError = new("Test.Failed", "Something went wrong.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_carries_error()
    {
        var result = Result.Failure(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void Failure_with_none_throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
        Assert.Throws<ArgumentException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void Generic_success_exposes_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Reading_value_of_failure_throws()
    {
        var result = Result.Failure<int>(SomeError);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("Test.Failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Implicit_conversions_from_value_and_error()
    {
        Result<string> success = "ok";
        Result<string> failure = SomeError;
        Result plainFailure = SomeError;

        Assert.Equal("ok", success.Value);
        Assert.Equal(SomeError, failure.Error);
        Assert.True(plainFailure.IsFailure);
    }
}
