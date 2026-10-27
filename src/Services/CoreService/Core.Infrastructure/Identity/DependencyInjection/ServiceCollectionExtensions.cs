using FluentValidation;
using FluentValidation.AspNetCore;
using Core.Application.Abstractions;
using Core.Application.Identity.Abstractions;
using Core.Application.Identity.Authorization;
using Core.Application.Identity.Common.Interfaces;
using Core.Application.Identity.Common.Options;
using Core.Application.Identity.DTOs.User;
using Core.Application.Identity.Interfaces;
using Core.Application.Identity.Notifications;
using Core.Application.Identity.Services;
using Core.Application.Identity.Services.Authorization;
using Core.Application.Identity.Services.Notifications;
using Core.Application.Identity.Validators;
using Core.Infrastructure.Identity.Http;
using Core.Infrastructure.Identity.Identity;
using Core.Infrastructure.Identity.Identity.TokenHandler;
using Core.Infrastructure.Identity.Services;
using Core.Infrastructure.Identity.Services.Authorization;
using Core.Infrastructure.Identity.Services.Captcha;
using Core.Infrastructure.Identity.Services.Hasher;
using Core.Infrastructure.Identity.Services.Otp;
using Core.Infrastructure.Identity.Services.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure.Identity.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityApplication(this WebApplicationBuilder builder)
    {
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddFluentValidationClientsideAdapters();
        builder.Services.AddMemoryCache();
        RegisterDistributedCaching(builder);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();
        builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();

        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddSingleton<ICaptchaService, CaptchaService>();
        builder.Services.AddScoped<ISmsService, SmsService>();
        builder.Services.AddHttpClient();

        builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
        builder.Services.AddScoped<ISmsNotificationService, SmsNotificationService>();
        builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<ISmsNotificationService>());

        builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
        builder.Services.AddScoped<IOtpCacheService, RedisOtpCacheService>();

        builder.Services.Configure<AuthSessionOptions>(builder.Configuration.GetSection("Session"));
        builder.Services.AddScoped<ISessionCacheService, RedisSessionCacheService>();

        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
        builder.Services.AddScoped<IPermissionCacheService, DistributedPermissionCacheService>();
        builder.Services.AddScoped<IIdentityClient, LocalIdentityClient>();

        builder.Services.AddTransient<IValidator<CreateUserDto>, CreateUserDtoValidator>();
        builder.Services.AddTransient<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();
        builder.Services.AddTransient<IValidator<SendOtpDto>, SendOtpDtoValidator>();
        builder.Services.AddTransient<IValidator<VerifyOtpDto>, VerifyOtpDtoValidator>();
        builder.Services.AddValidatorsFromAssembly(typeof(UserService).Assembly, ServiceLifetime.Transient);

        return builder.Services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IHasher, HasherService>();
        builder.Services.AddScoped<Core.Application.Identity.Abstractions.ITokenHelper, TokenHelper>();
        builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        return builder.Services;
    }

    private static void RegisterDistributedCaching(WebApplicationBuilder builder)
    {
        var redisConnection = builder.Configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "InvestmentCaseManagement.Identity";
            });
            return;
        }

        builder.Services.AddDistributedMemoryCache();
        builder.Services.TryAddSingleton<IConfigureOptions<RedisCacheOptions>, NoopRedisCacheOptionsSetup>();
    }
}

internal sealed class NoopRedisCacheOptionsSetup : IConfigureOptions<RedisCacheOptions>
{
    public void Configure(RedisCacheOptions options)
    {
    }
}
