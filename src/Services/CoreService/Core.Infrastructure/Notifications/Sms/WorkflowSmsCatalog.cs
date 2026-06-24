using System.Text.Json;
using Core.Application.Notifications.Sms;
using Core.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure.Notifications.Sms;

public sealed class WorkflowSmsCatalog : IWorkflowSmsCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly WorkflowSmsConfigRoot _config;

    public WorkflowSmsCatalog(
        IHostEnvironment environment,
        IOptions<WorkflowSmsOptions> options,
        ILogger<WorkflowSmsCatalog> logger)
    {
        var smsOptions = options.Value;
        var configPath = Path.IsPathRooted(smsOptions.ConfigFilePath)
            ? smsOptions.ConfigFilePath
            : Path.Combine(environment.ContentRootPath, smsOptions.ConfigFilePath);

        _config = LoadConfig(configPath, logger);
    }

    public bool IsGloballyEnabled => _config.GlobalEnabled;

    public string Signature => _config.Signature;

    public WorkflowSmsStepConfig? FindStep(CaseModuleType module, int toStatus)
    {
        var moduleKey = module switch
        {
            CaseModuleType.Investment => "investment",
            CaseModuleType.Guarantee => "guarantee",
            CaseModuleType.Loan => "loan",
            _ => null
        };

        if (moduleKey is null || !_config.Modules.TryGetValue(moduleKey, out var moduleConfig))
            return null;

        return moduleConfig.Steps.FirstOrDefault(s => s.Status == toStatus);
    }

    private static WorkflowSmsConfigRoot LoadConfig(string configPath, ILogger logger)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                logger.LogWarning("Workflow SMS config not found at {ConfigPath}; notifications disabled", configPath);
                return new WorkflowSmsConfigRoot { GlobalEnabled = false };
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<WorkflowSmsConfigRoot>(json, JsonOptions);
            if (config is null)
            {
                logger.LogWarning("Workflow SMS config at {ConfigPath} is empty; notifications disabled", configPath);
                return new WorkflowSmsConfigRoot { GlobalEnabled = false };
            }

            logger.LogInformation("Workflow SMS config loaded from {ConfigPath}", configPath);
            return config;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load workflow SMS config from {ConfigPath}; notifications disabled", configPath);
            return new WorkflowSmsConfigRoot { GlobalEnabled = false };
        }
    }
}
