namespace SterlingVale.Domain.ValueObjects;

/// <summary>Base helper for validated string-backed identifiers.</summary>
internal static class IdGuard
{
    public static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} must be a non-empty identifier.", name)
            : value.Trim();
}

/// <summary>Strongly-typed household identifier.</summary>
public readonly record struct HouseholdId(string Value)
{
    /// <summary>Creates a validated household id.</summary>
    public static HouseholdId Create(string value) => new(IdGuard.Require(value, nameof(HouseholdId)));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly-typed account identifier.</summary>
public readonly record struct AccountId(string Value)
{
    /// <summary>Creates a validated account id.</summary>
    public static AccountId Create(string value) => new(IdGuard.Require(value, nameof(AccountId)));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly-typed position identifier.</summary>
public readonly record struct PositionId(string Value)
{
    /// <summary>Creates a validated position id.</summary>
    public static PositionId Create(string value) => new(IdGuard.Require(value, nameof(PositionId)));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly-typed custodian identifier.</summary>
public readonly record struct CustodianId(string Value)
{
    /// <summary>Creates a validated custodian id.</summary>
    public static CustodianId Create(string value) => new(IdGuard.Require(value, nameof(CustodianId)));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// A synthetic instrument symbol. Symbols are uppercase, non-empty tokens. Note: proposed trades
/// are always asset-class notionals, never named symbols — symbols exist only for pricing lookups.
/// </summary>
public readonly record struct Symbol
{
    private Symbol(string value) => Value = value;

    /// <summary>The uppercase symbol token.</summary>
    public string Value { get; }

    /// <summary>Creates a validated, uppercased symbol.</summary>
    public static Symbol Create(string value) =>
        new(IdGuard.Require(value, nameof(Symbol)).ToUpperInvariant());

    /// <inheritdoc />
    public override string ToString() => Value;
}
