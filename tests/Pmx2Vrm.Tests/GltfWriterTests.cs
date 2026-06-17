using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class GltfWriterTests
{
    [Fact]
    public void Glb_container_has_valid_header_and_chunks()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        int pos = buffer.AddVec3(new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }, computeBounds: true);
        int idx = buffer.AddIndices(new[] { 0, 1, 2 });
        var bin = buffer.Build();

        root.Meshes.Add(new GltfMesh
        {
            Primitives = { new GltfPrimitive { Attributes = { ["POSITION"] = pos }, Indices = idx } },
        });
        root.Nodes.Add(new GltfNode { Mesh = 0 });
        root.Scenes.Add(new GltfScene { Nodes = { 0 } });
        root.Scene = 0;

        var glb = GlbWriter.Write(root, bin);

        // glb magic + version
        Assert.Equal(0x46546C67u, BitConverter.ToUInt32(glb, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(glb, 4));
        Assert.Equal((uint)glb.Length, BitConverter.ToUInt32(glb, 8));

        // SharpGLTF must accept it as a valid glTF container.
        var model = SharpGLTF.Schema2.ModelRoot.ReadGLB(new MemoryStream(glb), new SharpGLTF.Schema2.ReadSettings());
        Assert.Single(model.LogicalMeshes);
        Assert.Equal(3, model.LogicalMeshes[0].Primitives[0].GetVertexAccessor("POSITION").Count);
    }

    [Fact]
    public void Empty_collections_are_omitted_from_json()
    {
        var root = new GltfRoot();
        var json = System.Text.Json.JsonSerializer.Serialize(root, GlbWriter.JsonOptions);
        Assert.DoesNotContain("\"meshes\"", json);
        Assert.DoesNotContain("\"extensions\"", json);
        Assert.Contains("\"asset\"", json);
    }
}

public class MeshConverterTests
{
    private static PmxModel Parse() => PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));

    [Fact]
    public void Produces_one_primitive_per_material()
    {
        var model = Parse();
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var converted = new MeshConverter(new CoordinateConverter()).Convert(model, buffer);

        Assert.Equal(model.Materials.Count, converted.Mesh.Primitives.Count);
        Assert.Equal(3, converted.VertexCount);
    }

    [Fact]
    public void Primitive_carries_all_skinned_attributes()
    {
        var model = Parse();
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var converted = new MeshConverter(new CoordinateConverter()).Convert(model, buffer);

        var attrs = converted.Mesh.Primitives[0].Attributes;
        Assert.Contains("POSITION", attrs);
        Assert.Contains("NORMAL", attrs);
        Assert.Contains("TEXCOORD_0", attrs);
        Assert.Contains("JOINTS_0", attrs);
        Assert.Contains("WEIGHTS_0", attrs);
    }

    [Fact]
    public void Position_accessor_has_bounds()
    {
        var model = Parse();
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var converted = new MeshConverter(new CoordinateConverter()).Convert(model, buffer);

        var posAccessor = root.Accessors[converted.PositionAccessor];
        Assert.NotNull(posAccessor.Min);
        Assert.NotNull(posAccessor.Max);
    }
}
