namespace SterlingVale.Application.Oracle;

/// <summary>Pure valuation primitives shared by the oracle. Kept separate for focused unit testing.</summary>
public static class Valuation
{
    /// <summary>Native (pre-FX) value of a holding: <c>quantity × price</c>, both in the symbol's currency.</summary>
    public static decimal NativeValue(decimal quantity, decimal price) => quantity * price;
}
