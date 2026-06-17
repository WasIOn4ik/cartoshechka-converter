using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Vrm;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class EndToEndConversionTests
{
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Theory]
    [InlineData(VrmVersion.Vrm1)]
    [InlineData(VrmVersion.Vrm0)]
    public void ConvertFile_writes_a_loadable_vrm(VrmVersion version)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pmx2vrm_e2e_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "tex"));
        try
        {
            var pmxPath = Path.Combine(dir, "model.pmx");
            var vrmPath = Path.Combine(dir, "model.vrm");
            File.WriteAllBytes(pmxPath, SyntheticPmx.Build());
            File.WriteAllBytes(Path.Combine(dir, "tex", "body.png"), Convert.FromBase64String(OnePixelPng));

            new VrmConverter().ConvertFile(pmxPath, vrmPath, new ConversionOptions
            {
                Version = version,
                Meta = new VrmMeta { Title = "E2E", Author = "Tester" },
            });

            Assert.True(File.Exists(vrmPath));
            var model = SharpGLTF.Schema2.ModelRoot.ReadGLB(
                new MemoryStream(File.ReadAllBytes(vrmPath)), new SharpGLTF.Schema2.ReadSettings());
            Assert.Single(model.LogicalMeshes);
            Assert.NotEmpty(model.LogicalNodes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
