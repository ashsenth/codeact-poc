using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SterlingVale.Api.Hosting;

/// <summary>
/// Shared API host setup used identically by both the Classic and CodeAct composition roots, so the
/// HTTP contract, serialization, and error handling cannot diverge between modes.
/// </summary>
public static class ApiSetup
{
    /// <summary>Registers problem details and canonical JSON serialization.</summary>
    public static IServiceCollection AddSterlingValeApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        return services;
    }

    /// <summary>Adds the shared middleware (RFC 7807 problem details for failures).</summary>
    public static WebApplication UseSterlingValeApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
