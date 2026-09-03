namespace SterlingVale.Tools;

/// <summary>
/// Stable, canonical names for the seven granular tools. Both the Classic and CodeAct agents derive
/// their registrations from these names — they are a benchmark contract and must never diverge.
/// </summary>
public static class ToolNames
{
    /// <summary>Lists all households.</summary>
    public const string ListHouseholds = "list_households";

    /// <summary>Gets a household's investment policy.</summary>
    public const string GetPolicy = "get_policy";

    /// <summary>Lists the accounts of a household.</summary>
    public const string ListAccounts = "list_accounts";

    /// <summary>Lists the positions of an account.</summary>
    public const string ListPositions = "list_positions";

    /// <summary>Gets the price quote for a symbol.</summary>
    public const string GetPrice = "get_price";

    /// <summary>Gets the FX rate between two currencies.</summary>
    public const string GetFx = "get_fx";

    /// <summary>Classifies a symbol into an asset class.</summary>
    public const string AssetClass = "asset_class";

    /// <summary>All seven tool names in stable order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        ListHouseholds,
        GetPolicy,
        ListAccounts,
        ListPositions,
        GetPrice,
        GetFx,
        AssetClass,
    ];
}
