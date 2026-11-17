"""Hand-authored no-LLM feasibility control; inputs come exclusively from call_tool."""
import json


def number(value):
    return float(value)


def ratio(value, total):
    return round(value / total if total else 0.0, 10)


def money(value, currency):
    return round(value, 0 if currency == "JPY" else 2)


prices = {}
rates = {}
households = call_tool("list_households")
selected = [h for h in households if not CONTROL_SCOPE or h["id"] in CONTROL_SCOPE]
if CONTROL_SCOPE and {h["id"] for h in selected} != set(CONTROL_SCOPE):
    raise ValueError("Requested household not returned by list_households")
reports = []
for household in selected:
    household_id = household["id"]
    base = household["baseCurrency"]
    policy = call_tool("get_policy", householdId=household_id)
    class_values = {}
    symbol_values = {}
    total = 0.0
    foreign = 0.0
    for account in call_tool("list_accounts", householdId=household_id):
        for position in call_tool("list_positions", accountId=account["id"]):
            symbol = position["symbol"]
            if symbol not in prices:
                prices[symbol] = call_tool("get_price", symbol=symbol)
            quote = prices[symbol]
            source = quote["currency"]
            key = (source, base)
            if key not in rates:
                rates[key] = number(call_tool(
                    "get_fx", fromCurrency=source, toCurrency=base
                )["rate"])
            value = number(position["quantity"]) * number(quote["price"]) * rates[key]
            total += value
            asset_class = position["assetClass"]
            class_values[asset_class] = class_values.get(asset_class, 0.0) + value
            symbol_values[symbol] = symbol_values.get(symbol, 0.0) + value
            if source != base:
                foreign += value

    drifts = []
    breaches = []
    trades = []
    tolerance = number(policy["driftToleranceFraction"])
    for target in policy["targets"]:
        asset_class = target["assetClass"]
        value = class_values.get(asset_class, 0.0)
        target_fraction = number(target["targetFraction"])
        actual = ratio(value, total)
        drift = round(actual - target_fraction, 10)
        breached = abs(drift) > tolerance
        drifts.append({
            "assetClass": asset_class, "actualFraction": actual,
            "targetFraction": target_fraction, "driftFraction": drift, "breached": breached,
        })
        if breached:
            breaches.append({
                "kind": "AllocationDrift",
                "detail": asset_class + " drift",
                "observed": abs(drift), "limit": tolerance,
            })
        if total > 0:
            delta = total * target_fraction - value
            trades.append({
                "assetClass": asset_class, "direction": "Buy" if delta >= 0 else "Sell",
                "notional": money(abs(delta), base), "currency": base,
            })

    concentration_limit = number(policy["maxConcentrationFraction"])
    for symbol in sorted(symbol_values):
        concentration = ratio(symbol_values[symbol], total)
        if concentration > concentration_limit:
            breaches.append({
                "kind": "Concentration", "detail": symbol + " concentration",
                "observed": concentration, "limit": concentration_limit,
            })
    for kind, value, limit, detail in [
        ("FxExposure", foreign, policy["maxFxExposureFraction"],
         "FX exposure"),
        ("CryptoExposure", class_values.get("Crypto", 0.0), policy["maxCryptoExposureFraction"],
         "Crypto exposure"),
    ]:
        exposure = ratio(value, total)
        if exposure > number(limit):
            breaches.append({
                "kind": kind, "detail": detail, "observed": exposure, "limit": number(limit),
            })
    reports.append({
        "householdId": household_id, "baseCurrency": base, "totalValue": money(total, base),
        "flagged": bool(breaches), "drifts": drifts, "breaches": breaches, "proposedTrades": trades,
    })

print(json.dumps({
    "runId": CONTROL_RUN_ID, "generatedAt": CONTROL_GENERATED_AT,
    "status": "complete", "missingItems": [], "households": reports,
}, separators=(",", ":"), allow_nan=False))
