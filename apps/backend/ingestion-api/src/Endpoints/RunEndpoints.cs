using FluentValidation;
using IngestionApi.Dtos;
using IngestionApi.Services;
using IngestionApi.Validators;

namespace IngestionApi.Endpoints;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/v1/runs");

        group.MapPost("/", async (
            StartRunRequestDto body,
            IValidator<StartRunRequestDto> validator,
            IIngestionRunService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(body, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }

            try
            {
                var run = await service.StartAsync(body, cancellationToken);
                return Results.Json(run, statusCode: StatusCodes.Status201Created);
            }
            catch (SourceConnectorNotFoundException exception)
            {
                return Results.Json(
                    new { isSuccessful = false, message = exception.Message },
                    statusCode: StatusCodes.Status404NotFound);
            }
            catch (IngestionRunConflictException exception)
            {
                return Results.Json(
                    new { isSuccessful = false, message = exception.Message },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapGet("/{runId}", async (
            string runId,
            IIngestionRunService service,
            CancellationToken cancellationToken) =>
        {
            var run = await service.GetAsync(runId, cancellationToken);
            return run is null ? NotFound(runId) : Results.Ok(run);
        });

        group.MapPost("/{runId}/complete", async (
            string runId,
            CompleteRunRequestDto body,
            IValidator<CompleteRunRequestDto> validator,
            IIngestionRunService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(body, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }

            var run = await service.CompleteAsync(runId, body, cancellationToken);
            return run is null ? NotFound(runId) : Results.Ok(run);
        });

        group.MapPost("/{runId}/fail", async (
            string runId,
            FailRunRequestDto body,
            IValidator<FailRunRequestDto> validator,
            IIngestionRunService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(body, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }

            var run = await service.FailAsync(runId, body, cancellationToken);
            return run is null ? NotFound(runId) : Results.Ok(run);
        });
    }

    private static IResult NotFound(string runId) =>
        Results.Json(
            new { isSuccessful = false, message = $"Ingestion run '{runId}' was not found" },
            statusCode: StatusCodes.Status404NotFound);
}
