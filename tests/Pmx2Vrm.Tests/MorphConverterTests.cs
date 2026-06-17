using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Mapping;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class MorphNameDictionaryTests
{
    [Theory]
    [InlineData("まばたき", VrmExpressionPreset.Blink)]
    [InlineData("あ", VrmExpressionPreset.Aa)]
    [InlineData("笑い", VrmExpressionPreset.Happy)]
    [InlineData("怒り", VrmExpressionPreset.Angry)]
    public void Maps_standard_morphs(string name, VrmExpressionPreset expected)
    {
        Assert.True(MorphNameDictionary.TryGet(name, out var p));
        Assert.Equal(expected, p);
    }

    [Fact]
    public void Preset_keys_differ_by_version()
    {
        Assert.Equal("aa", VrmExpressionPreset.Aa.ToVrm10Key());
        Assert.Equal("a", VrmExpressionPreset.Aa.ToVrm0Key());
        Assert.Equal("happy", VrmExpressionPreset.Happy.ToVrm10Key());
        Assert.Equal("joy", VrmExpressionPreset.Happy.ToVrm0Key());
    }
}

public class MorphConverterTests
{
    private static (ConvertedMesh mesh, ConvertedMorphs morphs, GltfRoot root) Run()
    {
        var model = PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var coords = new CoordinateConverter();
        var mesh = new MeshConverter(coords).Convert(model, buffer);
        var morphs = new MorphConverter(coords).Convert(model, mesh, buffer);
        return (mesh, morphs, root);
    }

    [Fact]
    public void Vertex_morph_becomes_a_target_on_every_primitive()
    {
        var (mesh, morphs, _) = Run();
        Assert.Single(morphs.TargetNames);
        Assert.Equal("blink", morphs.TargetNames[0]);
        foreach (var prim in mesh.Mesh.Primitives)
        {
            Assert.NotNull(prim.Targets);
            Assert.Single(prim.Targets!);
            Assert.Contains("POSITION", prim.Targets![0]);
        }
        Assert.Single(mesh.Mesh.Weights!);
    }

    [Fact]
    public void Vertex_morph_produces_expression_with_preset()
    {
        var (_, morphs, _) = Run();
        var expr = Assert.Single(morphs.Expressions);
        Assert.Equal(VrmExpressionPreset.Blink, expr.Preset);
        var bind = Assert.Single(expr.Binds);
        Assert.Equal(0, bind.MorphTargetIndex);
        Assert.Equal(1f, bind.Weight, 3);
    }

    [Fact]
    public void Duplicate_morph_names_are_made_unique()
    {
        // Two vertex morphs sharing a name would crash UniGLTF's AddBlendShapeFrame.
        var model = new PmxModel();
        model.Vertices.Add(new PmxVertex());
        for (int i = 0; i < 3; i++)
        {
            var morph = new PmxMorph { NameUniversal = "smile", Type = PmxMorphType.Vertex };
            morph.Vertex.Add(new PmxVertexMorphOffset { VertexIndex = 0, Offset = new System.Numerics.Vector3(0, 1, 0) });
            model.Morphs.Add(morph);
        }

        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var coords = new CoordinateConverter();
        var mesh = new ConvertedMesh
        {
            Mesh = new GltfMesh { Primitives = { new GltfPrimitive() } },
            VertexCount = 1,
            PositionAccessor = 0,
        };
        var morphs = new MorphConverter(coords).Convert(model, mesh, buffer);

        Assert.Equal(3, morphs.TargetNames.Count);
        Assert.Equal(morphs.TargetNames.Count, morphs.TargetNames.Distinct().Count());
        Assert.Equal(new[] { "smile", "smile_2", "smile_3" }, morphs.TargetNames.ToArray());
    }

    [Fact]
    public void Bone_morph_is_skipped_with_warning()
    {
        var model = new PmxModel();
        model.Vertices.Add(new PmxVertex());
        var morph = new PmxMorph { NameUniversal = "boneMove", Type = PmxMorphType.Bone };
        morph.Bone.Add(new PmxBoneMorphOffset { BoneIndex = 0 });
        model.Morphs.Add(morph);

        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var coords = new CoordinateConverter();
        var mesh = new ConvertedMesh
        {
            Mesh = new GltfMesh { Primitives = { new GltfPrimitive() } },
            VertexCount = 1,
            PositionAccessor = 0,
        };

        var warnings = new List<string>();
        var morphs = new MorphConverter(coords, warnings.Add).Convert(model, mesh, buffer);

        Assert.Empty(morphs.Expressions);
        Assert.Single(warnings);
    }
}
