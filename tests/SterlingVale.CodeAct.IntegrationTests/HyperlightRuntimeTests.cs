using Microsoft.Agents.AI.Hyperlight;
using SterlingVale.Telemetry;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.CodeAct.IntegrationTests;

/// <summary>
/// Hyperlight runtime tests. They are SKIPPED with an explicit reason when
/// <c>HYPERLIGHT_PYTHON_GUEST_PATH</c> is not set, and never fake a successful sandbox run. When a
/// guest and hardware virtualization are available, they verify the provider is constructed with the
/// seven provider-owned tools and disposed cleanly.
///
/// <para>The deeper <c>execute_code</c> / <c>call_tool</c> / state-isolation / denied-network E2E
/// follows the official CodeActEndToEndTests pattern and additionally requires a live guest; see
/// docs/sdk-findings.md for the provider invocation API in this SDK version.</para>
/// </summary>
[Trait("Category", "Hyperlight")]
public sealed class HyperlightRuntimeTests
{
    private const string GuestPathVariable = "HYPERLIGHT_PYTHON_GUEST_PATH";

    [SkippableFact]
    [Trait("Category", "Hyperlight")]
    public void Provider_constructs_with_provider_owned_tools_and_disposes()
    {
        string? guestPath = Environment.GetEnvironmentVariable(GuestPathVariable);
        Skip.If(string.IsNullOrWhiteSpace(guestPath), $"{GuestPathVariable} not set; Hyperlight runtime test skipped.");

        var service = new PortfolioToolService(SnapshotHelper.Build(), new ToolMetrics());
        var providerOwnedTools = new ToolCatalog().CreateTools(service).ToList();

        var options = HyperlightCodeActProviderOptions.CreateForWasm(guestPath!);
        options.Tools = providerOwnedTools;

        using var provider = new HyperlightCodeActProvider(options);
        Assert.NotNull(provider);
    }
}
