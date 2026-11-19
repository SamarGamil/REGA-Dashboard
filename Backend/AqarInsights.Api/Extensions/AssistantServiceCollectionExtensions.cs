using AqarInsights.Api.Models.Assistant;
using AqarInsights.Api.Services.Assistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AqarInsights.Api.Extensions;

public static class AssistantServiceCollectionExtensions
{
    // "Talk to Data" LLM engine (OpenAI-compatible; disabled until configured in appsettings).
    public static IServiceCollection AddAssistantServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AssistantSettings>(configuration.GetSection(AssistantSettings.SectionName));
        services.AddScoped<AssistantContextBuilder>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddHttpClient("assistant", c => c.Timeout = TimeSpan.FromSeconds(60));

        return services;
    }
}
