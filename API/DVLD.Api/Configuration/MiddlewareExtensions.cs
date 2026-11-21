using DVLD.Api.Results;

using Microsoft.AspNetCore.WebUtilities;

namespace DVLD.Api.Configuration;

public static class MiddlewareExtensions
{
    public static WebApplication UseDvldPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseDvldRequestLogging();

        app.UseExceptionHandler();

        // Responses produced outside MVC (401 challenge, 403 forbidden, unmatched-route 404,
        // rate-limiter 429) carry no body of their own. Render them with the same RFC 7807
        // envelope so clients never receive an empty or differently shaped failure.
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;

            if (httpContext.Response.HasStarted ||
                httpContext.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            var statusCode = httpContext.Response.StatusCode;

            var problemDetails = ApiProblemDetails.Create(
                httpContext,
                statusCode,
                ReasonPhrases.GetReasonPhrase(statusCode),
                ApiProblemDetails.DefaultDetailForStatusCode(statusCode));

            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: ApiProblemDetails.ContentType,
                cancellationToken: httpContext.RequestAborted);
        });

        app.UseHttpsRedirection();

        // Serve the React production bundle from the same origin as the API.
        // The Vite development server and its cookie-rewriting proxy are not used in production.
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseCors("ReactApp");

        app.UseRateLimiter();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();

        // Client-side routes (for example /people or /applications/123) must load the SPA.
        // Unknown /api routes must remain API 404 responses and must never receive index.html.
        app.MapFallback(
            "{*path:nonfile}",
            async context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var webRootPath = app.Environment.WebRootPath
                    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
                var indexPath = Path.Combine(webRootPath, "index.html");

                if (!File.Exists(indexPath))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(
                    indexPath,
                    context.RequestAborted);
            });

        return app;
    }
}
