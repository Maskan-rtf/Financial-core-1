using BuildingBlocks.Observability.Correlation;
using BuildingBlocks.Observability.DependencyInjection;
using Core.API.Swagger;
using Core.Domain.Identity;
using Elsa.Extensions;
using Core.Infrastructure.Identity.Http;
using Microsoft.AspNetCore.Authentication;

namespace Core.API.DependencyInjection;

public static class WebApplicationPipelineExtensions
{
    public static WebApplication UseCoreApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UsePlatformObservabilityPipeline();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = OpenApiMetadata.Title;
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.DefaultModelsExpandDepth(2);

            var descriptions = app.DescribeApiVersions();
            foreach (var description in descriptions)
            {
                var url = $"/swagger/{description.GroupName}/swagger.json";
                options.SwaggerEndpoint(url, $"{OpenApiMetadata.Title} ({description.GroupName})");
            }

            options.RoutePrefix = "swagger";
        });

        app.UseCors("CorsPolicy");
        app.UseAuthentication();
        app.UseMiddleware<SessionActivityMiddleware>();
        app.UseAuthorization();

        app.UseElsaStudioApi();
        app.MapControllers();
        app.MapHealthChecks("/health");

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        foreach (var address in app.Urls)
        {
            logger.LogInformation("Application is running on: {Address}", address);
            logger.LogInformation("Swagger UI available at: {Address}/swagger", address);
        }

        return app;
    }

    private static WebApplication UseElsaStudioApi(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue("Elsa:Studio:Enabled", true);
        if (!enabled)
            return app;

        var apiBasePath = app.Configuration.GetValue("Elsa:Studio:ApiBasePath", "/elsa/api")!;
        var requireAuthentication = app.Configuration.GetValue("Elsa:Studio:RequireAuthentication", false);

        if (requireAuthentication)
        {
            app.UseWhen(
                context => context.Request.Path.StartsWithSegments(apiBasePath),
                branch =>
                {
                    branch.Use(async (context, next) =>
                    {
                        if (context.User.Identity?.IsAuthenticated != true)
                        {
                            await context.ChallengeAsync();
                            return;
                        }

                        if (!context.User.IsInRole(UserRoleClaims.Admin))
                        {
                            await context.ForbidAsync();
                            return;
                        }

                        await next(context);
                    });
                });
        }

        app.UseWorkflowsApi(apiBasePath);

        return app;
    }
}
