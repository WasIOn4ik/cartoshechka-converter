using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Xunit;

namespace Pmx2Vrm.Tests;

public class SparseAccessorTests
{
    [Fact]
    public void Sparse_accessor_materialises_to_dense_values()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);

        // Base triangle so the mesh is valid, plus a sparse morph target that
        // only moves vertex 2.
        int pos = buffer.AddVec3(new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }, computeBounds: true);
        int idx = buffer.AddIndices(new[] { 0, 1, 2 });
        int target = buffer.AddSparseVec3(3, new[] { (2, new Vector3(0, 0, 5)) });
        var bin = buffer.Build();

        root.Meshes.Add(new GltfMesh
        {
            Primitives =
            {
                new GltfPrimitive
                {
                    Attributes = { ["POSITION"] = pos },
                    Indices = idx,
                    Targets = new() { new() { ["POSITION"] = target } },
                },
            },
            Weights = new[] { 0f },
        });
        root.Nodes.Add(new GltfNode { Mesh = 0 });
        root.Scenes.Add(new GltfScene { Nodes = { 0 } });
        root.Scene = 0;

        var glb = GlbWriter.Write(root, bin);
        var model = SharpGLTF.Schema2.ModelRoot.ReadGLB(new MemoryStream(glb), new SharpGLTF.Schema2.ReadSettings());

        var deltas = model.LogicalAccessors[target].AsVector3Array();
        Assert.Equal(3, deltas.Count);
        Assert.Equal(Vector3.Zero, deltas[0]);
        Assert.Equal(Vector3.Zero, deltas[1]);
        Assert.Equal(new Vector3(0, 0, 5), deltas[2]);
    }

    [Fact]
    public void Sparse_bounds_fold_in_the_zero_baseline()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        int target = buffer.AddSparseVec3(10, new[] { (3, new Vector3(2, 0, 0)) });
        buffer.Build();

        var acc = root.Accessors[target];
        Assert.NotNull(acc.Sparse);
        Assert.Equal(1, acc.Sparse!.Count);
        // min includes the implicit zeros, max includes the single delta.
        Assert.Equal(new[] { 0f, 0f, 0f }, acc.Min);
        Assert.Equal(new[] { 2f, 0f, 0f }, acc.Max);
    }

    [Fact]
    public void Duplicate_indices_keep_last_and_stay_sorted()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        int target = buffer.AddSparseVec3(5, new[]
        {
            (4, new Vector3(1, 0, 0)),
            (1, new Vector3(2, 0, 0)),
            (1, new Vector3(9, 0, 0)), // duplicate of index 1 -> last wins
        });
        buffer.Build();

        Assert.Equal(2, root.Accessors[target].Sparse!.Count);
    }
}
