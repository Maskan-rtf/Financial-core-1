using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Core.API.Swagger;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddCoreSwagger(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Encrypted JWT access token from `POST /api/v1.0/identity/users/verify-otp`. " +
                    "Paste the token only — Swagger adds the `Bearer` prefix automatically."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            options.DocInclusionPredicate((documentName, api) =>
            {
                var path = api.RelativePath ?? string.Empty;
                if (path.StartsWith("elsa/", StringComparison.OrdinalIgnoreCase))
                    return false;

                return string.IsNullOrEmpty(api.GroupName)
                    || string.Equals(api.GroupName, documentName, StringComparison.OrdinalIgnoreCase);
            });
            options.TagActionsBy(api =>
            {
                var controller = api.ActionDescriptor.RouteValues.TryGetValue("controller", out var name)
                    ? name
                    : "Default";
                return [controller ?? "Default"];
            });

            options.OrderActionsBy(api => api.RelativePath);
            options.DocumentFilter<SwaggerTagDescriptionsDocumentFilter>();

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });

        return services;
    }
}
