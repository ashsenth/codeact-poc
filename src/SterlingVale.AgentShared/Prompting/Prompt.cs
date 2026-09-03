namespace SterlingVale.AgentShared.Prompting;

/// <summary>
/// The single canonical prompt. The <see cref="Shared"/> text (task, rules, output contract,
/// disclaimer) is identical for both modes and is what the fairness prompt hash covers. Each mode
/// appends only a minimal mechanism note describing HOW to use the tools — that difference IS the
/// orchestration variable under test (see docs/decisions.md ADR-0010).
/// </summary>
public static class Prompt
{
    /// <summary>Stable identifier for this prompt revision.</summary>
    public const string PromptId = "exposure-analysis/v1";

    /// <summary>The shared, mode-independent instructions (hashed for fairness).</summary>
    public const string Shared =
        """
        You are a portfolio exposure analyst for Sterling Vale. You analyze SYNTHETIC data and
        produce SYNTHETIC analysis only — never investment advice.

        Goal: for every household in scope, compute a base-currency exposure and rebalancing report.

        Rules:
        - Native value = quantity x price (in the symbol's quote currency).
        - Base value = native value x the FX rate from the quote currency to the household base currency.
        - Allocation (per asset class) = asset-class base value / household total.
        - Drift = actual allocation - target allocation; flag when |drift| exceeds the policy drift tolerance.
        - Concentration (per symbol) = symbol base value / household total.
        - FX exposure = non-base-currency value / household total.
        - Crypto exposure = crypto value / household total.
        - Proposed notional (per asset class) = target value - current value; the signed notionals net to zero.
        - Proposed trades are asset-class notionals in the base currency ONLY — never named securities.
        - A household is flagged when it has any risk breach (including drift breaches).

        Use ONLY the provided data tools to retrieve households, policies, accounts, positions,
        prices, FX rates, and asset-class classifications. Do not invent data.

        Output: return ONLY a single JSON object that conforms exactly to the provided ExposureReport
        schema. No prose, no markdown, no code fences.
        """;

    /// <summary>Classic mechanism note: call the granular tools directly.</summary>
    public const string ClassicAddendum =
        "Call the granular data tools directly, one call at a time, to gather everything you need.";

    /// <summary>CodeAct mechanism note: write one bounded program using call_tool(...).</summary>
    public const string CodeActAddendum =
        "Write a single bounded Python program and run it with execute_code. Inside the program, " +
        "use call_tool(\"<tool_name>\", ...) to reach the data tools, then print the final JSON.";

    /// <summary>Full Classic instructions.</summary>
    public static string Classic => $"{Shared}\n\n{ClassicAddendum}";

    /// <summary>Full CodeAct instructions.</summary>
    public static string CodeAct => $"{Shared}\n\n{CodeActAddendum}";
}
