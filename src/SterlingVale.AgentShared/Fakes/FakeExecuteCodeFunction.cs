using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace SterlingVale.AgentShared.Fakes;

/// <summary>
/// An offline stand-in for the Hyperlight <c>execute_code</c> tool, used when no Hyperlight guest is
/// available so the CodeAct API can run end-to-end with the fake model. It executes nothing and
/// returns a benign success result; the deterministic fake model supplies the actual answer. Never
/// used when a real Hyperlight provider is wired.
/// </summary>
public static class FakeExecuteCodeFunction
{
    /// <summary>Creates the offline <c>execute_code</c> AIFunction.</summary>
    public static AIFunction Create() => AIFunctionFactory.Create(Run, "execute_code");

    [Description("Executes a bounded program in an isolated sandbox and returns its output.")]
    private static ExecuteCodeResult Run(
        [Description("The program source to execute.")] string code) =>
        new(Stdout: string.Empty, Stderr: string.Empty, ExitCode: 0, Success: true);

    private sealed record ExecuteCodeResult(string Stdout, string Stderr, int ExitCode, bool Success);
}
