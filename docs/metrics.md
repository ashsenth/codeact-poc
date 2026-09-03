# Metrics & Oracle Computation Rules

This document defines (1) the exact computation rules of the deterministic correctness **oracle**
(`SterlingVale.Application.Oracle.ExposureOracle`) and (2) the precision, rounding, and tolerance
conventions used throughout the domain. Benchmark/telemetry metric definitions are added in a later
phase.

## Valuation

| Quantity | Rule |
| --- | --- |
| Native value | `quantity × price` (both in the symbol's quote currency) |
| Base value | `native value × FX(quoteCurrency → householdBaseCurrency)` |
| Household total | Σ base value over all positions in all accounts of the household |

## FX conversion

- **Direct:** if a rate `From→To` exists, multiply by it.
- **Inverse:** if only `To→From` exists, divide by that rate.
- **Identity:** `From == To` ⇒ rate `1`.
- **Missing:** if neither direction exists, throw `FxRateNotFoundException` (value is never silently
  dropped). The synthetic datasets guarantee full coverage, so this only occurs on malformed input.
- **Precision:** conversions use full `decimal` precision; **no intermediate rounding**. Rounding to
  a currency's minor units happens only when a `Money` value is materialized.

## Allocation, drift, and risk

| Quantity | Rule |
| --- | --- |
| Allocation (per asset class) | `assetClassBaseValue / householdTotal` |
| Drift | `actualAllocation − targetAllocation` (may be negative) |
| Drift breach | `|drift| > policy.DriftTolerance` |
| Concentration (per symbol) | `symbolBaseValue / householdTotal`; breach when `> policy.MaxConcentration` |
| FX exposure | `nonBaseCurrencyValue / householdTotal`; breach when `> policy.MaxFxExposure` |
| Crypto exposure | `cryptoValue / householdTotal`; breach when `> policy.MaxCryptoExposure` |
| Flagged household | has at least one risk breach (including drift breaches) |

## Rebalancing

- Target value for an asset class = `householdTotal × targetFraction`.
- Proposed notional = `targetValue − currentValue`; positive ⇒ **Buy**, negative ⇒ **Sell**.
- Notionals are asset-class amounts in the household base currency — **never named securities**.
- **Invariant:** because target fractions sum to 1, the signed proposed notionals net to
  approximately zero (within `FinancialMath.NetNotionalTolerance` relative to the household total,
  after money rounding).

## Precision, rounding, tolerance

| Setting | Value | Source |
| --- | --- | --- |
| Money rounding | banker's rounding (`MidpointRounding.ToEven`) to the currency's minor units | `FinancialMath.RoundMoney` |
| Ratio rounding | 10 decimal places | `FinancialMath.RatioDecimals` |
| Default ratio tolerance | `0.0001` (1 bp) | `FinancialMath.DefaultRatioTolerance` |
| Allocation-sum tolerance | `0.0001` | `FinancialMath.AllocationSumTolerance` |
| Net-notional tolerance | `0.0001` | `FinancialMath.NetNotionalTolerance` |

## Zero-value behavior

- When a household total is `0`, `FinancialMath.SafeDivide` yields `0` for every allocation,
  concentration, and exposure (no divide-by-zero).
- No proposed trades are produced for a zero-total household (all deltas are `0`).
- Drift for each class equals `0 − target = −target`, which may register as a drift breach; this is
  an intentional, documented edge case. The synthetic datasets guarantee non-zero totals.
