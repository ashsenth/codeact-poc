using Microsoft.Extensions.AI;
using SterlingVale.Tools;
using Xunit;

namespace SterlingVale.Tools.Tests;

public sealed class ToolCatalogTests
{
    private readonly ToolCatalog _catalog = new();

    [Fact]
    public void Catalog_creates_exactly_seven_tools()
    {
        var tools = _catalog.CreateTools(TestSnapshot.Service());
        Assert.Equal(7, tools.Count);
    }

    [Fact]
    public void Catalog_tool_names_match_canonical_names()
    {
        var tools = _catalog.CreateTools(TestSnapshot.Service());
        var actual = tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var expected = ToolNames.All.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_tool_has_a_non_empty_description()
    {
        var tools = _catalog.CreateTools(TestSnapshot.Service());
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
    }

    [Fact]
    public void Fingerprint_is_stable_across_independent_builds()
    {
        // Simulates Classic and CodeAct independently building tools from the same catalog:
        // the fingerprint must be identical (the core fairness guarantee).
        var classicTools = _catalog.CreateTools(TestSnapshot.Service());
        var codeActTools = _catalog.CreateTools(TestSnapshot.Service());

        var classicPrint = ToolCatalog.ComputeFingerprint(classicTools);
        var codeActPrint = ToolCatalog.ComputeFingerprint(codeActTools);

        Assert.Equal(classicPrint, codeActPrint);
    }

    [Fact]
    public void Tools_expose_json_schemas()
    {
        var tools = _catalog.CreateTools(TestSnapshot.Service());
        Assert.All(tools, t => Assert.NotEqual(default, t.JsonSchema));
    }
}
