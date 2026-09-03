using SterlingVale.DataGenerator;

namespace SterlingVale.Benchmark.Commands;

/// <summary>Regenerates each profile, validates it, and reports on-disk manifest presence.</summary>
public static class ValidateData
{
    /// <summary>Runs validation; returns 0 when all profiles are valid, 1 otherwise.</summary>
    public static int Run(string dataRoot)
    {
        var generator = new SyntheticDataGenerator();
        var validator = new DatasetValidator();
        bool ok = true;

        foreach (var profile in DatasetProfile.All)
        {
            var dataset = generator.Generate(profile);
            var result = validator.Validate(dataset);
            if (!result.IsValid)
            {
                ok = false;
                Console.Error.WriteLine($"[{profile.Name}] INVALID ({result.Errors.Count} errors): {string.Join("; ", result.Errors.Take(5))}");
                continue;
            }

            string manifest = Path.Combine(dataRoot, profile.Name, "manifest.json");
            string onDisk = File.Exists(manifest) ? "on-disk manifest present" : "not generated on disk";
            Console.WriteLine(
                $"[{profile.Name}] VALID — {dataset.Households.Count} households, {dataset.Accounts.Count} accounts, " +
                $"{dataset.Positions.Count} positions; {onDisk}");
        }

        return ok ? 0 : 1;
    }
}
