# Full-Small correctness diagnosis: pair 1, H0001

This is historical v2 evidence, not the current scorer/runbook. The later shared
v3 prompt and completeness detection now report missing components with unavailable
numeric metrics and `UnearnedCompleteness`; they do not rescore this preserved sample.
See the [canonical methodology](../benchmark-methodology.md#validity-rules) and
[current results](../v3-responses-results.md).

## Finding and first divergence

CodeAct's first returned household has **USD 826,579.16**, which matches the unchanged
oracle and an independent decimal calculation from all 26 positions in three accounts.
It then returns `drifts: []`, `flagged: false`, `breaches: []`, `proposedTrades: []`.

**First computational divergence: allocations/drifts are never calculated or emitted.**
Allocations live in each drift object's `actualFraction`; the first expected Equity
allocation `0.4720894350` is absent, not a different numeric result.
In serialized JSON key order, the first unequal field after the correct total is
`flagged: false` (oracle true). These are two views of the same omitted analysis.

**Cause classification: schema field meaning / semantic completeness failure.**
This sample does not show a valuation error, a percent-versus-fraction threshold error,
or an oracle valuation bug. The generated program retrieves policy but never uses its
targets or thresholds: it explicitly hard-codes false and empty arrays. The structural
schema accepts those values; they do not mean "analysis complete/no breaches."
The model copies this placeholder analysis into its final report and declares `complete`.

| Field | CodeAct H0001 | Unchanged oracle / hand check |
| --- | --- | --- |
| Base currency | USD | USD: match |
| Total value | 826,579.16 | 826,579.16: match |
| Allocations | Absent: empty drifts | Five actual/target pairs below |
| Drift | Absent | Five actual-minus-target values below |
| Flagged | false | **true** |
| Breaches | none | **4**: Cash drift, Crypto drift, FX exposure, Crypto exposure |
| Trades | none | Five asset-class notionals below |

The actual [second Python program](runs/sample-codeact-1/codeact/generated-code-002.py)
calculates `total_value`, then unconditionally constructs these empty analytical fields.
The first program has the same omission and additionally fails at its final print because
it omitted `import json`. Adding that import in program 002 fixes printing, not analysis.
Program 003 repeats the placeholder logic for H0006-H0010; program 004 requests an empty
10:15 slice. Successful execution is not correctness.

## Independent hand verification of the oracle

The table below was calculated independently with Python `Decimal`, reading the generated
dataset, **not** by calling the oracle's valuation, FX or financial-math helpers. It was then
compared with a separate invocation of the unchanged C# oracle. The committed invocation
excerpts expose the same input quantities, quotes, classifications, policy and FX rates.
Use quote currency (matching position currency for these inputs), multiply directly to USD,
sum without intermediate rounding, round money half-to-even to cents and ratios to 10 places.

Example: `314.63 * 132.80 EUR * 1.08 USD/EUR = 45,125.493120 USD`.
JPY-to-USD is `0.0067`, GBP-to-USD `1.27`, EUR-to-USD `1.08`; USD identity is 1.

Position suffixes below share the H0001 prefix. Accounts A01/A02/A03 contain 9/8/9 positions.

| Position / symbol | Class | Quantity | Quote price | Currency | FX to USD | Raw USD value |
| --- | --- | ---: | ---: | --- | ---: | ---: |
| A01-P01 / FI_JP_GOV | FixedIncome | 109.71 | 100.00 | JPY | .0067 | 73.50570000 |
| A01-P02 / EQ_EU_CORE | Equity | 314.63 | 132.80 | EUR | 1.08 | 45125.493120 |
| A01-P03 / EQ_EU_CORE | Equity | 222.54 | 132.80 | EUR | 1.08 | 31917.576960 |
| A01-P04 / EQ_EU_CORE | Equity | 149.49 | 132.80 | EUR | 1.08 | 21440.453760 |
| A01-P05 / CASH_JPY | Cash | 34078.47 | 1 | JPY | .0067 | 228.325749 |
| A01-P06 / ALT_INFRA | Alternative | 246.76 | 42.10 | EUR | 1.08 | 11219.683680 |
| A01-P07 / FI_US_AGG | FixedIncome | 146.17 | 98.30 | USD | 1 | 14368.5110 |
| A01-P08 / EQ_UK_CORE | Equity | 295.44 | 78.55 | GBP | 1.27 | 29472.651240 |
| A01-P09 / CRY_ETH | Crypto | 2.8705 | 3421.75 | USD | 1 | 9822.133375 |
| A02-P01 / ALT_REIT | Alternative | 281.11 | 55.25 | USD | 1 | 15531.3275 |
| A02-P02 / FI_EU_GOV | FixedIncome | 183.86 | 101.20 | EUR | 1.08 | 20095.162560 |
| A02-P03 / FI_UK_GILT | FixedIncome | 296.77 | 95.60 | GBP | 1.27 | 36031.439240 |
| A02-P04 / CRY_BTC | Crypto | 1.9728 | 61234.50 | USD | 1 | 120803.421600 |
| A02-P05 / EQ_US_LARGE | Equity | 453.48 | 187.42 | USD | 1 | 84991.2216 |
| A02-P06 / EQ_EU_CORE | Equity | 44.51 | 132.80 | EUR | 1.08 | 6383.802240 |
| A02-P07 / EQ_EU_CORE | Equity | 259.14 | 132.80 | EUR | 1.08 | 37166.895360 |
| A02-P08 / EQ_US_LARGE | Equity | 281.64 | 187.42 | USD | 1 | 52784.9688 |
| A03-P01 / EQ_JP_CORE | Equity | 143.10 | 2450 | JPY | .0067 | 2348.986500 |
| A03-P02 / CRY_BTC | Crypto | .8183 | 61234.50 | USD | 1 | 50108.191350 |
| A03-P03 / EQ_EU_CORE | Equity | 58.43 | 132.80 | EUR | 1.08 | 8380.264320 |
| A03-P04 / EQ_US_LARGE | Equity | 209.55 | 187.42 | USD | 1 | 39273.8610 |
| A03-P05 / FI_EU_GOV | FixedIncome | 491.44 | 101.20 | EUR | 1.08 | 53712.426240 |
| A03-P06 / FI_UK_GILT | FixedIncome | 473.20 | 95.60 | GBP | 1.27 | 57452.158400 |
| A03-P07 / FI_US_AGG | FixedIncome | 475.63 | 98.30 | USD | 1 | 46754.4290 |
| A03-P08 / CASH_JPY | Cash | 23754.94 | 1 | JPY | .0067 | 159.158098 |
| A03-P09 / EQ_UK_CORE | Equity | 310.08 | 78.55 | GBP | 1.27 | 30933.115680 |

Raw total **826,579.16407200**, money total **826,579.16 USD**.
Allocations use the raw denominator, not the cent-rounded total.
Policy drift tolerance is **0.05**, meaning five percentage points, not 5.

| Class | Raw class USD | Actual fraction | Target | Drift | Breached? | Signed proposed USD |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| Equity | 390219.290580 | .4720894350 | .50 | -.0279105650 | no | +23070.29 Buy |
| FixedIncome | 228487.63214000 | .2764255888 | .30 | -.0235744112 | no | +19486.12 Buy |
| Cash | 387.483847 | .0004687801 | .10 | -.0995312199 | **yes** | +82270.43 Buy |
| Alternative | 26751.011180 | .0323635198 | .07 | -.0376364802 | no | +31109.53 Buy |
| Crypto | 180733.746325 | .2186526762 | .03 | +.1886526762 | **yes** | -155936.37 Sell |

Each trade is `rawTotal * target - rawClassValue`, rounded only at output; signed trades net
to **0.00 USD**. The five rounded actual fractions sum to **0.9999999999**.

| Additional risk | Hand calculation | Policy | Result |
| --- | --- | --- | --- |
| Largest symbol concentration | EQ_US_LARGE: 177050.0514 / 826579.164072 = .2141961219 | maximum .60 | no breach |
| Non-USD exposure | 392141.098847 / 826579.164072 = .4744144492 | maximum .30 | **FX exposure breach** |
| Crypto exposure | 180733.746325 / 826579.164072 = .2186526762 | maximum .10 | **Crypto exposure breach** |

Two drift breaches plus those two exposure breaches yield **four**; household flagged is true.
All five allocation/drift/trade tuples, total and risk values match the
[unchanged oracle output](oracle-h0001.json). No oracle arithmetic bug was found for this household.

## Why Classic returned only one of ten households

The benchmark requested all ten households, batching disabled. Classic called list_households
once, fetched all ten policies and all ten account lists (**33 returned accounts**, captured
without truncation/redaction). It then requested positions for only **ten accounts**:
all accounts of H0001 (3), H0002 (4), H0003 (3); **23 accounts were never queried**.
It fetched 17 distinct price quotes, 3 FX rates and 17 classifications, but did not complete
position retrieval for H0004-H0010.

The model then voluntarily stopped at **turn 8**, **23.525 seconds**, finish `stop`, with only
**282 final-response output tokens**. Limits were 20 turns, 120 seconds and 16,384 output
tokens. All 68 tool invocations succeeded; their captures were untruncated/unredacted.
The final model response itself contains only H0001, invented total **1,000,000 USD**, targets
copied as actual allocations, zero drift and arbitrary Buy 5,000 / Sell 3,000 trades. The
application did not drop nine output households or truncate a larger answer.

Immediate cause: **premature model completion / scope and semantic noncompliance**. This is
not evidence of a harness slice, worker timeout, turn/output cap, auth failure or forced
tool-call rejection. The captured behavior does not establish a model-internal reason or a
general capacity limit.

## Scoring caveat, separate from the model's first divergence

The scorer's allocation MAE loops only over emitted matching drifts; its trade MAE loops
only over emitted trades. No pairs means zero by current implementation. Thus CodeAct's
empty arrays produce zero numeric MAE, **not evidence of correct calculations**. Flag/breach
recall correctly forces Partial here. This is a **coverage weakness in scoring**, not an
oracle valuation bug. Existing scores and oracle behavior are preserved.

## Proposed fixes: not applied, approval required

1. **Shared prompt only, new hash cohort:** explicitly require five allocation/drift entries
   and five asset-class trades for every positive-value household; compute policy comparisons
   before deriving flags/breaches, and never represent unperformed analysis as empty/no breach.
   Require checking the final ID set against all scoped households and enumerated account
   coverage; if work cannot be completed, declare incomplete with the missing calculations.
   State fractions/strict `>` comparisons explicitly. These reinforce existing intent, but
   prompting alone is not a guaranteed fix. Apply any approved wording to **Shared**, identically
   for both modes, never just the CodeAct addendum, and start a new prompt-hash cohort.
2. **Shared semantic completeness validation:** reject a declared-complete answer that omits
   scoped household IDs or required analytical components; record it as a model task outcome,
   not infrastructure invalidity. Do not repair/fill values or increase limits.
3. **Scoring coverage:** separately report missing households/classes/trades and make missing
   numeric comparisons unavailable or penalized rather than zero-error. Revalidate classification
   against omitted-field controls before any approved scoring/oracle change.
4. Re-run the scoped/full-Small experiment only after approval of the chosen fixes; keep this
   cohort intact. Medium/Large remain unrun and require separate approval.
