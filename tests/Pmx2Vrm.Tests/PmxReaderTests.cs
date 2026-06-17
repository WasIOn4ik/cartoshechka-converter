using System.Numerics;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class PmxReaderTests
{
    private static PmxModel Parse() => PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));

    [Fact]
    public void Reads_header_and_model_info()
    {
        var m = Parse();
        Assert.Equal(2.0f, m.Header.Version);
        Assert.Equal(PmxEncoding.Utf16Le, m.Header.Encoding);
        Assert.Equal("テストモデル", m.NameLocal);
        Assert.Equal("TestModel", m.NameUniversal);
        Assert.Equal("comment", m.CommentUniversal);
    }

    [Fact]
    public void Reads_vertices_with_weights()
    {
        var m = Parse();
        Assert.Equal(3, m.Vertices.Count);

        var v1 = m.Vertices[1];
        Assert.Equal(PmxWeightType.Bdef2, v1.WeightType);
        Assert.Equal(2, v1.Weights.Length);
        Assert.Equal(0, v1.Weights[0].BoneIndex);
        Assert.Equal(0.75f, v1.Weights[0].Weight, 5);
        Assert.Equal(0.25f, v1.Weights[1].Weight, 5);

        Assert.Equal(new Vector3(1, 0, 0), m.Vertices[1].Position);
    }

    [Fact]
    public void Reads_triangle_indices()
    {
        var m = Parse();
        Assert.Equal(new[] { 0, 1, 2 }, m.Indices.ToArray());
    }

    [Fact]
    public void Reads_textures_and_material()
    {
        var m = Parse();
        Assert.Single(m.TexturePaths);
        Assert.Equal("tex/body.png", m.TexturePaths[0]);

        var mat = Assert.Single(m.Materials);
        Assert.Equal("Face", mat.NameUniversal);
        Assert.True(mat.Flags.HasFlag(PmxMaterialFlags.HasEdge));
        Assert.Equal(0, mat.TextureIndex);
        Assert.Equal(-1, mat.SphereTextureIndex);
        Assert.Equal(PmxToonReference.Shared, mat.ToonReference);
        Assert.Equal(1, mat.ToonValue);
        Assert.Equal(3, mat.SurfaceCount);
    }

    [Fact]
    public void Reads_bones_including_ik()
    {
        var m = Parse();
        Assert.Equal(3, m.Bones.Count);
        Assert.Equal("center", m.Bones[0].NameUniversal);
        Assert.Equal(-1, m.Bones[0].ParentIndex);

        var ik = m.Bones[1];
        Assert.True(ik.HasFlag(PmxBoneFlags.Ik));
        Assert.Equal(0, ik.IkTargetIndex);
        Assert.Equal(20, ik.IkLoopCount);
        Assert.Single(ik.IkLinks);
        Assert.Equal(0, ik.IkLinks[0].BoneIndex);
        Assert.False(ik.IkLinks[0].HasLimit);
    }

    [Fact]
    public void Reads_vertex_morph()
    {
        var m = Parse();
        var morph = Assert.Single(m.Morphs);
        Assert.Equal("blink", morph.NameUniversal);
        Assert.Equal(PmxMorphType.Vertex, morph.Type);
        var off = Assert.Single(morph.Vertex);
        Assert.Equal(2, off.VertexIndex);
        Assert.Equal(new Vector3(0, -0.1f, 0), off.Offset);
    }

    [Fact]
    public void Reads_display_frame()
    {
        var m = Parse();
        var f = Assert.Single(m.DisplayFrames);
        Assert.True(f.IsSpecial);
        var item = Assert.Single(f.Items);
        Assert.Equal(PmxDisplayItemType.Morph, item.Type);
        Assert.Equal(0, item.Index);
    }

    [Fact]
    public void Reads_rigid_body_and_joint()
    {
        var m = Parse();
        var rb = Assert.Single(m.RigidBodies);
        Assert.Equal("hairPhys", rb.NameUniversal);
        Assert.Equal(2, rb.BoneIndex);
        Assert.Equal(PmxRigidBodyShape.Capsule, rb.Shape);
        Assert.Equal(PmxPhysicsMode.Physics, rb.PhysicsMode);
        Assert.Equal(1.0f, rb.Mass, 5);

        var j = Assert.Single(m.Joints);
        Assert.Equal("hairJoint", j.NameUniversal);
        Assert.Equal(0, j.RigidBodyAIndex);
        Assert.Equal(new Vector3(10, 10, 10), j.SpringRotation);
    }

    [Fact]
    public void Rejects_non_pmx_data()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
        Assert.Throws<InvalidDataException>(() => PmxReader.Read(new MemoryStream(bytes)));
    }
}
