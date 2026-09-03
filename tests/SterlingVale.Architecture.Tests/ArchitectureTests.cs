using System.Reflection;
using NetArchTest.Rules;
using SterlingVale.AgentShared.Agents;
using SterlingVale.Application.Oracle;
using SterlingVale.CodeAct;
using SterlingVale.Domain;
using SterlingVale.Infrastructure.Data;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Architecture.Tests;

/// <summary>
/// Enforces the dependency-direction rules: Domain and Application stay pure, the oracle is never a
/// tool, and the two orchestration factories never reference each other.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(FinancialMath).Assembly;
    private static readonly Assembly Application = typeof(ExposureOracle).Assembly;
    private static readonly Assembly Tools = typeof(ToolCatalog).Assembly;
    private static readonly Assembly AgentShared = typeof(ClassicAgentFactory).Assembly;
    private static readonly Assembly CodeActLib = typeof(CodeActAgentFactory).Assembly;
    private static readonly Assembly Infrastructure = typeof(DatasetLoader).Assembly;

    private static readonly string[] ForbiddenInDomainAndApplication =
    [
        "Microsoft.Extensions.AI",
        "Microsoft.Agents",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection",
        "Azure",
        "SterlingVale.Infrastructure",
        "SterlingVale.AgentShared",
        "SterlingVale.Tools",
    ];

    [Fact]
    public void Domain_has_no_infrastructure_or_framework_dependencies()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny([.. ForbiddenInDomainAndApplication, "SterlingVale.Application"])
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenInDomainAndApplication)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Tools_never_depend_on_the_oracle()
    {
        // The correctness oracle must never be reachable as a tool.
        var result = Types.InAssembly(Tools)
            .ShouldNot()
            .HaveDependencyOn(typeof(ExposureOracle).FullName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_oracle()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(typeof(ExposureOracle).FullName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void CodeAct_factory_does_not_reference_the_classic_factory()
    {
        var result = Types.InAssembly(CodeActLib)
            .ShouldNot()
            .HaveDependencyOn(typeof(ClassicAgentFactory).FullName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void AgentShared_does_not_reference_the_codeact_factory()
    {
        var result = Types.InAssembly(AgentShared)
            .ShouldNot()
            .HaveDependencyOn(typeof(CodeActAgentFactory).FullName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
