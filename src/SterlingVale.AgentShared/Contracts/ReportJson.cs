using System.Text.Json;
using System.Text.Json.Serialization;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Domain.Model;
using SterlingVale.Domain.ValueObjects;

namespace SterlingVale.AgentShared.Contracts;

/// <summary>Canonical serialization for the exposure report wire contract (used by both modes).</summary>
public static class ReportJson
{
    /// <summary>Stable JSON options: camelCase, string enums, no indentation for hashing stability.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Indented variant for human-readable artifacts.</summary>
    public static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);

    /// <summary>Serializes a domain report to its canonical wire JSON.</summary>
    public static string Serialize(ExposureReport report, bool indented = false) =>
        JsonSerializer.Serialize(ToDto(report), indented ? IndentedOptions : Options);

    /// <summary>Attempts to parse wire JSON into a report DTO. Returns null on malformed JSON.</summary>
    public static ExposureReportDto? TryParse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ExposureReportDto>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Maps a domain report to its wire DTO.</summary>
    public static ExposureReportDto ToDto(ExposureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var households = report.Households.Select(h => new HouseholdExposureDto(
            h.HouseholdId.Value,
            h.TotalValue.Currency.Code,
            h.TotalValue.Amount,
            h.Flagged,
            h.Drifts.Select(d => new AllocationDriftDto(
                d.AssetClass.ToString(),
                d.Actual.Fraction,
                d.Target.Fraction,
                d.Drift.Fraction,
                d.Breached)).ToList(),
            h.Breaches.Select(b => new RiskBreachDto(
                b.Kind.ToString(),
                b.Detail,
                b.Observed,
                b.Limit)).ToList(),
            h.ProposedTrades.Select(t => new ProposedTradeDto(
                t.AssetClass.ToString(),
                t.Direction.ToString(),
                t.Notional.Amount,
                t.Notional.Currency.Code)).ToList())).ToList();

        return new ExposureReportDto(report.RunId, report.GeneratedAt.ToString("O"), households);
    }

    /// <summary>Maps a wire DTO back to a domain report. Throws on malformed enum/currency values.</summary>
    public static ExposureReport ToDomain(ExposureReportDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var households = dto.Households.Select(h =>
        {
            var baseCcy = Currency.Parse(h.BaseCurrency);
            var drifts = h.Drifts.Select(d => new AllocationDrift(
                AssetClasses.Parse(d.AssetClass),
                Percentage.FromFraction(d.ActualFraction),
                Percentage.FromFraction(d.TargetFraction),
                Percentage.FromFraction(d.DriftFraction),
                d.Breached)).ToList();
            var breaches = h.Breaches.Select(b => new RiskBreach(
                Enum.Parse<RiskBreachKind>(b.Kind, ignoreCase: true),
                b.Detail,
                b.Observed,
                b.Limit)).ToList();
            var trades = h.ProposedTrades.Select(t => new ProposedTrade(
                AssetClasses.Parse(t.AssetClass),
                Enum.Parse<TradeDirection>(t.Direction, ignoreCase: true),
                new Money(t.Notional, Currency.Parse(t.Currency)))).ToList();
            return new HouseholdExposure(
                HouseholdId.Create(h.HouseholdId),
                new Money(h.TotalValue, baseCcy),
                drifts,
                breaches,
                trades,
                h.Flagged);
        }).ToList();

        return new ExposureReport(
            dto.RunId,
            DateTimeOffset.Parse(dto.GeneratedAt, System.Globalization.CultureInfo.InvariantCulture),
            households);
    }

    private static JsonSerializerOptions CreateOptions(bool indented = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
