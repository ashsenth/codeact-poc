using SterlingVale.AgentShared.Agents;
using SterlingVale.AgentShared.Analysis;
using SterlingVale.Api.Hosting;
using SterlingVale.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSterlingValeApi();
builder.Services.AddSterlingValeInfrastructure(builder.Configuration);

// The ONLY mode-specific registration: Classic direct-tool orchestration.
builder.Services.AddSingleton<IAgentFactory, ClassicAgentFactory>();

var app = builder.Build();
app.UseSterlingValeApi();
app.MapAnalysisEndpoints();
app.Run();

/// <summary>Exposes the Classic API entry point to the integration test host.</summary>
public partial class Program;
