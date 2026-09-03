using System.Security.Cryptography;
using System.Text;

namespace SterlingVale.AgentShared;

/// <summary>
/// Deterministic SHA-256 hashing used for all fairness fingerprints (prompt, schema, tools, model
/// configuration, dataset manifest). A single canonical implementation guarantees Classic and
/// CodeAct compute identical hashes for identical inputs.
/// </summary>
public static class Hashing
{
    /// <summary>Returns the lowercase hex SHA-256 of a UTF-8 string (newlines normalized to '\n').</summary>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
