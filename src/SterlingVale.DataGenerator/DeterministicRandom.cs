namespace SterlingVale.DataGenerator;

/// <summary>
/// A deterministic, framework-independent pseudo-random generator (SplitMix64). Using a fixed
/// algorithm — rather than <see cref="System.Random"/> — guarantees byte-identical datasets (and
/// therefore identical SHA-256 hashes) across .NET versions and platforms for a given seed.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    /// <summary>Creates a generator seeded with the given value.</summary>
    public DeterministicRandom(ulong seed) => _state = seed;

    private ulong NextRaw()
    {
        // SplitMix64.
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a double in [0, 1).</summary>
    public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentException("maxExclusive must be greater than minInclusive.");
        }

        ulong range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextRaw() % range);
    }

    /// <summary>
    /// Returns a decimal in [min, max] quantized to <paramref name="decimals"/> places so serialized
    /// output is stable.
    /// </summary>
    public decimal NextDecimal(decimal min, decimal max, int decimals)
    {
        decimal unit = (decimal)NextDouble();
        decimal value = min + ((max - min) * unit);
        return Math.Round(value, decimals, MidpointRounding.ToEven);
    }

    /// <summary>Picks a random element from a list.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(0, items.Count)];

    /// <summary>Returns true with the given probability.</summary>
    public bool Chance(double probability) => NextDouble() < probability;
}
