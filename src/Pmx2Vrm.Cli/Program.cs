using System.CommandLine;
using System.Globalization;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Vrm;

// Parse numbers (e.g. --scale 0.08) the same way regardless of system locale.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var inputArg = new Argument<FileInfo>("input") { Description = "Path to the source .pmx file." };

var outputOpt = new Option<FileInfo?>("--output", "-o") { Description = "Output .vrm path (default: alongside input)." };
var versionOpt = new Option<string>("--vrm-version") { Description = "Target VRM version: 0 or 1.", DefaultValueFactory = _ => "0" };
var scaleOpt = new Option<float>("--scale") { Description = "Metres per MMD unit.", DefaultValueFactory = _ => CoordinateConverter.DefaultScale };
var titleOpt = new Option<string?>("--title") { Description = "Model title for VRM meta." };
var authorOpt = new Option<string?>("--author") { Description = "Author for VRM meta." };
var collidersOpt = new Option<bool>("--spring-colliders") { Description = "Emit curated body spring-bone colliders (on by default; no-op, kept for compatibility)." };
var noCollidersOpt = new Option<bool>("--no-colliders") { Description = "Disable the curated body spring-bone colliders." };
var noTposeOpt = new Option<bool>("--no-tpose") { Description = "Skip A-pose to T-pose normalization (debug)." };

var root = new RootCommand("Pmx2Vrm — convert MMD PMX models to VRM (0.x / 1.0).")
{
    inputArg, outputOpt, versionOpt, scaleOpt, titleOpt, authorOpt, collidersOpt, noCollidersOpt, noTposeOpt,
};

root.SetAction(parse =>
{
    var input = parse.GetValue(inputArg)!;
    if (!input.Exists)
    {
        Console.Error.WriteLine($"Input file not found: {input.FullName}");
        return 1;
    }

    var output = parse.GetValue(outputOpt)
        ?? new FileInfo(Path.ChangeExtension(input.FullName, ".vrm"));

    var versionText = parse.GetValue(versionOpt);
    var version = versionText is "0" or "0.x" or "vrm0" ? VrmVersion.Vrm0 : VrmVersion.Vrm1;

    var options = new ConversionOptions
    {
        Version = version,
        Scale = parse.GetValue(scaleOpt),
        SpringColliders = !parse.GetValue(noCollidersOpt),
        TPose = !parse.GetValue(noTposeOpt),
        Warn = msg => Console.Error.WriteLine($"warning: {msg}"),
        Meta = new VrmMeta
        {
            Title = parse.GetValue(titleOpt) ?? Path.GetFileNameWithoutExtension(input.Name),
            Author = parse.GetValue(authorOpt) ?? "Unknown",
        },
    };

    try
    {
        new VrmConverter().ConvertFile(input.FullName, output.FullName, options);
        Console.WriteLine($"Converted '{input.Name}' -> '{output.FullName}' (VRM {(version == VrmVersion.Vrm1 ? "1.0" : "0.x")}).");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Conversion failed: {ex.Message}");
        return 2;
    }
});

return root.Parse(args).Invoke();
