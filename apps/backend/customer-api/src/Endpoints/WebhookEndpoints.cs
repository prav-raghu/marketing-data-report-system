using CustomerApi.Auth;
using CustomerApi.Services;
using CustomerApi.Validators;
using FluentValidation;
using DotNetMonoRepoTemplate.Types;

namespace CustomerApi.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/webhooks");

        group.MapPost("/subscriptions", async (
            CreateWebhookSubscriptionDto body,
            IValidator<CreateWebhookSubscriptionDto> validator,
            WebhookSubscriptionService service,
            HttpContext context) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var userId = context.GetCurrentUser()?.Id;
            var result = await service.CreateSubscriptionAsync(body, userId);
            return Results.Json(
                result,
                statusCode: result.IsSuccessful ? StatusCodes.Status201Created : StatusCodes.Status400BadRequest);
        });

        group.MapGet("/subscriptions", async (HttpContext context, WebhookSubscriptionService service, string? active) =>
        {
            var userId = context.GetCurrentUser()!.Id;
            bool? isActive = active switch
            {
                "true" => true,
                "false" => false,
                _ => null,
            };
            var result = await service.ListSubscriptionsAsync(userId, isActive);
            return Results.Ok(result);
        });

        group.MapGet("/subscriptions/{id}", async (string id, HttpContext context, WebhookSubscriptionService service) =>
        {
            var userId = context.GetCurrentUser()!.Id;
            var result = await service.GetSubscriptionAsync(id, userId);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        });

        group.MapPut("/subscriptions/{id}", async (
            string id,
            UpdateWebhookSubscriptionDto body,
            IValidator<UpdateWebhookSubscriptionDto> validator,
            HttpContext context,
            WebhookSubscriptionService service) =>
        {
            var validation = await validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                return validation.ToBadRequest();
            }
            var userId = context.GetCurrentUser()!.Id;
            var result = await service.UpdateSubscriptionAsync(id, userId, body);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        });

        group.MapDelete("/subscriptions/{id}", async (string id, HttpContext context, WebhookSubscriptionService service) =>
        {
            var userId = context.GetCurrentUser()!.Id;
            var result = await service.DeleteSubscriptionAsync(id, userId);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        });

        group.MapGet("/subscriptions/{id}/deliveries", async (
            string id,
            HttpContext context,
            WebhookSubscriptionService service,
            string? limit) =>
        {
            var userId = context.GetCurrentUser()!.Id;
            var take = int.TryParse(limit, out var parsed) ? parsed : 50;
            var result = await service.GetDeliveriesAsync(id, userId, take);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        });

        group.MapPost("/subscriptions/{id}/regenerate-secret", async (string id, HttpContext context, WebhookSubscriptionService service) =>
        {
            var userId = context.GetCurrentUser()!.Id;
            var result = await service.RegenerateSecretAsync(id, userId);
            return Results.Json(result, statusCode: result.IsSuccessful ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        });

        group.MapPost("/retry", async (RetryWebhookDeliveryDto body, WebhookDeliveryService deliveryService) =>
        {
            await deliveryService.RetryFailedDeliveryAsync(body.DeliveryId);
            return Results.Ok(new { isSuccessful = true, message = "Delivery retry initiated" });
        });
    }
}

public sealed record RetryWebhookDeliveryDto
{
    public required string DeliveryId { get; init; }
}
