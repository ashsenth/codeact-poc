"""Pinned, public configuration for the approved stateless Responses v3 cohorts."""
import json
import os
import subprocess
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from decimal import Decimal


SOL_FILTER = ("productName eq 'Azure OpenAI GPT6' and armRegionName eq 'eastus' "
              "and contains(meterName, '6.1-sol') and contains(skuName, 'Std Gl')")
FOUR_FILTER = "armRegionName eq 'eastus' and contains(meterName, 'gpt 4o 1120')"


def price_url(query):
    return "https://prices.azure.com/api/retail/prices?" + urllib.parse.urlencode({
        "api-version": "2023-01-01-preview", "currencyCode": "'USD'", "$filter": query})


COHORTS = {
    "gpt-4o-v3-enforced": {
        "deployment": "gpt-4o", "model": "gpt-4o", "version": "2024-11-20", "reasoning": "not-supported",
        "rates": {"inputPerMillion": "2.5", "outputPerMillion": "10", "cachedInputPerMillion": "1.25"},
        "meters": {"gpt 4o 1120 Inp glbl Tokens": "2.5", "gpt 4o 1120 Outp glbl Tokens": "10",
                   "gpt 4o 1120 cached Inp glbl Tokens": "1.25"},
        "sourceUrl": price_url(FOUR_FILTER),
    },
    "gpt-6.1-sol-v3": {
        "deployment": "gpt-6.1-sol", "model": "gpt-6.1-sol", "version": "2026-09-29", "reasoning": "medium",
        "rates": {"inputPerMillion": "2", "outputPerMillion": "10", "cachedInputPerMillion": ".1",
                  "cacheWritePerMillion": "2.5", "longContextThreshold": "272000",
                  "longInputPerMillion": "4", "longOutputPerMillion": "15",
                  "longCachedInputPerMillion": ".2", "longCacheWritePerMillion": "5"},
        "meters": {"6.1-sol ShortCo Inp Std Gl 1M Tokens": "2", "6.1-sol ShortCo Opt Std Gl 1M Tokens": "10",
                   "6.1-sol ShortCo Cd Inp Std Gl 1M Tokens": ".1", "6.1-sol ShortCo Cd Wr Std Gl 1M Tokens": "2.5",
                   "6.1-sol LongCo Inp Std Gl 1M Tokens": "4", "6.1-sol LongCo Opt Std Gl 1M Tokens": "15",
                   "6.1-sol LongCo Cd Inp Std Gl 1M Tokens": ".2", "6.1-sol LongCo Cd Wr Std Gl 1M Tokens": "5"},
        "sourceUrl": price_url(SOL_FILTER),
    },
}

RATE_ENV = {
    "inputPerMillion": "PRICING_INPUT_PER_MILLION", "outputPerMillion": "PRICING_OUTPUT_PER_MILLION",
    "cachedInputPerMillion": "PRICING_CACHED_INPUT_PER_MILLION", "cacheWritePerMillion": "PRICING_CACHE_WRITE_PER_MILLION",
    "longContextThreshold": "PRICING_LONG_CONTEXT_THRESHOLD", "longInputPerMillion": "PRICING_LONG_INPUT_PER_MILLION",
    "longOutputPerMillion": "PRICING_LONG_OUTPUT_PER_MILLION", "longCachedInputPerMillion": "PRICING_LONG_CACHED_INPUT_PER_MILLION",
    "longCacheWritePerMillion": "PRICING_LONG_CACHE_WRITE_PER_MILLION",
}


def deployment_metadata(spec):
    resource = os.environ.get("AZURE_OPENAI_RESOURCE_ID")
    if not resource or not resource.startswith("/subscriptions/"):
        raise ValueError("AZURE_OPENAI_RESOURCE_ID must identify the Azure OpenAI account.")
    base = "https://management.azure.com" + resource
    account = json.loads(subprocess.check_output(
        ["az", "rest", "--url", base + "?api-version=2024-10-01", "--only-show-errors"], text=True))
    if account["location"].lower() != "eastus":
        raise ValueError("Pricing is verified only for the East US account.")
    metadata = json.loads(subprocess.check_output(
        ["az", "rest", "--url", base + "/deployments/" + spec["deployment"] + "?api-version=2024-10-01",
         "--only-show-errors"], text=True))
    model = metadata["properties"]["model"]
    if (model["name"], model["version"], metadata["sku"]["name"]) != (spec["model"], spec["version"], "GlobalStandard"):
        raise ValueError("Deployment model/version/SKU differs from the pinned cohort.")
    if spec["model"] == "gpt-6.1-sol" and metadata["properties"]["versionUpgradeOption"] != "NoAutoUpgrade":
        raise ValueError("gpt-6.1-sol must retain NoAutoUpgrade.")
    return {"location": account["location"], "deployment": spec["deployment"], "model": model,
            "sku": metadata["sku"], "rateLimits": metadata["properties"]["rateLimits"],
            "versionUpgradeOption": metadata["properties"]["versionUpgradeOption"]}


def verify(cohort):
    spec = COHORTS[cohort]
    metadata = deployment_metadata(spec)
    items, url = [], spec["sourceUrl"]
    while url:
        with urllib.request.urlopen(url, timeout=90) as response:
            page = json.load(response, parse_float=Decimal)
        items.extend(page["Items"])
        url = page.get("NextPageLink")
    selected = []
    for name, rate in spec["meters"].items():
        candidates = [item for item in items if item["meterName"] == name and item["type"] == "Consumption"
                      and item["currencyCode"] == "USD" and item["tierMinimumUnits"] == 0]
        if len(candidates) != 1:
            raise ValueError(f"No unique Azure Retail meter for {name}: {len(candidates)}; stop, no model runs.")
        item = candidates[0]
        factor = {"1K": Decimal(1000), "1M": Decimal(1)}[item["unitOfMeasure"]]
        if Decimal(item["retailPrice"]) * factor != Decimal(rate):
            raise ValueError(f"Retail rate changed for {name}; stop, no model runs.")
        selected.append({key: str(value) if isinstance(value, Decimal) else value for key, value in item.items()})
    return {"cohort": cohort, "verifiedAtUtc": datetime.now(timezone.utc).isoformat(),
            "sourceUrl": spec["sourceUrl"], "deployment": metadata, "meters": selected,
            "defaultReasoningEffort": spec["reasoning"]}


def configure_environment(env, cohort):
    spec = COHORTS[cohort]
    for key in RATE_ENV.values():
        env.pop(key, None)
    for key, value in spec["rates"].items():
        env[RATE_ENV[key]] = value
    env.update({
        "AZURE_OPENAI_DEPLOYMENT_NAME": spec["deployment"], "AZURE_OPENAI_MODEL_NAME": spec["model"],
        "AZURE_OPENAI_MODEL_VERSION": spec["version"], "MODEL_API_MODE": "responses",
        "MODEL_DEFAULT_REASONING_EFFORT": spec["reasoning"], "PRICING_SOURCE_URL": spec["sourceUrl"],
        "PRICING_VERIFIED_ON": datetime.now(timezone.utc).date().isoformat(),
        "PRICING_BASIS": "Azure Retail Prices API; USD; East US; GlobalStandard; per-request cache/context rates; reasoning included in output",
    })
