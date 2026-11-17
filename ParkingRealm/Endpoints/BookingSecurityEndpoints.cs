using Microsoft.AspNetCore.Antiforgery;

namespace ParkingRealm.Endpoints;

public static class BookingSecurityEndpoints
{
    public static void MapBookingSecurityEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { RequestToken = antiforgery.GetAndStoreTokens(context).RequestToken });
        }).RequireAuthorization();
}

public class BookingAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(title: "Invalid request verification token.", statusCode: StatusCodes.Status400BadRequest);
        }
        return await next(context);
    }
}
