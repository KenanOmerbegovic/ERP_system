using Erp.Modules.Core.Contracts.Common;
using Erp.Modules.Core.Endpoints;
using Erp.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Core.Tests.Endpoints;

public class HttpResultsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 422)]
    public void Error_type_decides_status_code(ErrorType type, int statusCode)
    {
        var problem = Assert.IsType<ProblemHttpResult>(HttpResults.Problem(new Error("Test.Code", "Message.", type)));

        Assert.Equal(statusCode, problem.StatusCode);
        Assert.Equal("Message.", problem.ProblemDetails.Title);
        Assert.Equal("Test.Code", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void Validation_error_lists_codes_per_property()
    {
        var error = new ValidationError(new Dictionary<string, string[]> { ["Name"] = ["NotEmptyValidator"] });

        var problem = Assert.IsType<ProblemHttpResult>(HttpResults.Problem(error));

        Assert.Equal(400, problem.StatusCode);
        var details = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal(["NotEmptyValidator"], details.Errors["Name"]);
        Assert.Equal("Validation.Failed", details.Extensions["code"]);
    }

    [Fact]
    public void Created_returns_new_id()
    {
        var id = Guid.NewGuid();

        var created = Assert.IsType<Created<CreatedResponse>>(HttpResults.Created(Result.Success(id)));

        Assert.Equal(id, created.Value!.Id);
    }

    [Fact]
    public void Success_without_value_is_no_content()
    {
        Assert.IsType<NoContent>(HttpResults.NoContent(Result.Success()));
    }
}
