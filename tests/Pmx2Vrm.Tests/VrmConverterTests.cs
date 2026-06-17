using System.Text.Json;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class VrmConverterTests
{
    // 1x1 transparent PNG, so the synthetic "tex/body.png" embeds rather than
    // becoming an unresolved external URI (which SharpGLTF refuses to read).
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static byte[] Convert(VrmVersion version)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pmx2vrm_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "tex"));
        File.WriteAllBytes(Path.Combine(dir, "tex", "body.png"), System.Convert.FromBase64String(OnePixelPng));
        try
        {
            var model = PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));
            return new VrmConverter().Convert(model, new ConversionOptions
            {
                Version = version,
                TextureBaseDir = dir,
                Meta = new VrmMeta { Title = "Test", Author = "Tester" },
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Extract and parse the JSON chunk from a glb container.</summary>
    private static JsonElement Json(byte[] glb)
    {
        uint jsonLen = BitConverter.ToUInt32(glb, 12);
        var json = new ReadOnlySpan<byte>(glb, 20, (int)jsonLen);
        return JsonDocument.Parse(json.ToArray()).RootElement;
    }

    [Fact]
    public void Produces_glb_readable_by_sharpgltf_with_skin_and_morphs()
    {
        var glb = Convert(VrmVersion.Vrm1);
        var model = SharpGLTF.Schema2.ModelRoot.ReadGLB(new MemoryStream(glb), new SharpGLTF.Schema2.ReadSettings());

        Assert.Single(model.LogicalMeshes);
        Assert.Single(model.LogicalSkins);
        var prim = model.LogicalMeshes[0].Primitives[0];
        Assert.NotNull(prim.GetVertexAccessor("JOINTS_0"));
        Assert.True(prim.MorphTargetsCount >= 1);
    }

    [Fact]
    public void Vrm1_has_humanoid_expressions_springbone_and_mtoon()
    {
        var root = Json(Convert(VrmVersion.Vrm1));
        var ext = root.GetProperty("extensions");

        var vrm = ext.GetProperty("VRMC_vrm");
        Assert.Equal("1.0", vrm.GetProperty("specVersion").GetString());
        Assert.True(vrm.GetProperty("humanoid").GetProperty("humanBones").TryGetProperty("hips", out _));

        var preset = vrm.GetProperty("expressions").GetProperty("preset");
        Assert.True(preset.TryGetProperty("blink", out _));

        Assert.True(ext.TryGetProperty("VRMC_springBone", out var spring));
        Assert.True(spring.GetProperty("springs").GetArrayLength() >= 1);

        var mat0 = root.GetProperty("materials")[0];
        Assert.True(mat0.GetProperty("extensions").TryGetProperty("VRMC_materials_mtoon", out _));
    }

    [Fact]
    public void Vrm0_has_vrm_extension_with_blendshapes_and_secondary()
    {
        var root = Json(Convert(VrmVersion.Vrm0));
        var vrm = root.GetProperty("extensions").GetProperty("VRM");

        Assert.Equal("0.0", vrm.GetProperty("specVersion").GetString());
        Assert.Equal("Test", vrm.GetProperty("meta").GetProperty("title").GetString());

        var humanBones = vrm.GetProperty("humanoid").GetProperty("humanBones");
        Assert.Contains(humanBones.EnumerateArray(), b => b.GetProperty("bone").GetString() == "hips");

        var groups = vrm.GetProperty("blendShapeMaster").GetProperty("blendShapeGroups");
        Assert.Contains(groups.EnumerateArray(), g => g.GetProperty("presetName").GetString() == "blink");

        Assert.True(vrm.GetProperty("secondaryAnimation").GetProperty("boneGroups").GetArrayLength() >= 1);
        Assert.True(vrm.GetProperty("materialProperties").GetArrayLength() >= 1);
    }

    [Fact]
    public void ExtensionsUsed_matches_version()
    {
        var v1 = Json(Convert(VrmVersion.Vrm1)).GetProperty("extensionsUsed").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("VRMC_vrm", v1);
        Assert.Contains("VRMC_springBone", v1);

        var v0 = Json(Convert(VrmVersion.Vrm0)).GetProperty("extensionsUsed").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("VRM", v0);
        Assert.DoesNotContain("VRMC_vrm", v0);
    }
}
