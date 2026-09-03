using SterlingVale.DataGenerator;

// CLI: generates every dataset profile into <output>/<profile>/ and validates each one.
// Usage: SterlingVale.DataGenerator [outputRoot]   (default: ./data)

string outputRoot = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "data");

var generator = new SyntheticDataGenerator();
var validator = new DatasetValidator();
var writer = new DatasetWriter();

int exitCode = 0;
foreach (var profile in DatasetProfile.All)
{
    var dataset = generator.Generate(profile);
    var validation = validator.Validate(dataset);
    string profileDir = Path.Combine(outputRoot, profile.Name);

    if (!validation.IsValid)
    {
        exitCode = 1;
        Console.Error.WriteLine($"[{profile.Name}] INVALID ({validation.Errors.Count} errors):");
        foreach (var error in validation.Errors.Take(20))
        {
            Console.Error.WriteLine($"  - {error}");
        }

        continue;
    }

    var manifest = writer.Write(dataset, profileDir);
    Console.WriteLine(
        $"[{profile.Name}] {dataset.Households.Count} households, " +
        $"{dataset.Accounts.Count} accounts, {dataset.Positions.Count} positions -> {profileDir}");
    foreach (var file in manifest.Files)
    {
        Console.WriteLine($"    {file.File,-18} {file.RecordCount,6} records  {file.Sha256}");
    }
}

return exitCode;
