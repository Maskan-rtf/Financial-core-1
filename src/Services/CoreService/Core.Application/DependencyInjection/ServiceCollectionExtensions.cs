using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Services.CoreService.Core.Application.Abstractions;
using Services.CoreService.Core.Application.Mapping;
using Services.CoreService.Core.Application.Services;
using Services.CoreService.Core.Application.Services.Implementations;


namespace Services.CoreService.Core.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICaseNumberGenerator, CaseNumberGenerator>();

        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IDataEntryService, DataEntryService>();
        services.AddScoped<IFinancialWorksheetService, FinancialWorksheetService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddValidatorsFromAssemblyContaining<MapsterConfig>(includeInternalTypes: true);

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(typeof(MapsterConfig).Assembly);
        services.AddSingleton(typeAdapterConfig);

        return services;
    }
}
