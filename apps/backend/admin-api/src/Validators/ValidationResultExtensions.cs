using FluentValidation.Results;

namespace AdminApi.Validators;

public static class ValidationResultExtensions
{
    public static IResult ToBadRequest(this ValidationResult result) =>
        Results.Json(
            new
            {
                isSuccessful = false,
                message = "Validation failed",
                errors = result.Errors.Select(error => new { field = error.PropertyName, message = error.ErrorMessage }),
            },
            statusCode: StatusCodes.Status400BadRequest);
}
