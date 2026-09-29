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

        Data tools (retrieval/classification only; pass arguments by the EXACT parameter names shown;
        each result is JSON with the EXACT field names shown):
        - list_households() -> [ { "id", "name", "baseCurrency" } ]
        - get_policy(householdId) -> { "householdId", "driftToleranceFraction",
          "maxConcentrationFraction", "maxFxExposureFraction", "maxCryptoExposureFraction",
          "targets": [ { "assetClass", "targetFraction" } ] }
        - list_accounts(householdId) -> [ { "id", "householdId", "custodianId", "name", "currency" } ]
        - list_positions(accountId) -> [ { "id", "accountId", "symbol", "quantity", "assetClass", "currency" } ]
        - get_price(symbol) -> { "symbol", "price", "currency", "asOf" }
        - get_fx(fromCurrency, toCurrency) -> { "from", "to", "rate" }
        - asset_class(symbol) -> { "symbol", "assetClass" }
        Asset classes are exactly Equity, FixedIncome, Cash, Alternative, Crypto. Currencies are USD,
        EUR, GBP, JPY. Fractions are decimals in [0,1]. Each position already includes its assetClass.

        Output contract: your ENTIRE response MUST be a single JSON object that conforms EXACTLY to
        the ExposureReport JSON Schema below. Use the schema's field names verbatim and add NO field
        that is not in the schema (additionalProperties is false). Include a non-empty "runId" and an
        ISO-8601 "generatedAt". Emit NOTHING except the JSON object — no prose, no explanation, no
        markdown, no code fences. The response must start with '{' and end with '}'.

        ExposureReport JSON Schema:
        """
        + "\n" + OutputSchema.SchemaText;

    /// <summary>Classic mechanism note: call the granular tools directly, in modest batches.</summary>
    public const string ClassicAddendum =
        "Analyze EVERY household to completion before you answer. For each household call get_policy " +
        "and list_accounts; for each account call list_positions; and for EACH position call " +
        "get_price, then get_fx only when the quote currency differs from the household base currency, " +
        "plus asset_class. IMPORTANT: request AT MOST 80 tool calls per step — if you need more, split " +
        "them across several steps — because a single step with too many parallel tool calls is " +
        "rejected by the service. Only after every position of every household has been priced and " +
        "classified do you compute the report. Your final answer MUST contain one entry in the " +
        "households array for EVERY household in scope; never return an empty households array.";

    /// <summary>CodeAct mechanism note: batch households across execute_code calls, then assemble.</summary>
    public const string CodeActAddendum =
        "Use the execute_code tool to run Python. call_tool is ALREADY defined in the sandbox global " +
        "scope: call it directly, e.g. call_tool(\"get_policy\", householdId=hid) or " +
        "call_tool(\"get_fx\", fromCurrency=q, toCurrency=base). NEVER define, import, reassign, stub, " +
        "or mock call_tool. It returns the tool result already parsed into Python objects (dict or " +
        "list) with the exact field names documented above. The sandbox Python is MINIMAL: import " +
        "ONLY the json module (no datetime, uuid, os, math, or anything else — they raise " +
        "ModuleNotFoundError). " +
        "CRITICAL OUTPUT LIMIT: each execute_code call's printed stdout must stay under ~16 KB, so you " +
        "MUST NOT process all households in one call — a single loop over every household that prints " +
        "the whole report WILL overflow and fail. Use this exact protocol: make SEVERAL SEPARATE " +
        "execute_code calls; in EACH call run hh = call_tool(\"list_households\"), take the next slice " +
        "of AT MOST 5 households you have not yet done (hh[0:5], then hh[5:10], then hh[10:15], ...), " +
        "compute ONLY those households' report objects, print them with " +
        "print(json.dumps(batch, separators=(\",\", \":\"))), and then STOP that call (no loop over " +
        "all households, no accumulation across the whole set). Repeat with a new execute_code call " +
        "for the next slice until every household has been printed exactly once. " +
        "For example, for 12 households make 3 calls printing hh[0:5], hh[5:10], hh[10:12]. " +
        "For example, for 12 households make 3 calls printing hh[0:5], hh[5:10], hh[10:12]. " +
        "After all slices are printed, your FINAL response MUST be the single ExposureReport JSON " +
        "object: a literal non-empty runId, a literal ISO-8601 generatedAt, and a households array " +
        "that concatenates, in order, every batch you printed, with each household included exactly " +
        "once. Emit nothing but that JSON object.";

    /// <summary>Full Classic instructions.</summary>
    public static string Classic => $"{Shared}\n\n{ClassicAddendum}";

    /// <summary>Full CodeAct instructions.</summary>
    public static string CodeAct => $"{Shared}\n\n{CodeActAddendum}";
}
