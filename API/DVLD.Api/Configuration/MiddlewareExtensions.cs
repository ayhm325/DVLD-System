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

        app.UseRouting();

        app.UseCors("ReactApp");

        app.UseRateLimiter();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
