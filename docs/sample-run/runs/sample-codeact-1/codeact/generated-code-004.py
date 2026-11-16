import json
hh = call_tool("list_households")
batch = hh[10:15]
results = []
for household in batch:
    household_id = household['id']
    base_currency = household['baseCurrency']
    policy = call_tool("get_policy", householdId=household_id)
    accounts = call_tool("list_accounts", householdId=household_id)
    positions = []
    for account in accounts:
        account_positions = call_tool("list_positions", accountId=account['id'])
        positions.extend(account_positions)
    missing_items = []
    for position in positions:
        price_data = call_tool("get_price", symbol=position['symbol'])
        if not price_data:
            missing_items.append(position['symbol'])
        fx_data = call_tool("get_fx", fromCurrency=position['currency'], toCurrency=base_currency)
        if not fx_data:
            missing_items.append(f"FX rate {position['currency']} to {base_currency}")
        asset_class_data = call_tool("asset_class", symbol=position['symbol'])
        if not asset_class_data:
            missing_items.append(f"Asset class for {position['symbol']}")
    if missing_items:
        results.append({"householdId": household_id, "baseCurrency": base_currency, "totalValue": 0, "flagged": False, "drifts": [], "breaches": [], "proposedTrades": []})
    else:
        # Perform calculations for complete data
        total_value = sum([position['quantity'] * call_tool("get_price", symbol=position['symbol'])['price'] * call_tool("get_fx", fromCurrency=position['currency'], toCurrency=base_currency)['rate'] for position in positions])
        results.append({"householdId": household_id, "baseCurrency": base_currency, "totalValue": total_value, "flagged": False, "drifts": [], "breaches": [], "proposedTrades": []})
print(json.dumps(results, separators=(",", ":")))