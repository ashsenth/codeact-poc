using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HyperlightSandbox.Api;
using Microsoft.Agents.AI.Hyperlight;
using SterlingVale.AgentShared.Contracts;
using SterlingVale.Application.Oracle;
using SterlingVale.Infrastructure.Data;

namespace SterlingVale.Diagnostics;

public static class SandboxCapacity
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static int Write(string[] args)
    {
        if (args.Length != 2 || Directory.Exists(args[1]))
            throw new ArgumentException("Usage: sandbox-capacity <existing-data-root> <new-output-directory>");
        string directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        var sizes = new List<object>();
        foreach (string profile in new[] { "small", "medium", "large" })
        {
            var snapshot = new DatasetLoader().Load(Path.Combine(args[0], profile)).Snapshot;
            var report = new ExposureOracle().Analyze(snapshot, new() { DatasetProfile = profile },
                new string('0', 32), DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
            string compact = ReportJson.Serialize(report);
            File.WriteAllText(Path.Combine(directory, profile + "-oracle.json"), compact);
            int bytes = Encoding.UTF8.GetByteCount(compact);
            sizes.Add(new { profile, households = report.Households.Count, compactUtf8Bytes = bytes,
                indentedUtf8Bytes = Encoding.UTF8.GetByteCount(ReportJson.Serialize(report, indented: true)) });
            Console.WriteLine($"{profile}: {report.Households.Count} households; compact oracle {bytes} UTF-8 bytes");
        }
        Type provider = typeof(HyperlightCodeActProviderOptions);
        Type builder = typeof(SandboxBuilder);
        var ffi = Assembly.Load("HyperlightSandbox.PInvoke").GetTypes().Single(t => t.Name == "FFISandboxOptions");
        var assemblies = new[] { provider.Assembly, builder.Assembly, ffi.Assembly }.Select(assembly => new
        {
            name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(),
            sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
        });
        File.WriteAllText(Path.Combine(directory, "capacity.json"), JsonSerializer.Serialize(new
        {
            kind = "offline-oracle-size-and-pinned-sdk-inspection", modelCalls = 0,
            serialization = "ReportJson canonical compact UTF-8; no BOM/newline; 32-character runId; fixed generatedAt",
            sizes, assemblies,
            providerOptions = provider.GetProperties().Select(p => new { p.Name, type = p.PropertyType.FullName }).OrderBy(p => p.Name),
            builderMethods = builder.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.ToString()).Order(StringComparer.Ordinal),
            ffiFields = ffi.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => new { f.Name, type = f.FieldType.FullName }),
        }, Json));
        return 0;
    }
}
