using SterlingVale.AgentShared.Analysis;
using SterlingVale.Api.Hosting;
using SterlingVale.CodeAct;
using SterlingVale.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSterlingValeApi();
builder.Services.AddSterlingValeInfrastructure(builder.Configuration);

// The ONLY mode-specific registration: CodeAct orchestration via HyperlightCodeActProvider.
builder.Services.AddSingleton<IAgentFactory, CodeActAgentFactory>();

var app = builder.Build();
app.UseSterlingValeApi();
app.MapAnalysisEndpoints();
app.Run();

/// <summary>Exposes the CodeAct API entry point to the integration test host.</summary>
public partial class Program;
